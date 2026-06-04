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

    /// <summary>A simple inclusive value band, used for a hybrid affix's extra stats.</summary>
    [System.Serializable]
    public class ValueRange
    {
        public float MinValue;
        public float MaxValue;
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

        [Tooltip("Hybrid affixes only: one band per AffixData.ExtraStats entry (aligned by index). " +
                 "Leave empty for single-stat affixes.")]
        public List<ValueRange> ExtraRanges = new List<ValueRange>();
    }

    [CreateAssetMenu(fileName = "NewAffix", menuName = "Curve-Dash/Items/Affix Template")]
    public class AffixData : ScriptableObject
    {
        public string AffixName;
        public AffixType TypeOfAffix; // Tiền tố hay Hậu tố
        public StatType Stat;

        [Header("Mod Group (mutual exclusion)")]
        [Tooltip("Affixes sharing a non-empty ModGroup are mutually exclusive on one item (PoE 'mod groups' — " +
                 "e.g. two different +Life affixes can't co-exist). Leave EMPTY to group by AffixName " +
                 "(each affix only excludes itself, matching legacy behaviour).")]
        public string ModGroup;

        [Header("Hybrid (extra stats)")]
        [Tooltip("Additional stats granted alongside the primary Stat (e.g. +Life AND +Mana). " +
                 "Each tier supplies a matching ExtraRanges band by index. Leave EMPTY for single-stat affixes.")]
        public List<StatType> ExtraStats = new List<StatType>();

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

        /// <summary>
        /// Key used to decide which affixes are mutually exclusive on one item. Defaults to the affix
        /// name (legacy: each affix only blocks itself) unless an explicit ModGroup is set.
        /// </summary>
        public string GroupKey => string.IsNullOrEmpty(ModGroup) ? AffixName : ModGroup;

        /// <summary>Integer-only stats stay integers; everything else keeps its fractional roll.</summary>
        private static float RollValue(StatType stat, float min, float max)
        {
            float v = Random.Range(min, max);
            if (stat == StatType.IncreasedPhysicalDamage || stat == StatType.IncreasedAttackSpeed)
                v = Mathf.Round(v);
            return v;
        }

        /// <summary>Resolves the (min,max) band for the i-th stat: -1 = primary, otherwise an extra stat.</summary>
        private void GetBand(AffixTier tier, int extraIndex, out float min, out float max)
        {
            if (extraIndex < 0 || ExtraStats == null || extraIndex >= ExtraStats.Count
                || tier.ExtraRanges == null || extraIndex >= tier.ExtraRanges.Count
                || tier.ExtraRanges[extraIndex] == null)
            {
                // Primary band, or an extra stat with no authored range → reuse the primary band.
                min = tier.MinValue;
                max = tier.MaxValue;
                return;
            }
            min = tier.ExtraRanges[extraIndex].MinValue;
            max = tier.ExtraRanges[extraIndex].MaxValue;
        }

        /// <summary>
        /// Rolls every StatModifier this affix grants from a specific tier: the primary Stat plus any
        /// hybrid ExtraStats. All modifiers share this affix's name so they count as one prefix/suffix.
        /// </summary>
        public List<StatModifier> RollTierMods(AffixTier tier)
        {
            var mods = new List<StatModifier>();
            GetBand(tier, -1, out float pMin, out float pMax);
            mods.Add(new StatModifier(Stat, RollValue(Stat, pMin, pMax), AffixName, TypeOfAffix));

            if (ExtraStats != null)
            {
                for (int i = 0; i < ExtraStats.Count; i++)
                {
                    GetBand(tier, i, out float min, out float max);
                    mods.Add(new StatModifier(ExtraStats[i], RollValue(ExtraStats[i], min, max), AffixName, TypeOfAffix));
                }
            }
            return mods;
        }

        /// <summary>(stat, midpoint) for every stat this affix grants on a tier — used by the power budget.</summary>
        public IEnumerable<(StatType stat, float midpoint)> TierMidpoints(AffixTier tier)
        {
            GetBand(tier, -1, out float pMin, out float pMax);
            yield return (Stat, (pMin + pMax) * 0.5f);

            if (ExtraStats != null)
            {
                for (int i = 0; i < ExtraStats.Count; i++)
                {
                    GetBand(tier, i, out float min, out float max);
                    yield return (ExtraStats[i], (min + max) * 0.5f);
                }
            }
        }

        /// <summary>Rolls only the primary StatModifier from a tier. Kept for legacy single-stat callers.</summary>
        public StatModifier RollTier(AffixTier tier)
        {
            GetBand(tier, -1, out float min, out float max);
            return new StatModifier(Stat, RollValue(Stat, min, max), AffixName, TypeOfAffix);
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
