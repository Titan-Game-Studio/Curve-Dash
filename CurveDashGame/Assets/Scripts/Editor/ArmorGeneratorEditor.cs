using UnityEditor;
using UnityEngine;
using System.IO;

namespace STG.CurveDash.Editor
{
    public class ArmorGeneratorEditor : EditorWindow
    {
        private string _armorName = "Iron Hat";
        private EquipmentSlot _slot = EquipmentSlot.Head;
        private ItemRarity _rarity = ItemRarity.Normal;
        private int _defense = 15;
        private int _healthBonus = 10;
        private int _modularPartIndex = 0;
        private string _description = "A basic protection piece.";

        private const string SavePath = "Assets/Data/DTOS/Armors/";

        [MenuItem("Curve-Dash/Tools/Armor Generator")]
        public static void ShowWindow()
        {
            GetWindow<ArmorGeneratorEditor>("Armor Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("PoE Armor Creation Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _armorName = EditorGUILayout.TextField("Armor Name", _armorName);
            _slot = (EquipmentSlot)EditorGUILayout.EnumPopup("Equipment Slot", _slot);
            _rarity = (ItemRarity)EditorGUILayout.EnumPopup("Rarity", _rarity);
            
            EditorGUILayout.Space();
            GUILayout.Label("Stats Settings", EditorStyles.boldLabel);
            _defense = EditorGUILayout.IntField("Defense Value", _defense);
            _healthBonus = EditorGUILayout.IntField("Health Bonus", _healthBonus);
            _modularPartIndex = EditorGUILayout.IntField("Modular Part Index", _modularPartIndex);
            
            EditorGUILayout.Space();
            _description = EditorGUILayout.TextField("Description", _description);

            EditorGUILayout.Space();
            if (GUILayout.Button("Create Single Armor", GUILayout.Height(30)))
            {
                CreateArmor(_armorName, _slot, _rarity, _defense, _healthBonus, _modularPartIndex, _description);
            }

            EditorGUILayout.Space();
            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button("Generate PoE Armor Database", GUILayout.Height(40)))
            {
                GeneratePoeArmors();
            }
            GUI.backgroundColor = Color.white;
        }

        private void CreateArmor(string aName, EquipmentSlot slot, ItemRarity rarity, int defense, int hp, int modIdx, string desc)
        {
            string slotDirName = slot.ToString().ToUpper();
            // Map common equipment slot names to standard capitalized folder names
            if (slot == EquipmentSlot.Head) slotDirName = "HEAD";
            else if (slot == EquipmentSlot.Body) slotDirName = "BODY";
            else if (slot == EquipmentSlot.Hands) slotDirName = "HANDS";
            else if (slot == EquipmentSlot.Feet) slotDirName = "FEET";

            string targetFolder = Path.Combine(SavePath, slotDirName);

            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
                AssetDatabase.Refresh();
            }

            ArmorItemData asset = ScriptableObject.CreateInstance<ArmorItemData>();
            asset.ItemName = aName;
            asset.Slot = slot;
            asset.Rarity = rarity;
            asset.Defense = defense;
            asset.HealthBonus = hp;
            asset.ModularPartIndex = modIdx;
            asset.Description = desc;

            string safeName = aName.Replace(" ", "_").Replace("'", "");
            string fileName = $"{rarity}_{safeName}.asset";
            string fullPath = Path.Combine(targetFolder, fileName).Replace("\\", "/");

            AssetDatabase.CreateAsset(asset, fullPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[ArmorGenerator] Created: {fullPath}");
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }

        private void GeneratePoeArmors()
        {
            // HELMETS (Head)
            CreateArmor("Iron Helmet", EquipmentSlot.Head, ItemRarity.Normal, 15, 10, 0, "A heavy iron bucket for basic skull protection.");
            CreateArmor("Enchanted Circlet", EquipmentSlot.Head, ItemRarity.Magic, 35, 25, 1, "Radiating soft blue mana glow.");
            CreateArmor("Hubris Circlet", EquipmentSlot.Head, ItemRarity.Rare, 75, 55, 2, "An ornate crown of pure mystical energy.");
            CreateArmor("Abyssus", EquipmentSlot.Head, ItemRarity.Unique, 180, 0, 3, "Adds massive physical damage but increases damage taken.");
            CreateArmor("Goldrim", EquipmentSlot.Head, ItemRarity.Unique, 50, 40, 4, "Provides excellent elemental resistances for early levels.");

            // BODY ARMORS (Body)
            CreateArmor("Plate Vest", EquipmentSlot.Body, ItemRarity.Normal, 35, 25, 0, "A simple padded leather chest protector.");
            CreateArmor("Sharkskin Tunic", EquipmentSlot.Body, ItemRarity.Magic, 80, 60, 1, "Lightweight, flexible tunic with subtle magical weaving.");
            CreateArmor("Astral Plate", EquipmentSlot.Body, ItemRarity.Rare, 180, 120, 2, "Heavy steel armor carrying stellar blessings.");
            CreateArmor("Kaoms Heart", EquipmentSlot.Body, ItemRarity.Unique, 0, 500, 3, "Has no sockets, but grants legendary life pool (+500 HP).");
            CreateArmor("Shavronnes Wrappings", EquipmentSlot.Body, ItemRarity.Unique, 250, 80, 4, "Chaos damage does not bypass Energy Shield.");

            // GLOVES (Hands)
            CreateArmor("Iron Gauntlets", EquipmentSlot.Hands, ItemRarity.Normal, 10, 5, 0, "Rough iron mitts.");
            CreateArmor("Stealth Gloves", EquipmentSlot.Hands, ItemRarity.Magic, 22, 15, 1, "Magic gloves that blend with shadows.");
            CreateArmor("Slink Gloves", EquipmentSlot.Hands, ItemRarity.Rare, 45, 30, 2, "Superbly balanced leather gloves used by masters.");
            CreateArmor("Facebreaker", EquipmentSlot.Hands, ItemRarity.Unique, 20, 10, 3, "Grants massive unarmed damage bonuses.");
            CreateArmor("Sadimas Touch", EquipmentSlot.Hands, ItemRarity.Unique, 15, 20, 4, "Highly sought gloves that increase item quantity found.");

            // BOOTS (Feet)
            CreateArmor("Iron Greaves", EquipmentSlot.Feet, ItemRarity.Normal, 10, 5, 0, "Basic metal boots.");
            CreateArmor("Slink Boots", EquipmentSlot.Feet, ItemRarity.Magic, 22, 15, 1, "Light and flexible magical running boots.");
            CreateArmor("Titan Greaves", EquipmentSlot.Feet, ItemRarity.Rare, 45, 30, 2, "Unshakable metal sabatons.");
            CreateArmor("Kaoms Roots", EquipmentSlot.Feet, ItemRarity.Unique, 100, 150, 3, "Prevents action speed from being slowed below base value.");
            CreateArmor("Skyforth", EquipmentSlot.Feet, ItemRarity.Unique, 50, 40, 4, "Increases mana pool and prevents stun based on mana.");

            AssetDatabase.Refresh();
            Debug.Log("[ArmorGenerator] PoE Armor Database generated successfully under Assets/Data/DTOS/Armors!");
        }
    }
}

