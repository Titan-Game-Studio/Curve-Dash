using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using STG.CurveDash;

namespace STG.CurveDash.Editor
{
    [InitializeOnLoad]
    public static class MasterItemCatalogRefresher
    {
        static MasterItemCatalogRefresher()
        {
            // Auto refresh the catalog silently on load or play mode transition
            EditorApplication.delayCall += () => RefreshCatalog(false);
        }

        [MenuItem("Curve-Dash/Tools/Refresh Master Item Catalog")]
        public static void RefreshCatalogMenu()
        {
            RefreshCatalog(true);
        }

        public static void RefreshCatalog(bool showDialog)
        {
            ItemCatalog catalog = null;
            string[] gameCatalogGuids = AssetDatabase.FindAssets("t:GameAssetCatalog");
            if (gameCatalogGuids.Length > 0)
            {
                var gameCatalog = AssetDatabase.LoadAssetAtPath<GameAssetCatalog>(AssetDatabase.GUIDToAssetPath(gameCatalogGuids[0]));
                if (gameCatalog != null) catalog = gameCatalog.MasterItemCatalog;
            }

            if (catalog == null)
            {
                string[] catalogGuids = AssetDatabase.FindAssets("t:ItemCatalog");
                if (catalogGuids != null && catalogGuids.Length > 0)
                {
                    catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(AssetDatabase.GUIDToAssetPath(catalogGuids[0]));
                }
            }

            if (catalog == null)
            {
                if (showDialog) EditorUtility.DisplayDialog("Catalog Refresh", "No ItemCatalog asset found in the project!", "OK");
                else Debug.LogWarning("[CatalogRefresher] No ItemCatalog asset found in the project!");
                return;
            }

            if (catalog.Items == null)
            {
                catalog.Items = new List<ItemData>();
            }

            string[] queries = new string[] { "t:ItemData", "t:WeaponData", "t:ArmorItemData", "t:GemItemData", "t:FlaskItemData", "t:CurrencyItemData", "t:EquippableData" };
            HashSet<ItemData> foundItems = new HashSet<ItemData>();

            foreach (var query in queries)
            {
                string[] itemGuids = AssetDatabase.FindAssets(query);
                foreach (string guid in itemGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                    if (item != null)
                    {
                        foundItems.Add(item);
                    }
                }
            }

            // Clean list and add all found items
            catalog.Items.Clear();
            catalog.Items.AddRange(foundItems);

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Debug.Log($"<color=green>[CatalogRefresher] Successfully registered {foundItems.Count} items (Weapons, Armors, Gems, Flasks, Currencies) to MasterItemCatalog!</color>");

            if (showDialog)
            {
                EditorUtility.DisplayDialog("Catalog Refresh", $"Successfully refreshed! Registered {foundItems.Count} items in the catalog.", "OK");
            }
        }
    }
}
