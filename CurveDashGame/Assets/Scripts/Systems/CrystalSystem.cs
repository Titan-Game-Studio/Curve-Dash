using Leopotam.EcsLite;
using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class CrystalSystem : ITickable
    {
        private readonly ObjectSpawner spawner;
        private const float RotationSpeed = 60.0f;

        private readonly EcsPool<ViewLinkComponent> viewLinkPool;
        
        private readonly EcsFilter crystalFilter;
        private readonly EcsFilter ballHitCrystalFilter;

        public CrystalSystem(EcsWorld world, ObjectSpawner spawner)
        {
            this.spawner = spawner;

            viewLinkPool = world.GetPool<ViewLinkComponent>();
            
            crystalFilter = world.Filter<CrystalComponent>().End();
            ballHitCrystalFilter = world.Filter<CrystalComponent>().Inc<BallHitComponent>().End();
        }

        public void Tick()
        {
            float delta = RotationSpeed * Time.deltaTime;
            foreach (var crystal in crystalFilter)
            {
                ref var viewLinkComponent = ref viewLinkPool.Get(crystal);
                
                Transform transform = viewLinkComponent.Transform;

                transform.Rotate(Vector3.up, delta, Space.World);
            }

            foreach (var crystal in ballHitCrystalFilter)
                spawner.DespawnObject(crystal);
        }
    }
}