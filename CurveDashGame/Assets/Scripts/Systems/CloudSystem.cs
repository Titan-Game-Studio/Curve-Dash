using Leopotam.EcsLite;
using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class CloudSystem : ITickable
    {
        private readonly EcsWorld world;
        private readonly ObjectSpawner spawner;
        private readonly EcsFilter cloudFilter;
        private readonly EcsPool<CloudComponent> cloudPool;
        private readonly EcsPool<ViewLinkComponent> viewLinkPool;

        public CloudSystem(EcsWorld world, ObjectSpawner spawner)
        {
            this.world = world;
            this.spawner = spawner;
            cloudFilter = world.Filter<CloudComponent>().End();
            cloudPool = world.GetPool<CloudComponent>();
            viewLinkPool = world.GetPool<ViewLinkComponent>();
        }

        public void Tick()
        {
            foreach (var entity in cloudFilter)
            {
                ref var cloudComponent = ref cloudPool.Get(entity);
                ref var viewLinkComponent = ref viewLinkPool.Get(entity);
                
                cloudComponent.Lifetime -= Time.deltaTime;
                if (cloudComponent.Lifetime <= 0)
                {
                    spawner.DespawnObject(entity);
                    continue; // Skip moving after despawn
                }

                // Move cloud backwards continuously directly in world space
                viewLinkComponent.Transform.Translate(Vector3.back * cloudComponent.Speed * Time.deltaTime, Space.World);
            }
        }
    }
}
