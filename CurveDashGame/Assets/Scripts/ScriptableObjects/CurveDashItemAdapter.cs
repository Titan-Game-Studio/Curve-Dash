using UnityEngine;
using DevionGames.InventorySystem;

namespace STG.CurveDash
{
    [CreateAssetMenu(fileName = "New CurveDash Item Adapter", menuName = "Curve-Dash/Inventory/Item Adapter")]
    public class CurveDashItemAdapter : DevionGames.InventorySystem.Item
    {
        [Header("Original Game Data Link")]
        [SerializeField] private ItemData m_OriginalItemData;

        public ItemData OriginalItemData
        {
            get => m_OriginalItemData;
            set
            {
                m_OriginalItemData = value;
                SyncData();
            }
        }

        public override int MaxStack => m_OriginalItemData != null ? 99 : base.MaxStack;

        protected override void OnEnable()
        {
            base.OnEnable();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                SyncData();
            }
#endif
        }

        public void SyncData()
        {
            if (m_OriginalItemData != null)
            {
                this.Name = m_OriginalItemData.ItemName;

#if UNITY_EDITOR
                // Auto-healing for missing icons on the original item asset
                if (m_OriginalItemData.Icon == null)
                {
                    string iconName = "";
                    if (m_OriginalItemData is FlaskItemData flaskData)
                    {
                        string nameLower = flaskData.ItemName.ToLower();
                        if (nameLower.Contains("life")) iconName = "lifeflask12";
                        else if (nameLower.Contains("mana")) iconName = "Health Potion";
                        else if (nameLower.Contains("quicksilver")) iconName = "sprint";
                        else if (nameLower.Contains("granite")) iconName = "bismuth";
                        else if (nameLower.Contains("diamond")) iconName = "silver";
                        else iconName = "Health Potion";
                    }
                    else if (m_OriginalItemData is GemItemData gemData)
                    {
                        if (gemData.GemType == GemType.Support)
                        {
                            iconName = "FasterAttacks";
                        }
                        else
                        {
                            iconName = "PrismOfFear";
                        }
                    }
                    else if (m_OriginalItemData is CurrencyItemData)
                    {
                        iconName = "Gold";
                    }

                    if (!string.IsNullOrEmpty(iconName))
                    {
                        string iconPath = $"Assets/Textures/Icons/{iconName}.png";
                        var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                        if (sprite != null)
                        {
                            m_OriginalItemData.Icon = sprite;
                            UnityEditor.EditorUtility.SetDirty(m_OriginalItemData);
                        }
                    }
                }
#endif

                this.Icon = m_OriginalItemData.Icon;

                // Sync Description field using Reflection because m_Description is private inside DevionGames.Item
                var descField = typeof(DevionGames.InventorySystem.Item).GetField("m_Description", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (descField != null)
                {
                    descField.SetValue(this, m_OriginalItemData.Description);
                }

                // Sync Rarity, Category, and Prices/Currencies with Devion Games database if available
                SyncDatabaseReferences();

                // General Metadata Properties
                SetOrUpdateProperty("Item Type", m_OriginalItemData.Type.ToString(), Color.white);
                SetOrUpdateProperty("Rarity", m_OriginalItemData.Rarity.ToString(), GetRarityColor(m_OriginalItemData.Rarity));

                // Subtype Specific Properties
                if (m_OriginalItemData is FlaskItemData flask)
                {
                    SetOrUpdateProperty("Flask Type", flask.FlaskType.ToString(), Color.cyan);
                    SetOrUpdateProperty("Recovery Amount", flask.RecoveryAmount, Color.green);
                    SetOrUpdateProperty("Duration", flask.Duration, Color.white);
                    SetOrUpdateProperty("Max Charges", flask.MaxCharges, Color.white);
                    SetOrUpdateProperty("Charges Per Use", flask.ChargesUsedPerUse, Color.white);
                    if (flask.SpeedModifier != 1.0f)
                    {
                        SetOrUpdateProperty("Speed Modifier", flask.SpeedModifier, Color.yellow);
                    }
                    if (flask.AttackSpeedModifier != 1.0f)
                    {
                        SetOrUpdateProperty("Attack Speed Modifier", flask.AttackSpeedModifier, Color.yellow);
                    }
                }
                else if (m_OriginalItemData is GemItemData gem)
                {
                    SetOrUpdateProperty("Gem Type", gem.GemType.ToString(), gem.SocketColor);
                    SetOrUpdateProperty("Socket Color", gem.SocketColor, gem.SocketColor);
                    SetOrUpdateProperty("Ability", gem.EmbeddedAbility != null ? gem.EmbeddedAbility.AbilityName : "None", Color.green);
                }
                else if (m_OriginalItemData is CurrencyItemData currency)
                {
                    SetOrUpdateProperty("Currency Type", currency.CurrencyType.ToString(), new Color(1f, 0.6f, 0f));
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
            if (m_OriginalItemData == null) return;

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
                    var matchingRarity = db.raritys.Find(r => r.Name.Equals(m_OriginalItemData.Rarity.ToString(), System.StringComparison.OrdinalIgnoreCase));
                    if (matchingRarity != null)
                    {
                        this.Rarity = matchingRarity;
                    }
                }

                // 2. Sync Category (Essential to prevent NullReferenceException in ItemCollectionEditor.DrawItemLabel!)
                if (db.categories != null && db.categories.Count > 0)
                {
                    string targetCategoryName = "General";
                    if (m_OriginalItemData is WeaponData) targetCategoryName = "Weapon";
                    else if (m_OriginalItemData is ArmorItemData) targetCategoryName = "Armor";
                    else if (m_OriginalItemData is FlaskItemData) targetCategoryName = "Potions"; // Devion default is "Potions" or "Consumable"
                    else if (m_OriginalItemData is GemItemData) targetCategoryName = "Gem";
                    else if (m_OriginalItemData is CurrencyItemData) targetCategoryName = "Currency";

                    // Try to find a category that contains our target name
                    var matchingCategory = db.categories.Find(c => c.Name.IndexOf(targetCategoryName, System.StringComparison.OrdinalIgnoreCase) >= 0);
                    
                    // If not found, try to match by exact type name or fall back to any weapon/armor keyword
                    if (matchingCategory == null && (m_OriginalItemData is WeaponData || m_OriginalItemData is ArmorItemData))
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
                if (m_OriginalItemData is FlaskItemData flask)
                {
                    if (flask.FlaskType == FlaskType.Life || flask.FlaskType == FlaskType.Mana)
                    {
                        baseValue = flask.RecoveryAmount * 0.1f + flask.MaxCharges * 0.1f;
                    }
                    else
                    {
                        baseValue = 25f; // Utility flasks like Quicksilver/Granite are more valuable
                    }
                }
                else if (m_OriginalItemData is GemItemData gem)
                {
                    baseValue = 15f; // Gems are high value
                    if (gem.EmbeddedAbility != null)
                    {
                        baseValue += gem.EmbeddedAbility.Cooldown * 0.5f;
                    }
                }
                else if (m_OriginalItemData is CurrencyItemData currency)
                {
                    switch (currency.CurrencyType)
                    {
                        case CurrencyType.ScrollOfWisdom: baseValue = 1f; break;
                        case CurrencyType.OrbOfTransmutation: baseValue = 5f; break;
                        case CurrencyType.OrbOfAlteration: baseValue = 8f; break;
                        case CurrencyType.BlacksmithWhetstone: baseValue = 10f; break;
                        case CurrencyType.ArmourerScrap: baseValue = 10f; break;
                        case CurrencyType.ChaosOrb: baseValue = 50f; break;
                        case CurrencyType.ExaltedOrb: baseValue = 250f; break;
                        default: baseValue = 10f; break;
                    }
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
