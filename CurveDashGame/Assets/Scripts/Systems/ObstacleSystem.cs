using Leopotam.EcsLite;
using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class ObstacleSystem : ITickable
    {
        private readonly ObjectSpawner spawner;
        private const float RotationSpeed = 60.0f;

        private readonly EcsPool<ViewLinkComponent> viewLinkPool;

        private readonly EcsFilter obstacleFilter;
        private readonly EcsFilter playerHitObstacleFilter;
        private readonly EcsFilter deadEnemyFilter;

        public ObstacleSystem(EcsWorld world, ObjectSpawner spawner)
        {
            this.spawner = spawner;

            viewLinkPool = world.GetPool<ViewLinkComponent>();

            obstacleFilter = world.Filter<ObstacleComponent>().End();
            playerHitObstacleFilter = world.Filter<ObstacleComponent>().Inc<PlayerHitObstacleEvent>().End();
            deadEnemyFilter = world.Filter<EnemyComponent>().Inc<EnemyDeadEvent>().End();
        }

        public void Tick()
        {



            foreach (var obstacle in deadEnemyFilter)
            {
                if (viewLinkPool.Has(obstacle))
                {
                    ref var viewLink = ref viewLinkPool.Get(obstacle);
                    if (viewLink.View != null)
                    {
                        Vector3 deathPos = viewLink.View.transform.position;
                        
                        // 40% chance of item dropping
                        if (Random.value <= 0.40f)
                        {
                            float roll = Random.value;
                            if (roll < 0.25f)
                            {
                                spawner.SpawnWeaponPickup(deathPos);
                                Debug.Log("<color=yellow>[EnemyDrop] Enemy dropped a WEAPON!</color>");
                            }
                            else if (roll < 0.50f)
                            {
                                spawner.SpawnArmorPickup(deathPos);
                                Debug.Log("<color=yellow>[EnemyDrop] Enemy dropped an ARMOR piece!</color>");
                            }
                            else if (roll < 0.70f)
                            {
                                spawner.SpawnGemPickup(deathPos);
                                Debug.Log("<color=yellow>[EnemyDrop] Enemy dropped a GEM!</color>");
                            }
                            else if (roll < 0.85f)
                            {
                                spawner.SpawnFlaskPickup(deathPos);
                                Debug.Log("<color=yellow>[EnemyDrop] Enemy dropped a FLASK!</color>");
                            }
                            else
                            {
                                spawner.SpawnCurrencyPickup(deathPos);
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
