using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace STG.CurveDash.Editor
{
    /// <summary>
    /// Automatic Build Preprocessor that runs right before building the game.
    /// It automatically clears all testing abilities socketed inside WeaponData and ArmorItemData assets,
    /// ensuring that the shipped production build starts with clean, empty item sockets.
    /// Also includes a manual menu item to clean up the assets at any time.
    /// </summary>
    public class BuildSocketsCleaner : IPreprocessBuildWithReport
    {
        // Executes first before other build setup steps
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("<color=orange>[Build System] Pre-build process starting... Cleaning up all testing abilities/sockets from base assets.</color>");
            ClearAllSocketsInternal(false);
        }

        [MenuItem("Curve-Dash/Tools/Clear All Testing Sockets", false, 50)]
        public static void ManualClearAllSockets()
        {
            if (EditorUtility.DisplayDialog("Clear All Testing Sockets?", 
                "Are you sure you want to clear the 'Abilities' list on ALL Weapon and Armor assets in the project?\n\nThis will reset them to empty sockets for a clean build/test state.", 
                "Yes, Clear All Sockets", "Cancel"))
            {
                ClearAllSocketsInternal(true);
            }
        }

        private static void ClearAllSocketsInternal(bool showDialog)
        {
            int weaponsCleared = 0;
            int armorsCleared = 0;

            // 1. Scan and clear all WeaponData assets
            string[] weaponGuids = AssetDatabase.FindAssets("t:WeaponData");
            foreach (string guid in weaponGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
                if (weapon != null && weapon.Abilities != null && weapon.Abilities.Count > 0)
                {
                    Undo.RecordObject(weapon, "Clear Testing Sockets");
                    weapon.Abilities.Clear();
                    EditorUtility.SetDirty(weapon);
                    weaponsCleared++;
                }
            }

            // 2. Scan and clear all ArmorItemData assets
            string[] armorGuids = AssetDatabase.FindAssets("t:ArmorItemData");
            foreach (string guid in armorGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var armor = AssetDatabase.LoadAssetAtPath<ArmorItemData>(path);
                if (armor != null && armor.Abilities != null && armor.Abilities.Count > 0)
                {
                    Undo.RecordObject(armor, "Clear Testing Sockets");
                    armor.Abilities.Clear();
                    EditorUtility.SetDirty(armor);
                    armorsCleared++;
                }
            }

            if (weaponsCleared > 0 || armorsCleared > 0)
            {
                AssetDatabase.SaveAssets();
                string message = $"Successfully cleared testing abilities for {weaponsCleared} Weapons and {armorsCleared} Armors!";
                Debug.Log($"<color=lime>[Build System] {message}</color>");
                
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Sockets Cleared", message, "OK");
                }
            }
            else
            {
                Debug.Log("[Build System] All Weapons and Armors were already clean. No action required.");
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Already Clean", "All weapons and armors already have empty sockets!", "OK");
                }
            }
        }
    }
}

