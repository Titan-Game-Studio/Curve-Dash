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
        private readonly EcsFilter ballHitObstacleFilter;

        public ObstacleSystem(EcsWorld world, ObjectSpawner spawner)
        {
            this.spawner = spawner;

            viewLinkPool = world.GetPool<ViewLinkComponent>();

            obstacleFilter = world.Filter<ObstacleComponent>().End();
            ballHitObstacleFilter = world.Filter<ObstacleComponent>().Inc<BallHitObstacleEvent>().End();
        }

        public void Tick()
        {
            foreach (var obstacle in ballHitObstacleFilter)
                spawner.DespawnObject(obstacle);
        }
    }
}