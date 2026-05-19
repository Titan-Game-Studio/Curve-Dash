using UnityEditor;
using UnityEngine;

namespace STG.CurveDash.Editor
{
    [CustomEditor(typeof(ItemData), true)]
    public class ItemDataInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            ItemData item = (ItemData)target;

            // Draw default inspector fields
            DrawDefaultInspector();

            EditorGUILayout.Space();
            DrawPrefabValidation(item);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Inventory System Integration", EditorStyles.boldLabel);

            // Styled modern blue-indigo button for creating the inventory adapter
            GUI.backgroundColor = new Color(0.24f, 0.44f, 0.94f, 1f); 
            if (GUILayout.Button("🛡️ Create/Update Inventory Adapter", GUILayout.Height(32)))
            {
                CurveDashInventoryEditorUtility.CreateAdapter(item);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space();
        }

        public static void DrawPrefabValidation(ItemData item)
        {
            if (item == null) return;

            if (item.Prefab == null || string.IsNullOrEmpty(item.Prefab.AssetGUID))
            {
                EditorGUILayout.HelpBox("⚠️ WARNING: Prefab is currently null! Do NOT assign 'ItemPickup.prefab' here.\n" +
                                        "Please assign the actual 3D model, or click the button below to assign the default visual model.", MessageType.Warning);

                if (item is GemItemData || item is FlaskItemData || item is CurrencyItemData)
                {
                    GUI.backgroundColor = new Color(0.1f, 0.7f, 0.3f, 1f);
                    if (GUILayout.Button("💚 Assign Default Visual Prefab", GUILayout.Height(28)))
                    {
                        string defaultPrefabPath = "";
                        string defaultAddress = "";
                        if (item is GemItemData) { defaultPrefabPath = "Assets/Prefabs/Pickups/GemDefault.prefab"; defaultAddress = "GemDefault"; }
                        else if (item is FlaskItemData) { defaultPrefabPath = "Assets/Prefabs/Pickups/FlaskDefault.prefab"; defaultAddress = "FlaskDefault"; }
                        else if (item is CurrencyItemData) { defaultPrefabPath = "Assets/Prefabs/Pickups/CurencyDefault.prefab"; defaultAddress = "CurencyDefault"; }

                        if (!string.IsNullOrEmpty(defaultPrefabPath))
                        {
                            string guid = AssetDatabase.AssetPathToGUID(defaultPrefabPath);
                            if (!string.IsNullOrEmpty(guid))
                            {
                                CurveDashInventoryEditorUtility.MarkAsAddressableIfNecessary(guid, defaultAddress);
                                item.Prefab = new UnityEngine.AddressableAssets.AssetReferenceGameObject(guid);
                                EditorUtility.SetDirty(item);
                                AssetDatabase.SaveAssets();
                                AssetDatabase.Refresh();
                            }
                        }
                    }
                    GUI.backgroundColor = Color.white;
                }
            }
            else
            {
                string path = AssetDatabase.GUIDToAssetPath(item.Prefab.AssetGUID);
                if (!string.IsNullOrEmpty(path) && path.EndsWith("ItemPickup.prefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    EditorGUILayout.HelpBox("❌ ERROR: You MUST NOT assign 'ItemPickup' as the DTO's Prefab!\n" +
                                            "Please use one of the default pickup models (CurencyDefault, FlaskDefault, GemDefault) or the actual visual model instead.", MessageType.Error);
                }
            }
        }
    }
}
