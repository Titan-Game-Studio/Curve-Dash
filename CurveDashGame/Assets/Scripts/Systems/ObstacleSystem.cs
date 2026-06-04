using Leopotam.EcsLite;
using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class ObstacleSystem : ITickable
    {
        private readonly ObjectSpawner spawner;
        private readonly PlayerStatService playerStatService;
        private const float RotationSpeed = 60.0f;

        private readonly EcsPool<ViewLinkComponent> viewLinkPool;
        private readonly EcsPool<EnemyComponent> enemyPool;

        private readonly EcsFilter obstacleFilter;
        private readonly EcsFilter playerHitObstacleFilter;
        private readonly EcsFilter deadEnemyFilter;

        public ObstacleSystem(EcsWorld world, ObjectSpawner spawner, PlayerStatService playerStatService)
        {
            this.spawner = spawner;
            this.playerStatService = playerStatService;

            viewLinkPool = world.GetPool<ViewLinkComponent>();
            enemyPool = world.GetPool<EnemyComponent>();

            obstacleFilter = world.Filter<ObstacleComponent>().End();
            playerHitObstacleFilter = world.Filter<ObstacleComponent>().Inc<PlayerHitObstacleEvent>().End();
            deadEnemyFilter = world.Filter<EnemyComponent>().Inc<EnemyDeadEvent>().End();
        }

        public void Tick()
        {



            foreach (var obstacle in deadEnemyFilter)
            {
                ref var enemy = ref enemyPool.Get(obstacle);
                if (enemy.Data != null)
                    playerStatService.AddScore((int)enemy.Data.ExperienceReward);

                if (viewLinkPool.Has(obstacle))
                {
                    ref var viewLink = ref viewLinkPool.Get(obstacle);
                    if (viewLink.View != null)
                    {
                        Transform parentTransform = viewLink.View.transform.parent;
                        Vector3 deathPos = viewLink.View.transform.position;
                        
                        // 100% chance of item dropping for incredible rewarding gameplay feedback!
                        if (Random.value <= 1.0f)
                        {
                            float roll = Random.value;
                            if (roll < 0.22f)
                            {
                                // Weapon rolls affixes at the monster's level (deeper = stronger tiers).
                                spawner.SpawnWeaponPickup(deathPos, parentTransform, enemy.Level);
                                Debug.Log($"<color=yellow>[EnemyDrop] Enemy (lvl {enemy.Level}) dropped a WEAPON!</color>");
                            }
                            else if (roll < 0.44f)
                            {
                                // Armor rolls affixes at the monster's level too.
                                spawner.SpawnArmorPickup(deathPos, parentTransform, enemy.Level);
                                Debug.Log($"<color=yellow>[EnemyDrop] Enemy (lvl {enemy.Level}) dropped an ARMOR piece!</color>");
                            }
                            else if (roll < 0.60f)
                            {
                                // Accessory (amulet/ring/belt) rolls affixes at the monster's level.
                                spawner.SpawnAccessoryPickup(deathPos, parentTransform, enemy.Level);
                                Debug.Log($"<color=yellow>[EnemyDrop] Enemy (lvl {enemy.Level}) dropped an ACCESSORY!</color>");
                            }
                            else if (roll < 0.74f)
                            {
                                spawner.SpawnGemPickup(deathPos, parentTransform);
                                Debug.Log("<color=yellow>[EnemyDrop] Enemy dropped a GEM!</color>");
                            }
                            else if (roll < 0.87f)
                            {
                                spawner.SpawnFlaskPickup(deathPos, parentTransform);
                                Debug.Log("<color=yellow>[EnemyDrop] Enemy dropped a FLASK!</color>");
                            }
                            else
                            {
                                spawner.SpawnCurrencyPickup(deathPos, parentTransform);
                                Debug.Log("<color=yellow>[EnemyDrop] Enemy dropped CURRENCY!</color>");
                            }
                        }
                    }
                }
                spawner.DespawnObject(obstacle);
            }
        }
    }
}
