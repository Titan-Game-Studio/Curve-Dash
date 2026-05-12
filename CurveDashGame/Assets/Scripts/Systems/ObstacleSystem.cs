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
            // foreach (var obstacle in playerHitObstacleFilter)
            //     spawner.DespawnObject(obstacle);


            foreach (var obstacle in deadEnemyFilter)
                spawner.DespawnObject(obstacle);
        }
    }
}
