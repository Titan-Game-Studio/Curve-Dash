using Leopotam.EcsLite;
using Zenject;
using UnityEngine;

namespace STG.CurveDash
{
    public class PowerUpPickupSystem : ITickable
    {
        private readonly ObjectSpawner spawner;
        private readonly EcsFilter hitMountFilter;
        private readonly EcsFilter hitAuraFilter;
        private readonly EcsPool<ViewLinkComponent> viewLinkPool;
        private readonly EcsFilter playerFilter;

        public PowerUpPickupSystem(EcsWorld world, ObjectSpawner spawner)
        {
            this.spawner = spawner;
            hitMountFilter = world.Filter<MountPickupComponent>().Inc<PlayerHitMountEvent>().End();
            hitAuraFilter = world.Filter<AuraPickupComponent>().Inc<PlayerHitAuraEvent>().End();
            playerFilter = world.Filter<PlayerComponent>().End();
            viewLinkPool = world.GetPool<ViewLinkComponent>();
        }

        public void Tick()
        {
            // Handle Mount Pickups
            foreach (var mountEntity in hitMountFilter)
            {
                ref var viewLink = ref viewLinkPool.Get(mountEntity);
                var mountView = viewLink.View.GetComponent<MountPickupView>();
                
                if (mountView != null)
                {
                    foreach (var playerEntity in playerFilter)
                    {
                        ref var playerViewLink = ref viewLinkPool.Get(playerEntity);
                        var playerView = playerViewLink.View.GetComponent<PlayerView>();
                        if (playerView != null)
                        {
                            playerView.UpdateSkin(mountView.MountIndex);
                        }
                    }
                }
                
                Debug.Log($"[PowerUpPickupSystem] Player picked up Mount {mountView?.MountIndex}. Despawning entity {mountEntity}");
                mountView?.gameObject.SetActive(false); // Force hide
                spawner.DespawnObject(mountEntity);
            }

            // Handle Aura Pickups
            foreach (var auraEntity in hitAuraFilter)
            {
                ref var viewLink = ref viewLinkPool.Get(auraEntity);
                var auraView = viewLink.View.GetComponent<AuraPickupView>();
                
                if (auraView != null)
                {
                    foreach (var playerEntity in playerFilter)
                    {
                        ref var playerViewLink = ref viewLinkPool.Get(playerEntity);
                        var playerView = playerViewLink.View.GetComponent<PlayerView>();
                        if (playerView != null)
                        {
                            playerView.UpdateAura(auraView.AuraIndex);
                        }
                    }
                }
                
                Debug.Log($"[PowerUpPickupSystem] Player picked up Aura {auraView?.AuraIndex}. Despawning entity {auraEntity}");
                auraView?.gameObject.SetActive(false); // Force hide
                spawner.DespawnObject(auraEntity);
            }
        }
    }
}
