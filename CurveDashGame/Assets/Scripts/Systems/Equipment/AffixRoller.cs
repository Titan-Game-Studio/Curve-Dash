using System.Collections.Generic;
using UnityEngine;

namespace STG.CurveDash
{
    /// <summary>
    /// Bounded-random affix generation (PoE-style). Given a template pool, the item's tags and item
    /// level, and how many prefixes/suffixes to roll, it builds a weighted pool of eligible
    /// (affix, tier) pairs and picks from it without repeating an affix. Because every tier carries
    /// its own item-level gate and spawn weight, the result is varied yet balanced by construction.
    /// </summary>
    public static class AffixRoller
    {
        /// <summary>Loads every AffixData template (editor: AssetDatabase; runtime: Resources).</summary>
        public static List<AffixData> LoadAllTemplates()
        {
            var templates = new List<AffixData>();
#if UNITY_EDITOR
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:AffixData"))
            {
                var affix = UnityEditor.AssetDatabase.LoadAssetAtPath<AffixData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (affix != null) templates.Add(affix);
            }
#endif
            if (templates.Count == 0)
                templates.AddRange(Resources.LoadAll<AffixData>(""));
            return templates;
        }

        /// <summary>Number of prefixes/suffixes to roll for a given rarity (Normal = none).</summary>
        public static void GetAffixCounts(ItemRarity rarity, out int numPrefixes, out int numSuffixes)
        {
            numPrefixes = 0;
            numSuffixes = 0;
            switch (rarity)
            {
                case ItemRarity.Magic:
                    numPrefixes = Random.Range(0, 2);
                    numSuffixes = Random.Range(0, 2);
                    if (numPrefixes == 0 && numSuffixes == 0)
                    {
                        if (Random.value > 0.5f) numPrefixes = 1;
                        else numSuffixes = 1;
                    }
                    break;
                case ItemRarity.Rare:
                    numPrefixes = Random.Range(1, 4);
                    numSuffixes = Random.Range(1, 4);
                    break;
                case ItemRarity.Unique:
                    numPrefixes = Random.Range(0, 2);
                    numSuffixes = Random.Range(0, 2);
                    break;
            }
        }

        /// <summary>Convenience: rolls a full affix set for any item at the given level and rarity.</summary>
        public static List<StatModifier> RollFor(ItemData item, int itemLevel, ItemRarity rarity)
        {
            if (item == null || rarity == ItemRarity.Normal) return new List<StatModifier>();
            var templates = LoadAllTemplates();
            GetAffixCounts(rarity, out int numPrefixes, out int numSuffixes);
            return RollAffixes(templates, item.GetAffixTags(), itemLevel, numPrefixes, numSuffixes);
        }

        public static List<StatModifier> RollAffixes(
            IReadOnlyList<AffixData> templates,
            IReadOnlyList<ItemTag> itemTags,
            int itemLevel,
            int numPrefixes,
            int numSuffixes)
        {
            var result = new List<StatModifier>();
            if (templates == null || templates.Count == 0) return result;

            var usedGroups = new HashSet<string>();
            float targetPower = 0f; // sum of each rolled tier's MIDPOINT power (the "expected" total)
            float actualPower = 0f; // sum of the actually-rolled values' power
            RollGroup(templates, itemTags, itemLevel, AffixType.Prefix, numPrefixes, usedGroups, result, ref targetPower, ref actualPower);
            RollGroup(templates, itemTags, itemLevel, AffixType.Suffix, numSuffixes, usedGroups, result, ref targetPower, ref actualPower);

            NormalizeToBudget(result, targetPower, actualPower);
            return result;
        }

        // --- Power budget (Step 4) --------------------------------------------------------------
        // Keeps same-iLvl/same-composition items at ~equal total power by nudging the rolled values
        // toward the tier-midpoint total. A band leaves moderate variety intact and only reins in
        // unusually lucky/unlucky rolls. Toggle off to get raw uniform rolls.
        public static bool PowerBudgetEnabled = true;
        private const float MinBand = 0.90f; // weak rolls boosted up to 90% of the expected total
        private const float MaxBand = 1.10f; // lucky rolls capped at 110% of the expected total

        private static void NormalizeToBudget(List<StatModifier> result, float targetPower, float actualPower)
        {
            if (!PowerBudgetEnabled || result.Count == 0) return;
            if (targetPower <= 0.0001f || actualPower <= 0.0001f) return;

            float ratio = actualPower / targetPower;              // how far this roll is from expected
            float clamped = Mathf.Clamp(ratio, MinBand, MaxBand); // allowed deviation band
            float factor = clamped / ratio;                       // scale needed to land inside the band
            if (Mathf.Abs(factor - 1f) <= 0.0005f) return;        // already in-band — keep the variety

            foreach (var mod in result)
                mod.Value = RoundForStat(mod.Type, mod.Value * factor);
        }

        private static float RoundForStat(StatType stat, float value)
        {
            // Mirror AffixData.RollTier: integer-only stats stay integers after scaling.
            if (stat == StatType.IncreasedPhysicalDamage || stat == StatType.IncreasedAttackSpeed)
                return Mathf.Round(value);
            return value;
        }

        private static void RollGroup(
            IReadOnlyList<AffixData> templates,
            IReadOnlyList<ItemTag> itemTags,
            int itemLevel,
            AffixType type,
            int count,
            HashSet<string> usedGroups,
            List<StatModifier> result,
            ref float targetPower,
            ref float actualPower)
        {
            var entries = new List<(AffixData affix, AffixTier tier, int weight)>();

            for (int picked = 0; picked < count; picked++)
            {
                // Rebuild the eligible pool each pick so already-used mod groups are excluded.
                entries.Clear();
                int totalWeight = 0;

                foreach (var affix in templates)
                {
                    if (affix == null || affix.TypeOfAffix != type) continue;
                    if (usedGroups.Contains(affix.GroupKey)) continue;
                    if (!affix.CanRollOn(itemTags)) continue;

                    foreach (var tier in affix.GetEligibleTiers(itemLevel))
                    {
                        if (tier == null || tier.Weight <= 0) continue;
                        entries.Add((affix, tier, tier.Weight));
                        totalWeight += tier.Weight;
                    }
                }

                if (entries.Count == 0 || totalWeight <= 0) break; // pool exhausted

                int roll = Random.Range(0, totalWeight);
                int acc = 0;
                foreach (var e in entries)
                {
                    acc += e.weight;
                    if (roll < acc)
                    {
                        // A hybrid affix yields several modifiers but occupies one prefix/suffix slot
                        // (shared GroupKey), and its power sums across every stat it grants.
                        var mods = e.affix.RollTierMods(e.tier);
                        result.AddRange(mods);
                        usedGroups.Add(e.affix.GroupKey);

                        foreach (var (stat, midpoint) in e.affix.TierMidpoints(e.tier))
                            targetPower += StatPowerTable.Power(stat, midpoint);
                        foreach (var m in mods)
                            actualPower += StatPowerTable.Power(m.Type, m.Value);
                        break;
                    }
                }
            }
        }
    }
}
