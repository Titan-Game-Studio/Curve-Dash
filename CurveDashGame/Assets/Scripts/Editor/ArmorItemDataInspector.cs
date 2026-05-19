using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace STG.CurveDash.Editor
{
    [CustomEditor(typeof(ArmorItemData), true)]
    public class ArmorItemDataInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            ArmorItemData armor = (ArmorItemData)target;

            serializedObject.Update();

            SerializedProperty prop = serializedObject.GetIterator();
            if (prop.NextVisible(true))
            {
                do
                {
                    // Intercept and render a gorgeous random capabilities button right above Abilities
                    if (prop.name == "Abilities")
                    {
                        int maxSockets = armor.MaxSockets;
                        if (maxSockets > 0)
                        {
                            EditorGUILayout.Space();

                            // Beautiful green-blue style button
                            GUI.backgroundColor = new Color(0.12f, 0.73f, 0.53f, 1f); 
                            if (GUILayout.Button($"🎲 Add Random Abilities (Testing Link - Max {maxSockets} Sockets)", GUILayout.Height(30)))
                            {
                                AddRandomAbilitiesToArmor(armor);
                            }
                            GUI.backgroundColor = Color.white;
                            EditorGUILayout.Space();
                        }
                    }

                    if (prop.name != "m_Script")
                    {
                        EditorGUILayout.PropertyField(prop, true);
                    }
                }
                while (prop.NextVisible(false));
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            ItemDataInspector.DrawPrefabValidation(armor);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Inventory System Integration", EditorStyles.boldLabel);
            GUI.backgroundColor = new Color(0.24f, 0.44f, 0.94f, 1f); 
            if (GUILayout.Button("🛡️ Create/Update Inventory Adapter", GUILayout.Height(32)))
            {
                CurveDashInventoryEditorUtility.CreateAdapter(armor);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space();
        }

        private void AddRandomAbilitiesToArmor(ArmorItemData armor)
        {
            string[] guids = AssetDatabase.FindAssets("t:AbilityData");
            List<AbilityData> allAbilities = new List<AbilityData>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ab = AssetDatabase.LoadAssetAtPath<AbilityData>(path);
                if (ab != null) allAbilities.Add(ab);
            }

            if (allAbilities.Count == 0)
            {
                Debug.LogWarning("[ArmorItemDataInspector] No AbilityData assets found in project!");
                return;
            }

            int max = armor.MaxSockets;
            if (max == 0)
            {
                Debug.LogWarning($"[ArmorItemDataInspector] Sockets are not supported on {armor.Slot} slot!");
                return;
            }

            Undo.RecordObject(armor, "Add Random Ability");
            if (armor.Abilities == null) armor.Abilities = new List<AbilityData>();

            // If sockets are already full, act as a "Re-roll" button by clearing
            if (armor.Abilities.Count >= max)
            {
                armor.Abilities.Clear();
            }

            // Group into active and support for logical rolling
            List<PoEAbility> activePool = new List<PoEAbility>();
            List<SupportAbilityData> supportPool = new List<SupportAbilityData>();
            foreach (var ab in allAbilities)
            {
                if (ab is PoEAbility active) activePool.Add(active);
                else if (ab is SupportAbilityData support) supportPool.Add(support);
            }

            // 1. Roll 1 random Active skill first
            PoEAbility selectedActive = null;
            if (armor.Abilities.Count == 0 && activePool.Count > 0)
            {
                selectedActive = activePool[Random.Range(0, activePool.Count)];
                armor.Abilities.Add(selectedActive);
            }
            else
            {
                selectedActive = armor.Abilities.Find(x => x is PoEAbility) as PoEAbility;
            }

            // 2. Roll additional compatible support gems (75% chance) or secondary active skills (like Auras - 25% chance)
            int iterations = 0;
            while (armor.Abilities.Count < max && iterations < 50)
            {
                iterations++;
                
                if (Random.value < 0.75f && supportPool.Count > 0)
                {
                    var rolledSupport = supportPool[Random.Range(0, supportPool.Count)];
                    if (rolledSupport != null && !armor.Abilities.Contains(rolledSupport))
                    {
                        // Ensure it specifically supports our active skill
                        if (selectedActive != null && rolledSupport.IsCompatible(selectedActive))
                        {
                            armor.Abilities.Add(rolledSupport);
                        }
                    }
                }
                else if (activePool.Count > 0)
                {
                    var rolledActive = activePool[Random.Range(0, activePool.Count)];
                    if (rolledActive != null && !armor.Abilities.Contains(rolledActive))
                    {
                        armor.Abilities.Add(rolledActive);
                    }
                }
            }

            EditorUtility.SetDirty(armor);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=lime>[Smart Sockets] Successfully rolled {armor.Abilities.Count} compatible sockets for Armor '{armor.ItemName}' ({armor.Slot})!</color>");
        }
    }
}
