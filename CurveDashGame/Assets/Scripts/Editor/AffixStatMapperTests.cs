using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace STG.CurveDash.Tests
{
    [TestFixture]
    public class AffixStatMapperTests
    {
        // ─── GetMappings: known mappings ────────────────────────────────────────

        [Test]
        public void AddedPhysicalDamage_MapsTo_MinAndMaxDamage_Flat()
        {
            var mappings = AffixStatMapper.GetMappings(StatType.AddedPhysicalDamage).ToList();

            Assert.AreEqual(2, mappings.Count);
            Assert.IsTrue(mappings.Any(m => m.DevionStatName == "Min Damage" && m.Contribution == AffixStatMapper.ContributionType.FlatAdd));
            Assert.IsTrue(mappings.Any(m => m.DevionStatName == "Max Damage" && m.Contribution == AffixStatMapper.ContributionType.FlatAdd));
        }

        [Test]
        public void AddedFireDamage_MapsTo_MinAndMaxDamage_Flat()
        {
            var mappings = AffixStatMapper.GetMappings(StatType.AddedFireDamage).ToList();

            Assert.AreEqual(2, mappings.Count);
            Assert.IsTrue(mappings.All(m => m.Contribution == AffixStatMapper.ContributionType.FlatAdd));
        }

        [Test]
        public void AddedColdDamage_MapsTo_MinAndMaxDamage_Flat()
        {
            var mappings = AffixStatMapper.GetMappings(StatType.AddedColdDamage).ToList();

            Assert.AreEqual(2, mappings.Count);
            Assert.IsTrue(mappings.All(m => m.Contribution == AffixStatMapper.ContributionType.FlatAdd));
        }

        [Test]
        public void IncreasedPhysicalDamage_MapsTo_MinAndMaxDamage_PercentMult()
        {
            var mappings = AffixStatMapper.GetMappings(StatType.IncreasedPhysicalDamage).ToList();

            Assert.AreEqual(2, mappings.Count);
            Assert.IsTrue(mappings.Any(m => m.DevionStatName == "Min Damage" && m.Contribution == AffixStatMapper.ContributionType.PercentMult));
            Assert.IsTrue(mappings.Any(m => m.DevionStatName == "Max Damage" && m.Contribution == AffixStatMapper.ContributionType.PercentMult));
        }

        [Test]
        public void IncreasedCriticalChance_MapsTo_CriticalStrike_Flat()
        {
            var mappings = AffixStatMapper.GetMappings(StatType.IncreasedCriticalChance).ToList();

            Assert.AreEqual(1, mappings.Count);
            Assert.AreEqual("Critical Strike", mappings[0].DevionStatName);
            Assert.AreEqual(AffixStatMapper.ContributionType.FlatAdd, mappings[0].Contribution);
        }

        // ─── GetMappings: stats intentionally excluded from Devion ──────────────

        [Test]
        public void IncreasedAttackSpeed_ReturnsEmpty_NotMappedToDevion()
        {
            // Attack speed is consumed by CombatSystem via FinalAttackSpeed.
            // Mapping it to "Melee Attack"/"Ranged Attack" would cause EquipmentHandler's
            // PercentAdd modifier to multiply SyncCharacterInfoStats' avgDmg BaseValue.
            var mappings = AffixStatMapper.GetMappings(StatType.IncreasedAttackSpeed).ToList();

            Assert.AreEqual(0, mappings.Count, "IncreasedAttackSpeed must NOT map to any Devion stat to prevent avgDmg double-scaling.");
        }

        [Test]
        public void LifeStealPercentage_ReturnsEmpty()
        {
            var mappings = AffixStatMapper.GetMappings(StatType.LifeStealPercentage).ToList();

            Assert.AreEqual(0, mappings.Count);
        }

        [Test]
        public void KnockbackForce_ReturnsEmpty()
        {
            var mappings = AffixStatMapper.GetMappings(StatType.KnockbackForce).ToList();

            Assert.AreEqual(0, mappings.Count);
        }

        // ─── Accumulation logic (mirrors SyncAffixes internal math) ─────────────

        [Test]
        public void FlatAffixes_Accumulate_Correctly()
        {
            // Sharp (+10 physical) + Burning (+15 fire) → both add to Min/Max Damage flat
            var affixes = new List<StatModifier>
            {
                new StatModifier(StatType.AddedPhysicalDamage, 10f),
                new StatModifier(StatType.AddedFireDamage, 15f),
            };

            float minFlat = SumFlat(affixes, "Min Damage");
            float maxFlat = SumFlat(affixes, "Max Damage");

            Assert.AreEqual(25f, minFlat, 0.001f);
            Assert.AreEqual(25f, maxFlat, 0.001f);
        }

        [Test]
        public void PercentAffix_ComputesFinalValue_Correctly()
        {
            // Glorious (+60% increased physical) on weapon with BaseMinDamage=10
            float baseMin = 10f;
            var affixes = new List<StatModifier>
            {
                new StatModifier(StatType.IncreasedPhysicalDamage, 60f),
            };

            float flatAdd   = SumFlat(affixes, "Min Damage");
            float pctBonus  = SumPercent(affixes, "Min Damage");
            float finalMin  = (baseMin + flatAdd) * (1f + pctBonus / 100f);

            Assert.AreEqual(16f, finalMin, 0.001f);  // 10 * 1.6 = 16
        }

        [Test]
        public void FlatAndPercent_CombineInCorrectOrder()
        {
            // Sharp (+10 physical, flat) + Glorious (+60% physical, percent)
            // Expected: (10_base + 10_flat) * 1.6 = 32
            float baseMin = 10f;
            var affixes = new List<StatModifier>
            {
                new StatModifier(StatType.AddedPhysicalDamage, 10f),
                new StatModifier(StatType.IncreasedPhysicalDamage, 60f),
            };

            float flatAdd  = SumFlat(affixes, "Min Damage");
            float pctBonus = SumPercent(affixes, "Min Damage");
            float finalMin = (baseMin + flatAdd) * (1f + pctBonus / 100f);

            Assert.AreEqual(32f, finalMin, 0.001f);
        }

        [Test]
        public void CriticalChance_SumsCorrectly_AcrossMultipleAffixes()
        {
            var affixes = new List<StatModifier>
            {
                new StatModifier(StatType.IncreasedCriticalChance, 8f),
                new StatModifier(StatType.IncreasedCriticalChance, 5f),
            };

            float critBonus = affixes
                .Where(a => a.Type == StatType.IncreasedCriticalChance)
                .Sum(a => a.Value);

            Assert.AreEqual(13f, critBonus, 0.001f);
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private static float SumFlat(List<StatModifier> affixes, string statName)
        {
            float total = 0f;
            foreach (var a in affixes)
                foreach (var m in AffixStatMapper.GetMappings(a.Type))
                    if (m.DevionStatName == statName && m.Contribution == AffixStatMapper.ContributionType.FlatAdd)
                        total += a.Value;
            return total;
        }

        private static float SumPercent(List<StatModifier> affixes, string statName)
        {
            float total = 0f;
            foreach (var a in affixes)
                foreach (var m in AffixStatMapper.GetMappings(a.Type))
                    if (m.DevionStatName == statName && m.Contribution == AffixStatMapper.ContributionType.PercentMult)
                        total += a.Value;
            return total;
        }
    }
}
