using Leopotam.EcsLite;
using UnityEngine;
using Zenject;
using System.Collections.Generic;

namespace STG.CurveDash
{
    public class CombatSystem : ITickable
    {
        private readonly EcsWorld world;
        
        private readonly EcsFilter playerFilter;
        private readonly EcsFilter enemyFilter;

        private readonly EcsPool<PlayerCombatComponent> combatPool;
        private readonly EcsPool<ViewLinkComponent> viewLinkPool;
        private readonly EcsPool<EnemyHealthComponent> healthPool;
        private readonly EcsPool<EnemyComponent> enemyPool;
        private readonly EcsPool<EnemyDeadEvent> deadPool;

        private readonly GemLevelService _gemLevelService;
        private readonly PlayerStatService _playerStatService;
        private readonly BeltFlaskService _beltFlaskService;
        private readonly GameStateService _gameState;

        // World units the enemy is shoved per point of KnockbackForce rolled on the weapon.
        private const float KnockbackUnitsPerForce = 0.1f;
        // Crit chance used when the character-stat system is unavailable (e.g. unarmed fallback path).
        private const float FallbackCritChance = 10f;
        // PoE-style elemental resistance cap: resistance above this is wasted; negative resist amplifies.
        private const float MaxResistance = 75f;

        // --- Attribute & level damage scaling (tunable) ---
        // Strength → increased PHYSICAL (base/weapon) damage; Intelligence → increased ELEMENTAL damage;
        // each character Level → flat % increased TOTAL damage; Dexterity → increased attack speed (which
        // in this combat model also raises how often skills are cast).
        private const float PhysPercentPerStrength      = 0.5f; // +0.5% physical damage per Strength
        private const float ElePercentPerIntelligence   = 0.5f; // +0.5% elemental damage per Intelligence
        private const float DamagePercentPerLevel        = 3f;   // +3% total damage per character level
        private const float AttackSpeedPercentPerDexterity = 0.2f; // +0.2% attack speed per Dexterity

        // Fraction of elemental damage that lands after a resistance %. 30 res → 0.7; -20 res → 1.2.
        private static float ResistMultiplier(float resistPercent) => 1f - Mathf.Min(resistPercent, MaxResistance) / 100f;

        public CombatSystem(EcsWorld world, GemLevelService gemLevelService, PlayerStatService playerStatService, BeltFlaskService beltFlaskService, GameStateService gameState)
        {
            this.world = world;
            _gemLevelService = gemLevelService;
            _playerStatService = playerStatService;
            _beltFlaskService = beltFlaskService;
            _gameState = gameState;
            
            playerFilter = world.Filter<PlayerComponent>().Inc<PlayerCombatComponent>().Inc<ViewLinkComponent>().End();
            enemyFilter = world.Filter<EnemyHealthComponent>().Inc<EnemyComponent>().Inc<ViewLinkComponent>().Exc<EnemyDeadEvent>().End();

            
            combatPool = world.GetPool<PlayerCombatComponent>();
            viewLinkPool = world.GetPool<ViewLinkComponent>();
            healthPool = world.GetPool<EnemyHealthComponent>();
            enemyPool = world.GetPool<EnemyComponent>();
            deadPool = world.GetPool<EnemyDeadEvent>();
        }

        public void Tick()
        {
            // No combat (enemy facing, player attacks) while the world is frozen at Title / GameEnd.
            if (!_gameState.IsPlaying) return;

            // Smoothly rotate nearby enemies to face the player
            foreach (var playerEntity in playerFilter)
            {
                ref var playerView = ref viewLinkPool.Get(playerEntity);
                if (playerView.Transform == null) continue;
                Vector3 playerPos = playerView.Transform.position;

                foreach (var enemyEntity in enemyFilter)
                {
                    ref var enemyView = ref viewLinkPool.Get(enemyEntity);
                    if (enemyView.Transform == null) continue;

                    Vector3 direction = playerPos - enemyView.Transform.position;
                    direction.y = 0; // Lock Y axis to prevent vertical tilting

                    float distance = direction.magnitude;
                    if (distance <= 10f) // Within 10 units, rotate to face player
                    {
                        if (direction != Vector3.zero)
                        {
                            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                            enemyView.Transform.rotation = Quaternion.Slerp(enemyView.Transform.rotation, targetRotation, Time.deltaTime * 6f);
                        }
                    }
                }
            }

            foreach (var playerEntity in playerFilter)
            {
                ref var combat = ref combatPool.Get(playerEntity);

                // Bridge the equipped weapon (owned by PlayerView, updated on equip and on gem socket)
                // into the ECS combat component every tick. After the equipment system moved into
                // Devion/PlayerView, nothing else sets combat.CurrentWeapon anymore — so without this it
                // stays null: the player fights unarmed and weapon-socketed gem abilities never fire.
                {
                    ref var weaponLink = ref viewLinkPool.Get(playerEntity);
                    if (weaponLink.Transform != null)
                    {
                        var pv = weaponLink.Transform.GetComponent<PlayerView>();
                        if (pv != null) combat.CurrentWeapon = pv.GetWeapon();
                    }
                }

                // Determine attack parameters (support unarmed fallback if no weapon equipped)
                float attackRange = 2.0f; // Default unarmed attack range
                float attackSpeed = 1.0f; // Default unarmed attack speed (attacks per second)
                
                if (combat.CurrentWeapon != null && combat.CurrentWeapon.BaseData != null)
                {
                    attackRange = combat.CurrentWeapon.BaseData.BaseAttackRange;
                    if (attackRange <= 0.2f) attackRange = 2.0f; // Safe minimum range (melee fallback)

                    attackSpeed = combat.CurrentWeapon.FinalAttackSpeed;
                    if (attackSpeed < 0.1f) attackSpeed = 1.0f; // Safe minimum attack speed
                }

                // Dexterity grants increased attack speed. Note: skill casts are gated by attack speed in
                // this combat model, so this also speeds up how often socketed skills fire.
                if (_playerStatService != null)
                {
                    float dex = _playerStatService.GetPlayerStat().Dexterity;
                    attackSpeed *= 1f + dex * AttackSpeedPercentPerDexterity / 100f;
                }

                // Active flasks granting IncreasedAttackSpeed (value = % increase) multiply the real combat
                // attack rate, not just the animation. Data-driven: any flask with such a Buffs entry works.
                if (_beltFlaskService != null)
                    attackSpeed *= 1f + _beltFlaskService.SumActiveValue(StatType.IncreasedAttackSpeed) / 100f;

                if (combat.CooldownTimer > 0)
                {
                    combat.CooldownTimer -= Time.deltaTime;
                    continue;
                }

                ref var playerView = ref viewLinkPool.Get(playerEntity);
                if (playerView.Transform == null) continue;
                Vector3 playerPos = playerView.Transform.position;

                // Sync the player's AttackSpeed property with their real combat attack speed
                var playerViewComponent = playerView.Transform.GetComponent<PlayerView>();
                if (playerViewComponent != null)
                {
                    playerViewComponent.AttackSpeed = attackSpeed;
                }

                // Find closest enemy within range (ignoring vertical Y-axis height differences)
                int targetEnemy = -1;
                float closestDistSq = attackRange * attackRange;

                foreach (var enemyEntity in enemyFilter)
                {
                    ref var enemyView = ref viewLinkPool.Get(enemyEntity);
                    if (enemyView.Transform == null) continue;
                    
                    Vector3 diff = enemyView.Transform.position - playerPos;
                    diff.y = 0; // Lock Y axis
                    float distSq = diff.sqrMagnitude;

                    if (distSq <= closestDistSq)
                    {
                        closestDistSq = distSq;
                        targetEnemy = enemyEntity;
                    }
                }

                if (targetEnemy != -1)
                {
                    // Bow combat validation: skip attack if no arrow equipped
                    if (combat.CurrentWeapon != null && combat.CurrentWeapon.BaseData is BowData)
                    {
                        if (playerViewComponent == null || !(playerViewComponent.RightHandItem is OffHandData offHand && offHand.SubType == OffHandType.Arrow))
                        {
                            Debug.LogWarning("[CombatSystem] Bow requires Arrow equipped. Attack skipped.");
                            return;
                        }
                    }

                    // Set cooldown: Cooldown = 1 / attackSpeed
                    combat.CooldownTimer = 1f / attackSpeed;

                    if (playerViewComponent != null)
                    {
                        // combat.CurrentWeapon is now sourced FROM PlayerView at the top of the tick,
                        // so PlayerView already holds the authoritative weapon — no write-back needed.
                        // Get animation synchronization parameters from the weapon or use fallback values
                        bool useAnimationEvent = false;
                        float attackHitDelay = 0.25f; // default unarmed delay
                        string animationTrigger = "Attack"; // default attack trigger name

                        if (combat.CurrentWeapon != null && combat.CurrentWeapon.BaseData != null)
                        {
                            useAnimationEvent = combat.CurrentWeapon.BaseData.UseAnimationEvent;
                            attackHitDelay = combat.CurrentWeapon.BaseData.AttackHitDelay;

                            // Scan socketed abilities to see if any has a custom animation trigger override
                            var allAbilities = new System.Collections.Generic.List<AbilityData>();
                            if (combat.CurrentWeapon != null)
                            {
                                var weaponAbilities = combat.CurrentWeapon.GetAbilities();
                                if (weaponAbilities != null) allAbilities.AddRange(weaponAbilities);
                            }
                            if (playerViewComponent != null)
                            {
                                allAbilities.AddRange(playerViewComponent.GetEquippedArmorAbilities());
                            }
                            
                            foreach (var ab in allAbilities)
                            {
                                if (ab != null && !string.IsNullOrEmpty(ab.AnimationTriggerName))
                                {
                                    animationTrigger = ab.AnimationTriggerName;
                                    break; // prioritize the first specified animation override
                                }
                            }
                        }

                        int capturedPlayerEntity = playerEntity;
                        int capturedTargetEnemy = targetEnemy;
                        float capturedAttackRange = attackRange;

                        ref var targetView = ref viewLinkPool.Get(targetEnemy);
                        if (targetView.Transform != null)
                        {
                            playerViewComponent.RotateModelTowards(targetView.Transform.position);
                        }

                        playerViewComponent.TriggerAttack(
                            onImpact: () =>
                            {
                                // At the moment of impact, make sure the player still exists in combat/view pools
                                if (!combatPool.Has(capturedPlayerEntity) || !viewLinkPool.Has(capturedPlayerEntity)) return;

                                ref var currentPlayerView = ref viewLinkPool.Get(capturedPlayerEntity);

                                // Check if the original target is still valid (alive and in range)
                                int finalTargetEnemy = capturedTargetEnemy;
                                bool originalTargetValid = false;

                                if (healthPool.Has(capturedTargetEnemy) && !deadPool.Has(capturedTargetEnemy) && viewLinkPool.Has(capturedTargetEnemy))
                                {
                                    ref var enemyView = ref viewLinkPool.Get(capturedTargetEnemy);
                                    if (enemyView.Transform != null && currentPlayerView.Transform != null)
                                    {
                                        Vector3 diff = enemyView.Transform.position - currentPlayerView.Transform.position;
                                        diff.y = 0; // Lock Y axis
                                        float distSq = diff.sqrMagnitude;
                                        if (distSq <= capturedAttackRange * capturedAttackRange)
                                        {
                                            originalTargetValid = true;
                                        }
                                    }
                                }

                                // If original target is no longer valid, look for a new closest target within attack range
                                if (!originalTargetValid)
                                {
                                    finalTargetEnemy = -1;
                                    float closestDistSq = capturedAttackRange * capturedAttackRange;

                                    if (currentPlayerView.Transform != null)
                                    {
                                        Vector3 currentPos = currentPlayerView.Transform.position;
                                        foreach (var enemyEntity in enemyFilter)
                                        {
                                            ref var enemyView = ref viewLinkPool.Get(enemyEntity);
                                            if (enemyView.Transform == null) continue;

                                            Vector3 diff = enemyView.Transform.position - currentPos;
                                            diff.y = 0; // Lock Y axis
                                            float distSq = diff.sqrMagnitude;
                                            if (distSq <= closestDistSq)
                                            {
                                                closestDistSq = distSq;
                                                finalTargetEnemy = enemyEntity;
                                            }
                                        }
                                    }
                                }

                                 // Resolve all targets to damage based on the cast ability's projectile counts & area of effect
                                 System.Collections.Generic.List<int> targetsToHit = new System.Collections.Generic.List<int>();

                                 PoEAbility activeSkill = null;
                                 int finalProjectileCount = 1;
                                 float areaMult = 1f;   // AreaOfEffect modifiers (self + supports) scale the AoE radius
                                 int bonusTargets = 0;  // Pierce + Chain add extra enemies a single hit can reach

                                 if (combatPool.Has(capturedPlayerEntity))
                                 {
                                     ref var combatInside = ref combatPool.Get(capturedPlayerEntity);
                                     var allHitAbilities = new System.Collections.Generic.List<AbilityData>();
                                     if (combatInside.CurrentWeapon != null)
                                     {
                                         var weaponAbilities = combatInside.CurrentWeapon.GetAbilities();
                                         if (weaponAbilities != null) allHitAbilities.AddRange(weaponAbilities);
                                     }
                                     if (playerViewComponent != null)
                                     {
                                         allHitAbilities.AddRange(playerViewComponent.GetEquippedArmorAbilities());
                                     }

                                     foreach (var ab in allHitAbilities)
                                     {
                                         if (ab is PoEAbility poeAb)
                                         {
                                             activeSkill = poeAb;
                                             finalProjectileCount = poeAb.ProjectileCount;

                                             // The skill's own SelfModifiers may carry AoE / Pierce / Chain too.
                                             areaMult     *= SupportAbilityData.Multiplier(poeAb.SelfModifiers, SupportStat.AreaOfEffect);
                                             bonusTargets += Mathf.RoundToInt(SupportAbilityData.Flat(poeAb.SelfModifiers, SupportStat.Pierce))
                                                          +  Mathf.RoundToInt(SupportAbilityData.Flat(poeAb.SelfModifiers, SupportStat.Chain));

                                             // Read socketed support gems: extra projectiles, AoE radius and extra targets.
                                             foreach (var subAb in allHitAbilities)
                                             {
                                                 if (subAb is SupportAbilityData support && support.IsCompatible(poeAb))
                                                 {
                                                     finalProjectileCount += support.GetExtraProjectiles();
                                                     areaMult     *= support.GetAreaMultiplier();
                                                     bonusTargets += support.GetPierceCount() + support.GetChainCount();
                                                 }
                                             }
                                             break;
                                         }
                                     }
                                 }

                                 // Effective AoE radius and target cap after AreaOfEffect / Pierce / Chain.
                                 float aoeRange = capturedAttackRange * areaMult;
                                 int effectiveTargets = finalProjectileCount + bonusTargets;

                                 if (currentPlayerView.Transform != null)
                                 {
                                     Vector3 playerPos = currentPlayerView.Transform.position;

                                     if (activeSkill != null && activeSkill.AbilityName.ToLower().Contains("cyclone"))
                                     {
                                         // 1. Cyclone (AOE Spin): Hit ALL alive enemies within range in a 360-degree circle!
                                         foreach (var enemyEntity in enemyFilter)
                                         {
                                             if (deadPool.Has(enemyEntity)) continue;
                                             ref var enemyView = ref viewLinkPool.Get(enemyEntity);
                                             if (enemyView.Transform == null) continue;

                                             float distSq = (enemyView.Transform.position - playerPos).sqrMagnitude;
                                             if (distSq <= aoeRange * aoeRange)
                                             {
                                                 targetsToHit.Add(enemyEntity);
                                             }
                                         }
                                     }
                                     else if (effectiveTargets > 1)
                                      {
                                         // 2. Multi-Projectile (LMP/GMP or Split Arrow) + Pierce/Chain: hit several enemies in a frontal cone!
                                         Vector3 lookDir = currentPlayerView.Transform.forward;
                                         if (finalTargetEnemy != -1 && viewLinkPool.Has(finalTargetEnemy))
                                         {
                                             ref var mainTargetView = ref viewLinkPool.Get(finalTargetEnemy);
                                             if (mainTargetView.Transform != null)
                                             {
                                                 lookDir = (mainTargetView.Transform.position - playerPos);
                                                 lookDir.y = 0;
                                                 lookDir.Normalize();
                                             }
                                         }

                                         System.Collections.Generic.List<(int entity, float distSq)> enemiesInCone = new System.Collections.Generic.List<(int, float)>();
                                         float coneAngle = 60f + (effectiveTargets * 10f); // wider angle for more projectiles / extra targets

                                         foreach (var enemyEntity in enemyFilter)
                                         {
                                             if (deadPool.Has(enemyEntity)) continue;
                                             ref var enemyView = ref viewLinkPool.Get(enemyEntity);
                                             if (enemyView.Transform == null) continue;

                                             Vector3 toEnemy = enemyView.Transform.position - playerPos;
                                             toEnemy.y = 0;
                                             float distSq = toEnemy.sqrMagnitude;

                                             if (distSq <= aoeRange * aoeRange)
                                             {
                                                 float angle = Vector3.Angle(lookDir, toEnemy.normalized);
                                                 if (angle <= coneAngle / 2f)
                                                    {
                                                     enemiesInCone.Add((enemyEntity, distSq));
                                                 }
                                             }
                                         }

                                         // Sort enemies by distance to hit closest targets first
                                         enemiesInCone.Sort((a, b) => a.distSq.CompareTo(b.distSq));

                                         // Projectiles and Pierce/Chain are resolved as two SEPARATE passes:
                                         //
                                         // 1) Projectile pass — fire finalProjectileCount shots, distributed across
                                         //    the cone enemies closest-first and WRAPPING. Surplus projectiles pile
                                         //    back onto the closest enemies, so a lone monster struck by all 3
                                         //    projectiles takes 3 separate hits, while a pack gets one shot each.
                                         //
                                         // 2) Pierce/Chain pass — reach bonusTargets ADDITIONAL distinct enemies
                                         //    further back (one extra hit each). These never stack onto enemies the
                                         //    projectiles already hit, so pierce/chain only widen the spread, never
                                         //    multi-hit a single target.
                                         if (enemiesInCone.Count > 0)
                                         {
                                             for (int i = 0; i < finalProjectileCount; i++)
                                             {
                                                 targetsToHit.Add(enemiesInCone[i % enemiesInCone.Count].entity);
                                             }

                                             int projectileReach = Mathf.Min(finalProjectileCount, enemiesInCone.Count);
                                             for (int i = 0; i < bonusTargets; i++)
                                             {
                                                 int idx = projectileReach + i;
                                                 if (idx >= enemiesInCone.Count) break;
                                                 targetsToHit.Add(enemiesInCone[idx].entity);
                                             }
                                         }
                                     }
                                 }

                                 // Fallback: If no custom AOE/cone targets were acquired, hit the primary target
                                 if (targetsToHit.Count == 0 && finalTargetEnemy != -1)
                                 {
                                     targetsToHit.Add(finalTargetEnemy);
                                 }

                                 // Deduct mana cost of all active abilities (once per attack, not per target)
                                 bool abilitiesAllowed = true;
                                 if (_playerStatService != null)
                                 {
                                     float totalManaCost = 0f;
                                     if (combatPool.Has(capturedPlayerEntity))
                                     {
                                         ref var combatForMana = ref combatPool.Get(capturedPlayerEntity);
                                         var abilitiesForMana = new System.Collections.Generic.List<AbilityData>();
                                         if (combatForMana.CurrentWeapon != null)
                                         {
                                             var wa = combatForMana.CurrentWeapon.GetAbilities();
                                             if (wa != null) abilitiesForMana.AddRange(wa);
                                         }
                                         if (playerViewComponent != null)
                                             abilitiesForMana.AddRange(playerViewComponent.GetEquippedArmorAbilities());

                                         foreach (var ab in abilitiesForMana)
                                         {
                                             if (ab is PoEAbility poeAb && poeAb.SkillType != PoEAbilityType.Aura)
                                                 totalManaCost += poeAb.ManaCost;
                                         }
                                     }

                                     if (totalManaCost > 0f)
                                         abilitiesAllowed = _playerStatService.TrySpendMana(totalManaCost);
                                 }

                                 // Deal damage to ALL resolved targets!
                                 bool isFirstTarget = true;
                                 foreach (var targetEntity in targetsToHit)
                                 {
                                     if (healthPool.Has(targetEntity) && !deadPool.Has(targetEntity))
                                     {
                                         DealDamageToEnemy(capturedPlayerEntity, targetEntity, playerViewComponent, isFirstTarget && abilitiesAllowed);
                                         isFirstTarget = false;
                                     }
                                 }
                             },
                             attackSpeed: attackSpeed,
                             useAnimationEvent: useAnimationEvent,
                             attackHitDelay: attackHitDelay,
                             animationTrigger: animationTrigger
                         );
                     }
                     else
                     {
                         // Fallback: deal damage immediately if no PlayerView component exists
                         DealDamageToEnemy(playerEntity, targetEnemy, null, true);
                     }
                 }
             }
         }

         /// <summary>
         /// Refined damage-dealing function. Handles base weapon/unarmed damage, off-hand/shield bonus damage,
         /// critical hits, white flash visual feedback, and executions of weapon & off-hand PoE socketed abilities.
         /// </summary>
         private void DealDamageToEnemy(int playerEntity, int enemyEntity, PlayerView playerViewComponent, bool triggerAbilities)
        {
            ref var combat = ref combatPool.Get(playerEntity);
            ref var playerView = ref viewLinkPool.Get(playerEntity);
            ref var targetHealth = ref healthPool.Get(enemyEntity);

            float baseDamage = 0f;
            float offhandBonus = 0f;
            float elementalDamage = 0f;
            float totalDamage = 0f;
            bool isCrit = false;

            // Crit bonus contributed by the active skill + its support gems (filled in step 3.5).
            float skillCritBonus = 0f;

            // 1. Calculate Base Weapon or Unarmed Damage (physical)
            if (combat.CurrentWeapon != null)
            {
                baseDamage = combat.CurrentWeapon.GetRandomDamage();
                // 1b. Flat elemental damage (fire + cold) rolled on the weapon, mitigated per-type by
                //     the enemy's elemental resistances (physical is unaffected).
                MonsterData enemyData = enemyPool.Has(enemyEntity) ? enemyPool.Get(enemyEntity).Data : null;
                float fireRes = enemyData != null ? enemyData.FireResistance : 0f;
                float coldRes = enemyData != null ? enemyData.ColdResistance : 0f;
                elementalDamage = combat.CurrentWeapon.AddedFireDamage * ResistMultiplier(fireRes)
                                + combat.CurrentWeapon.AddedColdDamage * ResistMultiplier(coldRes);
            }
            else
            {
                // Default Unarmed Damage (e.g., 5.0 to 10.0 damage)
                baseDamage = Random.Range(5f, 10f);
            }

            // 1c. Attribute scaling: Strength boosts physical (base) damage, Intelligence boosts elemental.
            if (_playerStatService != null)
            {
                var attrStat = _playerStatService.GetPlayerStat();
                baseDamage      *= 1f + attrStat.Strength     * PhysPercentPerStrength    / 100f;
                elementalDamage *= 1f + attrStat.Intelligence * ElePercentPerIntelligence / 100f;
            }

            // 2. Add Off-hand / Shield Bonus Damage
            if (playerViewComponent != null)
            {
                if (playerViewComponent.LeftHandItem is OffHandData leftOffhand)
                {
                    offhandBonus += leftOffhand.BonusDamage;
                }
                else if (playerViewComponent.RightHandItem is OffHandData rightOffhand)
                {
                    offhandBonus += rightOffhand.BonusDamage;
                }
            }

            totalDamage = baseDamage + offhandBonus + elementalDamage;

            // 3. Apply socketed gem level multiplier (highest-level active gem wins)
            if (_gemLevelService != null && combat.CurrentWeapon != null)
            {
                float gemMultiplier = 1f;
                foreach (var ab in combat.CurrentWeapon.GetAbilities())
                {
                    if (ab is PoEAbility poeAb && poeAb.SkillType != PoEAbilityType.Aura)
                    {
                        float m = _gemLevelService.GetDamageMultiplier(ab);
                        if (m > gemMultiplier) gemMultiplier = m;
                    }
                }
                if (playerViewComponent != null)
                {
                    foreach (var ab in playerViewComponent.GetEquippedArmorAbilities())
                    {
                        if (ab is PoEAbility poeAb && poeAb.SkillType != PoEAbilityType.Aura)
                        {
                            float m = _gemLevelService.GetDamageMultiplier(ab);
                            if (m > gemMultiplier) gemMultiplier = m;
                        }
                    }
                }
                totalDamage *= gemMultiplier;
            }

            // 3.5. Apply active offensive skill's DamageMultiplier + AddedFlatDamage (+ support gem bonuses)
            {
                var allAbilitiesForDmg = new List<AbilityData>();
                if (combat.CurrentWeapon != null)
                {
                    var wa = combat.CurrentWeapon.GetAbilities();
                    if (wa != null) allAbilitiesForDmg.AddRange(wa);
                }
                if (playerViewComponent != null)
                    allAbilitiesForDmg.AddRange(playerViewComponent.GetEquippedArmorAbilities());

                // Pick the first active (non-Aura) PoEAbility as the primary skill
                PoEAbility primarySkill = null;
                foreach (var ab in allAbilitiesForDmg)
                {
                    if (ab is PoEAbility poeAb && poeAb.SkillType != PoEAbilityType.Aura)
                    {
                        primarySkill = poeAb;
                        break;
                    }
                }

                if (primarySkill != null)
                {
                    float skillMultiplier = primarySkill.DamageMultiplier;
                    float skillFlat = primarySkill.AddedFlatDamage;
                    skillCritBonus = primarySkill.CriticalChanceBonus;

                    // Stack support gem bonuses compatible with the primary skill
                    foreach (var ab in allAbilitiesForDmg)
                    {
                        if (ab is SupportAbilityData support && support.IsCompatible(primarySkill))
                        {
                            skillMultiplier *= support.GetDamageMultiplier();
                            skillFlat += support.GetAddedFlatDamage();
                            skillCritBonus += support.GetCriticalChanceBonus();
                        }
                    }

                    totalDamage = (totalDamage + skillFlat) * skillMultiplier;
                }
            }

            // 3.6. Level scaling: each character level adds a flat % to total damage, so a high-level
            //      character hits meaningfully harder with the same weapon.
            if (_playerStatService != null)
            {
                int lvl = _playerStatService.GetPlayerStat().Level;
                totalDamage *= 1f + Mathf.Max(0, lvl - 1) * DamagePercentPerLevel / 100f;
            }

            // 4. Critical Hit — driven by the character's stats (CurveDash_Character_Stats),
            //    plus weapon crit affix and the active skill/support crit bonuses.
            float critChance = FallbackCritChance;
            float critMultiplier = 1.5f;
            if (_playerStatService != null)
            {
                var pStat = _playerStatService.GetPlayerStat();
                critChance = pStat.CritChance;                                   // base from character sheet
                if (pStat.CritMultiplier > 0f) critMultiplier = pStat.CritMultiplier / 100f; // 150 → 1.5x
            }
            if (combat.CurrentWeapon != null) critChance += combat.CurrentWeapon.BonusCritChance; // IncreasedCriticalChance affix
            critChance += skillCritBonus;                                        // active skill + support gems

            if (Random.Range(0f, 100f) < critChance)
            {
                isCrit = true;
                totalDamage *= critMultiplier;
            }

            // 5. Subtract Health
            targetHealth.CurrentHealth -= totalDamage;

            // 5.5. Life Steal — return a % of damage dealt as life to the player
            if (_playerStatService != null && combat.CurrentWeapon != null && combat.CurrentWeapon.LifeStealPercent > 0f)
            {
                float heal = totalDamage * combat.CurrentWeapon.LifeStealPercent / 100f;
                if (heal > 0f) _playerStatService.AddLife(heal);
            }

            // 5.6. Knockback — shove the enemy away from the player along the ground plane
            if (combat.CurrentWeapon != null && combat.CurrentWeapon.KnockbackForce > 0f)
            {
                ref var kbEnemyView = ref viewLinkPool.Get(enemyEntity);
                if (kbEnemyView.Transform != null && playerView.Transform != null)
                {
                    Vector3 kbDir = kbEnemyView.Transform.position - playerView.Transform.position;
                    kbDir.y = 0f;
                    if (kbDir.sqrMagnitude > 0.0001f)
                    {
                        kbDir.Normalize();
                        kbEnemyView.Transform.position += kbDir * (combat.CurrentWeapon.KnockbackForce * KnockbackUnitsPerForce);
                    }
                }
            }

            // Log damage with rich info
            if (isCrit)
            {
                Debug.Log($"<color=orange>[Combat] CRITICAL HIT! Player dealt {totalDamage:F1} damage (Phys: {baseDamage:F1} + Offhand: {offhandBonus:F1} + Elem: {elementalDamage:F1}) to enemy! HP left: {targetHealth.CurrentHealth:F1}</color>");
            }
            else
            {
                Debug.Log($"[Combat] Player dealt {totalDamage:F1} damage (Phys: {baseDamage:F1} + Offhand: {offhandBonus:F1} + Elem: {elementalDamage:F1}) to enemy! HP left: {targetHealth.CurrentHealth:F1}");
            }

            // 6. Handle Enemy Death — restore flask charges + give gem XP
            if (targetHealth.CurrentHealth <= 0)
            {
                deadPool.Add(enemyEntity);
                _beltFlaskService?.OnEnemyKilled();

                if (_gemLevelService != null)
                {
                    var gemsToXP = new List<AbilityData>();
                    if (combat.CurrentWeapon != null)
                        gemsToXP.AddRange(combat.CurrentWeapon.GetAbilities());
                    if (playerViewComponent != null)
                        gemsToXP.AddRange(playerViewComponent.GetEquippedArmorAbilities());

                    var leveledUp = _gemLevelService.AddKillXP(gemsToXP);
                    foreach (var info in leveledUp)
                        Debug.Log($"<color=yellow>[GemLevel] Level up! {info}</color>");
                }
            }

            // 7. Flash enemy white on damage
            ref var targetEnemyView = ref viewLinkPool.Get(enemyEntity);
            if (targetEnemyView.Transform != null)
            {
                var monsterView = targetEnemyView.Transform.GetComponent<STG.CurveDash.Views.MonsterView>();
                if (monsterView != null)
                {
                    monsterView.FlashWhite(0.25f);
                    monsterView.SetHealth(targetHealth.CurrentHealth, targetHealth.MaxHealth);
                }

                // Floating damage number above the enemy (orange + "!" on crit, white otherwise).
                // Small horizontal jitter so multiple projectile hits on the same enemy show as
                // separate numbers instead of stacking on the exact same spot.
                Vector3 dmgPopPos = targetEnemyView.Transform.position + Vector3.up * 2.0f
                                  + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.2f, 0.2f), 0f);
                Color dmgColor = isCrit ? new Color(1f, 0.55f, 0f) : Color.white;
                STG.CurveDash.Views.FloatingDamageNumber.Spawn(dmgPopPos, totalDamage, dmgColor, isCrit);
            }

            // 7. Auras are NOT triggered on hit anymore. They are persistent, duration-based
            //    self-buffs handled by AuraSystem (cast → last for Duration → recast).

            // 7.5. Gather all Active Offensive Skills across all gear
            List<AbilityData> activeOffensiveSkills = new List<AbilityData>();

            // - Weapon active skills
            if (combat.CurrentWeapon != null)
            {
                var weaponAbilities = combat.CurrentWeapon.GetAbilities();
                foreach (var ab in weaponAbilities)
                {
                    if (ab is PoEAbility active && active.SkillType != PoEAbilityType.Aura)
                    {
                        activeOffensiveSkills.Add(active);
                    }
                }
            }

            // - Armor active skills
            if (playerViewComponent != null)
            {
                var armorAbilities = playerViewComponent.GetEquippedArmorAbilities();
                foreach (var ab in armorAbilities)
                {
                    if (ab is PoEAbility active && active.SkillType != PoEAbilityType.Aura)
                    {
                        if (!activeOffensiveSkills.Contains(active))
                        {
                            activeOffensiveSkills.Add(active);
                        }
                    }
                }
            }

            // - Off-hand active skills
            if (playerViewComponent != null)
            {
                if (playerViewComponent.LeftHandItem is OffHandData leftOffHand && leftOffHand.Abilities != null)
                {
                    foreach (var ab in leftOffHand.Abilities)
                    {
                        if (ab is PoEAbility active && active.SkillType != PoEAbilityType.Aura)
                        {
                            if (!activeOffensiveSkills.Contains(active)) activeOffensiveSkills.Add(active);
                        }
                    }
                }
                if (playerViewComponent.RightHandItem is OffHandData rightOffHand && rightOffHand.Abilities != null)
                {
                    foreach (var ab in rightOffHand.Abilities)
                    {
                        if (ab is PoEAbility active && active.SkillType != PoEAbilityType.Aura)
                        {
                            if (!activeOffensiveSkills.Contains(active)) activeOffensiveSkills.Add(active);
                        }
                    }
                }
            }

            // 8. Execute ALL Active Offensive Skills simultaneously (chained, no gap between skills)
            if (triggerAbilities && activeOffensiveSkills.Count > 0)
            {
                ref var enemyView = ref viewLinkPool.Get(enemyEntity);
                if (enemyView.Transform != null)
                {
                    Vector3 targetFeetPosition = GetEnemyFeetPosition(enemyView.Transform);
                    for (int i = 0; i < activeOffensiveSkills.Count; i++)
                    {
                        var skill = activeOffensiveSkills[i];
                        if (skill != null)
                        {
                            Debug.Log($"<color=lime>[Combat] Chain cast {i + 1}/{activeOffensiveSkills.Count}: '{skill.AbilityName}'</color>");
                            skill.Execute(playerView.Transform.gameObject, targetFeetPosition);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Finds the exact ground / feet position of an enemy by raycasting downwards.
        /// Prevents floaty or misaligned VFX on dynamically loaded models with varying pivots.
        /// </summary>
        private Vector3 GetEnemyFeetPosition(Transform enemyTransform)
        {
            if (enemyTransform == null) return Vector3.zero;

            Vector3 rootPos = enemyTransform.position;

            // Raycast down from a safe height to detect the ground collision point under the enemy
            if (Physics.Raycast(rootPos + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 5f))
            {
                return hit.point;
            }

            // Fallback: If no collider is detected below, use the root position directly (ground floor)
            return rootPos;
        }
    }
}

