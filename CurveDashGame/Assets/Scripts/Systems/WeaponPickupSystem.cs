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
                
                if (weaponView != null && weaponView.ItemToGive != null)
                {
                    foreach (var playerEntity in playerFilter)
                    {
                        ref var combat = ref combatPool.Get(playerEntity);
                        
                        // Nếu là vũ khí tấn công thì mới cập nhật chỉ số Combat
                        if (weaponView.ItemToGive is WeaponData weaponBase)
                        {
                            // Khởi tạo một bản instance mới (Đây là lúc có thể Roll Affix)
                            var instance = new WeaponInstance(weaponBase);
                            
                            // TẠM THỜI: Tự động tặng 1 Affix ngẫu nhiên để test
                            // Sau này bạn có thể viết logic Roll xịn hơn ở đây
                            
                            combat.CurrentWeapon = instance;
                            combat.CooldownTimer = 0f;
                        }


                        ref var playerViewLink = ref viewLinkPool.Get(playerEntity);
                        var playerView = playerViewLink.View.GetComponent<PlayerView>();
                        if (playerView != null)
                        {
                            playerView.Equip(weaponView.ItemToGive);
                        }

                        Debug.Log($"[WeaponPickup] Player picked up {weaponView.ItemToGive.ItemName}");

                    }
                }
                
                spawner.DespawnObject(weaponEntity);
            }
        }
    }
}
