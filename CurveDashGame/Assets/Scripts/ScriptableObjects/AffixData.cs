using System.Collections.Generic;
using UnityEngine;

namespace STG.CurveDash
{
    public enum AffixType
    {
        Prefix,
        Suffix
    }

    /// <summary>
    /// Item categories an affix is allowed to spawn on. An affix with an empty AllowedTags list
    /// can roll on ANY item; otherwise it only rolls on items that share at least one tag.
    /// Items declare their tags via <see cref="ItemData.GetAffixTags"/>.
    /// </summary>
    public enum ItemTag
    {
        Weapon,
        Armour,
        Accessory,
        Ring,
        Amulet,
        Belt,
        Flask,
    }

    /// <summary>
    /// One value band of an affix, gated by item level and weighted for rarity.
    /// Stronger tiers normally require a higher item level and use a lower spawn weight.
    /// </summary>
    [System.Serializable]
    public class AffixTier
    {
        [Tooltip("Optional cosmetic label, e.g. T1 / T2.")]
        public string TierName;

        [Tooltip("Minimum item level required for this tier to be eligible to roll.")]
        public int RequiredItemLevel = 0;

        public float MinValue;
        public float MaxValue;

        [Tooltip("Spawn weight. Higher = more common. Use a lower weight for strong tiers so they stay rare.")]
        public int Weight = 1000;
    }

    [CreateAssetMenu(fileName = "NewAffix", menuName = "Curve-Dash/Items/Affix Template")]
    public class AffixData : ScriptableObject
    {
        public string AffixName;
        public AffixType TypeOfAffix; // Tiền tố hay Hậu tố
        public StatType Stat;

        [Header("Spawn Restriction")]
        [Tooltip("Item categories this affix may roll on. Leave EMPTY to allow any item.")]
        public List<ItemTag> AllowedTags = new List<ItemTag>();

        [Header("Tiers (item-level gated, weighted)")]
        [Tooltip("Each tier is a value band gated by item level with its own spawn weight. " +
                 "If left empty, the legacy Min/Max range below is used as a single open tier.")]
        public List<AffixTier> Tiers = new List<AffixTier>();

        [Header("Legacy Range (used only when Tiers is empty)")]
        public float MinValue;
        public float MaxValue;

        /// <summary>True if this affix may roll on an item carrying any of the given tags.</summary>
        public bool CanRollOn(IReadOnlyList<ItemTag> itemTags)
        {
            if (AllowedTags == null || AllowedTags.Count == 0) return true; // unrestricted
            if (itemTags == null) return false;
            for (int i = 0; i < itemTags.Count; i++)
                if (AllowedTags.Contains(itemTags[i])) return true;
            return false;
        }

        /// <summary>
        /// Tiers whose RequiredItemLevel &lt;= itemLevel. When no tiers are authored, yields a single
        /// synthesized tier from the legacy Min/Max range so existing affix assets keep working.
        /// </summary>
        public IEnumerable<AffixTier> GetEligibleTiers(int itemLevel)
        {
            if (Tiers != null && Tiers.Count > 0)
            {
                foreach (var t in Tiers)
                    if (t != null && t.RequiredItemLevel <= itemLevel)
                        yield return t;
            }
            else
            {
                yield return new AffixTier
                {
                    RequiredItemLevel = 0,
                    MinValue = MinValue,
                    MaxValue = MaxValue,
                    Weight = 1000,
                };
            }
        }

        /// <summary>Rolls a concrete StatModifier from a specific tier's value band.</summary>
        public StatModifier RollTier(AffixTier tier)
        {
            float rolled = Random.Range(tier.MinValue, tier.MaxValue);
            if (Stat == StatType.IncreasedPhysicalDamage || Stat == StatType.IncreasedAttackSpeed)
                rolled = Mathf.Round(rolled);
            return new StatModifier(Stat, rolled, AffixName, TypeOfAffix);
        }

        /// <summary>Legacy uniform roll over the legacy Min/Max range. Kept for older callers.</summary>
        public StatModifier Roll()
        {
            float rolledValue = Random.Range(MinValue, MaxValue);
            if (Stat == StatType.IncreasedPhysicalDamage || Stat == StatType.IncreasedAttackSpeed)
            {
                rolledValue = Mathf.Round(rolledValue);
            }
            return new StatModifier(Stat, rolledValue, AffixName, TypeOfAffix);
        }
    }
}
