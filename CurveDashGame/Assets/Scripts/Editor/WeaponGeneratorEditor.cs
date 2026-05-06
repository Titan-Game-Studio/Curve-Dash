using UnityEditor;
using UnityEngine;
using System.IO;

namespace STG.CurveDash.Editor
{
    public class WeaponGeneratorEditor : EditorWindow
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

        private string _weaponName = "Exquisite Foil";
        private WeaponGenCategory _category = WeaponGenCategory.OneHandedSword;
        private ItemRarity _rarity = ItemRarity.Normal;

        [Header("Weapon Stats")]
        private float _minDamage = 15f;
        private float _maxDamage = 22f;
        private float _attackSpeed = 1.4f;
        private float _attackRange = 1.6f;

        [Header("Off-Hand Stats (Shield/Arrow)")]
        private float _blockChance = 25f;
        private float _bonusDamage = 0f;

        private string _description = "A swift and elegant weapon.";

        private const string SavePath = "Assets/Data/DTOS/Weapons/";

        [MenuItem("Curve Dash/Tools/Weapon Generator")]
        public static void ShowWindow()
        {
            GetWindow<WeaponGeneratorEditor>("Weapon Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("PoE Weapon Creation Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _weaponName = EditorGUILayout.TextField("Weapon Name", _weaponName);
            
            EditorGUI.BeginChangeCheck();
            _category = (WeaponGenCategory)EditorGUILayout.EnumPopup("Weapon Category", _category);
            if (EditorGUI.EndChangeCheck())
            {
                // Auto-fill sensible default stats on category change
                ApplySensibleDefaults();
            }

            _rarity = (ItemRarity)EditorGUILayout.EnumPopup("Rarity", _rarity);

            EditorGUILayout.Space();
            if (_category == WeaponGenCategory.Shield || _category == WeaponGenCategory.Arrow)
            {
                GUILayout.Label("Off-Hand Stats Settings", EditorStyles.boldLabel);
                _blockChance = EditorGUILayout.FloatField("Block Chance", _blockChance);
                _bonusDamage = EditorGUILayout.FloatField("Bonus Damage", _bonusDamage);
            }
            else
            {
                GUILayout.Label("Weapon Stats Settings", EditorStyles.boldLabel);
                _minDamage = EditorGUILayout.FloatField("Min Damage", _minDamage);
                _maxDamage = EditorGUILayout.FloatField("Max Damage", _maxDamage);
                _attackSpeed = EditorGUILayout.FloatField("Attack Speed", _attackSpeed);
                _attackRange = EditorGUILayout.FloatField("Attack Range", _attackRange);
            }

            EditorGUILayout.Space();
            _description = EditorGUILayout.TextField("Description", _description);

            EditorGUILayout.Space();
            if (GUILayout.Button("Create Single Weapon", GUILayout.Height(30)))
            {
                CreateWeapon(_weaponName, _category, _rarity, _minDamage, _maxDamage, _attackSpeed, _attackRange, _blockChance, _bonusDamage, _description);
            }

            EditorGUILayout.Space();
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Generate Full PoE Weapon Database", GUILayout.Height(40)))
            {
                GeneratePoeWeaponDatabase();
            }
            GUI.backgroundColor = Color.white;
        }

        private void ApplySensibleDefaults()
        {
            switch (_category)
            {
                case WeaponGenCategory.OneHandedSword:
                    _minDamage = 14f; _maxDamage = 20f; _attackSpeed = 1.4f; _attackRange = 1.6f;
                    _description = "A fast and precise blade.";
                    break;
                case WeaponGenCategory.GreatSword:
                    _minDamage = 30f; _maxDamage = 42f; _attackSpeed = 0.85f; _attackRange = 2.2f;
                    _description = "A heavy, devastating two-handed sword.";
                    break;
                case WeaponGenCategory.Hammer:
                    _minDamage = 40f; _maxDamage = 58f; _attackSpeed = 0.65f; _attackRange = 2f;
                    _description = "A massive crushing war hammer.";
                    break;
                case WeaponGenCategory.GreatBow:
                    _minDamage = 16f; _maxDamage = 24f; _attackSpeed = 1.1f; _attackRange = 4.5f;
                    _description = "A powerful long bow that shoots from afar.";
                    break;
                case WeaponGenCategory.Shield:
                    _blockChance = 35f; _bonusDamage = 2f;
                    _description = "A solid defensive shield.";
                    break;
                case WeaponGenCategory.Arrow:
                    _blockChance = 0f; _bonusDamage = 6f;
                    _description = "Supplements range damage.";
                    break;
            }
        }

        private void CreateWeapon(string wName, WeaponGenCategory cat, ItemRarity rarity, float minDmg, float maxDmg, float speed, float range, float block, float bonus, string desc)
        {
            string folderName = "";
            switch (cat)
            {
                case WeaponGenCategory.OneHandedSword: folderName = "ONE-HANDED SWORDS"; break;
                case WeaponGenCategory.GreatSword: folderName = "GREAT SWORDS"; break;
                case WeaponGenCategory.Hammer: folderName = "HAMMERS"; break;
                case WeaponGenCategory.GreatBow: folderName = "GREAT BOWS"; break;
                case WeaponGenCategory.Shield: folderName = "SHIELDS"; break;
                case WeaponGenCategory.Arrow: folderName = "ARROWS"; break;
            }

            string targetFolder = Path.Combine(SavePath, folderName);
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
                AssetDatabase.Refresh();
            }

            string safeName = wName.Replace(" ", "_").Replace("'", "");
            string fileName = $"{rarity}_{safeName}.asset";
            string fullPath = Path.Combine(targetFolder, fileName).Replace("\\", "/");

            ScriptableObject assetInstance = null;

            if (cat == WeaponGenCategory.OneHandedSword)
            {
                OneHandedWeaponData w = ScriptableObject.CreateInstance<OneHandedWeaponData>();
                w.BaseMinDamage = minDmg; w.BaseMaxDamage = maxDmg; w.BaseAttackSpeed = speed; w.BaseAttackRange = range;
                assetInstance = w;
            }
            else if (cat == WeaponGenCategory.GreatSword || cat == WeaponGenCategory.Hammer)
            {
                TwoHandedWeaponData w = ScriptableObject.CreateInstance<TwoHandedWeaponData>();
                w.BaseMinDamage = minDmg; w.BaseMaxDamage = maxDmg; w.BaseAttackSpeed = speed; w.BaseAttackRange = range;
                assetInstance = w;
            }
            else if (cat == WeaponGenCategory.GreatBow)
            {
                BowData w = ScriptableObject.CreateInstance<BowData>();
                w.BaseMinDamage = minDmg; w.BaseMaxDamage = maxDmg; w.BaseAttackSpeed = speed; w.BaseAttackRange = range;
                assetInstance = w;
            }
            else if (cat == WeaponGenCategory.Shield)
            {
                OffHandData o = ScriptableObject.CreateInstance<OffHandData>();
                o.SubType = OffHandType.Shield; o.BlockChance = block; o.BonusDamage = bonus;
                assetInstance = o;
            }
            else if (cat == WeaponGenCategory.Arrow)
            {
                OffHandData o = ScriptableObject.CreateInstance<OffHandData>();
                o.SubType = OffHandType.Arrow; o.BlockChance = 0; o.BonusDamage = bonus;
                assetInstance = o;
            }

            if (assetInstance != null)
            {
                ItemData item = (ItemData)assetInstance;
                item.ItemName = wName;
                item.Rarity = rarity;
                item.Description = desc;
                if (cat == WeaponGenCategory.Shield)
                {
                    item.Type = ItemType.Armor;
                }
                else
                {
                    item.Type = ItemType.Weapon;
                }

                AssetDatabase.CreateAsset(assetInstance, fullPath);
                AssetDatabase.SaveAssets();

                Debug.Log($"[WeaponGenerator] Created: {fullPath}");
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = assetInstance;
            }
        }

        private void GeneratePoeWeaponDatabase()
        {
            // ONE-HANDED SWORDS
            CreateWeapon("Rusted Sword", WeaponGenCategory.OneHandedSword, ItemRarity.Normal, 8, 12, 1.3f, 1.5f, 0, 0, "A simple rusted sword.");
            CreateWeapon("Glinting Sword", WeaponGenCategory.OneHandedSword, ItemRarity.Magic, 14, 20, 1.4f, 1.6f, 0, 0, "Glints with clean steel edge.");
            CreateWeapon("Scaeva", WeaponGenCategory.OneHandedSword, ItemRarity.Rare, 22, 30, 1.5f, 1.7f, 0, 0, "A high quality golden gladius.");
            CreateWeapon("Paradoxica", WeaponGenCategory.OneHandedSword, ItemRarity.Unique, 32, 45, 1.65f, 1.8f, 0, 0, "Deals double damage on critical strike.");

            // GREAT SWORDS
            CreateWeapon("Corroded Blade", WeaponGenCategory.GreatSword, ItemRarity.Normal, 18, 26, 0.8f, 2f, 0, 0, "Heavy two-handed rusted blade.");
            CreateWeapon("Heaver Blade", WeaponGenCategory.GreatSword, ItemRarity.Magic, 30, 42, 0.85f, 2.2f, 0, 0, "Magically light but hits hard.");
            CreateWeapon("Reaver Sword", WeaponGenCategory.GreatSword, ItemRarity.Rare, 48, 65, 0.9f, 2.4f, 0, 0, "An elegant executioner greatsword.");
            CreateWeapon("Starforge", WeaponGenCategory.GreatSword, ItemRarity.Unique, 72, 100, 0.95f, 2.6f, 0, 0, "Grants massive physical power and high shock capacity.");

            // HAMMERS
            CreateWeapon("Driftwood Club", WeaponGenCategory.Hammer, ItemRarity.Normal, 24, 36, 0.6f, 1.8f, 0, 0, "A heavy sea-soaked wood club.");
            CreateWeapon("Heavy Club", WeaponGenCategory.Hammer, ItemRarity.Magic, 40, 58, 0.65f, 2f, 0, 0, "Reinforced with magic iron bands.");
            CreateWeapon("Spiked Club", WeaponGenCategory.Hammer, ItemRarity.Rare, 64, 90, 0.7f, 2.2f, 0, 0, "Deadly spiked war maul.");
            CreateWeapon("Marohi Erqi", WeaponGenCategory.Hammer, ItemRarity.Unique, 96, 135, 0.75f, 2.4f, 0, 0, "Extremely slow but creates ground ripples.");

            // GREAT BOWS
            CreateWeapon("Crude Bow", WeaponGenCategory.GreatBow, ItemRarity.Normal, 10, 15, 1f, 4f, 0, 0, "Flimsy basic hunting bow.");
            CreateWeapon("Short Bow", WeaponGenCategory.GreatBow, ItemRarity.Magic, 16, 24, 1.1f, 4.5f, 0, 0, "Compact and quick to draw.");
            CreateWeapon("Long Bow", WeaponGenCategory.GreatBow, ItemRarity.Rare, 26, 38, 1.2f, 5f, 0, 0, "High caliber tactical bow.");
            CreateWeapon("Reach of the Council", WeaponGenCategory.GreatBow, ItemRarity.Unique, 40, 58, 1.3f, 5.5f, 0, 0, "Fires additional arrows simultaneously.");

            // SHIELDS
            CreateWeapon("Splintered Shield", WeaponGenCategory.Shield, ItemRarity.Normal, 0, 0, 0, 0, 25f, 0f, "Barely holds together.");
            CreateWeapon("Rusted Round Shield", WeaponGenCategory.Shield, ItemRarity.Magic, 0, 0, 0, 0, 35f, 2f, "Solid round buckler.");
            CreateWeapon("Bronze Shield", WeaponGenCategory.Shield, ItemRarity.Rare, 0, 0, 0, 0, 45f, 5f, "Reflective protective guard.");
            CreateWeapon("Aegis Aurora", WeaponGenCategory.Shield, ItemRarity.Unique, 0, 0, 0, 0, 55f, 10f, "Restores energy shield on block.");

            // ARROWS
            CreateWeapon("Serrated Arrow", WeaponGenCategory.Arrow, ItemRarity.Normal, 0, 0, 0, 0, 0f, 3f, "Basic pointed arrows.");
            CreateWeapon("Flight Arrow", WeaponGenCategory.Arrow, ItemRarity.Magic, 0, 0, 0, 0, 0f, 6f, "Enchanted with high speed flight runes.");
            CreateWeapon("Barbed Arrow", WeaponGenCategory.Arrow, ItemRarity.Rare, 0, 0, 0, 0, 0f, 10f, "Causes bleed and heavy damage over time.");
            CreateWeapon("Voidfletcher", WeaponGenCategory.Arrow, ItemRarity.Unique, 0, 0, 0, 0, 0f, 15f, "Fires explosive void arrows.");

            AssetDatabase.Refresh();
            Debug.Log("[WeaponGenerator] Full PoE Weapon Database generated successfully under Assets/Data/DTOS/Weapons!");
        }
    }
}
