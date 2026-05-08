using Leopotam.EcsLite;
using UnityEngine;
using Zenject;

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
        private readonly EcsPool<EnemyDeadEvent> deadPool;
        
        public CombatSystem(EcsWorld world)
        {
            this.world = world;
            
            playerFilter = world.Filter<PlayerComponent>().Inc<PlayerCombatComponent>().Inc<ViewLinkComponent>().End();
            enemyFilter = world.Filter<EnemyHealthComponent>().Inc<EnemyComponent>().Inc<ViewLinkComponent>().Exc<EnemyDeadEvent>().End();

            
            combatPool = world.GetPool<PlayerCombatComponent>();
            viewLinkPool = world.GetPool<ViewLinkComponent>();
            healthPool = world.GetPool<EnemyHealthComponent>();
            deadPool = world.GetPool<EnemyDeadEvent>();
        }

        public void Tick()
        {
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

                // Determine attack parameters (support unarmed fallback if no weapon equipped)
                float attackRange = 2.0f; // Default unarmed attack range
                float attackSpeed = 1.0f; // Default unarmed attack speed (attacks per second)
                
                if (combat.CurrentWeapon != null)
                {
                    attackRange = combat.CurrentWeapon.BaseData.BaseAttackRange;
                    attackSpeed = combat.CurrentWeapon.FinalAttackSpeed;
                }

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

                // Find closest enemy within range
                int targetEnemy = -1;
                float closestDistSq = attackRange * attackRange;

                foreach (var enemyEntity in enemyFilter)
                {
                    ref var enemyView = ref viewLinkPool.Get(enemyEntity);
                    if (enemyView.Transform == null) continue;
                    
                    float distSq = (enemyView.Transform.position - playerPos).sqrMagnitude;

                    if (distSq <= closestDistSq)
                    {
                        closestDistSq = distSq;
                        targetEnemy = enemyEntity;
                    }
                }

                if (targetEnemy != -1)
                {
                    // Set cooldown: Cooldown = 1 / attackSpeed
                    combat.CooldownTimer = 1f / attackSpeed;

                    if (playerViewComponent != null)
                    {
                        playerViewComponent.CurrentWeaponInstance = combat.CurrentWeapon;
                        // Get animation synchronization parameters from the weapon or use fallback values
                        bool useAnimationEvent = false;
                        float attackHitDelay = 0.25f; // default unarmed delay
                        string animationTrigger = "Attack"; // default attack trigger name

                        if (combat.CurrentWeapon != null && combat.CurrentWeapon.BaseData != null)
                        {
                            useAnimationEvent = combat.CurrentWeapon.BaseData.UseAnimationEvent;
                            attackHitDelay = combat.CurrentWeapon.BaseData.AttackHitDelay;

                            // Scan socketed abilities to see if any has a custom animation trigger override
                            var abilities = combat.CurrentWeapon.GetAbilities();
                            if (abilities != null)
                            {
                                foreach (var ab in abilities)
                                {
                                    if (ab != null && !string.IsNullOrEmpty(ab.AnimationTriggerName))
                                    {
                                        animationTrigger = ab.AnimationTriggerName;
                                        break; // prioritize the first specified animation override
                                    }
                                }
                            }
                        }

                        int capturedPlayerEntity = playerEntity;
                        int capturedTargetEnemy = targetEnemy;
                        float capturedAttackRange = attackRange;

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
                                        float distSq = (enemyView.Transform.position - currentPlayerView.Transform.position).sqrMagnitude;
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

                                            float distSq = (enemyView.Transform.position - currentPos).sqrMagnitude;
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

                                 if (combatPool.Has(capturedPlayerEntity))
                                 {
                                     ref var combatInside = ref combatPool.Get(capturedPlayerEntity);
                                     if (combatInside.CurrentWeapon != null)
                                     {
                                         var weaponAbilities = combatInside.CurrentWeapon.GetAbilities();
                                         foreach (var ab in weaponAbilities)
                                         {
                                             if (ab is PoEAbility poeAb)
                                             {
                                                 activeSkill = poeAb;
                                                 finalProjectileCount = poeAb.ProjectileCount;

                                                 // Read support gems socketed in the same weapon to scale projectile counts
                                                 foreach (var subAb in weaponAbilities)
                                                 {
                                                     if (subAb is SupportAbilityData support && support.IsCompatible(poeAb))
                                                     {
                                                         finalProjectileCount += support.ExtraProjectiles;
                                                     }
                                                 }
                                                 break;
                                             }
                                         }
                                     }
                                 }

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
                                             if (distSq <= capturedAttackRange * capturedAttackRange)
                                             {
                                                 targetsToHit.Add(enemyEntity);
                                             }
                                         }
                                     }
                                     else if (finalProjectileCount > 1)
                                      {
                                         // 2. Multi-Projectile (LMP/GMP or Split Arrow): Hit multiple unique enemies in a frontal cone!
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
                                         float coneAngle = 60f + (finalProjectileCount * 10f); // wider angle for more projectiles

                                         foreach (var enemyEntity in enemyFilter)
                                         {
                                             if (deadPool.Has(enemyEntity)) continue;
                                             ref var enemyView = ref viewLinkPool.Get(enemyEntity);
                                             if (enemyView.Transform == null) continue;

                                             Vector3 toEnemy = enemyView.Transform.position - playerPos;
                                             toEnemy.y = 0;
                                             float distSq = toEnemy.sqrMagnitude;

                                             if (distSq <= capturedAttackRange * capturedAttackRange)
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

                                         int hitCount = Mathf.Min(enemiesInCone.Count, finalProjectileCount);
                                         for (int i = 0; i < hitCount; i++)
                                         {
                                             targetsToHit.Add(enemiesInCone[i].entity);
                                         }
                                     }
                                 }

                                 // Fallback: If no custom AOE/cone targets were acquired, hit the primary target
                                 if (targetsToHit.Count == 0 && finalTargetEnemy != -1)
                                 {
                                     targetsToHit.Add(finalTargetEnemy);
                                 }

                                 // Deal damage to ALL resolved targets!
                                 bool isFirstTarget = true;
                                 foreach (var targetEntity in targetsToHit)
                                 {
                                     if (healthPool.Has(targetEntity) && !deadPool.Has(targetEntity))
                                     {
                                         DealDamageToEnemy(capturedPlayerEntity, targetEntity, playerViewComponent, isFirstTarget);
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
            float totalDamage = 0f;
            bool isCrit = false;

            // 1. Calculate Base Weapon or Unarmed Damage
            if (combat.CurrentWeapon != null)
            {
                baseDamage = combat.CurrentWeapon.GetRandomDamage();
            }
            else
            {
                // Default Unarmed Damage (e.g., 5.0 to 10.0 damage)
                baseDamage = Random.Range(5f, 10f);
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

            totalDamage = baseDamage + offhandBonus;

            // 3. Critical Hit Mechanic (Base 10% chance for a 1.5x damage critical hit)
            float critChance = 10f; 
            if (Random.Range(0f, 100f) < critChance)
            {
                isCrit = true;
                totalDamage *= 1.5f;
            }

            // 4. Subtract Health
            targetHealth.CurrentHealth -= totalDamage;

            // Log damage with rich info
            if (isCrit)
            {
                Debug.Log($"<color=orange>[Combat] CRITICAL HIT! Player dealt {totalDamage:F1} damage (Base: {baseDamage:F1} + Offhand: {offhandBonus:F1}) to enemy! HP left: {targetHealth.CurrentHealth:F1}</color>");
            }
            else
            {
                Debug.Log($"[Combat] Player dealt {totalDamage:F1} damage (Base: {baseDamage:F1} + Offhand: {offhandBonus:F1}) to enemy! HP left: {targetHealth.CurrentHealth:F1}");
            }

            // 5. Handle Enemy Death
            if (targetHealth.CurrentHealth <= 0)
            {
                deadPool.Add(enemyEntity);
            }

            // 6. Flash enemy white on damage
            ref var targetEnemyView = ref viewLinkPool.Get(enemyEntity);
            if (targetEnemyView.Transform != null)
            {
                var monsterView = targetEnemyView.Transform.GetComponent<STG.CurveDash.Views.MonsterView>();
                if (monsterView != null)
                {
                    monsterView.FlashWhite(0.25f);
                }
            }

            // 7. Execute Weapon Socketed Abilities
            if (triggerAbilities && combat.CurrentWeapon != null)
            {
                ref var enemyView = ref viewLinkPool.Get(enemyEntity);
                var weaponAbilities = combat.CurrentWeapon.GetAbilities();
                foreach (var ability in weaponAbilities)
                {
                    if (ability != null)
                    {
                        ability.Execute(playerView.Transform.gameObject, enemyView.Transform.position + Vector3.up * 1f);
                    }
                }
            }

            // 8. Execute Off-hand Socketed Abilities (Shield / Arrows)
            if (triggerAbilities && playerViewComponent != null)
            {
                if (playerViewComponent.LeftHandItem is OffHandData leftOffHand && leftOffHand.Abilities != null)
                {
                    ref var enemyView = ref viewLinkPool.Get(enemyEntity);
                    foreach (var ability in leftOffHand.Abilities)
                    {
                        if (ability != null)
                        {
                            ability.Execute(playerView.Transform.gameObject, enemyView.Transform.position);
                        }
                    }
                }
                else if (playerViewComponent.RightHandItem is OffHandData rightOffHand && rightOffHand.Abilities != null)
                {
                    ref var enemyView = ref viewLinkPool.Get(enemyEntity);
                    foreach (var ability in rightOffHand.Abilities)
                    {
                        if (ability != null)
                        {
                            ability.Execute(playerView.Transform.gameObject, enemyView.Transform.position);
                        }
                    }
                }
            }
        }
    }
}

