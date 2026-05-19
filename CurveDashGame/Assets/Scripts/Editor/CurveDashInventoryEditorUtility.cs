using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

namespace STG.CurveDash.Editor
{
    public static class CurveDashInventoryEditorUtility
    {
        [MenuItem("Curve-Dash/Database/Sync & Build All Database Assets (One-Click)", false, 10)]
        public static void SyncAndBuildAllAssets()
        {
            // Progress Bar to give premium feedback!
            EditorUtility.DisplayProgressBar("Curve-Dash Database Sync", "Starting Master Database Synchronization...", 0f);

            try
            {
                // Step 1: Link and register Weapon Prefabs & Addressables
                EditorUtility.DisplayProgressBar("Curve-Dash Database Sync", "[1/5] Syncing Weapon Prefabs & Addressables...", 0.2f);
                int weaponCount = RunWeaponPrefabLinking();

                // Step 2: Link and register Armor Prefabs & Addressables
                EditorUtility.DisplayProgressBar("Curve-Dash Database Sync", "[2/5] Mapping Armor Prefabs & Addressables...", 0.4f);
                int armorCount = RunArmorPrefabLinking();

                // Step 3: Link and register Default Pickup Prefabs & Addressables
                EditorUtility.DisplayProgressBar("Curve-Dash Database Sync", "[3/5] Mapping Gems, Flasks & Currencies Pickup Prefabs...", 0.6f);
                int defaultCount = RunDefaultPickupPrefabLinking();

                // Step 4: Create, update, and link Devion Adapters for ALL ItemData
                EditorUtility.DisplayProgressBar("Curve-Dash Database Sync", "[4/5] Generating Devion Inventory Adapters...", 0.8f);
                int adapterCount = RunAdapterGenerationAndLinking();

                // Step 5: Clean and validate all DTOs (Delete any remaining null prefabs)
                EditorUtility.DisplayProgressBar("Curve-Dash Database Sync", "[5/5] Cleaning & Validating DTOs...", 0.9f);
                int deletedCount = CleanAndValidateAllDTOs();

                // Step 6: Finalize & Refresh AssetDatabase
                EditorUtility.DisplayProgressBar("Curve-Dash Database Sync", "Finalizing assets & updating Unity project...", 0.95f);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.ClearProgressBar();

                // Show elegant success dialog
                string message = $"💎 Curve-Dash Database synchronized successfully!\n\n" +
                                 $"• {weaponCount} Weapon/Equippable prefabs linked & registered to Addressables.\n" +
                                 $"• {armorCount} Armor prefabs mapped, linked, and registered to Addressables.\n" +
                                 $"• {defaultCount} Gem, Flask & Currency default pickup prefabs linked & registered to Addressables.\n" +
                                 $"• {adapterCount} Devion Inventory Adapters created/updated & linked back to ItemData.\n" +
                                 $"• {deletedCount} Orphaned/Invalid DTOs with no prefabs deleted.\n\n" +
                                 $"Everything has been built with 'One-Click' precision. You are ready to play!";
                
                EditorUtility.DisplayDialog("Database Sync Complete", message, "Fantastic");
            }
            catch (System.Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[Database Sync Error] {ex}");
                EditorUtility.DisplayDialog("Database Sync Failed", $"An error occurred during synchronization:\n{ex.Message}", "OK");
            }
        }

        #region Private Sync Executors

        private static string GetBaseAssetName(string variantName)
        {
            string clean = variantName;
            if (clean.StartsWith("Normal_")) clean = clean.Substring("Normal_".Length);
            else if (clean.StartsWith("Magic_")) clean = clean.Substring("Magic_".Length);
            else if (clean.StartsWith("Rare_")) clean = clean.Substring("Rare_".Length);
            else if (clean.StartsWith("Unique_")) clean = clean.Substring("Unique_".Length);

            return clean.Replace("_", " ");
        }

        private static int CleanAndValidateAllDTOs()
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemData");
            int deletedCount = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemData itemData = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (itemData == null) continue;

                if (itemData.Prefab == null || !itemData.Prefab.RuntimeKeyIsValid())
                {
                    Debug.LogWarning($"🗑️ [Database Validator] DTO '{itemData.name}' at '{path}' has no Prefab assigned. Deleting asset...");
                    AssetDatabase.DeleteAsset(path);
                    deletedCount++;
                }
            }
            return deletedCount;
        }

        private static int RunWeaponPrefabLinking()
        {
            string[] guids = AssetDatabase.FindAssets("t:EquippableData");
            int linkedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                EquippableData eqData = AssetDatabase.LoadAssetAtPath<EquippableData>(path);
                if (eqData == null) continue;

                if (eqData.VisualModel == null)
                {
                    // Attempt to locate base asset to inherit visuals/grip/offsets
                    string baseName = GetBaseAssetName(eqData.name);
                    if (baseName != eqData.name)
                    {
                        string[] baseGuids = AssetDatabase.FindAssets($"\"{baseName}\" t:EquippableData");
                        if (baseGuids.Length > 0)
                        {
                            string basePath = AssetDatabase.GUIDToAssetPath(baseGuids[0]);
                            EquippableData baseEq = AssetDatabase.LoadAssetAtPath<EquippableData>(basePath);
                            if (baseEq != null && baseEq.VisualModel != null)
                            {
                                eqData.VisualModel = baseEq.VisualModel;
                                eqData.PositionOffset = baseEq.PositionOffset;
                                eqData.RotationOffset = baseEq.RotationOffset;
                                eqData.MainAnimator = baseEq.MainAnimator;
                                EditorUtility.SetDirty(eqData);
                                Debug.Log($"🛡️ [Database Healer] Inherited visuals for variant '{eqData.name}' from base '{baseEq.name}'");
                            }
                        }
                    }
                }

                if (eqData.VisualModel != null)
                {
                    string visualPath = AssetDatabase.GetAssetPath(eqData.VisualModel);
                    if (!string.IsNullOrEmpty(visualPath))
                    {
                        string visualGuid = AssetDatabase.AssetPathToGUID(visualPath);
                        if (!string.IsNullOrEmpty(visualGuid))
                        {
                            // Automatically register the Visual Model as Addressable
                            MarkAsAddressableIfNecessary(visualGuid, Path.GetFileNameWithoutExtension(visualPath));

                            var assetRef = new UnityEngine.AddressableAssets.AssetReferenceGameObject(visualGuid);

                            // Link the Prefab field to point to this addressable VisualModel
                            if (eqData.Prefab == null || eqData.Prefab.AssetGUID != visualGuid)
                            {
                                eqData.Prefab = assetRef;
                                EditorUtility.SetDirty(eqData);
                                linkedCount++;
                            }
                        }
                    }
                }
            }
            return linkedCount;
        }

        private static int RunArmorPrefabLinking()
        {
            string[] guids = AssetDatabase.FindAssets("t:ArmorItemData");
            int linkedCount = 0;
            string armorPartsDir = "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Non-Skinned Mesh Parts/Armor Parts";

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ArmorItemData armorData = AssetDatabase.LoadAssetAtPath<ArmorItemData>(path);
                if (armorData == null) continue;

                // Determine prefab name prefix based on slot
                string prefix = "";
                switch (armorData.Slot)
                {
                    case EquipmentSlot.Head:
                        prefix = "Head Armor";
                        break;
                    case EquipmentSlot.Body:
                        prefix = "Chest Armor";
                        break;
                    case EquipmentSlot.Hands:
                        prefix = "Arm Armor";
                        break;
                    case EquipmentSlot.Feet:
                        prefix = "Feet Armor";
                        break;
                }

                if (string.IsNullOrEmpty(prefix)) continue;

                // Determine Type and Color based on rarity and name
                int nameHash = Mathf.Abs(armorData.name.GetHashCode());
                int type = 1;
                int color = 1;

                switch (armorData.Rarity)
                {
                    case ItemRarity.Normal:
                        type = (nameHash % 2) + 1; // Type 1 or 2
                        color = 1;
                        break;
                    case ItemRarity.Magic:
                        type = (nameHash % 2) + 3; // Type 3 or 4
                        color = 2;
                        break;
                    case ItemRarity.Rare:
                        type = 5;
                        color = 3;
                        break;
                    case ItemRarity.Unique:
                        type = 6;
                        color = 3;
                        break;
                }

                string prefabName = $"{prefix} Type {type} Color {color} Part.prefab";
                string prefabPath = Path.Combine(armorPartsDir, prefabName).Replace('\\', '/');

                int exactChildIndex = (type - 1) * 3 + (color - 1);
                string exactPartName = $"{prefix} Type {type} Color {color}";

                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabAsset != null)
                {
                    string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
                    if (!string.IsNullOrEmpty(prefabGuid))
                    {
                        // Register as Addressable
                        MarkAsAddressableIfNecessary(prefabGuid, Path.GetFileNameWithoutExtension(prefabName));

                        var assetRef = new UnityEngine.AddressableAssets.AssetReferenceGameObject(prefabGuid);
                        
                        // Check if it's already assigned or index needs syncing
                        if (armorData.Prefab == null || armorData.Prefab.AssetGUID != prefabGuid || armorData.ModularPartIndex != exactChildIndex || armorData.MeshPartName != exactPartName)
                        {
                            armorData.Prefab = assetRef;
                            armorData.ModularPartIndex = exactChildIndex; // Synchronize exact ModularIndex!
                            armorData.MeshPartName = exactPartName; // Synchronize exact MeshPartName!
                            EditorUtility.SetDirty(armorData);
                            linkedCount++;
                        }
                    }
                }
            }
            return linkedCount;
        }

        private static int RunDefaultPickupPrefabLinking()
        {
            int linkedCount = 0;

            // 1. Gems
            string[] gemGuids = AssetDatabase.FindAssets("t:GemItemData");
            string gemPrefabPath = "Assets/Prefabs/Pickups/GemDefault.prefab";
            linkedCount += LinkDefaultPrefab(gemGuids, gemPrefabPath, "GemDefault");

            // 2. Flasks
            string[] flaskGuids = AssetDatabase.FindAssets("t:FlaskItemData");
            string flaskPrefabPath = "Assets/Prefabs/Pickups/FlaskDefault.prefab";
            linkedCount += LinkDefaultPrefab(flaskGuids, flaskPrefabPath, "FlaskDefault");

            // 3. Currencies
            string[] currencyGuids = AssetDatabase.FindAssets("t:CurrencyItemData");
            string currencyPrefabPath = "Assets/Prefabs/Pickups/CurencyDefault.prefab";
            linkedCount += LinkDefaultPrefab(currencyGuids, currencyPrefabPath, "CurencyDefault");

            return linkedCount;
        }

        private static int LinkDefaultPrefab(string[] guids, string prefabPath, string address)
        {
            int count = 0;
            string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            if (string.IsNullOrEmpty(prefabGuid)) return 0;

            MarkAsAddressableIfNecessary(prefabGuid, address);
            var assetRef = new UnityEngine.AddressableAssets.AssetReferenceGameObject(prefabGuid);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemData itemData = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (itemData == null) continue;

                if (itemData.Prefab == null || itemData.Prefab.AssetGUID != prefabGuid)
                {
                    itemData.Prefab = assetRef;
                    EditorUtility.SetDirty(itemData);
                    count++;
                }
            }
            return count;
        }

        private static int RunAdapterGenerationAndLinking()
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemData");
            int createdOrUpdatedCount = 0;

            // Tải Devion ItemDatabase để tự động đăng ký tất cả Adapter vào hệ thống!
            DevionGames.InventorySystem.ItemDatabase devionDB = null;
            string[] dbGuids = AssetDatabase.FindAssets("t:ItemDatabase");
            if (dbGuids.Length > 0)
            {
                devionDB = AssetDatabase.LoadAssetAtPath<DevionGames.InventorySystem.ItemDatabase>(AssetDatabase.GUIDToAssetPath(dbGuids[0]));
            }

            string dummyPath = "Assets/Data/DTOS/DummyArmorEquip.prefab";
            GameObject dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(dummyPath);
            if (dummyPrefab == null)
            {
                var go = new GameObject("DummyArmorEquip");
                dummyPrefab = PrefabUtility.SaveAsPrefabAsset(go, dummyPath);
                Object.DestroyImmediate(go);
            }

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemData itemData = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (itemData != null)
                {
                    if (CreateAdapter(itemData))
                    {
                        createdOrUpdatedCount++;

                        if (devionDB != null && itemData.DevionAdapter != null)
                        {
                            if (!devionDB.items.Contains(itemData.DevionAdapter))
                            {
                                devionDB.items.Add(itemData.DevionAdapter);
                                EditorUtility.SetDirty(devionDB);
                            }
                        }
                    }
                }
            }
            return createdOrUpdatedCount;
        }

        #endregion

        #region Public Utility Helpers (Can still be called individually if needed)

        [MenuItem("Assets/Curve-Dash/Create Inventory Adapter(s)", false, 1)]
        [MenuItem("Curve-Dash/Advanced/Tools/Create Selected Inventory Adapter(s)", false, 100)]
        public static void CreateAdaptersForSelectedItems()
        {
            var selectedObjects = Selection.objects;
            int count = 0;

            foreach (var obj in selectedObjects)
            {
                if (obj is ItemData itemData)
                {
                    if (CreateAdapter(itemData))
                    {
                        count++;
                    }
                }
            }

            if (count > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"<color=lime>[Inventory Adapter Creator] Successfully created/updated {count} Inventory Adapter(s)!</color>");
            }
            else
            {
                Debug.LogWarning("[Inventory Adapter Creator] No valid Curve-Dash ItemData assets were selected.");
            }
        }

        [MenuItem("Curve-Dash/Advanced/Tools/Auto-Link All Existing Adapters", false, 110)]
        public static void AutoLinkAllAdapters()
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemData");
            int linkedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemData itemData = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (itemData == null) continue;

                string adapterName = itemData.name + "_Adapter";
                string[] adapterGuids = AssetDatabase.FindAssets($"{adapterName} t:Item");
                DevionGames.InventorySystem.Item foundAdapter = null;

                foreach (string adGuid in adapterGuids)
                {
                    string adPath = AssetDatabase.GUIDToAssetPath(adGuid);
                    if (Path.GetFileNameWithoutExtension(adPath) == adapterName)
                    {
                        foundAdapter = AssetDatabase.LoadAssetAtPath<DevionGames.InventorySystem.Item>(adPath);
                        if (foundAdapter != null) break;
                    }
                }

                if (foundAdapter != null)
                {
                    if (itemData.DevionAdapter != foundAdapter)
                    {
                        itemData.DevionAdapter = foundAdapter;
                        EditorUtility.SetDirty(itemData);
                        linkedCount++;
                    }
                }
            }

            if (linkedCount > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Adapter Linker", $"Successfully linked {linkedCount} ItemData assets to their existing Devion Adapters!", "Awesome");
            }
            else
            {
                EditorUtility.DisplayDialog("Adapter Linker", "All ItemData assets are already correctly linked to their existing adapters.", "OK");
            }
        }

        public static bool CreateAdapter(ItemData itemData)
        {
            if (itemData == null) return false;

            string itemPath = AssetDatabase.GetAssetPath(itemData);
            if (string.IsNullOrEmpty(itemPath))
            {
                Debug.LogError($"[Inventory Adapter Creator] Cannot find asset path for {itemData.ItemName}. Is it saved?");
                return false;
            }

            string adapterName = itemData.name + "_Adapter";
            
            // SEARCH THE ENTIRE PROJECT GLOBALLY FIRST!
            string[] adapterGuids = AssetDatabase.FindAssets($"{adapterName} t:Item");
            string targetPath = null;
            ScriptableObject adapter = null;
            bool isNew = false;

            foreach (string guid in adapterGuids)
            {
                string adPath = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(adPath) == adapterName)
                {
                    targetPath = adPath;
                    adapter = AssetDatabase.LoadAssetAtPath<ScriptableObject>(adPath);
                    if (adapter != null) break;
                }
            }

            if (adapter != null)
            {
                // We found an existing adapter in the project! Let's update that one instead of creating a duplicate!
                if (adapter is CurveDashEquipmentAdapter eqAdapter)
                {
                    eqAdapter.OriginalEquipmentData = itemData;
                    eqAdapter.SyncData();
                }
                else if (adapter is CurveDashItemAdapter itemAdapter)
                {
                    itemAdapter.OriginalItemData = itemData;
                    itemAdapter.SyncData();
                }
            }
            else
            {
                // No existing adapter found anywhere in the project! Create a new one in the same folder as the item!
                string directory = Path.GetDirectoryName(itemPath);
                targetPath = Path.Combine(directory, adapterName + ".asset").Replace("\\", "/");
                isNew = true;

                if (itemData is EquippableData || itemData is ArmorItemData)
                {
                    var newAdapter = ScriptableObject.CreateInstance<CurveDashEquipmentAdapter>();
                    newAdapter.OriginalEquipmentData = itemData;
                    newAdapter.SyncData();
                    adapter = newAdapter;
                }
                else
                {
                    var newAdapter = ScriptableObject.CreateInstance<CurveDashItemAdapter>();
                    newAdapter.OriginalItemData = itemData;
                    newAdapter.SyncData();
                    adapter = newAdapter;
                }
            }

            if (isNew)
            {
                AssetDatabase.CreateAsset(adapter, targetPath);
                Debug.Log($"[Inventory Adapter Creator] Created new Adapter: {adapterName} at: {targetPath}");
            }
            else
            {
                EditorUtility.SetDirty(adapter);
                Debug.Log($"[Inventory Adapter Creator] Synchronized existing Adapter: {adapterName} at: {targetPath}");
            }

            // Automatically link the adapter back to the original item data asset
            itemData.DevionAdapter = adapter as DevionGames.InventorySystem.Item;
            EditorUtility.SetDirty(itemData);

            return true;
        }

        public static void MarkAsAddressableIfNecessary(string guid, string address)
        {
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                var entry = settings.FindAssetEntry(guid);
                if (entry == null)
                {
                    var group = settings.DefaultGroup;
                    entry = settings.CreateOrMoveEntry(guid, group);
                    if (entry != null)
                    {
                        entry.address = address;
                        settings.SetDirty(UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.ModificationEvent.EntryAdded, entry, true);
                        Debug.Log($"[Addressable Auto-Register] Successfully registered prefab '{address}' to Addressables default group!");
                    }
                }
            }
        }

        #endregion
    }
}
