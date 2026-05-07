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
                    // Deal damage using our refined damage-dealing function
                    DealDamageToEnemy(playerEntity, targetEnemy, playerViewComponent);

                    // Set cooldown: Cooldown = 1 / attackSpeed
                    combat.CooldownTimer = 1f / attackSpeed;
                    
                    if (playerViewComponent != null)
                    {
                        playerViewComponent.PlayAttack();
                    }
                }
            }
        }

        /// <summary>
        /// Refined damage-dealing function. Handles base weapon/unarmed damage, off-hand/shield bonus damage,
        /// critical hits, white flash visual feedback, and executions of weapon & off-hand PoE socketed abilities.
        /// </summary>
        private void DealDamageToEnemy(int playerEntity, int enemyEntity, PlayerView playerViewComponent)
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
            if (combat.CurrentWeapon != null && combat.CurrentWeapon.BaseData.Abilities != null)
            {
                ref var enemyView = ref viewLinkPool.Get(enemyEntity);
                foreach (var ability in combat.CurrentWeapon.BaseData.Abilities)
                {
                    if (ability != null)
                    {
                        ability.Execute(playerView.Transform.gameObject, enemyView.Transform.position);
                    }
                }
            }

            // 8. Execute Off-hand Socketed Abilities (Shield / Arrows)
            if (playerViewComponent != null)
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

