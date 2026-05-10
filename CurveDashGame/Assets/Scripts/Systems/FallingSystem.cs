using Leopotam.EcsLite;
using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class FallingSystem : ITickable
    {
        private readonly ObjectSpawner spawner;
        private readonly EcsFilter fallingFilter;
        private readonly EcsPool<FallingComponent> fallingPool;
        private readonly EcsPool<ViewLinkComponent> viewLinkPool;
        private readonly EcsPool<PlayerComponent> playerPool;
        
        public FallingSystem(EcsWorld world, ObjectSpawner spawner)
        {
            this.spawner = spawner;
            fallingPool = world.GetPool<FallingComponent>();
            viewLinkPool = world.GetPool<ViewLinkComponent>();
            playerPool = world.GetPool<PlayerComponent>();
            fallingFilter = world.Filter<FallingComponent>().End();
        }
        
        public void Tick()
        {
            foreach (var entity in fallingFilter)
            {
                ref var fallingComponent = ref fallingPool.Get(entity);
                fallingComponent.FallingDelay -= Time.deltaTime;
                if (fallingComponent.FallingDelay <= 0.0f && !fallingComponent.Falling)
                {
                    ref var viewLinkComponent = ref viewLinkPool.Get(entity);
                    
                    // Only apply physical gravity drop if the falling entity is the Player
                    if (playerPool.Has(entity) && viewLinkComponent.View != null)
                    {
                        foreach (var rb in viewLinkComponent.View.GetComponentsInChildren<Rigidbody>())
                            rb.isKinematic = false;
                    }
                        
                    fallingComponent.Falling = true;
                }
                
                if (fallingComponent.FallingDelay <= -2.0f)
                    spawner.DespawnObject(entity);
            }
        }
    }
}