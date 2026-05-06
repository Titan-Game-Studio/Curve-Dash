using UnityEditor;
using UnityEngine;
using System.IO;

namespace STG.CurveDash.Editor
{
    public class AbilityGeneratorEditor : EditorWindow
    {
        private string _abilityName = "Fireball";
        private PoEElementType _element = PoEElementType.Fire;
        private PoEAbilityType _skillType = PoEAbilityType.Spell;
        
        private float _cooldown = 1.0f;
        private float _manaCost = 15f;
        private float _damageMultiplier = 1.2f;
        private float _addedFlatDamage = 5f;
        private float _attackRange = 5.0f;
        private int _projectileCount = 1;
        private float _speed = 12f;
        private string _skillDescription = "Fires an explosive ball of fire.";

        private const string SavePath = "Assets/Data/DTOS/Abilities/";

        [MenuItem("Curve Dash/Tools/Ability Generator")]
        public static void ShowWindow()
        {
            GetWindow<AbilityGeneratorEditor>("Ability Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("PoE Ability Creation Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _abilityName = EditorGUILayout.TextField("Ability Name", _abilityName);
            _element = (PoEElementType)EditorGUILayout.EnumPopup("Element Type", _element);
            _skillType = (PoEAbilityType)EditorGUILayout.EnumPopup("Skill Type", _skillType);
            
            EditorGUILayout.Space();
            GUILayout.Label("Resource & Timing", EditorStyles.boldLabel);
            _cooldown = EditorGUILayout.FloatField("Cooldown (seconds)", _cooldown);
            _manaCost = EditorGUILayout.FloatField("Mana Cost", _manaCost);

            EditorGUILayout.Space();
            GUILayout.Label("Combat & Projectile Stats", EditorStyles.boldLabel);
            _damageMultiplier = EditorGUILayout.FloatField("Damage Multiplier", _damageMultiplier);
            _addedFlatDamage = EditorGUILayout.FloatField("Added Flat Damage", _addedFlatDamage);
            _attackRange = EditorGUILayout.FloatField("Attack Range", _attackRange);
            _projectileCount = EditorGUILayout.IntField("Projectile Count", _projectileCount);
            _speed = EditorGUILayout.FloatField("Projectile/Move Speed", _speed);
            
            EditorGUILayout.Space();
            _skillDescription = EditorGUILayout.TextField("Description", _skillDescription);

            EditorGUILayout.Space();
            if (GUILayout.Button("Create Single PoE Ability", GUILayout.Height(30)))
            {
                CreateAbility(_abilityName, _element, _skillType, _cooldown, _manaCost, _damageMultiplier, _addedFlatDamage, _attackRange, _projectileCount, _speed, _skillDescription);
            }

            EditorGUILayout.Space();
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Generate PoE Abilities Database", GUILayout.Height(40)))
            {
                GeneratePoeAbilities();
            }
            GUI.backgroundColor = Color.white;
        }

        private void CreateAbility(string aName, PoEElementType elem, PoEAbilityType sType, float cd, float mana, float dmgMul, float flat, float rng, int proj, float spd, string desc)
        {
            string typeFolder = sType.ToString().ToUpper();
            if (sType == PoEAbilityType.Melee) typeFolder = "MELEE";
            else if (sType == PoEAbilityType.Spell) typeFolder = "SPELLS";
            else if (sType == PoEAbilityType.Ranged) typeFolder = "RANGED";
            else if (sType == PoEAbilityType.Aura) typeFolder = "AURAS";

            string targetFolder = Path.Combine(SavePath, typeFolder);

            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
                AssetDatabase.Refresh();
            }

            PoEAbility asset = ScriptableObject.CreateInstance<PoEAbility>();
            asset.AbilityName = aName;
            asset.Element = elem;
            asset.SkillType = sType;
            asset.Cooldown = cd;
            asset.ManaCost = mana;
            asset.DamageMultiplier = dmgMul;
            asset.AddedFlatDamage = flat;
            asset.AttackRange = rng;
            asset.ProjectileCount = proj;
            asset.Speed = spd;
            asset.SkillDescription = desc;

            string safeName = aName.Replace(" ", "_").Replace("'", "");
            string fileName = $"{elem}_{safeName}.asset";
            string fullPath = Path.Combine(targetFolder, fileName).Replace("\\", "/");

            AssetDatabase.CreateAsset(asset, fullPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AbilityGenerator] Created: {fullPath}");
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }

        private void GeneratePoeAbilities()
        {
            // MELEE SKILLS (Melee, Physical / Chaos)
            CreateAbility("Heavy Strike", PoEElementType.Physical, PoEAbilityType.Melee, 0.5f, 5f, 1.8f, 15f, 1.8f, 1, 0f, "A massive, powerful slam with knockback and high stun multiplier.");
            CreateAbility("Cyclone", PoEElementType.Physical, PoEAbilityType.Melee, 0f, 2f, 0.6f, 2f, 2.5f, 1, 0f, "Spin continuously, attacking all surrounding targets with rapid movement.");
            CreateAbility("Flicker Strike", PoEElementType.Chaos, PoEAbilityType.Melee, 1.5f, 8f, 1.4f, 10f, 6f, 1, 0f, "Teleport instantly to a nearby enemy and strike them down with raw chaos energy.");

            // SPELLS (Fire / Cold / Lightning Spells)
            CreateAbility("Fireball", PoEElementType.Fire, PoEAbilityType.Spell, 0.8f, 12f, 1.3f, 8f, 6.5f, 1, 12f, "Unleashes an explosive ball of flame that bursts in an AOE upon contact.");
            CreateAbility("Arc", PoEElementType.Lightning, PoEAbilityType.Spell, 0.6f, 15f, 1.1f, 5f, 7.0f, 1, 20f, "Releases a bolt of fork lightning that chains through multiple nearby targets.");
            CreateAbility("Freezing Pulse", PoEElementType.Cold, PoEAbilityType.Spell, 0.7f, 14f, 1.15f, 6f, 5.5f, 1, 15f, "Fires a wide wave of freezing ice that pierces all enemies and has high freeze rate.");

            // BOW SKILLS (Ranged, Physical / Cold / Fire)
            CreateAbility("Split Arrow", PoEElementType.Physical, PoEAbilityType.Ranged, 0.5f, 6f, 0.9f, 4f, 8.0f, 5, 16f, "Fires a volley of 5 arrows in a wide frontal cone.");
            CreateAbility("Tornado Shot", PoEElementType.Physical, PoEAbilityType.Ranged, 0.8f, 10f, 1.1f, 8f, 7.5f, 3, 14f, "Fires a special arrow that explodes into a swirling vortex of secondary arrows.");
            CreateAbility("Ice Shot", PoEElementType.Cold, PoEAbilityType.Ranged, 0.6f, 8f, 1.2f, 6f, 8.0f, 1, 15f, "Fires an arrow that explodes into a cone of icy shards behind the first target hit.");

            // AURAS (Buffs)
            CreateAbility("Hatred", PoEElementType.Cold, PoEAbilityType.Aura, 10f, 50f, 1.0f, 0f, 15.0f, 1, 0f, "An aura that reserves mana to grant massive extra cold damage based on physical power.");
            CreateAbility("Determination", PoEElementType.Physical, PoEAbilityType.Aura, 10f, 50f, 1.0f, 0f, 15.0f, 1, 0f, "An aura that reserves mana to grant a massive boost to evasion and physical defense.");
            CreateAbility("Grace", PoEElementType.Lightning, PoEAbilityType.Aura, 10f, 50f, 1.0f, 0f, 15.0f, 1, 0f, "An aura that reserves mana to grant a massive boost to movement speed and agility.");

            AssetDatabase.Refresh();
            Debug.Log("[AbilityGenerator] PoE Ability Database generated successfully under Assets/Data/DTOS/Abilities!");
        }
    }
}
