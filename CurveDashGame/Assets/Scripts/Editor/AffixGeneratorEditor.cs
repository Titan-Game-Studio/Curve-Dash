using UnityEditor;
using UnityEngine;
using System.IO;

namespace STG.CurveDash.Editor
{
    public class AffixGeneratorEditor : EditorWindow
    {
        private string _affixName = "New Affix";
        private AffixType _affixType = AffixType.Prefix;
        private StatType _statType = StatType.AddedPhysicalDamage;
        private float _minValue = 5f;
        private float _maxValue = 10f;
        
        private const string SavePath = "Assets/Data/DTOS/Affixs/";

        [MenuItem("Curve-Dash/Tools/Affix Generator")]
        public static void ShowWindow()
        {
            GetWindow<AffixGeneratorEditor>("Affix Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Affix Creation Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _affixName = EditorGUILayout.TextField("Affix Name", _affixName);
            _affixType = (AffixType)EditorGUILayout.EnumPopup("Affix Type", _affixType);
            _statType = (StatType)EditorGUILayout.EnumPopup("Stat Type", _statType);
            
            EditorGUILayout.BeginHorizontal();
            _minValue = EditorGUILayout.FloatField("Min Value", _minValue);
            _maxValue = EditorGUILayout.FloatField("Max Value", _maxValue);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            if (GUILayout.Button("Create Single Affix", GUILayout.Height(30)))
            {
                CreateAffix(_affixName, _affixType, _statType, _minValue, _maxValue);
            }

            EditorGUILayout.Space();
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Generate Standard PoE Pack", GUILayout.Height(40)))
            {
                GenerateStandardPack();
            }
            GUI.backgroundColor = Color.white;
        }

        private void CreateAffix(string aName, AffixType aType, StatType sType, float min, float max)
        {
            // Kiểm tra và tạo thư mục nếu chưa có
            if (!Directory.Exists(SavePath))
            {
                Directory.CreateDirectory(SavePath);
                AssetDatabase.Refresh();
            }

            AffixData asset = ScriptableObject.CreateInstance<AffixData>();
            asset.AffixName = aName;
            asset.TypeOfAffix = aType;
            asset.Stat = sType;
            asset.MinValue = min;
            asset.MaxValue = max;

            string fileName = $"{aType}_{aName.Replace(" ", "_")}.asset";
            string fullPath = Path.Combine(SavePath, fileName);
            
            AssetDatabase.CreateAsset(asset, fullPath);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[AffixGenerator] Created: {fullPath}");
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }

        private void GenerateStandardPack()
        {
            // PREFIXES
            CreateAffix("Sharp", AffixType.Prefix, StatType.AddedPhysicalDamage, 5, 15);
            CreateAffix("Heavy", AffixType.Prefix, StatType.AddedPhysicalDamage, 15, 30);
            CreateAffix("Burning", AffixType.Prefix, StatType.AddedFireDamage, 10, 20);
            CreateAffix("Freezing", AffixType.Prefix, StatType.AddedColdDamage, 10, 20);
            CreateAffix("Glorious", AffixType.Prefix, StatType.IncreasedPhysicalDamage, 40, 80);

            // SUFFIXES
            CreateAffix("Cheetah", AffixType.Suffix, StatType.IncreasedAttackSpeed, 10, 25);
            CreateAffix("Eagle", AffixType.Suffix, StatType.IncreasedCriticalChance, 5, 15);
            CreateAffix("Vampire", AffixType.Suffix, StatType.LifeStealPercentage, 2, 5);
            CreateAffix("Giant", AffixType.Suffix, StatType.KnockbackForce, 10, 20);
            
            AssetDatabase.Refresh();
            Debug.Log("[AffixGenerator] Standard PoE Pack generated successfully!");
        }
    }
}

