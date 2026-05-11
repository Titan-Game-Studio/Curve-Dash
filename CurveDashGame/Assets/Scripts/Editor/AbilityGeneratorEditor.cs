using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace STG.CurveDash.Editor
{
    public class AbilityGeneratorEditor : EditorWindow
    {
        private int _selectedTab = 0;

        // --- ACTIVE ABILITY FIELDS ---
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

        // --- SUPPORT ABILITY FIELDS ---
        private string _supportName = "Lesser Multiple Projectiles";
        private string _supportDescription = "Fires 2 additional projectiles but reduces damage by 15%.";
        private float _supportDmgMultPercent = -15f;
        private float _supportAddedFlatDmg = 0f;
        private float _supportCritBonus = 0f;
        private int _supportExtraProjectiles = 2;
        private float _supportSpeedMultPercent = 10f;

        // Compatibility Toggle Filters for Support Gem
        private bool _meleeSupported = false;
        private bool _spellSupported = true;
        private bool _rangedSupported = true;
        private bool _auraSupported = false;

        private bool _physSupported = true;
        private bool _fireSupported = true;
        private bool _coldSupported = true;
        private bool _lightningSupported = true;
        private bool _chaosSupported = true;

        private const string SavePath = "Assets/Data/DTOS/Abilities/";

        [MenuItem("Curve-Dash/Tools/Ability Generator")]
        public static void ShowWindow()
        {
            GetWindow<AbilityGeneratorEditor>("Ability Generator");
        }

        private void OnGUI()
        {
            _selectedTab = GUILayout.Toolbar(_selectedTab, new string[] { "Active Abilities", "Support Abilities" });
            EditorGUILayout.Space();

            if (_selectedTab == 0)
            {
                DrawActiveAbilitiesTab();
            }
            else
            {
                DrawSupportAbilitiesTab();
            }
        }

        private void DrawActiveAbilitiesTab()
        {
            GUILayout.Label("PoE Active Ability Creation Tool", EditorStyles.boldLabel);
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
            if (GUILayout.Button("Create Single Active Ability", GUILayout.Height(30)))
            {
                CreateActiveAbility(_abilityName, _element, _skillType, _cooldown, _manaCost, _damageMultiplier, _addedFlatDamage, _attackRange, _projectileCount, _speed, _skillDescription);
            }

            EditorGUILayout.Space();
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Generate Full Active Abilities Database", GUILayout.Height(40)))
            {
                GeneratePoeAbilities();
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawSupportAbilitiesTab()
        {
            GUILayout.Label("PoE Support Ability Creation Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _supportName = EditorGUILayout.TextField("Support Name", _supportName);
            _supportDescription = EditorGUILayout.TextField("Description", _supportDescription);

            EditorGUILayout.Space();
            GUILayout.Label("Skill Type Compatibility (Supported Types)", EditorStyles.boldLabel);
            _meleeSupported = EditorGUILayout.Toggle("Supports MELEE", _meleeSupported);
            _spellSupported = EditorGUILayout.Toggle("Supports SPELLS", _spellSupported);
            _rangedSupported = EditorGUILayout.Toggle("Supports RANGED", _rangedSupported);
            _auraSupported = EditorGUILayout.Toggle("Supports AURAS", _auraSupported);

            EditorGUILayout.Space();
            GUILayout.Label("Elemental Compatibility (Supported Elements)", EditorStyles.boldLabel);
            _physSupported = EditorGUILayout.Toggle("Supports PHYSICAL", _physSupported);
            _fireSupported = EditorGUILayout.Toggle("Supports FIRE", _fireSupported);
            _coldSupported = EditorGUILayout.Toggle("Supports COLD", _coldSupported);
            _lightningSupported = EditorGUILayout.Toggle("Supports LIGHTNING", _lightningSupported);
            _chaosSupported = EditorGUILayout.Toggle("Supports CHAOS", _chaosSupported);

            EditorGUILayout.Space();
            GUILayout.Label("Stat Modifiers", EditorStyles.boldLabel);
            _supportDmgMultPercent = EditorGUILayout.FloatField("Damage Multiplier (%)", _supportDmgMultPercent);
            _supportAddedFlatDmg = EditorGUILayout.FloatField("Added Flat Damage", _supportAddedFlatDmg);
            _supportCritBonus = EditorGUILayout.FloatField("Crit Chance Bonus (%)", _supportCritBonus);
            _supportExtraProjectiles = EditorGUILayout.IntField("Extra Projectiles", _supportExtraProjectiles);
            _supportSpeedMultPercent = EditorGUILayout.FloatField("Projectile Speed (%)", _supportSpeedMultPercent);

            EditorGUILayout.Space();
            if (GUILayout.Button("Create Single Support Ability", GUILayout.Height(30)))
            {
                // Gather compatibility lists
                List<PoEAbilityType> compatibleTypes = new List<PoEAbilityType>();
                if (_meleeSupported) compatibleTypes.Add(PoEAbilityType.Melee);
                if (_spellSupported) compatibleTypes.Add(PoEAbilityType.Spell);
                if (_rangedSupported) compatibleTypes.Add(PoEAbilityType.Ranged);
                if (_auraSupported) compatibleTypes.Add(PoEAbilityType.Aura);

                List<PoEElementType> compatibleElements = new List<PoEElementType>();
                if (_physSupported) compatibleElements.Add(PoEElementType.Physical);
                if (_fireSupported) compatibleElements.Add(PoEElementType.Fire);
                if (_coldSupported) compatibleElements.Add(PoEElementType.Cold);
                if (_lightningSupported) compatibleElements.Add(PoEElementType.Lightning);
                if (_chaosSupported) compatibleElements.Add(PoEElementType.Chaos);

                CreateSupportAbility(_supportName, _supportDescription, compatibleTypes, compatibleElements,
                    _supportDmgMultPercent, _supportAddedFlatDmg, _supportCritBonus, _supportExtraProjectiles, _supportSpeedMultPercent);
            }

            EditorGUILayout.Space();
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Generate Presets Support Gems Database", GUILayout.Height(40)))
            {
                GeneratePoeSupports();
            }
            GUI.backgroundColor = Color.white;
        }

        private void CreateActiveAbility(string aName, PoEElementType elem, PoEAbilityType sType, float cd, float mana, float dmgMul, float flat, float rng, int proj, float spd, string desc)
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

            Debug.Log($"[AbilityGenerator] Created Active: {fullPath}");
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }

        private void CreateSupportAbility(string sName, string desc, List<PoEAbilityType> allowedTypes, List<PoEElementType> allowedElems,
            float dmgMult, float flat, float crit, int extraProj, float speedMult)
        {
            string targetFolder = Path.Combine(SavePath, "SUPPORTS");

            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
                AssetDatabase.Refresh();
            }

            SupportAbilityData asset = ScriptableObject.CreateInstance<SupportAbilityData>();
            asset.AbilityName = sName;
            asset.SupportDescription = desc;
            asset.SupportedSkillTypes = allowedTypes;
            asset.SupportedElements = allowedElems;
            asset.DamageMultiplierPercent = dmgMult;
            asset.AddedFlatDamageBonus = flat;
            asset.CriticalChanceBonus = crit;
            asset.ExtraProjectiles = extraProj;
            asset.SpeedMultiplierPercent = speedMult;

            string safeName = sName.Replace(" ", "_").Replace("'", "");
            string fileName = $"Support_{safeName}.asset";
            string fullPath = Path.Combine(targetFolder, fileName).Replace("\\", "/");

            AssetDatabase.CreateAsset(asset, fullPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AbilityGenerator] Created Support: {fullPath}");
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }

        private void GeneratePoeAbilities()
        {
            // MELEE SKILLS (Melee, Physical / Chaos)
            CreateActiveAbility("Heavy Strike", PoEElementType.Physical, PoEAbilityType.Melee, 0.5f, 5f, 1.8f, 15f, 1.8f, 1, 0f, "A massive, powerful slam with knockback and high stun multiplier.");
            CreateActiveAbility("Cyclone", PoEElementType.Physical, PoEAbilityType.Melee, 0f, 2f, 0.6f, 2f, 2.5f, 1, 0f, "Spin continuously, attacking all surrounding targets with rapid movement.");
            CreateActiveAbility("Flicker Strike", PoEElementType.Chaos, PoEAbilityType.Melee, 1.5f, 8f, 1.4f, 10f, 6f, 1, 0f, "Teleport instantly to a nearby enemy and strike them down with raw chaos energy.");

            // SPELLS (Fire / Cold / Lightning Spells)
            CreateActiveAbility("Fireball", PoEElementType.Fire, PoEAbilityType.Spell, 0.8f, 12f, 1.3f, 8f, 6.5f, 1, 12f, "Unleashes an explosive ball of flame that bursts in an AOE upon contact.");
            CreateActiveAbility("Arc", PoEElementType.Lightning, PoEAbilityType.Spell, 0.6f, 15f, 1.1f, 5f, 7.0f, 1, 20f, "Releases a bolt of fork lightning that chains through multiple nearby targets.");
            CreateActiveAbility("Freezing Pulse", PoEElementType.Cold, PoEAbilityType.Spell, 0.7f, 14f, 1.15f, 6f, 5.5f, 1, 15f, "Fires a wide wave of freezing ice that pierces all enemies and has high freeze rate.");

            // BOW SKILLS (Ranged, Physical / Cold / Fire)
            CreateActiveAbility("Split Arrow", PoEElementType.Physical, PoEAbilityType.Ranged, 0.5f, 6f, 0.9f, 4f, 8.0f, 5, 16f, "Fires a volley of 5 arrows in a wide frontal cone.");
            CreateActiveAbility("Tornado Shot", PoEElementType.Physical, PoEAbilityType.Ranged, 0.8f, 10f, 1.1f, 8f, 7.5f, 3, 14f, "Fires a special arrow that explodes into a swirling vortex of secondary arrows.");
            CreateActiveAbility("Ice Shot", PoEElementType.Cold, PoEAbilityType.Ranged, 0.6f, 8f, 1.2f, 6f, 8.0f, 1, 15f, "Fires an arrow that explodes into a cone of icy shards behind the first target hit.");

            // AURAS (Buffs)
            CreateActiveAbility("Hatred", PoEElementType.Cold, PoEAbilityType.Aura, 10f, 50f, 1.0f, 0f, 15.0f, 1, 0f, "An aura that reserves mana to grant massive extra cold damage based on physical power.");
            CreateActiveAbility("Determination", PoEElementType.Physical, PoEAbilityType.Aura, 10f, 50f, 1.0f, 0f, 15.0f, 1, 0f, "An aura that reserves mana to grant a massive boost to evasion and physical defense.");
            CreateActiveAbility("Grace", PoEElementType.Lightning, PoEAbilityType.Aura, 10f, 50f, 1.0f, 0f, 15.0f, 1, 0f, "An aura that reserves mana to grant a massive boost to movement speed and agility.");

            AssetDatabase.Refresh();
            Debug.Log("[AbilityGenerator] PoE Active Ability Database generated successfully under Assets/Data/DTOS/Abilities!");
        }

        private void GeneratePoeSupports()
        {
            // 1. Lesser Multiple Projectiles (LMP) - Supports Spells & Ranged
            CreateSupportAbility(
                "Lesser Multiple Projectiles",
                "Supports projectile spells and ranged abilities. Adds 2 extra projectiles but reduces damage by 15%.",
                new List<PoEAbilityType> { PoEAbilityType.Spell, PoEAbilityType.Ranged },
                new List<PoEElementType>(), // Empty = supports all elements
                -15f, 0f, 0f, 2, 10f
            );

            // 2. Greater Multiple Projectiles (GMP) - Supports Spells & Ranged
            CreateSupportAbility(
                "Greater Multiple Projectiles",
                "Supports projectile spells and ranged abilities. Adds 4 extra projectiles but reduces damage by 26%.",
                new List<PoEAbilityType> { PoEAbilityType.Spell, PoEAbilityType.Ranged },
                new List<PoEElementType>(),
                -26f, 0f, 0f, 4, 20f
            );

            // 3. Melee Physical Damage Support - Supports Melee Physical skills only
            CreateSupportAbility(
                "Melee Physical Damage",
                "Supports melee attack skills. Deals 35% MORE physical damage.",
                new List<PoEAbilityType> { PoEAbilityType.Melee },
                new List<PoEElementType> { PoEElementType.Physical },
                35f, 5f, 0f, 0, 0f
            );

            // 4. Elemental Focus Support - Supports any Elemental Spell/Attack (Fire, Cold, Lightning)
            CreateSupportAbility(
                "Elemental Focus",
                "Supports skills that deal elemental damage. Deals 40% MORE fire, cold, or lightning damage.",
                new List<PoEAbilityType> { PoEAbilityType.Spell, PoEAbilityType.Melee, PoEAbilityType.Ranged },
                new List<PoEElementType> { PoEElementType.Fire, PoEElementType.Cold, PoEElementType.Lightning },
                40f, 0f, 0f, 0, 0f
            );

            // 5. Faster Projectiles Support - Supports Spells & Ranged
            CreateSupportAbility(
                "Faster Projectiles",
                "Supports spells and ranged abilities. Projectiles fly 50% faster and deal 10% increased damage.",
                new List<PoEAbilityType> { PoEAbilityType.Spell, PoEAbilityType.Ranged },
                new List<PoEElementType>(),
                10f, 0f, 0f, 0, 50f
            );

            // 6. Increased Critical Strikes - Supports All Elements & Damage types
            CreateSupportAbility(
                "Increased Critical Strikes",
                "Supports any damage dealing skill. Grants massive +8% base critical strike chance.",
                new List<PoEAbilityType> { PoEAbilityType.Melee, PoEAbilityType.Spell, PoEAbilityType.Ranged },
                new List<PoEElementType>(),
                0f, 0f, 8f, 0, 0f
            );

            AssetDatabase.Refresh();
            Debug.Log("[AbilityGenerator] Preset Support Gems Database generated successfully under Assets/Data/DTOS/Abilities/SUPPORTS!");
        }
    }
}

