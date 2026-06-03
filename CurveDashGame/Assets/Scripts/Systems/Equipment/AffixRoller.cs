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
        public static List<StatModifier> RollAffixes(
            IReadOnlyList<AffixData> templates,
            IReadOnlyList<ItemTag> itemTags,
            int itemLevel,
            int numPrefixes,
            int numSuffixes)
        {
            var result = new List<StatModifier>();
            if (templates == null || templates.Count == 0) return result;

            var usedNames = new HashSet<string>();
            RollGroup(templates, itemTags, itemLevel, AffixType.Prefix, numPrefixes, usedNames, result);
            RollGroup(templates, itemTags, itemLevel, AffixType.Suffix, numSuffixes, usedNames, result);
            return result;
        }

        private static void RollGroup(
            IReadOnlyList<AffixData> templates,
            IReadOnlyList<ItemTag> itemTags,
            int itemLevel,
            AffixType type,
            int count,
            HashSet<string> usedNames,
            List<StatModifier> result)
        {
            var entries = new List<(AffixData affix, AffixTier tier, int weight)>();

            for (int picked = 0; picked < count; picked++)
            {
                // Rebuild the eligible pool each pick so already-used affixes are excluded.
                entries.Clear();
                int totalWeight = 0;

                foreach (var affix in templates)
                {
                    if (affix == null || affix.TypeOfAffix != type) continue;
                    if (usedNames.Contains(affix.AffixName)) continue;
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
                        result.Add(e.affix.RollTier(e.tier));
                        usedNames.Add(e.affix.AffixName);
                        break;
                    }
                }
            }
        }
    }
}
