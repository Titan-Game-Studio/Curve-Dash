using System.Collections.Generic;
using System.IO;
using STG.CurveDash;
using UnityEditor;
using UnityEngine;

namespace STG.CurveDash.Editor
{
    public class CurveDashRPGGeneratorWindow : EditorWindow
    {
        private const string RPG_BASE_PATH = "Assets/Data/DTOS/RPGItems";
        private const string ADAPTERS_BASE_PATH = "Assets/Data/DTOS/Adapters";

        [MenuItem("Curve-Dash/Tools/RPG Item & Adapter Generator", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<CurveDashRPGGeneratorWindow>("RPG Generator");
            window.minSize = new Vector2(450, 400);
            window.Show();
        }

        private void OnGUI()
        {
            GUI.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 1f);
            EditorGUILayout.BeginVertical("box");

            // Header

            var titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 18;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.normal.textColor = Color.cyan;
            EditorGUILayout.LabelField("Curve-Dash RPG Content & Adapter Generator", titleStyle, GUILayout.Height(30));
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox("This tool automatically generates PoE-inspired Gems, Flasks, and Currencies under clean directory structures, and provides one-click bulk Adapter generation for all items (including Weapons and Armors) organized by folders.", MessageType.Info);
            EditorGUILayout.Space();

            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();

            // Part 1: Item Generation
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("1. Generate RPG Base Items", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Target Path: {RPG_BASE_PATH}", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            GUI.backgroundColor = new Color(0.12f, 0.73f, 0.53f, 1f);

            if (GUILayout.Button("💎 Generate All RPG Items (Gems, Flasks, Currencies)", GUILayout.Height(35)))
            {
                GenerateRPGItems();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // Part 2: Adapter Generation
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("2. Generate & Organize Inventory Adapters", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Target Path: {ADAPTERS_BASE_PATH}", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            GUI.backgroundColor = new Color(0.24f, 0.44f, 0.94f, 1f);

            if (GUILayout.Button("🛡️ Generate & Organize All Adapters by Folders", GUILayout.Height(35)))
            {
                GenerateAndOrganizeAdapters();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // Part 3: Auto-Heal & Price Generator
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("3. Auto-Heal Icons & Pricing", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Bulk assign default icons and auto-calculate buy/sell prices for all existing assets.", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            GUI.backgroundColor = new Color(0.9f, 0.47f, 0.13f, 1f);

            if (GUILayout.Button("✨ Bulk Sync & Auto-Heal Icons and Prices for All Assets", GUILayout.Height(35)))
            {
                BulkSyncAndHealAllAdapters();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("PoE Features Summary:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("- Gems: Automatically scans pre-existing AbilityData (Spells, Melee, Ranged, Auras, Supports) and wraps them into Gems.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("- Flasks: Small Life/Mana Flasks, Quicksilver (+Speed), Granite (+Armor), and Diamond (+Crit) Flasks.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("- Currencies: Scroll of Wisdom, Chaos, Exalted, Transmutation, Alteration, Blacksmith, and Armourer orbs.", EditorStyles.wordWrappedLabel);
        }

        private void BulkSyncAndHealAllAdapters()
        {
            string[] adapterGuids = AssetDatabase.FindAssets("t:CurveDashItemAdapter");
            int itemAdapterCount = 0;
            foreach (string guid in adapterGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var adapter = AssetDatabase.LoadAssetAtPath<CurveDashItemAdapter>(path);
                if (adapter != null)
                {
                    adapter.SyncData();
                    EditorUtility.SetDirty(adapter);
                    itemAdapterCount++;
                }
            }

            string[] equipGuids = AssetDatabase.FindAssets("t:CurveDashEquipmentAdapter");
            int equipAdapterCount = 0;
            foreach (string guid in equipGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var adapter = AssetDatabase.LoadAssetAtPath<CurveDashEquipmentAdapter>(path);
                if (adapter != null)
                {
                    adapter.SyncData();
                    EditorUtility.SetDirty(adapter);
                    equipAdapterCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Success", $"Successfully Auto-Healed and Synchronized {itemAdapterCount + equipAdapterCount} Assets!\n\n- Updated {itemAdapterCount} Item Adapters\n- Updated {equipAdapterCount} Equipment Adapters\n\nAll missing icons have been auto-assigned from 'Assets/Textures/Icons' and realistic buy/sell prices have been generated based on item rarity & stats.", "OK");
        }

        private void GenerateRPGItems()
        {
            EnsureDirectory(RPG_BASE_PATH);
            EnsureDirectory($"{RPG_BASE_PATH}/Gems");
            EnsureDirectory($"{RPG_BASE_PATH}/Flasks");
            EnsureDirectory($"{RPG_BASE_PATH}/Currencies");

            int currencyCount = CreateCurrencies();
            int flaskCount = CreateFlasks();
            int gemCount = CreateGemsFromAbilities();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Success", $"RPG Items Generated Successfully!\n\n- Currencies created: {currencyCount}\n- Flasks created: {flaskCount}\n- Gems wrapped from abilities: {gemCount}\n\nAll items are saved in: {RPG_BASE_PATH}", "OK");
        }

        private int CreateCurrencies()
        {
            var currencies = new List<(string name, CurrencyType type, string desc)>
            {
                ("Scroll_Wisdom", CurrencyType.ScrollOfWisdom, "Identifies an item's hidden properties."),
                ("Orb_Transmutation", CurrencyType.OrbOfTransmutation, "Upgrades a normal item to a magic item."),
                ("Orb_Alteration", CurrencyType.OrbOfAlteration, "Reforges a magic item with new random properties."),
                ("Chaos_Orb", CurrencyType.ChaosOrb, "Reforges a rare item with new random properties."),
                ("Exalted_Orb", CurrencyType.ExaltedOrb, "Augments a rare item with a new random property."),
                ("Blacksmith_Whetstone", CurrencyType.BlacksmithWhetstone, "Improves the quality of a weapon."),
                ("Armourer_Scrap", CurrencyType.ArmourerScrap, "Improves the quality of an armor piece.")
            };

            int created = 0;
            foreach (var c in currencies)
            {
                string path = $"{RPG_BASE_PATH}/Currencies/Currency_{c.name}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<CurrencyItemData>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<CurrencyItemData>();
                    AssetDatabase.CreateAsset(asset, path);
                    created++;
                }
                asset.ItemName = c.name.Replace("_", " ");
                asset.CurrencyType = c.type;
                asset.Description = c.desc;
                asset.Rarity = ItemRarity.Normal;
                EditorUtility.SetDirty(asset);
            }
            return created;
        }

        private int CreateFlasks()
        {
            var flasks = new List<(string name, FlaskType type, float recovery, float dur, int maxChg, int chgUse, float speed, string desc)>
            {
                ("Life_Flask_Small", FlaskType.Life, 50f, 5.0f, 30, 10, 1.0f, "Recovers 50 Life over 5 seconds."),
                ("Mana_Flask_Small", FlaskType.Mana, 35f, 4.0f, 30, 10, 1.0f, "Recovers 35 Mana over 4 seconds."),
                ("Quicksilver_Flask", FlaskType.Utility, 0f, 4.0f, 60, 20, 1.4f, "Increases Movement Speed by 40% during effect."),
                ("Granite_Flask", FlaskType.Utility, 0f, 4.0f, 60, 30, 1.0f, "Grants +1000 Armor during effect."),
                ("Diamond_Flask", FlaskType.Utility, 0f, 4.0f, 40, 20, 1.0f, "Your Critical Strike Chance is Lucky during effect.")
            };

            int created = 0;
            foreach (var f in flasks)
            {
                string path = $"{RPG_BASE_PATH}/Flasks/Flask_{f.name}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<FlaskItemData>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<FlaskItemData>();
                    AssetDatabase.CreateAsset(asset, path);
                    created++;
                }
                asset.ItemName = f.name.Replace("_", " ");
                asset.FlaskType = f.type;
                asset.RecoveryAmount = f.recovery;
                asset.Duration = f.dur;
                asset.MaxCharges = f.maxChg;
                asset.ChargesUsedPerUse = f.chgUse;
                asset.SpeedModifier = f.speed;
                asset.Description = f.desc;
                asset.Rarity = ItemRarity.Normal;
                EditorUtility.SetDirty(asset);
            }
            return created;
        }

        private int CreateGemsFromAbilities()
        {
            // Crawl existing abilities from "Assets/Data/DTOS/Abilities"
            string[] guids = AssetDatabase.FindAssets("t:AbilityData", new string[] { "Assets/Data/DTOS/Abilities" });
            int created = 0;

            foreach (string guid in guids)
            {
                string abilityPath = AssetDatabase.GUIDToAssetPath(guid);
                var ability = AssetDatabase.LoadAssetAtPath<AbilityData>(abilityPath);
                if (ability == null) continue;

                // Determine name and color based on folder structure
                string gemName = ability.name + "_Gem";
                string gemPath = $"{RPG_BASE_PATH}/Gems/{gemName}.asset";

                var gemAsset = AssetDatabase.LoadAssetAtPath<GemItemData>(gemPath);
                if (gemAsset == null)
                {
                    gemAsset = ScriptableObject.CreateInstance<GemItemData>();
                    AssetDatabase.CreateAsset(gemAsset, gemPath);
                    created++;
                }

                gemAsset.ItemName = ability.AbilityName != null ? ability.AbilityName + " Gem" : ability.name.Replace("_", " ") + " Gem";
                gemAsset.EmbeddedAbility = ability;
                gemAsset.Rarity = ItemRarity.Normal;

                // Set Gem Properties depending on path and filename
                if (ability.name.Contains("Support_") || abilityPath.Contains("/SUPPORTS/"))
                {
                    gemAsset.GemType = GemType.Support;
                    gemAsset.SocketColor = Color.blue; // Supports default to Intellect Blue
                    gemAsset.Description = $"Supports socketed skills. Linked Ability: {ability.name}";
                }
                else
                {
                    gemAsset.GemType = GemType.Skill;
                    gemAsset.Description = $"Grants active ability '{gemAsset.ItemName}' when socketed. Cooldown: {ability.Cooldown}s.";

                    if (abilityPath.Contains("/SPELLS/"))
                    {
                        gemAsset.SocketColor = Color.blue; // Intelligence
                    }
                    else if (abilityPath.Contains("/MELEE/") || abilityPath.Contains("/AURAS/"))
                    {
                        gemAsset.SocketColor = Color.red; // Strength
                    }
                    else if (abilityPath.Contains("/RANGED/"))
                    {
                        gemAsset.SocketColor = Color.green; // Dexterity
                    }
                    else
                    {
                        gemAsset.SocketColor = Color.green; // Default green
                    }
                }

                EditorUtility.SetDirty(gemAsset);
            }

            return created;
        }

        private void GenerateAndOrganizeAdapters()
        {
            EnsureDirectory(ADAPTERS_BASE_PATH);
            EnsureDirectory($"{ADAPTERS_BASE_PATH}/Gems");
            EnsureDirectory($"{ADAPTERS_BASE_PATH}/Flasks");
            EnsureDirectory($"{ADAPTERS_BASE_PATH}/Currencies");
            EnsureDirectory($"{ADAPTERS_BASE_PATH}/Weapons");
            EnsureDirectory($"{ADAPTERS_BASE_PATH}/Armors");

            int count = 0;

            // 1. Adapting Gems
            count += BulkCreateAdapters($"{RPG_BASE_PATH}/Gems", $"{ADAPTERS_BASE_PATH}/Gems");

            // 2. Adapting Flasks
            count += BulkCreateAdapters($"{RPG_BASE_PATH}/Flasks", $"{ADAPTERS_BASE_PATH}/Flasks");

            // 3. Adapting Currencies
            count += BulkCreateAdapters($"{RPG_BASE_PATH}/Currencies", $"{ADAPTERS_BASE_PATH}/Currencies");

            // 4. Adapting pre-existing Weapons
            count += BulkCreateAdapters("Assets/Data/DTOS/Weapons", $"{ADAPTERS_BASE_PATH}/Weapons");

            // 5. Adapting pre-existing Armors
            count += BulkCreateAdapters("Assets/Data/DTOS/Armors", $"{ADAPTERS_BASE_PATH}/Armors");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Success", $"Adapters Generated & Organized successfully!\n\nCreated/Updated {count} Adapters inside folder: {ADAPTERS_BASE_PATH}", "OK");
        }

        private int BulkCreateAdapters(string sourceFolder, string targetFolder)
        {
            if (!Directory.Exists(sourceFolder)) return 0;

            string[] assetFiles = Directory.GetFiles(sourceFolder, "*.asset", SearchOption.AllDirectories);
            int count = 0;

            foreach (string file in assetFiles)
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
                    if (existing != null)
                    {
                        adapter = existing;
                        existing.OriginalEquipmentData = itemData;
                        existing.SyncData();
                    }
                    else
                    {
                        var newAdapter = ScriptableObject.CreateInstance<CurveDashEquipmentAdapter>();
                        newAdapter.OriginalEquipmentData = itemData;
                        newAdapter.SyncData();
                        adapter = newAdapter;
                        isNew = true;
                    }
                }
                else
                {
                    var existing = AssetDatabase.LoadAssetAtPath<CurveDashItemAdapter>(adapterPath);
                    if (existing != null)
                    {
                        adapter = existing;
                        existing.OriginalItemData = itemData;
                        existing.SyncData();
                    }
                    else
                    {
                        var newAdapter = ScriptableObject.CreateInstance<CurveDashItemAdapter>();
                        newAdapter.OriginalItemData = itemData;
                        newAdapter.SyncData();
                        adapter = newAdapter;
                        isNew = true;
                    }
                }

                if (isNew)
                {
                    AssetDatabase.CreateAsset(adapter, adapterPath);
                }
                else
                {
                    EditorUtility.SetDirty(adapter);
                }
                count++;
            }

            return count;
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.ImportAsset(path);
            }
        }
    }
}

