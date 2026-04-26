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
            enemyFilter = world.Filter<EnemyHealthComponent>().Inc<ViewLinkComponent>().Exc<EnemyDeadEvent>().End();
            
            combatPool = world.GetPool<PlayerCombatComponent>();
            viewLinkPool = world.GetPool<ViewLinkComponent>();
            healthPool = world.GetPool<EnemyHealthComponent>();
            deadPool = world.GetPool<EnemyDeadEvent>();
        }

        public void Tick()
        {
            foreach (var playerEntity in playerFilter)
            {
                ref var combat = ref combatPool.Get(playerEntity);
                if (combat.CurrentWeapon == null) continue;

                if (combat.CooldownTimer > 0)
                {
                    combat.CooldownTimer -= Time.deltaTime;
                    continue;
                }

                ref var playerView = ref viewLinkPool.Get(playerEntity);
                Vector3 playerPos = playerView.Transform.position;

                // Find closest enemy
                int targetEnemy = -1;
                float closestDistSq = combat.CurrentWeapon.AttackRange * combat.CurrentWeapon.AttackRange;

                foreach (var enemyEntity in enemyFilter)
                {
                    ref var enemyView = ref viewLinkPool.Get(enemyEntity);
                    float distSq = (enemyView.Transform.position - playerPos).sqrMagnitude;

                    if (distSq <= closestDistSq)
                    {
                        closestDistSq = distSq;
                        targetEnemy = enemyEntity;
                    }
                }

                if (targetEnemy != -1)
                {
                    ref var targetHealth = ref healthPool.Get(targetEnemy);
                    targetHealth.CurrentHealth -= combat.CurrentWeapon.Damage;
                    
                    Debug.Log($"[Combat] Player attacked an enemy for {combat.CurrentWeapon.Damage} damage! Enemy HP left: {targetHealth.CurrentHealth}");

                    if (targetHealth.CurrentHealth <= 0)
                    {
                        deadPool.Add(targetEnemy);
                    }

                    combat.CooldownTimer = combat.CurrentWeapon.AttackCooldown;
                }
            }
        }
    }
}

