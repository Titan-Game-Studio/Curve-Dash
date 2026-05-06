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
                float attackRange = combat.CurrentWeapon.BaseData.BaseAttackRange;
                float closestDistSq = attackRange * attackRange;

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
                    float damage = combat.CurrentWeapon.GetRandomDamage();
                    targetHealth.CurrentHealth -= damage;

                    Debug.Log($"[Combat] Player attacked an enemy for {damage:F1} damage! Enemy HP left: {targetHealth.CurrentHealth:F1}");
                    
                    if (targetHealth.CurrentHealth <= 0)
                    {
                        deadPool.Add(targetEnemy);
                    }

                    // Kích hoạt toàn bộ chiêu thức trong Socket
                    if (combat.CurrentWeapon.BaseData.Abilities != null)
                    {
                        ref var enemyView = ref viewLinkPool.Get(targetEnemy);
                        foreach (var ability in combat.CurrentWeapon.BaseData.Abilities)
                        {
                            if (ability != null)
                            {
                                ability.Execute(playerView.Transform.gameObject, enemyView.Transform.position);
                            }
                        }
                    }

                    // Tốc độ đánh: Cooldown = 1 / AttackSpeed
                    combat.CooldownTimer = 1f / combat.CurrentWeapon.FinalAttackSpeed;
                    
                    var playerViewComponent = playerView.Transform.GetComponent<PlayerView>();
                    if (playerViewComponent != null)
                    {
                        playerViewComponent.PlayAttack();
                    }
                }
            }
        }
    }
}

