using UnityEditor;
using UnityEngine;
using System.IO;

namespace STG.CurveDash.Editor
{
    public static class CurveDashInventoryEditorUtility
    {
        [MenuItem("Assets/Curve-Dash/Create Inventory Adapter(s)", false, 1)]
        [MenuItem("Curve-Dash/Tools/Create Selected Inventory Adapter(s)", false, 20)]
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

        public static bool CreateAdapter(ItemData itemData)
        {
            if (itemData == null) return false;

            string itemPath = AssetDatabase.GetAssetPath(itemData);
            if (string.IsNullOrEmpty(itemPath))
            {
                Debug.LogError($"[Inventory Adapter Creator] Cannot find asset path for {itemData.ItemName}. Is it saved?");
                return false;
            }

            string directory = Path.GetDirectoryName(itemPath);
            string adapterName = itemData.name + "_Adapter.asset";
            string adapterPath = Path.Combine(directory, adapterName).Replace("\\", "/");

            ScriptableObject adapter;
            bool isNew = false;

            if (itemData is EquippableData || itemData is ArmorItemData)
            {
                // Check if adapter already exists
                var existing = AssetDatabase.LoadAssetAtPath<CurveDashEquipmentAdapter>(adapterPath);
                if (existing != null)
                {
                    adapter = existing;
                    existing.OriginalEquipmentData = itemData;
                    existing.SyncData();
                }
                else
                {
                    var newAdapter = ScriptableObject.CreateInstance<CurveDashEquipmentAdapter>();
                    newAdapter.OriginalEquipmentData = itemData;
                    newAdapter.SyncData();
                    adapter = newAdapter;
                    isNew = true;
                }
            }
            else
            {
                // Check if adapter already exists
                var existing = AssetDatabase.LoadAssetAtPath<CurveDashItemAdapter>(adapterPath);
                if (existing != null)
                {
                    adapter = existing;
                    existing.OriginalItemData = itemData;
                    existing.SyncData();
                }
                else
                {
                    var newAdapter = ScriptableObject.CreateInstance<CurveDashItemAdapter>();
                    newAdapter.OriginalItemData = itemData;
                    newAdapter.SyncData();
                    adapter = newAdapter;
                    isNew = true;
                }
            }

            if (isNew)
            {
                AssetDatabase.CreateAsset(adapter, adapterPath);
                Debug.Log($"[Inventory Adapter Creator] Created new Adapter: {adapterName} in {directory}");
            }
            else
            {
                EditorUtility.SetDirty(adapter);
                Debug.Log($"[Inventory Adapter Creator] Updated existing Adapter: {adapterName}");
            }

            return true;
        }
    }
}
