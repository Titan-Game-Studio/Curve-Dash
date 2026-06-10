using System.Collections.Generic;
using UnityEngine;
using DevionGames.InventorySystem;
using DevionGames.UIWidgets;

namespace STG.CurveDash
{
    /// <summary>
    /// PoE-style crafting logic for re-rolling a bag item's affixes with currency orbs. Pure logic
    /// (no UI): it counts/consumes currency from the Devion "Inventory" container, decides which orb
    /// applies to an item's current rarity, and rolls the new affix set via <see cref="AffixRoller"/>.
    /// <see cref="ItemRollUI"/> drives this and presents the result.
    ///
    /// Only the four crafting orbs are supported; quality/identify currencies are out of scope.
    /// Rolling is restricted to bag items — equipped gear is never rolled (avoids live stat recalc).
    /// </summary>
    public static class ItemRollService
    {
        public enum RollCurrency { Transmutation, Alteration, Chaos, Exalted }

        public struct RollResult
        {
            public bool Success;
            public string Message;
            public ItemRarity Rarity;
            public List<StatModifier> Affixes;
            public CurrencyType ConsumedType;
            public int Remaining;       // currency of that type still owned after the roll
        }

        private const string InventoryName = "Inventory";

        public static CurrencyType ToCurrencyType(RollCurrency c)
        {
            switch (c)
            {
                case RollCurrency.Transmutation: return CurrencyType.OrbOfTransmutation;
                case RollCurrency.Alteration:    return CurrencyType.OrbOfAlteration;
                case RollCurrency.Chaos:         return CurrencyType.ChaosOrb;
                default:                         return CurrencyType.ExaltedOrb;
            }
        }

        public static string DisplayName(RollCurrency c)
        {
            switch (c)
            {
                case RollCurrency.Transmutation: return "Orb of Transmutation";
                case RollCurrency.Alteration:    return "Orb of Alteration";
                case RollCurrency.Chaos:         return "Chaos Orb";
                default:                         return "Exalted Orb";
            }
        }

        public static string EffectText(RollCurrency c)
        {
            switch (c)
            {
                case RollCurrency.Transmutation: return "Normal → Magic (roll magic mods)";
                case RollCurrency.Alteration:    return "Reroll a Magic item's mods";
                case RollCurrency.Chaos:         return "Reroll a Rare item's mods";
                default:                         return "Add one mod to a Rare item";
            }
        }

        /// <summary>True only for gear that carries affixes — weapons, off-hands, armour and accessories.</summary>
        public static bool CanRoll(Item item)
        {
            if (!(item is CurveDashEquipmentAdapter adapter) || adapter.OriginalEquipmentData == null)
                return false;
            switch (adapter.OriginalEquipmentData)
            {
                case WeaponData _:
                case OffHandData _:
                case ArmorItemData _:
                case RingItemData _:
                case AmuletItemData _:
                case BeltItemData _:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>How many of a currency the player currently holds (summed stacks in the bag).</summary>
        public static int CountCurrency(CurrencyType type)
        {
            int total = 0;
            foreach (var container in WidgetUtility.FindAll<ItemContainer>(InventoryName))
            {
                if (container == null) continue;
                foreach (var slot in container.Slots)
                {
                    if (slot == null || slot.IsEmpty || slot.ObservedItem == null) continue;
                    if (slot.ObservedItem is CurveDashItemAdapter cda
                        && cda.OriginalItemData is CurrencyItemData currency
                        && currency.CurrencyType == type)
                    {
                        total += Mathf.Max(1, slot.ObservedItem.Stack);
                    }
                }
            }
            return total;
        }

        /// <summary>Whether a given orb may be used on this item right now, with a reason if not.</summary>
        public static bool IsEligible(CurveDashEquipmentAdapter item, RollCurrency c, out string reason)
        {
            reason = string.Empty;
            if (item == null) { reason = "No item"; return false; }

            if (CountCurrency(ToCurrencyType(c)) <= 0) { reason = "None owned"; return false; }

            var rarity = item.EffectiveRarity;
            switch (c)
            {
                case RollCurrency.Transmutation:
                    if (rarity != ItemRarity.Normal) { reason = "Needs a Normal item"; return false; }
                    return true;
                case RollCurrency.Alteration:
                    if (rarity != ItemRarity.Magic) { reason = "Needs a Magic item"; return false; }
                    return true;
                case RollCurrency.Chaos:
                    if (rarity != ItemRarity.Rare) { reason = "Needs a Rare item"; return false; }
                    return true;
                case RollCurrency.Exalted:
                    if (rarity != ItemRarity.Rare) { reason = "Needs a Rare item"; return false; }
                    if (!HasOpenAffixSlot(item)) { reason = "No open mod"; return false; }
                    return true;
            }
            return false;
        }

        // PoE Rare cap is 6 affix groups (3 prefixes + 3 suffixes); Exalted needs at least one open.
        private static bool HasOpenAffixSlot(CurveDashEquipmentAdapter item)
        {
            if (item.RolledAffixes == null) return true;
            var groups = new HashSet<string>();
            foreach (var m in item.RolledAffixes)
                if (m != null && !string.IsNullOrEmpty(m.AffixName)) groups.Add(m.AffixName);
            return groups.Count < 6;
        }

        /// <summary>Performs the roll: consumes one orb, rolls the new affixes/rarity, applies and saves.</summary>
        public static RollResult Roll(CurveDashEquipmentAdapter item, RollCurrency c)
        {
            var type = ToCurrencyType(c);
            if (item == null || item.OriginalEquipmentData == null)
                return Fail("Invalid item", type);

            if (!IsEligible(item, c, out string reason))
                return Fail(reason, type);

            var data = item.OriginalEquipmentData;
            int iLvl = Mathf.Max(item.RolledItemLevel, Mathf.Max(data.ItemLevel, 1));

            ItemRarity newRarity = item.EffectiveRarity;
            List<StatModifier> newAffixes;

            switch (c)
            {
                case RollCurrency.Transmutation:
                    newRarity = ItemRarity.Magic;
                    newAffixes = AffixRoller.RollFor(data, iLvl, ItemRarity.Magic);
                    break;
                case RollCurrency.Alteration:
                    newRarity = ItemRarity.Magic;
                    newAffixes = AffixRoller.RollFor(data, iLvl, ItemRarity.Magic);
                    break;
                case RollCurrency.Chaos:
                    newRarity = ItemRarity.Rare;
                    newAffixes = AffixRoller.RollFor(data, iLvl, ItemRarity.Rare);
                    break;
                default: // Exalted: keep existing mods, append one more
                    newRarity = ItemRarity.Rare;
                    newAffixes = item.RolledAffixes != null
                        ? new List<StatModifier>(item.RolledAffixes)
                        : new List<StatModifier>();
                    var added = AffixRoller.RollAdditionalAffix(data, iLvl, newAffixes);
                    if (added.Count == 0) return Fail("No mod could be added", type);
                    newAffixes.AddRange(added);
                    break;
            }

            // Consume exactly one orb before applying the result.
            var currencyItem = FindCurrencyItem(type);
            if (currencyItem == null) return Fail("None owned", type);
            ItemContainer.RemoveItem(InventoryName, currencyItem, 1);

            // Apply to the item (rebuilds the Devion stat properties used by the tooltip / equip path).
            item.RolledRarity = newRarity;
            item.SyncAffixes(newAffixes);

            InventoryManager.Save();

            return new RollResult
            {
                Success = true,
                Message = string.Empty,
                Rarity = newRarity,
                Affixes = newAffixes,
                ConsumedType = type,
                Remaining = CountCurrency(type),
            };
        }

        /// <summary>Icon of a currency the player owns (from the first matching bag item), else null.</summary>
        public static Sprite GetCurrencyIcon(CurrencyType type)
        {
            var item = FindCurrencyItem(type);
            return item != null ? item.Icon : null;
        }

        // First live currency item instance of the given type in the bag (for consumption).
        private static Item FindCurrencyItem(CurrencyType type)
        {
            foreach (var container in WidgetUtility.FindAll<ItemContainer>(InventoryName))
            {
                if (container == null) continue;
                foreach (var slot in container.Slots)
                {
                    if (slot == null || slot.IsEmpty || slot.ObservedItem == null) continue;
                    if (slot.ObservedItem is CurveDashItemAdapter cda
                        && cda.OriginalItemData is CurrencyItemData currency
                        && currency.CurrencyType == type)
                    {
                        return slot.ObservedItem;
                    }
                }
            }
            return null;
        }

        private static RollResult Fail(string message, CurrencyType type) => new RollResult
        {
            Success = false,
            Message = message,
            Affixes = new List<StatModifier>(),
            ConsumedType = type,
            Remaining = CountCurrency(type),
        };
    }
}
