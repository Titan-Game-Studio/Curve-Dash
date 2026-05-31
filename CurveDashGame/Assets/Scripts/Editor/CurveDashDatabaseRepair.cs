using UnityEditor;
using UnityEngine;
using DevionGames.InventorySystem;
using System.Collections.Generic;
using System.Linq;

namespace STG.CurveDash.Editor
{
    public static class CurveDashDatabaseRepair
    {
        [MenuItem("Curve-Dash/Database/Fix Ring/Flask/Belt/Amulet Regions", false, 50)]
        public static void FixAccessoryRegions()
        {
            // Find the database
            string[] dbGuids = AssetDatabase.FindAssets("t:ItemDatabase");
            if (dbGuids.Length == 0)
            {
                EditorUtility.DisplayDialog("Error", "No ItemDatabase found!", "OK");
                return;
            }

            string dbPath = AssetDatabase.GUIDToAssetPath(dbGuids[0]);
            ItemDatabase db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(dbPath);

            if (db == null)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to load ItemDatabase at {dbPath}", "OK");
                return;
            }

            Debug.Log($"<color=orange>[DatabaseRepair] Loaded database: {dbPath}</color>");
            Debug.Log($"<color=orange>[DatabaseRepair] Equipment regions in DB: {db.equipments?.Count ?? 0}</color>");

            // Find required regions (created manually in database)
            EquipmentRegion ringLeftRegion = FindRegionByName(db, "Ring Left");
            EquipmentRegion ringRightRegion = FindRegionByName(db, "Ring Right");
            EquipmentRegion flaskFirstRegion = FindRegionByName(db, "Flask Fist");
            EquipmentRegion flaskSecondRegion = FindRegionByName(db, "Flask Second");
            EquipmentRegion flaskThirdRegion = FindRegionByName(db, "Flask Third");
            EquipmentRegion beltRegion = FindRegionByName(db, "Belt");
            EquipmentRegion amuletRegion = FindRegionByName(db, "Amulet");

            if (ringLeftRegion == null || ringRightRegion == null || flaskFirstRegion == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "Missing EquipmentRegions in database!\n\n" +
                    "Please ensure these regions exist:\n" +
                    "• Ring Left\n• Ring Right\n• Flask Fist\n• Flask Second\n• Flask Third\n• Belt\n• Amulet",
                    "OK");
                return;
            }

            Debug.Log($"<color=lime>[DatabaseRepair] Found regions: Ring Left, Ring Right, Flask Fist/Second/Third, Belt, Amulet</color>");

            // Now update all Ring/Flask/Belt/Amulet adapters with correct regions
            string[] itemGuids = AssetDatabase.FindAssets("t:ItemData");
            int fixedCount = 0;

            foreach (string guid in itemGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemData itemData = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (itemData == null) continue;

                string adapterName = itemData.name + "_Adapter";
                CurveDashEquipmentAdapter adapter = null;

                // Find adapter
                string[] adapterGuids = AssetDatabase.FindAssets($"{adapterName} t:Item");
                foreach (string aGuid in adapterGuids)
                {
                    string aPath = AssetDatabase.GUIDToAssetPath(aGuid);
                    if (System.IO.Path.GetFileNameWithoutExtension(aPath) == adapterName)
                    {
                        adapter = AssetDatabase.LoadAssetAtPath<CurveDashEquipmentAdapter>(aPath);
                        break;
                    }
                }

                if (adapter == null) continue;

                // Assign region based on item type
                bool needsUpdate = false;
                EquipmentRegion targetRegion = null;

                if (itemData is RingItemData ring)
                {
                    targetRegion = (ring.Slot == EquipmentSlot.Ring1) ? ringLeftRegion : ringRightRegion;
                    Debug.Log($"<color=cyan>[Fix] Ring '{itemData.name}' ({ring.Slot}) → {targetRegion.Name}</color>");
                }
                else if (itemData is FlaskItemData flask)
                {
                    // Assign flasks in order: First, Second, Third
                    int flaskIndex = fixedCount % 3;
                    targetRegion = flaskIndex == 0 ? flaskFirstRegion : (flaskIndex == 1 ? flaskSecondRegion : flaskThirdRegion);
                    Debug.Log($"<color=cyan>[Fix] Flask '{itemData.name}' → {targetRegion.Name}</color>");
                }
                else if (itemData is BeltItemData)
                {
                    targetRegion = beltRegion;
                    Debug.Log($"<color=cyan>[Fix] Belt '{itemData.name}' → Belt</color>");
                }
                else if (itemData is AmuletItemData)
                {
                    targetRegion = amuletRegion;
                    Debug.Log($"<color=cyan>[Fix] Amulet '{itemData.name}' → Amulet</color>");
                }

                if (targetRegion != null && !adapter.Region.Contains(targetRegion))
                {
                    adapter.Region = new List<EquipmentRegion> { targetRegion };
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    EditorUtility.SetDirty(adapter);
                    fixedCount++;
                }
            }

            // Save everything
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Database Repair Complete",
                $"Fixed {fixedCount} Ring/Flask/Belt/Amulet adapters with correct equipment regions!\n\n" +
                $"Regions ensured:\n" +
                $"• Ring\n• Flask\n• Belt\n• Amulet",
                "Awesome");
        }

        private static EquipmentRegion FindRegionByName(ItemDatabase db, string regionName)
        {
            // Find region by name
            foreach (var region in db.equipments)
            {
                if (region != null && region.Name == regionName)
                {
                    return region;
                }
            }
            return null;
        }
    }
}
