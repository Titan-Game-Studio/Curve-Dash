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

                if (combat.CurrentWeapon == null) continue;

                if (combat.CooldownTimer > 0)
                {
                    combat.CooldownTimer -= Time.deltaTime;
                    continue;
                }

                ref var playerView = ref viewLinkPool.Get(playerEntity);
                if (playerView.Transform == null) continue;
                Vector3 playerPos = playerView.Transform.position;

                // Find closest enemy
                int targetEnemy = -1;
                float attackRange = combat.CurrentWeapon.BaseData.BaseAttackRange;
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
                    ref var targetHealth = ref healthPool.Get(targetEnemy);
                    float damage = combat.CurrentWeapon.GetRandomDamage();
                    targetHealth.CurrentHealth -= damage;

                    Debug.Log($"[Combat] Player attacked an enemy for {damage:F1} damage! Enemy HP left: {targetHealth.CurrentHealth:F1}");
                    
                    if (targetHealth.CurrentHealth <= 0)
                    {
                        deadPool.Add(targetEnemy);
                    }

                    // Kích hoạt hiệu ứng chớp trắng cho enemy khi nhận sát thương
                    ref var targetEnemyView = ref viewLinkPool.Get(targetEnemy);
                    if (targetEnemyView.Transform != null)
                    {
                        var monsterView = targetEnemyView.Transform.GetComponent<STG.CurveDash.Views.MonsterView>();
                        if (monsterView != null)
                        {
                            monsterView.FlashWhite(0.25f);
                        }
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

