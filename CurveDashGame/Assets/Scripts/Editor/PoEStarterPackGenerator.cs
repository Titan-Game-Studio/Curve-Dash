using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DevionGames.InventorySystem;

namespace STG.CurveDash.Editor
{
    public static class PoEStarterPackGenerator
    {
        private const string ActiveDatabasePath = "Assets/Data/DTOS/CurveDash_Active_Abilities.asset";
        private const string PassiveDatabasePath = "Assets/Data/DTOS/CurveDash_Passive_Constellations.asset";
        private const string IconsFolder = "Assets/Textures/Icons";

        private struct PoEStarterItem
        {
            public string Name;
            public string Description;
            public string CategoryName;
            public string RarityName;
            public float Cooldown;
            public float SuccessChance;
            public string IconName;
        }

        [MenuItem("Curve-Dash/Database/Generate PoE Starter Pack", false, 10)]
        public static void GeneratePoEStarterPack()
        {
            var activeDb = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ActiveDatabasePath);
            var passiveDb = AssetDatabase.LoadAssetAtPath<ItemDatabase>(PassiveDatabasePath);

            if (activeDb == null)
            {
                EditorUtility.DisplayDialog("Error", $"Could not find Active Database at path: {ActiveDatabasePath}\nPlease create or locate it first.", "OK");
                return;
            }

            if (passiveDb == null)
            {
                EditorUtility.DisplayDialog("Error", $"Could not find Passive Database at path: {PassiveDatabasePath}\nPlease create or locate it first.", "OK");
                return;
            }

            int activeAdded = 0;
            int passiveAdded = 0;

            // 1. Generate PoE Starter Spells/Abilities (Active)
            var activeStarters = new List<PoEStarterItem>()
            {
                new PoEStarterItem()
                {
                    Name = "Fireball",
                    Description = "Unleashes a fiery projectile that explodes in an area on impact, dealing fire spell damage to nearby enemies.",
                    CategoryName = "Spells",
                    RarityName = "Magic",
                    Cooldown = 1.5f,
                    SuccessChance = 100f,
                    IconName = "PrismOfFear"
                },
                new PoEStarterItem()
                {
                    Name = "Cleave",
                    Description = "[Requires Melee Weapon] Slashes with your weapon in an arc, hitting all enemies in front of you with physical attack damage.",
                    CategoryName = "Weapons",
                    RarityName = "Normal",
                    Cooldown = 2.0f,
                    SuccessChance = 100f,
                    IconName = "Sword"
                },
                new PoEStarterItem()
                {
                    Name = "Molten Shell",
                    Description = "Surrounds you with a protective shell of molten rock that absorbs 75% of incoming hit damage. Explodes when broken.",
                    CategoryName = "Spells",
                    RarityName = "Rare",
                    Cooldown = 8.0f,
                    SuccessChance = 100f,
                    IconName = "Shield"
                },
                new PoEStarterItem()
                {
                    Name = "Haste",
                    Description = "[Aura] Casts an aura that grants you and your allies significantly increased movement speed and attack/cast speed.",
                    CategoryName = "Spells",
                    RarityName = "Unique",
                    Cooldown = 15.0f,
                    SuccessChance = 100f,
                    IconName = "sprint"
                },
                new PoEStarterItem()
                {
                    Name = "Decoy Totem",
                    Description = "Summons a totem that taunts all nearby enemies, drawing their attacks away from you.",
                    CategoryName = "Spells",
                    RarityName = "Rare",
                    Cooldown = 6.0f,
                    SuccessChance = 100f,
                    IconName = "Rune"
                }
            };

            foreach (var starter in activeStarters)
            {
                if (AddOrUpdateStarter(activeDb, starter))
                    activeAdded++;
            }

            // 2. Generate PoE Starter Constellations (Passive)
            var passiveStarters = new List<PoEStarterItem>()
            {
                new PoEStarterItem()
                {
                    Name = "Heart of Oak",
                    Description = "Grants +10% Maximum Life and regenerates 1% of Life per second.",
                    CategoryName = "Skills",
                    RarityName = "Rare",
                    Cooldown = 0f,
                    SuccessChance = 100f,
                    IconName = "lifeflask12"
                },
                new PoEStarterItem()
                {
                    Name = "Iron Will",
                    Description = "Increases spell damage based on your Strength stat, fusing physical might with elemental magic.",
                    CategoryName = "Skills",
                    RarityName = "Unique",
                    Cooldown = 0f,
                    SuccessChance = 100f,
                    IconName = "Amulet"
                },
                new PoEStarterItem()
                {
                    Name = "Eldritch Battery",
                    Description = "Energy Shield protects Mana instead of Life, letting you cast massive spells without depleting defense.",
                    CategoryName = "Skills",
                    RarityName = "Unique",
                    Cooldown = 0f,
                    SuccessChance = 100f,
                    IconName = "bismuth"
                },
                new PoEStarterItem()
                {
                    Name = "Mind Over Matter",
                    Description = "30% of incoming damage is taken from your Mana pool before your Life pool is reduced.",
                    CategoryName = "Skills",
                    RarityName = "Unique",
                    Cooldown = 0f,
                    SuccessChance = 100f,
                    IconName = "topaz"
                },
                new PoEStarterItem()
                {
                    Name = "Chaos Inoculation",
                    Description = "Maximum Life becomes 1. Immunizes you to Chaos damage, and significantly increases Maximum Energy Shield.",
                    CategoryName = "Skills",
                    RarityName = "Unique",
                    Cooldown = 0f,
                    SuccessChance = 100f,
                    IconName = "silver"
                },
                new PoEStarterItem()
                {
                    Name = "Frenzy",
                    Description = "Grants +4% increased Attack Speed and +4% increased Movement Speed per Frenzy Charge.",
                    CategoryName = "Skills",
                    RarityName = "Magic",
                    Cooldown = 0f,
                    SuccessChance = 100f,
                    IconName = "FasterAttacks"
                }
            };

            foreach (var starter in passiveStarters)
            {
                if (AddOrUpdateStarter(passiveDb, starter))
                    passiveAdded++;
            }

            // 3. Save Changes
            EditorUtility.SetDirty(activeDb);
            EditorUtility.SetDirty(passiveDb);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("PoE Starter Pack Generator", 
                $"Successfully processed PoE Starter Pack!\n\n" +
                $"✓ Active Spells Processed: {activeAdded}/{activeStarters.Count}\n" +
                $"✓ Passive Constellations Processed: {passiveAdded}/{passiveStarters.Count}\n\n" +
                $"Databases saved successfully!", "Awesome");
        }

        private static bool AddOrUpdateStarter(ItemDatabase db, PoEStarterItem starter)
        {
            // Clean up any null entries in db.items list first
            db.items = db.items.Where(x => x != null).ToList();

            // Check if item already exists
            Skill skill = db.items.FirstOrDefault(x => x != null && x.Name == starter.Name) as Skill;
            bool isNew = false;

            if (skill == null)
            {
                skill = ScriptableObject.CreateInstance<Skill>();
                skill.name = starter.Name;
                skill.Name = starter.Name;
                skill.Id = System.Guid.NewGuid().ToString();
                isNew = true;
            }

            skill.DisplayName = starter.Name;

            // Load Sprite Icon
            string spritePath = $"{IconsFolder}/{starter.IconName}.png";
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (icon != null)
            {
                skill.Icon = icon;
            }
            else
            {
                Debug.LogWarning($"[PoE Generator] Sprite not found at '{spritePath}', using default or null.");
            }

            // Set Description via reflection
            var descField = typeof(Item).GetField("m_Description", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (descField != null)
            {
                descField.SetValue(skill, starter.Description);
            }

            // Map Category from database
            if (db.categories != null && db.categories.Count > 0)
            {
                Category targetCat = db.categories.FirstOrDefault(x => x != null && x.Name.ToLower().Contains(starter.CategoryName.ToLower()));
                if (targetCat == null) targetCat = db.categories[0]; // fallback
                skill.Category = targetCat;
            }

            // Map Rarity from database
            if (db.raritys != null && db.raritys.Count > 0)
            {
                Rarity targetRarity = db.raritys.FirstOrDefault(x => x != null && x.Name.ToLower().Contains(starter.RarityName.ToLower()));
                if (targetRarity == null) targetRarity = db.raritys[0]; // fallback
                skill.Rarity = targetRarity;
            }

            // Set Cooldown via reflection
            var cooldownField = typeof(UsableItem).GetField("m_Cooldown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (cooldownField != null)
            {
                cooldownField.SetValue(skill, starter.Cooldown);
            }

            // Set success chance via reflection
            var successField = typeof(Skill).GetField("m_FixedSuccessChance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (successField != null)
            {
                successField.SetValue(skill, starter.SuccessChance);
            }

            if (isNew)
            {
                AssetDatabase.AddObjectToAsset(skill, db);
                db.items.Add(skill);
                EditorUtility.SetDirty(db);
                Debug.Log($"[PoE Generator] Added new skill '{starter.Name}' inside database {db.name}.");
            }
            else
            {
                EditorUtility.SetDirty(skill);
                Debug.Log($"[PoE Generator] Updated existing skill '{starter.Name}' inside database {db.name}.");
            }

            return true;
        }
    }
}
