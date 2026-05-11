using UnityEngine;
using DevionGames.InventorySystem;

namespace STG.CurveDash
{
    [CreateAssetMenu(fileName = "New CurveDash Equipment Adapter", menuName = "Curve-Dash/Inventory/Equipment Adapter")]
    public class CurveDashEquipmentAdapter : DevionGames.InventorySystem.EquipmentItem
    {
        [Header("Original Game Data Link")]
        [SerializeField] private ItemData m_OriginalEquipmentData;

        public ItemData OriginalEquipmentData
        {
            get => m_OriginalEquipmentData;
            set
            {
                m_OriginalEquipmentData = value;
                SyncData();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SyncData();
        }

        public void SyncData()
        {
            if (m_OriginalEquipmentData != null)
            {
                this.Name = m_OriginalEquipmentData.ItemName;

#if UNITY_EDITOR
                // Auto-healing for missing icons on the original equipment asset
                if (m_OriginalEquipmentData.Icon == null)
                {
                    string iconName = "";
                    if (m_OriginalEquipmentData is WeaponData weapon)
                    {
                        iconName = weapon.ItemName.ToLower().Contains("shield") ? "Shield" : "Sword";
                    }
                    else if (m_OriginalEquipmentData is ArmorItemData armor)
                    {
                        switch (armor.Slot)
                        {
                            case EquipmentSlot.Head: iconName = "Head"; break;
                            case EquipmentSlot.Body: iconName = "Torso"; break;
                            case EquipmentSlot.Feet: iconName = "Feet"; break;
                            case EquipmentSlot.Amulet: iconName = "Amulet"; break;
                            case EquipmentSlot.Ring1:
                            case EquipmentSlot.Ring2: iconName = "Soulcord"; break; // Awesome Soulcord ring/jewel icon
                            case EquipmentSlot.Hands: iconName = "GlovesStrDex3"; break; // Awesome glove icon
                            case EquipmentSlot.Belt: iconName = "InjectorBelt"; break; // Awesome belt icon
                            case EquipmentSlot.OffHand: iconName = "Shield"; break;
                            case EquipmentSlot.MainHand: iconName = "Sword"; break;
                            default: iconName = "Torso"; break;
                        }
                    }

                    if (!string.IsNullOrEmpty(iconName))
                    {
                        string iconPath = $"Assets/Textures/Icons/{iconName}.png";
                        var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                        if (sprite != null)
                        {
                            m_OriginalEquipmentData.Icon = sprite;
                            UnityEditor.EditorUtility.SetDirty(m_OriginalEquipmentData);
                        }
                    }
                }
#endif

                this.Icon = m_OriginalEquipmentData.Icon;

                // Sync Description field using Reflection because m_Description is private inside DevionGames.Item
                var descField = typeof(DevionGames.InventorySystem.Item).GetField("m_Description", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (descField != null)
                {
                    descField.SetValue(this, m_OriginalEquipmentData.Description);
                }

                // Associate the visual model of our equipment as the world Prefab inside Devion Games
                if (m_OriginalEquipmentData is EquippableData equippable && equippable.VisualModel != null)
                {
                    this.Prefab = equippable.VisualModel;
                }

                // Sync Rarity, Category, and Prices/Currencies with Devion Games database if available
                SyncDatabaseReferences();

                // General Metadata Properties
                SetOrUpdateProperty("Equipment Type", m_OriginalEquipmentData.Type.ToString(), Color.white);
                SetOrUpdateProperty("Rarity", m_OriginalEquipmentData.Rarity.ToString(), GetRarityColor(m_OriginalEquipmentData.Rarity));

                // Subtype Specific Properties
                if (m_OriginalEquipmentData is WeaponData weaponData)
                {
                    int minDmg = Mathf.RoundToInt(weaponData.BaseMinDamage);
                    int maxDmg = Mathf.RoundToInt(weaponData.BaseMaxDamage);
                    
                    SetOrUpdateProperty("Min Damage", minDmg, Color.white);
                    SetOrUpdateProperty("Max Damage", maxDmg, Color.white);
                    SetOrUpdateProperty("Damage", new Vector2(minDmg, maxDmg), Color.yellow);
                    SetOrUpdateProperty("Attack Speed", weaponData.BaseAttackSpeed, Color.cyan);
                    SetOrUpdateProperty("Attack Range", weaponData.BaseAttackRange, Color.cyan);
                }
                else if (m_OriginalEquipmentData is ArmorItemData armorItem)
                {
                    SetOrUpdateProperty("Defense", armorItem.Defense, new Color(0.6f, 0.8f, 1f));
                    SetOrUpdateProperty("Heart", armorItem.HealthBonus, Color.green);
                    SetOrUpdateProperty("Armor Slot", armorItem.Slot.ToString(), Color.white);
                }
            }
        }

        private void SetOrUpdateProperty(string name, object value, Color displayColor)
        {
            var property = FindProperty(name);
            if (property == null)
            {
                AddProperty(name, value);
                property = FindProperty(name);
            }
            else
            {
                property.SetValue(value);
            }
            
            if (property != null)
            {
                property.show = true;
                property.color = displayColor;
            }
        }

        private Color GetRarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Magic: return new Color(0.35f, 0.55f, 1f); // Blue
                case ItemRarity.Rare: return new Color(1f, 0.85f, 0f); // Yellow/Gold
                case ItemRarity.Unique: return new Color(0.68f, 0.38f, 0.16f); // Brown/Orange
                default: return Color.white;
            }
        }

        private void SyncDatabaseReferences()
        {
            if (m_OriginalEquipmentData == null) return;

            DevionGames.InventorySystem.ItemDatabase db = null;

            if (Application.isPlaying)
            {
                var manager = FindAnyObjectByType<DevionGames.InventorySystem.InventoryManager>();
                if (manager != null)
                {
                    try
                    {
                        db = DevionGames.InventorySystem.InventoryManager.Database;
                    }
                    catch (System.Exception) { /* Fallback */ }
                }
            }
            else
            {
                #if UNITY_EDITOR
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemDatabase");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    db = UnityEditor.AssetDatabase.LoadAssetAtPath<DevionGames.InventorySystem.ItemDatabase>(path);
                }
                #endif
            }

            if (db != null)
            {
                // 1. Sync Rarity
                if (db.raritys != null)
                {
                    var matchingRarity = db.raritys.Find(r => r.Name.Equals(m_OriginalEquipmentData.Rarity.ToString(), System.StringComparison.OrdinalIgnoreCase));
                    if (matchingRarity != null)
                    {
                        this.Rarity = matchingRarity;
                    }
                }

                // 2. Sync Category (Essential to prevent NullReferenceException in ItemCollectionEditor.DrawItemLabel!)
                if (db.categories != null && db.categories.Count > 0)
                {
                    string targetCategoryName = "General";
                    if (m_OriginalEquipmentData is WeaponData) targetCategoryName = "Weapon";
                    else if (m_OriginalEquipmentData is ArmorItemData) targetCategoryName = "Armor";
                    else if (m_OriginalEquipmentData is FlaskItemData) targetCategoryName = "Potions"; // Devion default is "Potions" or "Consumable"
                    else if (m_OriginalEquipmentData is GemItemData) targetCategoryName = "Gem";
                    else if (m_OriginalEquipmentData is CurrencyItemData) targetCategoryName = "Currency";

                    // Try to find a category that contains our target name
                    var matchingCategory = db.categories.Find(c => c.Name.IndexOf(targetCategoryName, System.StringComparison.OrdinalIgnoreCase) >= 0);
                    
                    // If not found, try to match by exact type name or fall back to any weapon/armor keyword
                    if (matchingCategory == null && (m_OriginalEquipmentData is WeaponData || m_OriginalEquipmentData is ArmorItemData))
                    {
                        matchingCategory = db.categories.Find(c => c.Name.IndexOf("Equipment", System.StringComparison.OrdinalIgnoreCase) >= 0);
                    }

                    // Fallback to the first category if still null, so the editor window NEVER crashes!
                    if (matchingCategory == null)
                    {
                        matchingCategory = db.categories[0];
                    }

                    this.Category = matchingCategory;
                }

                // 3. Auto-generate Prices and Currency references
                if (db.currencies != null && db.currencies.Count > 0)
                {
                    // Search for Gold currency
                    var goldCurrency = db.currencies.Find(c => c.Name.Equals("Gold", System.StringComparison.OrdinalIgnoreCase));
                    if (goldCurrency == null)
                    {
                        goldCurrency = db.currencies[0]; // fallback to first available currency
                    }

                    if (goldCurrency != null)
                    {
                        this.BuyCurrency = goldCurrency;
                        this.SellCurrency = goldCurrency;
                    }
                }

                // Calculate baseline "Power/Strength" price
                float baseValue = 10f;
                if (m_OriginalEquipmentData is WeaponData weapon)
                {
                    float avgDmg = (weapon.BaseMinDamage + weapon.BaseMaxDamage) / 2f;
                    baseValue = avgDmg * 1.5f * weapon.BaseAttackSpeed;
                }
                else if (m_OriginalEquipmentData is ArmorItemData armor)
                {
                    baseValue = armor.Defense * 1.2f + armor.HealthBonus * 0.5f;
                }

                int baseBuyPrice = Mathf.Max(1, Mathf.RoundToInt(baseValue));
                int baseSellPrice = Mathf.Max(1, Mathf.RoundToInt(baseBuyPrice * 0.5f));

                // Assign to private fields in Devion.Item using Reflection
                var buyPriceField = typeof(DevionGames.InventorySystem.Item).GetField("m_BuyPrice", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (buyPriceField != null)
                {
                    buyPriceField.SetValue(this, baseBuyPrice);
                }
                var sellPriceField = typeof(DevionGames.InventorySystem.Item).GetField("m_SellPrice", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (sellPriceField != null)
                {
                    sellPriceField.SetValue(this, baseSellPrice);
                }
            }
        }
    }
}
