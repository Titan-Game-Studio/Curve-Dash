using Leopotam.EcsLite;
using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class EnemyMovementSystem : ITickable
    {
        private readonly EcsWorld _world;
        private readonly EcsFilter _enemyFilter;
        private readonly EcsPool<EnemyComponent> _enemyPool;
        private readonly EcsPool<ViewLinkComponent> _viewLinkPool;

        public EnemyMovementSystem(EcsWorld world)
        {
            _world = world;
            _enemyFilter = world.Filter<EnemyComponent>().Inc<ViewLinkComponent>().Exc<EnemyDeadEvent>().End();
            _enemyPool = world.GetPool<EnemyComponent>();
            _viewLinkPool = world.GetPool<ViewLinkComponent>();
        }

        public void Tick()
        {
            Vector3 playerPos = Vector3.zero;
            bool foundPlayer = false;
            var playerFilter = _world.Filter<PlayerComponent>().Inc<ViewLinkComponent>().End();
            var playerViewLinkPool = _world.GetPool<ViewLinkComponent>();
            foreach (var playerEntity in playerFilter)
            {
                ref var pViewLink = ref playerViewLinkPool.Get(playerEntity);
                if (pViewLink.Transform != null)
                {
                    playerPos = pViewLink.Transform.position;
                    foundPlayer = true;
                    break;
                }
            }

            foreach (var entity in _enemyFilter)
            {
                ref var enemy = ref _enemyPool.Get(entity);
                ref var viewLink = ref _viewLinkPool.Get(entity);

                if (viewLink.Transform != null && foundPlayer && !enemy.IsChargingAttack)
                {
                    Vector3 diff = playerPos - viewLink.Transform.position;

                    enemy.ZigzagTimer -= Time.deltaTime;
                    if (enemy.ZigzagTimer <= 0f || enemy.CurrentDirection == Vector3.zero)
                    {
                        enemy.ZigzagTimer = Random.Range(0.6f, 1.2f); // Thay đổi hướng zigzag ngẫu nhiên theo chu kỳ

                        Vector3 dirX = diff.x > 0 ? Vector3.right : Vector3.left;
                        Vector3 dirZ = diff.z > 0 ? Vector3.forward : Vector3.back;

                        if (Mathf.Abs(diff.x) < 0.3f)
                        {
                            enemy.CurrentDirection = dirZ;
                        }
                        else if (Mathf.Abs(diff.z) < 0.3f)
                        {
                            enemy.CurrentDirection = dirX;
                        }
                        else
                        {
                            // Luân phiên di chuyển theo trục X và trục Z để tạo ra đường đi zigzag giống người chơi
                            if (enemy.CurrentDirection == dirX || enemy.CurrentDirection == -dirX)
                            {
                                enemy.CurrentDirection = dirZ;
                            }
                            else
                            {
                                enemy.CurrentDirection = dirX;
                            }
                        }
                    }

                    viewLink.Transform.position += enemy.CurrentDirection * enemy.MoveSpeed * Time.deltaTime;
                    viewLink.Transform.rotation = Quaternion.LookRotation(enemy.CurrentDirection, Vector3.up);
                }

                // Handle attack cooldown
                if (enemy.AttackCooldown > 0f)
                {
                    enemy.AttackCooldown -= Time.deltaTime;
                }

                // Handle attack charge
                if (enemy.IsChargingAttack)
                {
                    enemy.AttackTimer -= Time.deltaTime;

                    if (foundPlayer && viewLink.Transform != null)
                    {
                        float distance = Vector3.Distance(playerPos, viewLink.Transform.position);
                        if (distance > 1.8f) // Dashing/escaping distance threshold
                        {
                            enemy.IsChargingAttack = false;
                            enemy.AttackCooldown = 0.5f; // Small cooldown before another attack attempt can start
                            UnityEngine.Debug.Log($"<color=cyan>[EnemyAttack] Player ESCAPED the attack range of Enemy {entity}! Attack canceled.</color>");
                            continue;
                        }
                    }

                    if (enemy.AttackTimer <= 0f)
                    {
                        enemy.IsChargingAttack = false;
                        enemy.AttackCooldown = 1.5f; // 1.5s cooldown before next attack can start

                        // Deal damage to player by triggering the hit event!
                        var playerFilter2 = _world.Filter<PlayerComponent>().End();
                        foreach (var playerEntity in playerFilter2)
                        {
                            var hitPool = _world.GetPool<PlayerHitByEnemyEvent>();
                            if (!hitPool.Has(playerEntity))
                            {
                                hitPool.Add(playerEntity);
                            }
                        }
                        UnityEngine.Debug.Log($"<color=red>[EnemyAttack] Enemy {entity} CHARGE COMPLETED! Player was still in range. Dealt damage.</color>");
                    }
                }
            }
        }
    }
}
