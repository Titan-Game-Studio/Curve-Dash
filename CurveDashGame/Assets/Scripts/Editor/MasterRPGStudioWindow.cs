using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DevionGames.InventorySystem;

namespace STG.CurveDash.Editor
{
    public class MasterRPGStudioWindow : EditorWindow
    {
        public enum WeaponGenCategory
        {
            OneHandedSword,
            GreatSword,
            Hammer,
            GreatBow,
            Shield,
            Arrow
        }

        private int _selectedTab = 0;
        private Vector2 _scrollPos;

        // --- DIRECTORY PATHS ---
        private const string WEAPONS_PATH = "Assets/Data/DTOS/Weapons";
        private const string ARMORS_PATH = "Assets/Data/DTOS/Armors";
        private const string ABILITIES_PATH = "Assets/Data/DTOS/Abilities";
        private const string AFFIXES_PATH = "Assets/Data/DTOS/Affixs";
        private const string RPG_BASE_PATH = "Assets/Data/DTOS/RPGItems";
        private const string ADAPTERS_BASE_PATH = "Assets/Data/DTOS/Adapters";

        // --- TAB 1: WEAPONS & ARMORS ---
        private bool _isWeapon = true;
        private string _equipName = "Exquisite Foil";
        private ItemRarity _equipRarity = ItemRarity.Normal;
        private WeaponGenCategory _weaponCat = WeaponGenCategory.OneHandedSword;
        private EquipmentSlot _armorSlot = EquipmentSlot.Head;
        private float _minDamage = 15f, _maxDamage = 22f, _attackSpeed = 1.4f, _attackRange = 1.6f;
        private int _armorDefense = 25, _armorHealth = 20, _modularIndex = 0;
        private float _blockChance = 30f, _bonusDamage = 0f;
        private string _equipDesc = "A refined and powerful piece of equipment.";

        // --- TAB 2: ABILITIES & GEMS ---
        private bool _isActiveAbility = true;
        private string _abilityName = "Fireball";
        private PoEElementType _element = PoEElementType.Fire;
        private PoEAbilityType _skillType = PoEAbilityType.Spell;
        private float _cooldown = 1.0f, _manaCost = 15f, _damageMult = 1.2f, _addedFlatDamage = 5f, _abilityRange = 6.0f, _projectileSpeed = 12f;
        private int _projCount = 1;
        private string _abilityDesc = "Unleashes a devastating elemental blast.";
        private float _supportDmgMult = -15f, _supportAddedFlat = 0f, _supportCrit = 0f, _supportSpeedMult = 10f;
        private int _supportExtraProj = 2;

        // --- TAB 3: FLASKS & CURRENCIES ---
        private bool _isFlask = true;
        private string _consumableName = "Life Flask";
        private FlaskType _flaskType = FlaskType.Life;
        private float _flaskRecovery = 50f, _flaskDuration = 5f, _flaskSpeedMod = 1f;
        private int _flaskMaxChg = 30, _flaskChgUse = 10;
        private CurrencyType _currencyType = CurrencyType.ChaosOrb;
        private string _consumableDesc = "A valuable item used by travelers.";

        // --- TAB 4: AFFIXES ---
        private string _affixName = "Sharp";
        private AffixType _affixType = AffixType.Prefix;
        private StatType _statType = StatType.AddedPhysicalDamage;
        private float _affixMin = 5f, _affixMax = 15f;

        [MenuItem("Curve-Dash/Tools/Master RPG Studio (All-in-One Generator)", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<MasterRPGStudioWindow>("Master RPG Studio");
            window.minSize = new Vector2(600, 500);
            window.Show();
        }

        private void OnGUI()
        {
            GUI.backgroundColor = new Color(0.12f, 0.15f, 0.22f, 1f);
            EditorGUILayout.BeginVertical("box");
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = new Color(0.2f, 0.8f, 1f);
            GUILayout.Label("✨ MASTER RPG STUDIO ✨", titleStyle, GUILayout.Height(35));
            GUILayout.Label("All-in-One DTO, Ability, Consumable, Affix & Adapter Generator", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();
            _selectedTab = GUILayout.Toolbar(_selectedTab, new string[] { "🗡️ Weapons & Armors", "🔮 Abilities & Gems", "⚗️ Flasks & Currencies", "✨ Affixes", "📦 Bulk Adapters & Sync" });
            EditorGUILayout.Space();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            switch (_selectedTab)
            {
                case 0: DrawWeaponsAndArmorsTab(); break;
                case 1: DrawAbilitiesTab(); break;
                case 2: DrawConsumablesTab(); break;
                case 3: DrawAffixesTab(); break;
                case 4: DrawBulkAdaptersTab(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        // ====================================================================
        // TAB 1: WEAPONS & ARMORS
        // ====================================================================
        private void DrawWeaponsAndArmorsTab()
        {
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = _isWeapon ? new Color(0.3f, 0.8f, 0.4f, 1f) : Color.white;
            if (GUILayout.Button("🗡️ Weapon Generator", GUILayout.Height(30))) _isWeapon = true;
            GUI.backgroundColor = !_isWeapon ? new Color(0.9f, 0.6f, 0.2f, 1f) : Color.white;
            if (GUILayout.Button("🛡️ Armor Generator", GUILayout.Height(30))) _isWeapon = false;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label(_isWeapon ? "Single Weapon Settings" : "Single Armor Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _equipName = EditorGUILayout.TextField("Item Name", _equipName);
            _equipRarity = (ItemRarity)EditorGUILayout.EnumPopup("Rarity", _equipRarity);

            if (_isWeapon)
            {
                EditorGUI.BeginChangeCheck();
                _weaponCat = (WeaponGenCategory)EditorGUILayout.EnumPopup("Weapon Category", _weaponCat);
                if (EditorGUI.EndChangeCheck())
                {
                    ApplyWeaponDefaults();
                }

                EditorGUILayout.Space();
                if (_weaponCat == WeaponGenCategory.Shield || _weaponCat == WeaponGenCategory.Arrow)
                {
                    _blockChance = EditorGUILayout.FloatField("Block Chance (%)", _blockChance);
                    _bonusDamage = EditorGUILayout.FloatField("Bonus Damage", _bonusDamage);
                }
                else
                {
                    _minDamage = EditorGUILayout.FloatField("Min Damage", _minDamage);
                    _maxDamage = EditorGUILayout.FloatField("Max Damage", _maxDamage);
                    _attackSpeed = EditorGUILayout.FloatField("Attack Speed", _attackSpeed);
                    _attackRange = EditorGUILayout.FloatField("Attack Range", _attackRange);
                }
            }
            else
            {
                _armorSlot = (EquipmentSlot)EditorGUILayout.EnumPopup("Equipment Slot", _armorSlot);
                _armorDefense = EditorGUILayout.IntField("Defense Value", _armorDefense);
                _armorHealth = EditorGUILayout.IntField("Health Bonus", _armorHealth);
                _modularIndex = EditorGUILayout.IntField("Modular Part Index", _modularIndex);
            }

            EditorGUILayout.Space();
            _equipDesc = EditorGUILayout.TextField("Description", _equipDesc);

            EditorGUILayout.Space();
            GUI.backgroundColor = new Color(0.2f, 0.7f, 1f);
            if (GUILayout.Button(_isWeapon ? "Create Single Weapon" : "Create Single Armor", GUILayout.Height(35)))
            {
                if (_isWeapon) CreateWeapon(_equipName, _weaponCat, _equipRarity, _minDamage, _maxDamage, _attackSpeed, _attackRange, _blockChance, _bonusDamage, _equipDesc);
                else CreateArmor(_equipName, _armorSlot, _equipRarity, _armorDefense, _armorHealth, _modularIndex, _equipDesc);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Database Preset Generator", EditorStyles.boldLabel);
            GUILayout.Label("Instantly generates the full PoE-inspired preset databases.", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            GUI.backgroundColor = new Color(0.1f, 0.8f, 0.5f);
            if (GUILayout.Button("⚡ Generate Full Preset Weapons & Armors Database", GUILayout.Height(40)))
            {
                GeneratePresetWeapons();
                GeneratePresetArmors();
                EditorUtility.DisplayDialog("Success", "Preset Weapons and Armors databases generated successfully!", "OK");
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
        }

        private void ApplyWeaponDefaults()
        {
            switch (_weaponCat)
            {
                case WeaponGenCategory.OneHandedSword:
                    _minDamage = 14f; _maxDamage = 20f; _attackSpeed = 1.4f; _attackRange = 1.6f;
                    break;
                case WeaponGenCategory.GreatSword:
                    _minDamage = 30f; _maxDamage = 42f; _attackSpeed = 0.85f; _attackRange = 2.2f;
                    break;
                case WeaponGenCategory.Hammer:
                    _minDamage = 40f; _maxDamage = 58f; _attackSpeed = 0.65f; _attackRange = 2f;
                    break;
                case WeaponGenCategory.GreatBow:
                    _minDamage = 16f; _maxDamage = 24f; _attackSpeed = 1.1f; _attackRange = 4.5f;
                    break;
            }
        }

        private void CreateWeapon(string wName, WeaponGenCategory cat, ItemRarity rarity, float min, float max, float spd, float rng, float blk, float bonus, string desc)
        {
            string folderName = cat.ToString().ToUpper();
            if (cat == WeaponGenCategory.OneHandedSword) folderName = "ONE-HANDED SWORDS";
            else if (cat == WeaponGenCategory.GreatSword) folderName = "GREAT SWORDS";
            else if (cat == WeaponGenCategory.GreatBow) folderName = "GREAT BOWS";

            string targetFolder = EnsureDirectory(Path.Combine(WEAPONS_PATH, folderName));
            string fullPath = Path.Combine(targetFolder, $"{rarity}_{wName.Replace(" ", "_")}.asset").Replace("\\", "/");

            ScriptableObject asset = null;
            if (cat == WeaponGenCategory.OneHandedSword)
            {
                var w = ScriptableObject.CreateInstance<OneHandedWeaponData>();
                w.BaseMinDamage = min; w.BaseMaxDamage = max; w.BaseAttackSpeed = spd; w.BaseAttackRange = rng; asset = w;
            }
            else if (cat == WeaponGenCategory.GreatSword || cat == WeaponGenCategory.Hammer)
            {
                var w = ScriptableObject.CreateInstance<TwoHandedWeaponData>();
                w.BaseMinDamage = min; w.BaseMaxDamage = max; w.BaseAttackSpeed = spd; w.BaseAttackRange = rng; asset = w;
            }
            else if (cat == WeaponGenCategory.GreatBow)
            {
                var w = ScriptableObject.CreateInstance<BowData>();
                w.BaseMinDamage = min; w.BaseMaxDamage = max; w.BaseAttackSpeed = spd; w.BaseAttackRange = rng; asset = w;
            }
            else if (cat == WeaponGenCategory.Shield || cat == WeaponGenCategory.Arrow)
            {
                var o = ScriptableObject.CreateInstance<OffHandData>();
                o.SubType = cat == WeaponGenCategory.Shield ? OffHandType.Shield : OffHandType.Arrow;
                o.BlockChance = blk; o.BonusDamage = bonus; asset = o;
            }

            if (asset != null)
            {
                var item = (ItemData)asset;
                item.ItemName = wName; item.Rarity = rarity; item.Description = desc;
                item.Type = cat == WeaponGenCategory.Shield ? ItemType.Armor : ItemType.Weapon;

                AssetDatabase.CreateAsset(asset, fullPath);
                AssetDatabase.SaveAssets();
                Selection.activeObject = asset;
                Debug.Log($"[Studio] Created Weapon: {fullPath}");
            }
        }

        private void CreateArmor(string aName, EquipmentSlot slot, ItemRarity rarity, int def, int hp, int modIdx, string desc)
        {
            string folderName = slot.ToString().ToUpper();
            if (slot == EquipmentSlot.Head) folderName = "HEAD";
            else if (slot == EquipmentSlot.Body) folderName = "BODY";
            else if (slot == EquipmentSlot.Hands) folderName = "HANDS";
            else if (slot == EquipmentSlot.Feet) folderName = "FEET";

            string targetFolder = EnsureDirectory(Path.Combine(ARMORS_PATH, folderName));
            string fullPath = Path.Combine(targetFolder, $"{rarity}_{aName.Replace(" ", "_")}.asset").Replace("\\", "/");

            var asset = ScriptableObject.CreateInstance<ArmorItemData>();
            asset.ItemName = aName; asset.Slot = slot; asset.Rarity = rarity; asset.Defense = def; asset.HealthBonus = hp; asset.ModularPartIndex = modIdx; asset.Description = desc;

            AssetDatabase.CreateAsset(asset, fullPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            Debug.Log($"[Studio] Created Armor: {fullPath}");
        }

        private void GeneratePresetWeapons()
        {
            CreateWeapon("Rusted Sword", WeaponGenCategory.OneHandedSword, ItemRarity.Normal, 8, 12, 1.3f, 1.5f, 0, 0, "A basic blade.");
            CreateWeapon("Scaeva", WeaponGenCategory.OneHandedSword, ItemRarity.Rare, 22, 30, 1.5f, 1.7f, 0, 0, "A golden gladius.");
            CreateWeapon("Reaver Sword", WeaponGenCategory.GreatSword, ItemRarity.Rare, 48, 65, 0.9f, 2.4f, 0, 0, "An executioner greatsword.");
            CreateWeapon("Starforge", WeaponGenCategory.GreatSword, ItemRarity.Unique, 72, 100, 0.95f, 2.6f, 0, 0, "Legendary celestial sword.");
            CreateWeapon("Reach of the Council", WeaponGenCategory.GreatBow, ItemRarity.Unique, 40, 58, 1.3f, 5.5f, 0, 0, "Fires multiple arrows.");
            CreateWeapon("Aegis Aurora", WeaponGenCategory.Shield, ItemRarity.Unique, 0, 0, 0, 0, 55f, 10f, "Legendary shield.");
        }

        private void GeneratePresetArmors()
        {
            CreateArmor("Iron Helmet", EquipmentSlot.Head, ItemRarity.Normal, 15, 10, 0, "Basic metal headgear.");
            CreateArmor("Goldrim", EquipmentSlot.Head, ItemRarity.Unique, 50, 40, 4, "High resistance circlet.");
            CreateArmor("Kaoms Heart", EquipmentSlot.Body, ItemRarity.Unique, 0, 500, 3, "Grants massive life pool (+500 HP).");
            CreateArmor("Facebreaker", EquipmentSlot.Hands, ItemRarity.Unique, 20, 10, 3, "Grants unarmed multipliers.");
            CreateArmor("Kaoms Roots", EquipmentSlot.Feet, ItemRarity.Unique, 100, 150, 3, "Unstoppable boots.");
        }

        // ====================================================================
        // TAB 2: ABILITIES & GEMS
        // ====================================================================
        private void DrawAbilitiesTab()
        {
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = _isActiveAbility ? new Color(0.8f, 0.3f, 0.5f, 1f) : Color.white;
            if (GUILayout.Button("🔮 Active Ability", GUILayout.Height(30))) _isActiveAbility = true;
            GUI.backgroundColor = !_isActiveAbility ? new Color(0.4f, 0.6f, 0.9f, 1f) : Color.white;
            if (GUILayout.Button("🌟 Support Ability", GUILayout.Height(30))) _isActiveAbility = false;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            if (_isActiveAbility)
            {
                GUILayout.Label("Active Ability Settings", EditorStyles.boldLabel);
                _abilityName = EditorGUILayout.TextField("Ability Name", _abilityName);
                _element = (PoEElementType)EditorGUILayout.EnumPopup("Element", _element);
                _skillType = (PoEAbilityType)EditorGUILayout.EnumPopup("Skill Type", _skillType);
                _cooldown = EditorGUILayout.FloatField("Cooldown", _cooldown);
                _manaCost = EditorGUILayout.FloatField("Mana Cost", _manaCost);
                _damageMult = EditorGUILayout.FloatField("Damage Multiplier", _damageMult);
                _addedFlatDamage = EditorGUILayout.FloatField("Added Flat Damage", _addedFlatDamage);
                _abilityRange = EditorGUILayout.FloatField("Attack Range", _abilityRange);
                _projCount = EditorGUILayout.IntField("Projectiles Count", _projCount);
                _projectileSpeed = EditorGUILayout.FloatField("Move/Proj Speed", _projectileSpeed);
                _abilityDesc = EditorGUILayout.TextField("Description", _abilityDesc);

                EditorGUILayout.Space();
                GUI.backgroundColor = new Color(0.8f, 0.3f, 0.5f);
                if (GUILayout.Button("Create Active Ability", GUILayout.Height(35)))
                {
                    CreateActiveAbility(_abilityName, _element, _skillType, _cooldown, _manaCost, _damageMult, _addedFlatDamage, _abilityRange, _projCount, _projectileSpeed, _abilityDesc);
                }
            }
            else
            {
                GUILayout.Label("Support Ability Settings", EditorStyles.boldLabel);
                _abilityName = EditorGUILayout.TextField("Support Name", _abilityName);
                _supportDmgMult = EditorGUILayout.FloatField("Damage Mult (%)", _supportDmgMult);
                _supportAddedFlat = EditorGUILayout.FloatField("Added Flat Damage", _supportAddedFlat);
                _supportCrit = EditorGUILayout.FloatField("Crit Bonus (%)", _supportCrit);
                _supportExtraProj = EditorGUILayout.IntField("Extra Projectiles", _supportExtraProj);
                _supportSpeedMult = EditorGUILayout.FloatField("Speed Mult (%)", _supportSpeedMult);
                _abilityDesc = EditorGUILayout.TextField("Description", _abilityDesc);

                EditorGUILayout.Space();
                GUI.backgroundColor = new Color(0.4f, 0.6f, 0.9f);
                if (GUILayout.Button("Create Support Ability", GUILayout.Height(35)))
                {
                    CreateSupportAbility(_abilityName, _abilityDesc, new List<PoEAbilityType> { PoEAbilityType.Spell, PoEAbilityType.Ranged }, new List<PoEElementType>(), _supportDmgMult, _supportAddedFlat, _supportCrit, _supportExtraProj, _supportSpeedMult);
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Preset Abilities Generator", EditorStyles.boldLabel);
            GUI.backgroundColor = new Color(0.6f, 0.3f, 0.9f);
            if (GUILayout.Button("⚡ Generate Full Abilities & Supports Presets", GUILayout.Height(40)))
            {
                CreateActiveAbility("Fireball", PoEElementType.Fire, PoEAbilityType.Spell, 0.8f, 12f, 1.3f, 8f, 6.5f, 1, 12f, "Explosive flame ball.");
                CreateActiveAbility("Cyclone", PoEElementType.Physical, PoEAbilityType.Melee, 0f, 2f, 0.6f, 2f, 2.5f, 1, 0f, "Spin continuously.");
                CreateSupportAbility("Lesser Multiple Projectiles", "Adds 2 extra projectiles but reduces damage by 15%.", new List<PoEAbilityType> { PoEAbilityType.Spell, PoEAbilityType.Ranged }, new List<PoEElementType>(), -15f, 0f, 0f, 2, 10f);
                CreateSupportAbility("Elemental Focus", "Deals 40% MORE elemental damage.", new List<PoEAbilityType> { PoEAbilityType.Spell, PoEAbilityType.Melee }, new List<PoEElementType> { PoEElementType.Fire, PoEElementType.Cold }, 40f, 0f, 0f, 0, 0f);
                EditorUtility.DisplayDialog("Success", "Preset Abilities generated successfully!", "OK");
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
        }

        private void CreateActiveAbility(string aName, PoEElementType elem, PoEAbilityType sType, float cd, float mana, float dmgMul, float flat, float rng, int proj, float spd, string desc)
        {
            string targetFolder = EnsureDirectory(Path.Combine(ABILITIES_PATH, sType.ToString().ToUpper()));
            string fullPath = Path.Combine(targetFolder, $"{elem}_{aName.Replace(" ", "_")}.asset").Replace("\\", "/");

            var asset = ScriptableObject.CreateInstance<PoEAbility>();
            asset.AbilityName = aName; asset.Element = elem; asset.SkillType = sType; asset.Cooldown = cd; asset.ManaCost = mana;
            asset.DamageMultiplier = dmgMul; asset.AddedFlatDamage = flat; asset.AttackRange = rng; asset.ProjectileCount = proj; asset.Speed = spd; asset.SkillDescription = desc;

            AssetDatabase.CreateAsset(asset, fullPath); AssetDatabase.SaveAssets(); Selection.activeObject = asset;
        }

        private void CreateSupportAbility(string sName, string desc, List<PoEAbilityType> allowedTypes, List<PoEElementType> allowedElems, float dmgMult, float flat, float crit, int extraProj, float speedMult)
        {
            string targetFolder = EnsureDirectory(Path.Combine(ABILITIES_PATH, "SUPPORTS"));
            string fullPath = Path.Combine(targetFolder, $"Support_{sName.Replace(" ", "_")}.asset").Replace("\\", "/");

            var asset = ScriptableObject.CreateInstance<SupportAbilityData>();
            asset.AbilityName = sName; asset.SupportDescription = desc; asset.SupportedSkillTypes = allowedTypes; asset.SupportedElements = allowedElems;
            asset.DamageMultiplierPercent = dmgMult; asset.AddedFlatDamageBonus = flat; asset.CriticalChanceBonus = crit; asset.ExtraProjectiles = extraProj; asset.SpeedMultiplierPercent = speedMult;

            AssetDatabase.CreateAsset(asset, fullPath); AssetDatabase.SaveAssets(); Selection.activeObject = asset;
        }

        // ====================================================================
        // TAB 3: FLASKS & CURRENCIES
        // ====================================================================
        private void DrawConsumablesTab()
        {
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = _isFlask ? new Color(0.1f, 0.7f, 0.8f, 1f) : Color.white;
            if (GUILayout.Button("⚗️ Flask Generator", GUILayout.Height(30))) _isFlask = true;
            GUI.backgroundColor = !_isFlask ? new Color(0.9f, 0.7f, 0.1f, 1f) : Color.white;
            if (GUILayout.Button("🪙 Currency Generator", GUILayout.Height(30))) _isFlask = false;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            if (_isFlask)
            {
                GUILayout.Label("Flask Settings", EditorStyles.boldLabel);
                _consumableName = EditorGUILayout.TextField("Flask Name", _consumableName);
                _flaskType = (FlaskType)EditorGUILayout.EnumPopup("Flask Type", _flaskType);
                _flaskRecovery = EditorGUILayout.FloatField("Recovery Amount", _flaskRecovery);
                _flaskDuration = EditorGUILayout.FloatField("Duration (sec)", _flaskDuration);
                _flaskMaxChg = EditorGUILayout.IntField("Max Charges", _flaskMaxChg);
                _flaskChgUse = EditorGUILayout.IntField("Charges Per Use", _flaskChgUse);
                _flaskSpeedMod = EditorGUILayout.FloatField("Speed Modifier", _flaskSpeedMod);
                _consumableDesc = EditorGUILayout.TextField("Description", _consumableDesc);

                EditorGUILayout.Space();
                GUI.backgroundColor = new Color(0.1f, 0.7f, 0.8f);
                if (GUILayout.Button("Create Flask", GUILayout.Height(35)))
                {
                    CreateFlask(_consumableName, _flaskType, _flaskRecovery, _flaskDuration, _flaskMaxChg, _flaskChgUse, _flaskSpeedMod, _consumableDesc);
                }
            }
            else
            {
                GUILayout.Label("Currency Settings", EditorStyles.boldLabel);
                _consumableName = EditorGUILayout.TextField("Currency Name", _consumableName);
                _currencyType = (CurrencyType)EditorGUILayout.EnumPopup("Currency Type", _currencyType);
                _consumableDesc = EditorGUILayout.TextField("Description", _consumableDesc);

                EditorGUILayout.Space();
                GUI.backgroundColor = new Color(0.9f, 0.7f, 0.1f);
                if (GUILayout.Button("Create Currency", GUILayout.Height(35)))
                {
                    CreateCurrency(_consumableName, _currencyType, _consumableDesc);
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Preset Consumables Generator", EditorStyles.boldLabel);
            GUI.backgroundColor = new Color(0.1f, 0.8f, 0.5f);
            if (GUILayout.Button("⚡ Generate Full Flasks & Currencies Presets", GUILayout.Height(40)))
            {
                CreateFlask("Life Flask Small", FlaskType.Life, 50f, 5f, 30, 10, 1f, "Recovers 50 Life.");
                CreateFlask("Quicksilver Flask", FlaskType.Utility, 0f, 4f, 60, 20, 1.4f, "Increases Movement Speed by 40%.");
                CreateCurrency("Chaos Orb", CurrencyType.ChaosOrb, "Reforges a rare item with new random properties.");
                CreateCurrency("Exalted Orb", CurrencyType.ExaltedOrb, "Augments a rare item with a new random property.");
                EditorUtility.DisplayDialog("Success", "Preset Consumables generated successfully!", "OK");
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
        }

        private void CreateFlask(string fName, FlaskType type, float rec, float dur, int maxC, int useC, float spd, string desc)
        {
            string targetFolder = EnsureDirectory(Path.Combine(RPG_BASE_PATH, "Flasks"));
            string fullPath = Path.Combine(targetFolder, $"Flask_{fName.Replace(" ", "_")}.asset").Replace("\\", "/");

            var asset = ScriptableObject.CreateInstance<FlaskItemData>();
            asset.ItemName = fName; asset.FlaskType = type; asset.RecoveryAmount = rec; asset.Duration = dur;
            asset.MaxCharges = maxC; asset.ChargesUsedPerUse = useC; asset.SpeedModifier = spd; asset.Description = desc;

            AssetDatabase.CreateAsset(asset, fullPath); AssetDatabase.SaveAssets(); Selection.activeObject = asset;
        }

        private void CreateCurrency(string cName, CurrencyType type, string desc)
        {
            string targetFolder = EnsureDirectory(Path.Combine(RPG_BASE_PATH, "Currencies"));
            string fullPath = Path.Combine(targetFolder, $"Currency_{cName.Replace(" ", "_")}.asset").Replace("\\", "/");

            var asset = ScriptableObject.CreateInstance<CurrencyItemData>();
            asset.ItemName = cName; asset.CurrencyType = type; asset.Description = desc;

            AssetDatabase.CreateAsset(asset, fullPath); AssetDatabase.SaveAssets(); Selection.activeObject = asset;
        }

        // ====================================================================
        // TAB 4: AFFIXES
        // ====================================================================
        private void DrawAffixesTab()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Affix Creation Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _affixName = EditorGUILayout.TextField("Affix Name", _affixName);
            _affixType = (AffixType)EditorGUILayout.EnumPopup("Affix Type", _affixType);
            _statType = (StatType)EditorGUILayout.EnumPopup("Stat Type", _statType);
            _affixMin = EditorGUILayout.FloatField("Min Value", _affixMin);
            _affixMax = EditorGUILayout.FloatField("Max Value", _affixMax);

            EditorGUILayout.Space();
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.9f);
            if (GUILayout.Button("Create Single Affix", GUILayout.Height(35)))
            {
                string targetFolder = EnsureDirectory(AFFIXES_PATH);
                string fullPath = Path.Combine(targetFolder, $"{_affixType}_{_affixName.Replace(" ", "_")}.asset").Replace("\\", "/");

                var asset = ScriptableObject.CreateInstance<AffixData>();
                asset.AffixName = _affixName; asset.TypeOfAffix = _affixType; asset.Stat = _statType; asset.MinValue = _affixMin; asset.MaxValue = _affixMax;

                AssetDatabase.CreateAsset(asset, fullPath); AssetDatabase.SaveAssets(); Selection.activeObject = asset;
                Debug.Log($"[Studio] Created Affix: {fullPath}");
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
        }

        // ====================================================================
        // TAB 5: BULK ADAPTERS & SYNC
        // ====================================================================
        private void DrawBulkAdaptersTab()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Devion Games Synchronization Engine", EditorStyles.boldLabel);
            GUILayout.Label("Instantly syncs, wraps, and auto-heals all items with Devion Inventory Adapters.", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
            if (GUILayout.Button("🛡️ Generate & Organize All Adapters by Folders", GUILayout.Height(40)))
            {
                GenerateAndOrganizeAdapters();
            }

            EditorGUILayout.Space();
            GUI.backgroundColor = new Color(1f, 0.5f, 0.1f);
            if (GUILayout.Button("✨ Bulk Sync & Auto-Heal Icons and Prices for All Assets", GUILayout.Height(40)))
            {
                BulkSyncAndHealAllAdapters();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
        }

        private void GenerateAndOrganizeAdapters()
        {
            EnsureDirectory(ADAPTERS_BASE_PATH);
            int count = 0;
            count += BulkCreateAdapters($"{RPG_BASE_PATH}/Gems", $"{ADAPTERS_BASE_PATH}/Gems");
            count += BulkCreateAdapters($"{RPG_BASE_PATH}/Flasks", $"{ADAPTERS_BASE_PATH}/Flasks");
            count += BulkCreateAdapters($"{RPG_BASE_PATH}/Currencies", $"{ADAPTERS_BASE_PATH}/Currencies");
            count += BulkCreateAdapters(WEAPONS_PATH, $"{ADAPTERS_BASE_PATH}/Weapons");
            count += BulkCreateAdapters(ARMORS_PATH, $"{ADAPTERS_BASE_PATH}/Armors");

            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", $"Successfully Created/Updated {count} Devion Adapters inside: {ADAPTERS_BASE_PATH}", "OK");
        }

        private int BulkCreateAdapters(string sourceFolder, string targetFolder)
        {
            if (!Directory.Exists(sourceFolder)) return 0;
            EnsureDirectory(targetFolder);

            string[] files = Directory.GetFiles(sourceFolder, "*.asset", SearchOption.AllDirectories);
            int count = 0;

            foreach (string file in files)
            {
                string cleanFile = file.Replace("\\", "/");
                var itemData = AssetDatabase.LoadAssetAtPath<ItemData>(cleanFile);
                if (itemData == null) continue;

                string adapterPath = $"{targetFolder}/{itemData.name}_Adapter.asset";
                bool isNew = false;
                ScriptableObject adapter;

                if (itemData is EquippableData || itemData is ArmorItemData)
                {
                    var existing = AssetDatabase.LoadAssetAtPath<CurveDashEquipmentAdapter>(adapterPath);
                    if (existing != null) { adapter = existing; existing.OriginalEquipmentData = itemData; existing.SyncData(); }
                    else { var newA = ScriptableObject.CreateInstance<CurveDashEquipmentAdapter>(); newA.OriginalEquipmentData = itemData; newA.SyncData(); adapter = newA; isNew = true; }
                }
                else
                {
                    var existing = AssetDatabase.LoadAssetAtPath<CurveDashItemAdapter>(adapterPath);
                    if (existing != null) { adapter = existing; existing.OriginalItemData = itemData; existing.SyncData(); }
                    else { var newA = ScriptableObject.CreateInstance<CurveDashItemAdapter>(); newA.OriginalItemData = itemData; newA.SyncData(); adapter = newA; isNew = true; }
                }

                if (isNew) AssetDatabase.CreateAsset(adapter, adapterPath);
                else EditorUtility.SetDirty(adapter);
                count++;
            }
            return count;
        }

        private void BulkSyncAndHealAllAdapters()
        {
            string[] aGuids = AssetDatabase.FindAssets("t:CurveDashItemAdapter");
            int c1 = 0;
            foreach (string g in aGuids) { var a = AssetDatabase.LoadAssetAtPath<CurveDashItemAdapter>(AssetDatabase.GUIDToAssetPath(g)); if (a != null) { a.SyncData(); EditorUtility.SetDirty(a); c1++; } }

            string[] eGuids = AssetDatabase.FindAssets("t:CurveDashEquipmentAdapter");
            int c2 = 0;
            foreach (string g in eGuids) { var a = AssetDatabase.LoadAssetAtPath<CurveDashEquipmentAdapter>(AssetDatabase.GUIDToAssetPath(g)); if (a != null) { a.SyncData(); EditorUtility.SetDirty(a); c2++; } }

            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", $"Successfully Auto-Healed {c1 + c2} Assets!", "OK");
        }

        private static string EnsureDirectory(string path)
        {
            if (!Directory.Exists(path)) { Directory.CreateDirectory(path); AssetDatabase.ImportAsset(path); }
            return path;
        }
    }
}
