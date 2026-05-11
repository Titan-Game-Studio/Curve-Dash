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
    }
}
