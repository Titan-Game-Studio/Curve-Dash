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
            string[] catalogGuids = AssetDatabase.FindAssets("t:ItemCatalog");
            if (catalogGuids == null || catalogGuids.Length == 0)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Catalog Refresh", "No ItemCatalog asset found in the project!", "OK");
                }
                else
                {
                    Debug.LogWarning("[CatalogRefresher] No ItemCatalog asset found in the project!");
                }
                return;
            }

            string catalogPath = AssetDatabase.GUIDToAssetPath(catalogGuids[0]);
            ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(catalogPath);
            if (catalog == null)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Catalog Refresh", "Failed to load ItemCatalog from: " + catalogPath, "OK");
                }
                else
                {
                    Debug.LogError("[CatalogRefresher] Failed to load ItemCatalog from: " + catalogPath);
                }
                return;
            }

            if (catalog.Items == null)
            {
                catalog.Items = new List<ItemData>();
            }

            string[] itemGuids = AssetDatabase.FindAssets("t:ItemData");
            List<ItemData> foundItems = new List<ItemData>();

            foreach (string guid in itemGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item != null)
                {
                    foundItems.Add(item);
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
