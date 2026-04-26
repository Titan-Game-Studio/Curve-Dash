using Leopotam.EcsLite;
using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class ShieldSystem : ITickable
    {
        private readonly ObjectSpawner spawner;
        private const float RotationSpeed = 60.0f;

        private readonly EcsPool<ViewLinkComponent> viewLinkPool;
        private readonly EcsFilter shieldFilter;
        private readonly EcsFilter playerHitShieldFilter;

        public ShieldSystem(EcsWorld world, ObjectSpawner spawner)
        {
            this.spawner = spawner;

            viewLinkPool = world.GetPool<ViewLinkComponent>();
            shieldFilter = world.Filter<ShieldComponent>().End();
            playerHitShieldFilter = world.Filter<ShieldComponent>().Inc<PlayerHitShieldEvent>().End();
        }

        public void Tick()
        {
            float delta = RotationSpeed * Time.deltaTime;
            foreach (var shield in shieldFilter)
            {
                ref var viewLinkComponent = ref viewLinkPool.Get(shield);
                Transform transform = viewLinkComponent.Transform;
                transform.Rotate(Vector3.up, delta, Space.World);
            }

            foreach (var shield in playerHitShieldFilter)
                spawner.DespawnObject(shield);
        }
    }
}

