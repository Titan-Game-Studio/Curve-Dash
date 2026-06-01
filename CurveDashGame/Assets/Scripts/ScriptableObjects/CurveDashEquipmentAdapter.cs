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
                // Removed hardcoded icon assignment logic as requested.
#endif

                try
                {
                    if (m_OriginalEquipmentData.Icon != null)
                    {
                        this.Icon = m_OriginalEquipmentData.Icon;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[CurveDashEquipmentAdapter] Icon reference is missing or broken on '{m_OriginalEquipmentData.name}': {ex.Message}");
                    this.Icon = null;
                }
                
                Debug.Log($"<color=lime>[CurveDashEquipmentAdapter] SyncData for '{this.Name}' (Original='{m_OriginalEquipmentData.name}'): Icon='{(this.Icon != null ? "Assigned" : "NULL")}', OriginalIcon='{(m_OriginalEquipmentData.Icon != null ? "Assigned" : "NULL")}'</color>");

                // Sync Description field using Reflection because m_Description is private inside DevionGames.Item
                var descField = typeof(DevionGames.InventorySystem.Item).GetField("m_Description", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (descField != null)
                {
                    descField.SetValue(this, m_OriginalEquipmentData.Description);
                }

                this.Prefab = null;

                // Associate the visual model of our equipment as the world Prefab inside Devion Games
                if (m_OriginalEquipmentData is EquippableData equippable && equippable.VisualModel != null)
                {
                    this.Prefab = equippable.VisualModel;
                }
                else if (m_OriginalEquipmentData is ArmorItemData armorItem)
                {
#if UNITY_EDITOR
                    if (armorItem.Prefab != null && !string.IsNullOrEmpty(armorItem.Prefab.AssetGUID))
                    {
                        string armorPath = UnityEditor.AssetDatabase.GUIDToAssetPath(armorItem.Prefab.AssetGUID);
                        if (!string.IsNullOrEmpty(armorPath))
                        {
                            this.Prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(armorPath);
                        }
                    }
#endif
                }
                else if (m_OriginalEquipmentData is BeltItemData beltData)
                {
#if UNITY_EDITOR
                    if (beltData.Prefab != null && !string.IsNullOrEmpty(beltData.Prefab.AssetGUID))
                    {
                        string beltPath = UnityEditor.AssetDatabase.GUIDToAssetPath(beltData.Prefab.AssetGUID);
                        if (!string.IsNullOrEmpty(beltPath))
                            this.Prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(beltPath);
                    }
#endif
                }
                // AmuletItemData and RingItemData have no 3D model — Prefab stays null.

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
                    SetOrUpdateProperty("Health", armorItem.HealthBonus, Color.green);
                    SetOrUpdateProperty("Armor Slot", armorItem.Slot.ToString(), Color.white);
                }
                else if (m_OriginalEquipmentData is BeltItemData beltData)
                {
                    SetOrUpdateProperty("Health", beltData.HealthBonus, Color.green);
                    SetOrUpdateProperty("Life Regen", beltData.LifeRegeneration, new Color(0.5f, 1f, 0.5f));
                    SetOrUpdateProperty("Flask Slots", beltData.FlaskSlots.Count, Color.yellow);
                    SetOrUpdateProperty("Armor Slot", "Belt", Color.white);
                }
                else if (m_OriginalEquipmentData is AmuletItemData amuletData)
                {
                    SetOrUpdateProperty("Health", amuletData.HealthBonus, Color.green);
                    SetOrUpdateProperty("Mana", amuletData.ManaBonus, new Color(0.4f, 0.6f, 1f));
                    SetOrUpdateProperty("Crit Chance", amuletData.CritChanceBonus, Color.yellow);
                    SetOrUpdateProperty("All Resistances", amuletData.AllResistances, new Color(1f, 0.6f, 0.2f));
                    SetOrUpdateProperty("Armor Slot", "Amulet", Color.white);
                }
                else if (m_OriginalEquipmentData is RingItemData ringData)
                {
                    SetOrUpdateProperty("Health", ringData.HealthBonus, Color.green);
                    SetOrUpdateProperty("Added Damage", ringData.AddedFlatDamage, Color.yellow);
                    SetOrUpdateProperty("Attack Speed", ringData.AttackSpeedBonus, Color.cyan);
                    SetOrUpdateProperty("All Resistances", ringData.AllResistances, new Color(1f, 0.6f, 0.2f));
                    SetOrUpdateProperty("Armor Slot", ringData.Slot.ToString(), Color.white);
                }
            }
            else
            {
                Debug.LogWarning($"[CurveDashEquipmentAdapter] SyncData skipped because m_OriginalEquipmentData is null on '{this.name}'");
            }
        }

        // Call this BEFORE the item enters Devion's equipment container so EquipmentHandler
        // picks up the updated properties when it applies stat modifiers.
        public void SyncAffixes(System.Collections.Generic.List<StatModifier> affixes)
        {
            // Reset all properties to base values before applying new affix set.
            SyncData();

            if (affixes == null || affixes.Count == 0) return;

            // Accumulate flat and percent contributions per Devion stat name.
            var flatSums    = new System.Collections.Generic.Dictionary<string, float>();
            var percentSums = new System.Collections.Generic.Dictionary<string, float>();

            foreach (var affix in affixes)
            {
                foreach (var mapping in AffixStatMapper.GetMappings(affix.Type))
                {
                    string key = mapping.DevionStatName;
                    if (mapping.Contribution == AffixStatMapper.ContributionType.FlatAdd)
                    {
                        flatSums[key] = (flatSums.TryGetValue(key, out float fv) ? fv : 0f) + affix.Value;
                    }
                    else
                    {
                        percentSums[key] = (percentSums.TryGetValue(key, out float pv) ? pv : 0f) + affix.Value;
                    }
                }
            }

            // Track which stat names have already been processed.
            var processed = new System.Collections.Generic.HashSet<string>();

            // Stats that already have a base property (written by SyncData): merge flat+pct into one value.
            // EquipmentHandler infers modType from magnitude: >1 → Flat, <=1 → PercentAdd.
            // By writing the fully resolved value we avoid the limitation of a single property per name.
            foreach (var key in flatSums.Keys)
            {
                var prop = FindProperty(key);
                if (prop == null) continue;

                float baseVal = System.Convert.ToSingle(prop.GetValue());
                float flat    = flatSums.TryGetValue(key, out float fv)    ? fv : 0f;
                float pct     = percentSums.TryGetValue(key, out float pv) ? pv : 0f;
                float final   = (baseVal + flat) * (1f + pct / 100f);
                prop.SetValue(Mathf.RoundToInt(final));
                processed.Add(key);
            }

            foreach (var key in percentSums.Keys)
            {
                if (processed.Contains(key)) continue;

                var prop = FindProperty(key);
                if (prop != null)
                {
                    // Base property exists but only percent affix modifies it.
                    float baseVal = System.Convert.ToSingle(prop.GetValue());
                    float pct     = percentSums[key];
                    prop.SetValue(Mathf.RoundToInt(baseVal * (1f + pct / 100f)));
                    processed.Add(key);
                }
            }

            // Stats with NO base property: create new ones.
            // Flat stats → large value (>1) so Devion treats as Flat modifier.
            foreach (var kvp in flatSums)
            {
                if (processed.Contains(kvp.Key)) continue;
                float flat  = kvp.Value;
                float pct   = percentSums.TryGetValue(kvp.Key, out float pv) ? pv : 0f;
                float final = flat * (1f + pct / 100f);
                SetOrUpdateProperty(kvp.Key, final, Color.yellow);
                processed.Add(kvp.Key);
            }

            // Pure percent stats with no base (e.g. "Melee Attack" from IncreasedAttackSpeed):
            // write value in [0,1] so Devion auto-infers PercentAdd.
            foreach (var kvp in percentSums)
            {
                if (processed.Contains(kvp.Key)) continue;
                float percentAsDecimal = kvp.Value / 100f;
                SetOrUpdateProperty(kvp.Key, percentAsDecimal, Color.cyan);
                processed.Add(kvp.Key);
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
                        // Debug.Log($"<color=cyan>[DB-DIAG] '{this.name}' | Runtime DB='{db?.name ?? "NULL"}' | equipments={db?.equipments?.Count ?? 0} regions={string.Join(", ", db?.equipments?.ConvertAll(e => e.Name) ?? new System.Collections.Generic.List<string>())}</color>");
                    }
                    catch (System.Exception ex)
                    {
                        // Debug.LogWarning($"[DB-DIAG] Failed to get runtime database: {ex.Message}");
                    }
                }
                else
                {
                    // Debug.LogWarning($"[DB-DIAG] '{this.name}' | InventoryManager NOT FOUND in scene! Cannot get database.");
                }
            }
            else
            {
                #if UNITY_EDITOR
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemDatabase");
                // Debug.Log($"<color=cyan>[DB-DIAG][Editor] '{this.name}' | Found {guids.Length} ItemDatabase asset(s) in project:</color>");
                for (int i = 0; i < guids.Length; i++)
                {
                    string p = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                    // Debug.Log($"<color=cyan>[DB-DIAG][Editor]   [{i}] {p}{(i == 0 ? " ← USING THIS ONE" : "")}</color>");
                }
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

                // 4. Auto-assign correct EquipmentRegion to prevent Devion from overwriting MainHand weapon!
                if (db.equipments != null && db.equipments.Count > 0)
                {
                    // Debug.Log($"<color=orange>[REGION-DIAG] '{this.name}' | DB has {db.equipments.Count} regions: [{string.Join(", ", db.equipments.ConvertAll(r => r.Name))}]</color>");
                    DevionGames.InventorySystem.EquipmentRegion matchingRegion = null;
                    System.Collections.Generic.List<string> searchKeywords = new System.Collections.Generic.List<string>();

                    if (m_OriginalEquipmentData is WeaponData || m_OriginalEquipmentData is OffHandData)
                    {
                        var mainHandRegion = db.equipments.Find(r =>
                            r != null && (
                                r.Name.IndexOf("MainHand", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                r.Name.IndexOf("Main Hand", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                r.Name.IndexOf("Right", System.StringComparison.OrdinalIgnoreCase) >= 0));
                        var offHandRegion = db.equipments.Find(r =>
                            r != null && (
                                r.Name.IndexOf("OffHand", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                r.Name.IndexOf("Off Hand", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                r.Name.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0));

                        this.Region = new System.Collections.Generic.List<DevionGames.InventorySystem.EquipmentRegion>();

                        if (m_OriginalEquipmentData is TwoHandedWeaponData)
                        {
                            // Vũ khí 2 tay chiếm cả 2 slot → Devion tự block OffHand, icon hiện cả 2 tay
                            if (mainHandRegion != null) this.Region.Add(mainHandRegion);
                            if (offHandRegion  != null) this.Region.Add(offHandRegion);
                        }
                        else if (m_OriginalEquipmentData is OneHandedWeaponData || m_OriginalEquipmentData is BowData)
                        {
                            // Kiếm 1 tay và Cung: chỉ MainHand — tránh Devion hiện icon ở 2 slot
                            // Song kiếm được xử lý programmatically (xem PlayerView.PlaceDualWieldInOffHand)
                            if (mainHandRegion != null) this.Region.Add(mainHandRegion);
                        }
                        else if (m_OriginalEquipmentData is OffHandData)
                        {
                            // Khiên và Tên chỉ vào OffHand — tránh Devion thay thế kiếm chính
                            if (offHandRegion != null) this.Region.Add(offHandRegion);
                        }

                        UnityEngine.Debug.Log($"[CurveDashEquipmentAdapter] '{this.Name}' ({m_OriginalEquipmentData.GetType().Name}) → regions=[{string.Join(", ", this.Region.ConvertAll(r => r.Name))}]");
                        return;
                    }
                    else if (m_OriginalEquipmentData is ArmorItemData armorItem)
                    {
                        switch (armorItem.Slot)
                        {
                            case EquipmentSlot.Head:
                                searchKeywords.Add("Head");
                                break;
                            case EquipmentSlot.Body:
                                searchKeywords.AddRange(new[] { "Torso", "Chest", "Body" });
                                break;
                            case EquipmentSlot.Hands:
                                searchKeywords.AddRange(new[] { "Hands", "Gloves" });
                                break;
                            case EquipmentSlot.Feet:
                                searchKeywords.AddRange(new[] { "Feet", "Boots" });
                                break;
                            case EquipmentSlot.Amulet:
                                searchKeywords.Add("Amulet");
                                break;
                            case EquipmentSlot.Ring1:
                            case EquipmentSlot.Ring2:
                                searchKeywords.Add("Ring");
                                break;
                            case EquipmentSlot.Belt:
                                searchKeywords.Add("Belt");
                                break;
                        }
                    }
                    else if (m_OriginalEquipmentData is BeltItemData)
                    {
                        searchKeywords.Add("Belt");
                    }
                    else if (m_OriginalEquipmentData is AmuletItemData)
                    {
                        searchKeywords.Add("Amulet");
                    }
                    else if (m_OriginalEquipmentData is RingItemData ringData)
                    {
                        // Chỉ định Ring1 (Left) hay Ring2 (Right), fallback sang "Ring" chung
                        if (ringData.Slot == EquipmentSlot.Ring1)
                        {
                            searchKeywords.Add("Ring Left");
                            searchKeywords.Add("Ring");  // Fallback
                        }
                        else
                        {
                            searchKeywords.Add("Ring Right");
                            searchKeywords.Add("Ring");  // Fallback
                        }
                    }
                    else if (m_OriginalEquipmentData is FlaskItemData)
                    {
                        // Assign all 3 flask regions — PlayerView.PreAssignFlaskRegion narrows to one slot
                        // just before the equip action, then resets back to all 3 after placement.
                        var flaskRegions = new System.Collections.Generic.List<DevionGames.InventorySystem.EquipmentRegion>();
                        foreach (var regionName in new[] { "Flask 1", "Flask 2", "Flask 3" })
                        {
                            var r = db.equipments.Find(eq => eq != null && eq.Name == regionName);
                            if (r != null) flaskRegions.Add(r);
                        }
                        if (flaskRegions.Count > 0)
                        {
                            this.Region = flaskRegions;
                            return;
                        }
                        searchKeywords.Add("Flask");
                    }

                    // Try to find the region matching any of our keywords (case-insensitive)
                    foreach (var keyword in searchKeywords)
                    {
                        matchingRegion = db.equipments.Find(r => r != null && r.Name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0);
                        if (matchingRegion != null) break;
                    }

                    // Fallback to first if still not found
                    if (matchingRegion == null)
                    {
                        UnityEngine.Debug.LogWarning($"[CurveDashEquipmentAdapter] Could not find matching region for target keywords: {string.Join(", ", searchKeywords)}. Falling back to {db.equipments[0].Name}");
                        matchingRegion = db.equipments[0];
                    }
                    else
                    {
                        UnityEngine.Debug.Log($"[CurveDashEquipmentAdapter] Matched region '{matchingRegion.Name}' for item '{this.Name}' using keywords: {string.Join(", ", searchKeywords)}");
                    }

                    this.Region = new System.Collections.Generic.List<DevionGames.InventorySystem.EquipmentRegion> { matchingRegion };
                }
            }
        }
    }
}
