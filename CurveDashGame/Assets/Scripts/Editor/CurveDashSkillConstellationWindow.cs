using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DevionGames.InventorySystem;

namespace STG.CurveDash.Editor
{
    public class CurveDashSkillConstellationWindow : EditorWindow
    {
        private enum Tab
        {
            ActiveAbilities,
            PassiveConstellations
        }

        private Tab currentTab = Tab.ActiveAbilities;
        private ItemDatabase activeDatabase;
        private ItemDatabase passiveDatabase;

        // Path constants
        private const string ActiveDatabasePath = "Assets/Data/DTOS/CurveDash_Active_Abilities.asset";
        private const string PassiveDatabasePath = "Assets/Data/DTOS/CurveDash_Passive_Constellations.asset";

        // Editor state
        private Vector2 scrollPositionLeft;
        private Vector2 scrollPositionRight;
        private string searchString = "";
        private Item selectedItem;
        private bool isCreatingNew = false;

        // Form fields for new or selected item
        private string itemName = "New Skill";
        private string itemDisplayName = "New Skill";
        private bool useItemNameAsDisplayName = true;
        private string itemDescription = "";
        private Sprite itemIcon;
        private int selectedCategoryIndex = 0;
        private int selectedRarityIndex = 0;
        private float cooldown = 0f;
        private float successChance = 100f;

        [MenuItem("Curve-Dash/Database/Skills & Constellations Generator", false, 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<CurveDashSkillConstellationWindow>("Skill & Constellation Generator");
            window.minSize = new Vector2(850, 550);
            window.Show();
        }

        private void OnEnable()
        {
            LoadDatabases();
            ResetForm();
        }

        private void LoadDatabases()
        {
            activeDatabase = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ActiveDatabasePath);
            passiveDatabase = AssetDatabase.LoadAssetAtPath<ItemDatabase>(PassiveDatabasePath);

            if (activeDatabase == null)
                Debug.LogWarning($"[Skill Generator] Active database not found at '{ActiveDatabasePath}'. Please select or create it.");
            if (passiveDatabase == null)
                Debug.LogWarning($"[Skill Generator] Passive database not found at '{PassiveDatabasePath}'. Please select or create it.");
        }

        private ItemDatabase GetCurrentDatabase()
        {
            return currentTab == Tab.ActiveAbilities ? activeDatabase : passiveDatabase;
        }

        private void ResetForm()
        {
            selectedItem = null;
            isCreatingNew = true;
            itemName = currentTab == Tab.ActiveAbilities ? "New Active Ability" : "New Passive Node";
            itemDisplayName = itemName;
            useItemNameAsDisplayName = true;
            itemDescription = "";
            itemIcon = null;
            selectedCategoryIndex = 0;
            selectedRarityIndex = 0;
            cooldown = currentTab == Tab.ActiveAbilities ? 4f : 0f;
            successChance = 100f;
        }

        private void PopulateFormFromItem(Item item)
        {
            selectedItem = item;
            isCreatingNew = false;
            itemName = item.Name;
            itemDisplayName = item.DisplayName;
            useItemNameAsDisplayName = true; // Default behavior
            itemIcon = item.Icon;
            
            // Handle description using reflection
            itemDescription = "";
            var descField = typeof(Item).GetField("m_Description", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (descField != null)
            {
                itemDescription = (string)descField.GetValue(item);
            }

            // Find category and rarity indices
            var db = GetCurrentDatabase();
            if (db != null)
            {
                selectedCategoryIndex = db.categories.IndexOf(item.Category);
                if (selectedCategoryIndex < 0) selectedCategoryIndex = 0;

                selectedRarityIndex = db.raritys.IndexOf(item.Rarity);
                if (selectedRarityIndex < 0) selectedRarityIndex = 0;
            }

            // Skill specific fields
            cooldown = 0f;
            successChance = 100f;

            if (item is Skill skill)
            {
                var cooldownField = typeof(UsableItem).GetField("m_Cooldown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (cooldownField != null)
                {
                    cooldown = (float)cooldownField.GetValue(skill);
                }

                var successField = typeof(Skill).GetField("m_FixedSuccessChance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (successField != null)
                {
                    successChance = (float)successField.GetValue(skill);
                }
            }
        }

        private void OnGUI()
        {
            DrawHeader();

            // Navigation Tab Toolbar
            EditorGUI.BeginChangeCheck();
            currentTab = (Tab)GUILayout.Toolbar((int)currentTab, new string[] { "Active Abilities (Spells)", "Passive Constellations (Skills)" }, GUILayout.Height(35));
            if (EditorGUI.EndChangeCheck())
            {
                ResetForm();
                searchString = "";
            }

            ItemDatabase currentDb = GetCurrentDatabase();
            if (currentDb == null)
            {
                DrawMissingDatabaseUI();
                return;
            }

            GUILayout.Space(10);

            // Left-Right Master-Detail layout
            EditorGUILayout.BeginHorizontal();
            {
                // Left Column: List of existing sub-assets
                DrawLeftPane(currentDb);

                // Vertical separator
                GUILayout.Box("", GUILayout.Width(2), GUILayout.ExpandHeight(true));

                // Right Column: Detail / Edit / Create form
                DrawRightPane(currentDb);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            GUILayout.Space(10);
            Rect rect = EditorGUILayout.GetControlRect(false, 40);
            GUI.Box(rect, "", new GUIStyle("HelpBox"));
            
            // Header text
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            headerStyle.normal.textColor = new Color(1f, 0.65f, 0f); // Beautiful PoE Gold

            string dbTitle = currentTab == Tab.ActiveAbilities 
                ? "Active Abilities & Spells Database" 
                : "Passive Constellations & Stats Database";

            GUI.Label(rect, "⚡ " + dbTitle.ToUpper() + " ⚡", headerStyle);
            GUILayout.Space(15);
        }

        private void DrawMissingDatabaseUI()
        {
            EditorGUILayout.BeginVertical(new GUIStyle("HelpBox"));
            {
                GUILayout.Space(20);
                EditorGUILayout.HelpBox($"Database file could not be found automatically at expected path!\nExpected: " + 
                    (currentTab == Tab.ActiveAbilities ? ActiveDatabasePath : PassiveDatabasePath), MessageType.Warning);
                
                GUILayout.Space(10);
                if (currentTab == Tab.ActiveAbilities)
                {
                    activeDatabase = (ItemDatabase)EditorGUILayout.ObjectField("Select Active Database", activeDatabase, typeof(ItemDatabase), false);
                }
                else
                {
                    passiveDatabase = (ItemDatabase)EditorGUILayout.ObjectField("Select Passive Database", passiveDatabase, typeof(ItemDatabase), false);
                }

                GUILayout.Space(10);
                if (GUILayout.Button("Retry Load / Refresh Assets", GUILayout.Height(35)))
                {
                    LoadDatabases();
                }
                GUILayout.Space(20);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawLeftPane(ItemDatabase db)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(350), GUILayout.ExpandHeight(true));
            {
                // Toolbar Search field
                EditorGUILayout.BeginHorizontal();
                {
                    searchString = EditorGUILayout.TextField(searchString, new GUIStyle("SearchTextField"), GUILayout.Height(22));
                    if (GUILayout.Button("Clear", GUILayout.Width(50), GUILayout.Height(22)))
                    {
                        searchString = "";
                        GUI.FocusControl(null);
                    }
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(5);

                // Add New button at the top of the list
                if (GUILayout.Button("+ CREATE NEW ENTRY", GUILayout.Height(30)))
                {
                    ResetForm();
                }

                GUILayout.Space(5);

                // List of sub-assets
                scrollPositionLeft = EditorGUILayout.BeginScrollView(scrollPositionLeft, new GUIStyle("GroupBox"));
                {
                    var filteredItems = db.items
                        .Where(x => x != null && (string.IsNullOrEmpty(searchString) || x.Name.ToLower().Contains(searchString.ToLower())))
                        .ToList();

                    if (filteredItems.Count == 0)
                    {
                        EditorGUILayout.LabelField("No items match the query.", EditorStyles.centeredGreyMiniLabel);
                    }
                    else
                    {
                        foreach (var item in filteredItems)
                        {
                            DrawListItem(item, db);
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawListItem(Item item, ItemDatabase db)
        {
            bool isSelected = (selectedItem == item && !isCreatingNew);
            
            // Custom item style
            GUIStyle itemStyle = new GUIStyle(GUI.skin.button);
            itemStyle.alignment = TextAnchor.MiddleLeft;
            itemStyle.padding = new RectOffset(6, 6, 6, 6);
            if (isSelected)
            {
                itemStyle.normal.background = Texture2D.whiteTexture; // Highlight background in Pro skin
            }

            EditorGUILayout.BeginHorizontal();
            {
                // Item Icon preview inside list
                Rect iconRect = EditorGUILayout.GetControlRect(false, 30, GUILayout.Width(30));
                if (item.Icon != null)
                {
                    GUI.DrawTexture(iconRect, item.Icon.texture, ScaleMode.ScaleToFit);
                }
                else
                {
                    GUI.Box(iconRect, "No Icon", EditorStyles.centeredGreyMiniLabel);
                }

                // Item Select Button
                string label = $"{item.Name}\n<color=cyan>{(item.Category != null ? item.Category.Name : "No Category")}</color>";
                GUIStyle richTextLabel = new GUIStyle(EditorStyles.label) { richText = true, fontSize = 11 };
                
                if (GUILayout.Button(label, richTextLabel, GUILayout.ExpandWidth(true), GUILayout.Height(30)))
                {
                    PopulateFormFromItem(item);
                }

                // Delete button
                if (GUILayout.Button("❌", GUILayout.Width(25), GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog("Delete Sub-Asset", $"Are you sure you want to permanently delete skill '{item.Name}' from the database?", "Delete", "Cancel"))
                    {
                        DeleteSubAsset(db, item);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        private void DrawRightPane(ItemDatabase db)
        {
            scrollPositionRight = EditorGUILayout.BeginScrollView(scrollPositionRight, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            {
                EditorGUILayout.BeginVertical(new GUIStyle("GroupBox"));
                {
                    string modeTitle = isCreatingNew ? "✦ CREATE A NEW ENTRY ✦" : "✎ EDITING EXISTING ENTRY ✎";
                    GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 14,
                        alignment = TextAnchor.MiddleCenter
                    };
                    titleStyle.normal.textColor = isCreatingNew ? Color.green : Color.cyan;
                    GUILayout.Label(modeTitle, titleStyle);
                    GUILayout.Space(15);

                    // Form Fields
                    itemName = EditorGUILayout.TextField("Ability / Node Name", itemName);
                    itemDisplayName = EditorGUILayout.TextField("Display Name", itemDisplayName);

                    GUILayout.Space(5);
                    
                    // Icon Picker
                    itemIcon = (Sprite)EditorGUILayout.ObjectField("Icon Sprite", itemIcon, typeof(Sprite), false, GUILayout.Height(64));

                    GUILayout.Space(5);

                    // Description multilines
                    EditorGUILayout.LabelField("Description");
                    itemDescription = EditorGUILayout.TextArea(itemDescription, GUILayout.Height(80));

                    GUILayout.Space(10);

                    // Dropdowns for Category and Rarity
                    DrawDropdowns(db);

                    GUILayout.Space(10);

                    // Cooldown and success fields (Skills properties)
                    cooldown = EditorGUILayout.FloatField("Cooldown (Seconds)", cooldown);
                    successChance = EditorGUILayout.Slider("Base Success Chance (%)", successChance, 0f, 100f);

                    GUILayout.Space(25);

                    // Action Buttons
                    EditorGUILayout.BeginHorizontal();
                    {
                        if (isCreatingNew)
                        {
                            if (GUILayout.Button("⚡ CREATE AND ADD TO DATABASE", GUILayout.Height(35)))
                            {
                                CreateSubAsset(db);
                            }
                        }
                        else
                        {
                            if (GUILayout.Button("💾 SAVE CHANGES", GUILayout.Height(35)))
                            {
                                SaveSubAsset(db);
                            }

                            if (GUILayout.Button("Discard Changes", GUILayout.Height(35), GUILayout.Width(120)))
                            {
                                PopulateFormFromItem(selectedItem);
                            }
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawDropdowns(ItemDatabase db)
        {
            // Category Dropdown
            if (db.categories != null && db.categories.Count > 0)
            {
                string[] categoryNames = db.categories.Select(x => x != null ? x.Name : "None").ToArray();
                if (selectedCategoryIndex >= categoryNames.Length) selectedCategoryIndex = 0;
                selectedCategoryIndex = EditorGUILayout.Popup("Category Type", selectedCategoryIndex, categoryNames);
            }
            else
            {
                EditorGUILayout.HelpBox("No Categories found in database. Items will have default category.", MessageType.Info);
            }

            GUILayout.Space(5);

            // Rarity Dropdown
            if (db.raritys != null && db.raritys.Count > 0)
            {
                string[] rarityNames = db.raritys.Select(x => x != null ? x.Name : "None").ToArray();
                if (selectedRarityIndex >= rarityNames.Length) selectedRarityIndex = 0;
                selectedRarityIndex = EditorGUILayout.Popup("Rarity Type", selectedRarityIndex, rarityNames);
            }
            else
            {
                EditorGUILayout.HelpBox("No Rarities found in database. Items will have default rarity.", MessageType.Info);
            }
        }

        private void CreateSubAsset(ItemDatabase db)
        {
            if (string.IsNullOrEmpty(itemName))
            {
                EditorUtility.DisplayDialog("Error", "Skill/Ability Name cannot be empty!", "OK");
                return;
            }

            // Create instance of Skill
            Skill newSkill = ScriptableObject.CreateInstance<Skill>();
            newSkill.name = itemName;
            newSkill.Name = itemName;
            newSkill.DisplayName = itemDisplayName;
            newSkill.Icon = itemIcon;

            // Generate UUID
            newSkill.Id = System.Guid.NewGuid().ToString();

            // Set Description via reflection
            var descField = typeof(Item).GetField("m_Description", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (descField != null)
            {
                descField.SetValue(newSkill, itemDescription);
            }

            // Set Category & Rarity
            if (db.categories != null && selectedCategoryIndex < db.categories.Count)
                newSkill.Category = db.categories[selectedCategoryIndex];

            if (db.raritys != null && selectedRarityIndex < db.raritys.Count)
                newSkill.Rarity = db.raritys[selectedRarityIndex];

            // Set Cooldown & FixedSuccessChance via reflection
            var cooldownField = typeof(UsableItem).GetField("m_Cooldown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (cooldownField != null)
            {
                cooldownField.SetValue(newSkill, cooldown);
            }

            var successField = typeof(Skill).GetField("m_FixedSuccessChance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (successField != null)
            {
                successField.SetValue(newSkill, successChance);
            }

            // Add as sub-asset to database
            AssetDatabase.AddObjectToAsset(newSkill, db);
            db.items.Add(newSkill);

            // Save changes
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=lime>[Skill Generator] Successfully added sub-asset skill '{itemName}' to database {db.name}!</color>");
            
            // Reload and select the new skill
            PopulateFormFromItem(newSkill);
        }

        private void SaveSubAsset(ItemDatabase db)
        {
            if (selectedItem == null) return;

            selectedItem.name = itemName;
            selectedItem.Name = itemName;
            selectedItem.DisplayName = itemDisplayName;
            selectedItem.Icon = itemIcon;

            // Set Description via reflection
            var descField = typeof(Item).GetField("m_Description", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (descField != null)
            {
                descField.SetValue(selectedItem, itemDescription);
            }

            // Set Category & Rarity
            if (db.categories != null && selectedCategoryIndex < db.categories.Count)
                selectedItem.Category = db.categories[selectedCategoryIndex];

            if (db.raritys != null && selectedRarityIndex < db.raritys.Count)
                selectedItem.Rarity = db.raritys[selectedRarityIndex];

            // Skill specific fields
            if (selectedItem is Skill skill)
            {
                var cooldownField = typeof(UsableItem).GetField("m_Cooldown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (cooldownField != null)
                {
                    cooldownField.SetValue(skill, cooldown);
                }

                var successField = typeof(Skill).GetField("m_FixedSuccessChance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (successField != null)
                {
                    successField.SetValue(skill, successChance);
                }
            }

            // Save and refresh
            EditorUtility.SetDirty(selectedItem);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=cyan>[Skill Generator] Successfully updated skill '{itemName}' inside database {db.name}!</color>");
            
            // Re-select to repaint
            PopulateFormFromItem(selectedItem);
        }

        private void DeleteSubAsset(ItemDatabase db, Item item)
        {
            if (item == null) return;

            db.items.Remove(item);
            
            // If the item is currently selected, reset the form
            if (selectedItem == item)
            {
                ResetForm();
            }

            // Safely delete from asset file
            DestroyImmediate(item, true);

            // Save database changes
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=red>[Skill Generator] Successfully deleted sub-asset skill '{item.Name}' from database {db.name}!</color>");
        }
    }
}
