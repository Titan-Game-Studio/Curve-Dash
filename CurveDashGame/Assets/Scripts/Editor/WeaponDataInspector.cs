using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace STG.CurveDash.Editor
{
    [CustomEditor(typeof(WeaponData), true)]
    public class WeaponDataInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            WeaponData weapon = (WeaponData)target;

            serializedObject.Update();
            
            SerializedProperty prop = serializedObject.GetIterator();
            if (prop.NextVisible(true))
            {
                do
                {
                    // Check if this property is the Abilities (Sockets) list, draw our awesome button right above it!
                    if (prop.name == "Abilities")
                    {
                        EditorGUILayout.Space();
                        
                        // Styled modern green-blue button for random roll
                        GUI.backgroundColor = new Color(0.12f, 0.73f, 0.53f, 1f); 
                        if (GUILayout.Button("🎲 Add Smart Abilities (Weapon-Type Linked)", GUILayout.Height(30)))
                        {
                            AddRandomAbilitiesToWeapon(weapon);
                        }
                        GUI.backgroundColor = Color.white;
                        EditorGUILayout.Space();
                    }

                    // Avoid drawing m_Script
                    if (prop.name != "m_Script")
                    {
                        EditorGUILayout.PropertyField(prop, true);
                    }
                }
                while (prop.NextVisible(false));
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void AddRandomAbilitiesToWeapon(WeaponData weapon)
        {
            // Gather all available AbilityData using AssetDatabase
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
                Debug.LogWarning("[WeaponDataInspector] No AbilityData assets found in project! Please create some first.");
                return;
            }

            // Record undo action so developer can Ctrl+Z
            Undo.RecordObject(weapon, "Add Smart Sockets");
            if (weapon.Abilities == null) weapon.Abilities = new List<AbilityData>();

            // Always clear list if it's already full, letting it act as a "Re-roll" button
            if (weapon.Abilities.Count >= weapon.MaxSockets)
            {
                weapon.Abilities.Clear();
            }

            // Filter compatible abilities specifically matched to this Weapon Type
            List<PoEAbility> compatibleActives;
            List<SupportAbilityData> compatibleSupports;
            GetCompatibleAbilitiesForWeapon(weapon, allAbilities, out compatibleActives, out compatibleSupports);

            if (compatibleActives.Count == 0)
            {
                Debug.LogWarning($"[WeaponDataInspector] No compatible active abilities found for Weapon '{weapon.ItemName}' ({weapon.GetType().Name})!");
                return;
            }

            // 1. Select the main Active skill to base our supports on
            PoEAbility selectedActive = null;
            if (weapon.Abilities.Count == 0)
            {
                selectedActive = compatibleActives[Random.Range(0, compatibleActives.Count)];
                weapon.Abilities.Add(selectedActive);
            }
            else
            {
                selectedActive = weapon.Abilities.Find(x => x is PoEAbility) as PoEAbility;
            }

            // 2. Roll additional compatible support gems (75% chance) or secondary active skills (like Auras - 25% chance)
            int iterations = 0;
            while (weapon.Abilities.Count < weapon.MaxSockets && iterations < 50)
            {
                iterations++;
                
                if (Random.value < 0.75f && compatibleSupports.Count > 0)
                {
                    var rolledSupport = compatibleSupports[Random.Range(0, compatibleSupports.Count)];
                    if (rolledSupport != null && !weapon.Abilities.Contains(rolledSupport))
                    {
                        // Ensure it specifically supports our active skill
                        if (selectedActive != null && rolledSupport.IsCompatible(selectedActive))
                        {
                            weapon.Abilities.Add(rolledSupport);
                        }
                    }
                }
                else
                {
                    var rolledActive = compatibleActives[Random.Range(0, compatibleActives.Count)];
                    if (rolledActive != null && !weapon.Abilities.Contains(rolledActive))
                    {
                        weapon.Abilities.Add(rolledActive);
                    }
                }
            }

            EditorUtility.SetDirty(weapon);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=lime>[Smart Sockets] Successfully rolled {weapon.Abilities.Count} compatible sockets for '{weapon.ItemName}' of class {weapon.GetType().Name}!</color>");
        }

        private void GetCompatibleAbilitiesForWeapon(
            WeaponData weapon, 
            List<AbilityData> allAbilities, 
            out List<PoEAbility> compatibleActives, 
            out List<SupportAbilityData> compatibleSupports)
        {
            compatibleActives = new List<PoEAbility>();
            compatibleSupports = new List<SupportAbilityData>();

            // Determine allowed active types based on weapon class
            List<PoEAbilityType> allowedActiveTypes = new List<PoEAbilityType>();
            
            if (weapon is BowData)
            {
                // Bows can only cast Ranged skills and Auras
                allowedActiveTypes.Add(PoEAbilityType.Ranged);
                allowedActiveTypes.Add(PoEAbilityType.Aura);
            }
            else if (weapon is OneHandedWeaponData || weapon is TwoHandedWeaponData)
            {
                // Swords/Maces/Hammers can only cast Melee skills and Auras
                allowedActiveTypes.Add(PoEAbilityType.Melee);
                allowedActiveTypes.Add(PoEAbilityType.Aura);
            }
            else
            {
                // Fallback for general weapons
                allowedActiveTypes.Add(PoEAbilityType.Melee);
                allowedActiveTypes.Add(PoEAbilityType.Spell);
                allowedActiveTypes.Add(PoEAbilityType.Ranged);
                allowedActiveTypes.Add(PoEAbilityType.Aura);
            }

            // Filter active abilities
            foreach (var ab in allAbilities)
            {
                if (ab is PoEAbility active && allowedActiveTypes.Contains(active.SkillType))
                {
                    compatibleActives.Add(active);
                }
            }

            // Filter support abilities that can support AT LEAST ONE of our compatible active abilities
            foreach (var ab in allAbilities)
            {
                if (ab is SupportAbilityData support)
                {
                    bool isComp = false;
                    foreach (var active in compatibleActives)
                    {
                        if (support.IsCompatible(active))
                        {
                            isComp = true;
                            break;
                        }
                    }
                    if (isComp)
                    {
                        compatibleSupports.Add(support);
                    }
                }
            }
        }
    }
}
