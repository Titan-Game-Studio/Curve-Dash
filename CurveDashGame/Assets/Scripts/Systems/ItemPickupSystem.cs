using System.Collections.Generic;
using Leopotam.EcsLite;
using Zenject;
using UnityEngine;

namespace STG.CurveDash
{
    public class ItemPickupSystem : ITickable
    {
        private readonly ObjectSpawner spawner;
        private readonly GameAssetCatalog assetCatalog;
        private readonly EcsFilter hitItemFilter;
        private readonly EcsPool<ItemPickupComponent> itemPickupPool;
        private readonly EcsPool<ViewLinkComponent> viewLinkPool;
        private readonly EcsPool<PlayerCombatComponent> combatPool;
        private readonly EcsFilter playerFilter;
        
        private static List<AbilityData> _cachedAbilities;

        // Reset static state when entering Play mode so newly added abilities are picked up
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => _cachedAbilities = null;

        public ItemPickupSystem(EcsWorld world, ObjectSpawner spawner, GameAssetCatalog assetCatalog)
        {
            this.spawner = spawner;
            this.assetCatalog = assetCatalog;
            hitItemFilter = world.Filter<ItemPickupComponent>().Inc<PlayerHitItemEvent>().End();
            playerFilter = world.Filter<PlayerComponent>().Inc<PlayerCombatComponent>().End();
            
            itemPickupPool = world.GetPool<ItemPickupComponent>();
            viewLinkPool = world.GetPool<ViewLinkComponent>();
            combatPool = world.GetPool<PlayerCombatComponent>();
        }

        public void Tick()
        {
            foreach (var itemEntity in hitItemFilter)
            {
                Debug.Log($"[ItemPickupSystem] Starting pickup processing for Entity {itemEntity}");
                try
                {
                    ref var itemViewLink = ref viewLinkPool.Get(itemEntity);
                    if (itemViewLink.View != null)
                    {
                        var itemView = itemViewLink.View.GetComponent<ItemPickupView>();
                        if (itemView != null && itemView.ItemToGive != null)
                        {
                            Debug.Log($"[ItemPickupSystem] Detected Item to give: {itemView.ItemToGive.ItemName} ({itemView.ItemToGive.GetType().Name})");
                                // INTEGRATE DEVION GAMES INVENTORY SYSTEM SAFELY:
                                try
                                {
                                    Debug.Log($"[ItemPickupSystem] Checking Devion Adapter for: {itemView.ItemToGive.ItemName}");
                                    DevionGames.InventorySystem.Item devionAdapter = itemView.ItemToGive.DevionAdapter;
                                    
                                    if (devionAdapter == null && DevionGames.InventorySystem.InventoryManager.Database != null)
                                    {
                                        string targetName = itemView.ItemToGive.name + "_Adapter";
                                        foreach (var dbItem in DevionGames.InventorySystem.InventoryManager.Database.items)
                                        {
                                            if (dbItem != null && dbItem.name == targetName)
                                            {
                                                devionAdapter = dbItem;
                                                itemView.ItemToGive.DevionAdapter = dbItem; // Cache it!
                                                break;
                                            }
                                        }
                                    }

                                    if (devionAdapter != null)
                                    {
                                        Debug.Log($"[ItemPickupSystem] Creating Devion instance for: {devionAdapter.name} (Template Icon: {(devionAdapter.Icon != null ? devionAdapter.Icon.name : "NULL")})");
                                        var devionInstance = DevionGames.InventorySystem.InventoryManager.CreateInstance(devionAdapter);
                                        if (devionInstance != null)
                                        {
                                            // Force explicit synchronization of properties (including Icon) on the newly cloned instance
                                            if (devionInstance is CurveDashEquipmentAdapter equipAdapter)
                                            {
                                                equipAdapter.SyncData();
                                                Debug.Log($"[ItemPickupSystem] Forced SyncData on equipment instance. Name: {equipAdapter.Name}, Icon: {(equipAdapter.Icon != null ? equipAdapter.Icon.name : "NULL")}");
                                            }
                                            else if (devionInstance is CurveDashItemAdapter itemAdapter)
                                            {
                                                itemAdapter.SyncData();
                                                Debug.Log($"[ItemPickupSystem] Forced SyncData on item instance. Name: {itemAdapter.Name}, Icon: {(itemAdapter.Icon != null ? itemAdapter.Icon.name : "NULL")}");
                                            }

                                            Debug.Log($"[ItemPickupSystem] Attempting to AddItem to Container 'Inventory' with Icon: {(devionInstance.Icon != null ? devionInstance.Icon.name : "NULL")}...");
                                            bool added = DevionGames.InventorySystem.ItemContainer.AddItem("Inventory", devionInstance);
                                            Debug.Log($"[ItemPickupSystem] AddItem successful? {added}");
                                        }
                                    }
                                    else
                                    {
                                        Debug.LogWarning($"[ItemPickupSystem] Devion Adapter not found for: {itemView.ItemToGive.ItemName}");
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    Debug.LogError($"[ItemPickupSystem] Exception inside Devion Games integration block: {ex.Message}\n{ex.StackTrace}");
                                }

                                Debug.Log($"<color=green>[ItemPickup] Player picked up '{itemView.ItemToGive.ItemName}' successfully!</color>");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[ItemPickupSystem] Critical error in Tick loop for Entity {itemEntity}: {ex.Message}\n{ex.StackTrace}");
                }
                finally
                {
                    Debug.Log($"[ItemPickupSystem] Finalizing despawn for Entity {itemEntity}");
                    spawner.DespawnObject(itemEntity);
                }
            }
        }

        public static void AutoLinkTestingAbilities(WeaponInstance instance, WeaponData weaponBase, ItemCatalog catalog = null)
        {
            if (instance == null || weaponBase == null) return;

            // Clear any existing dynamic abilities to make it clean for testing
            instance.DynamicAbilities.Clear();

            // Load all available abilities (active & support) from catalog/resources/Editor database
            var allAbilities = GetAvailableAbilities(catalog);
            
            // Separate them into Active abilities (PoEAbility) and Support abilities (SupportAbilityData)
            List<PoEAbility> activeAbilities = new List<PoEAbility>();
            List<SupportAbilityData> supportAbilities = new List<SupportAbilityData>();

            foreach (var ab in allAbilities)
            {
                if (ab is PoEAbility active)
                {
                    activeAbilities.Add(active);
                }
                else if (ab is SupportAbilityData support)
                {
                    supportAbilities.Add(support);
                }
            }

            PoEAbility selectedActive = null;
            List<SupportAbilityData> selectedSupports = new List<SupportAbilityData>();

            // Match active abilities based on the type of weapon
            if (weaponBase is BowData)
            {
                // Find a ranged active skill (e.g. Ice Shot or Tornado Shot)
                selectedActive = activeAbilities.Find(a => a.SkillType == PoEAbilityType.Ranged && a.AbilityName.ToLower().Contains("ice"));
                if (selectedActive == null) selectedActive = activeAbilities.Find(a => a.SkillType == PoEAbilityType.Ranged);

                // Find compatible supports: LMP and Faster Projectiles
                var lmp = supportAbilities.Find(s => s.AbilityName.ToLower().Contains("lesser"));
                var fastProj = supportAbilities.Find(s => s.AbilityName.ToLower().Contains("faster"));
                
                if (lmp != null) selectedSupports.Add(lmp);
                if (fastProj != null) selectedSupports.Add(fastProj);
            }
            else if (weaponBase is TwoHandedWeaponData || weaponBase is OneHandedWeaponData)
            {
                // Find a melee active skill (e.g. Heavy Strike for Two-Handed, Cyclone for One-Handed)
                if (weaponBase is TwoHandedWeaponData)
                {
                    selectedActive = activeAbilities.Find(a => a.SkillType == PoEAbilityType.Melee && a.AbilityName.ToLower().Contains("heavy"));
                }
                if (selectedActive == null) selectedActive = activeAbilities.Find(a => a.SkillType == PoEAbilityType.Melee && a.AbilityName.ToLower().Contains("cyclone"));
                if (selectedActive == null) selectedActive = activeAbilities.Find(a => a.SkillType == PoEAbilityType.Melee);

                // Find compatible supports: Melee Physical Damage and Increased Critical Strikes
                var meleePhys = supportAbilities.Find(s => s.AbilityName.ToLower().Contains("melee physical"));
                var critStrike = supportAbilities.Find(s => s.AbilityName.ToLower().Contains("critical strikes"));

                if (meleePhys != null) selectedSupports.Add(meleePhys);
                if (critStrike != null) selectedSupports.Add(critStrike);
            }
            else
            {
                // Spell/generic fallback: Find a spell (e.g. Fireball)
                selectedActive = activeAbilities.Find(a => a.SkillType == PoEAbilityType.Spell && a.AbilityName.ToLower().Contains("fireball"));
                if (selectedActive == null) selectedActive = activeAbilities.Find(a => a.SkillType == PoEAbilityType.Spell);

                // Find compatible supports: LMP and Elemental Focus
                var lmp = supportAbilities.Find(s => s.AbilityName.ToLower().Contains("lesser"));
                var eleFocus = supportAbilities.Find(s => s.AbilityName.ToLower().Contains("elemental focus"));

                if (lmp != null) selectedSupports.Add(lmp);
                if (eleFocus != null) selectedSupports.Add(eleFocus);
            }

            // Fallback: if we didn't find any specific skill, pick the first active
            if (selectedActive == null && activeAbilities.Count > 0)
            {
                selectedActive = activeAbilities[0];
            }

            // Assign the matched active skill and supports to the weapon sockets!
            if (selectedActive != null)
            {
                instance.DynamicAbilities.Add(selectedActive);
                string supportsAddedLog = "";

                // Add as many compatible supports as sockets permit (MaxSockets - 1)
                int maxSupportsToLink = Mathf.Min(selectedSupports.Count, weaponBase.MaxSockets - 1);
                for (int i = 0; i < maxSupportsToLink; i++)
                {
                    instance.DynamicAbilities.Add(selectedSupports[i]);
                    supportsAddedLog += $", {selectedSupports[i].AbilityName}";
                }

                Debug.Log($"<color=orange>[AutoTestLink] Picked up '{weaponBase.ItemName}'. Automatically socketed Active: '{selectedActive.AbilityName}'{supportsAddedLog} for testing!</color>");
            }
            else
            {
                Debug.LogWarning($"[AutoTestLink] Picked up '{weaponBase.ItemName}' but could not find any active skills in the project to auto-socket!");
            }
        }

        /// <summary>
        /// Thu thập tất cả các kỹ năng độc bản hiện hữu trong Catalog hoặc nạp từ thư mục Resources/AssetDatabase
        /// </summary>
        public static List<AbilityData> GetAvailableAbilities(ItemCatalog catalog = null)
        {
            if (_cachedAbilities != null) return _cachedAbilities;

            _cachedAbilities = new List<AbilityData>();

            // Cách 1: Quét toàn bộ MasterItemCatalog để thu thập kỹ năng từ các vũ khí mẫu
            if (catalog != null)
            {
                foreach (var item in catalog.Items)
                {
                    if (item is WeaponData weapon && weapon.Abilities != null)
                    {
                        foreach (var ab in weapon.Abilities)
                        {
                            if (ab != null && !_cachedAbilities.Contains(ab))
                            {
                                _cachedAbilities.Add(ab);
                            }
                        }
                    }
                }
            }

#if UNITY_EDITOR
            // Cách 2: (Chỉ chạy trong Editor) Quét trực tiếp toàn bộ Project để lấy mọi AbilityData & SupportAbilityData
            // HÃY LUÔN CHẠY BƯỚC NÀY để nạp đầy đủ các kỹ năng vừa tạo mới chưa kịp đăng ký vào Catalog!
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:AbilityData");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var ab = UnityEditor.AssetDatabase.LoadAssetAtPath<AbilityData>(path);
                if (ab != null && !_cachedAbilities.Contains(ab))
                {
                    _cachedAbilities.Add(ab);
                }
            }
#endif

            // Cách 3: Dự phòng (Fallback) nếu chạy bản Build - nạp toàn bộ AbilityData có trong thư mục Resources
            if (_cachedAbilities.Count == 0)
            {
                var loadedAbilities = Resources.LoadAll<AbilityData>("");
                foreach (var ab in loadedAbilities)
                {
                    if (ab != null && !_cachedAbilities.Contains(ab))
                    {
                        _cachedAbilities.Add(ab);
                    }
                }
            }

            Debug.Log($"[ItemPickupSystem] Collected {_cachedAbilities.Count} unique available abilities for random rolling/auto-testing.");
            return _cachedAbilities;
        }
    }
}
