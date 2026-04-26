using Leopotam.EcsLite;
using Zenject;
using UnityEngine;

namespace STG.CurveDash
{
    public class WeaponPickupSystem : ITickable
    {
        private readonly ObjectSpawner spawner;
        private readonly EcsFilter hitWeaponFilter;
        private readonly EcsPool<WeaponPickupComponent> weaponPickupPool;
        private readonly EcsPool<ViewLinkComponent> viewLinkPool;
        private readonly EcsPool<PlayerCombatComponent> combatPool;
        private readonly EcsFilter playerFilter;

        public WeaponPickupSystem(EcsWorld world, ObjectSpawner spawner)
        {
            this.spawner = spawner;
            hitWeaponFilter = world.Filter<WeaponPickupComponent>().Inc<PlayerHitWeaponEvent>().End();
            playerFilter = world.Filter<PlayerComponent>().Inc<PlayerCombatComponent>().End();
            
            weaponPickupPool = world.GetPool<WeaponPickupComponent>();
            viewLinkPool = world.GetPool<ViewLinkComponent>();
            combatPool = world.GetPool<PlayerCombatComponent>();
        }

        public void Tick()
        {
            foreach (var weaponEntity in hitWeaponFilter)
            {
                ref var weaponViewLink = ref viewLinkPool.Get(weaponEntity);
                var weaponView = weaponViewLink.View.GetComponent<WeaponPickupView>();
                
                if (weaponView != null && weaponView.WeaponToGive != null)
                {
                    // Give weapon to player
                    foreach (var playerEntity in playerFilter)
                    {
                        ref var combat = ref combatPool.Get(playerEntity);
                        combat.CurrentWeapon = weaponView.WeaponToGive;
                        combat.CooldownTimer = 0f;

                        ref var playerViewLink = ref viewLinkPool.Get(playerEntity);
                        var playerView = playerViewLink.View.GetComponent<PlayerView>();
                        if (playerView != null)
                        {
                            playerView.EquipWeapon(weaponView.WeaponToGive);
                        }

                        Debug.Log($"[WeaponPickup] Player equipped {weaponView.WeaponToGive.WeaponName}");
                    }
                }
                
                // Despawn the pickup
                spawner.DespawnObject(weaponEntity);
            }
        }
    }
}
