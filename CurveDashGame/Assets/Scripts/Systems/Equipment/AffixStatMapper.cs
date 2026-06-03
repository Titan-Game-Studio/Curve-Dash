using System.Collections.Generic;

namespace STG.CurveDash
{
    public static class AffixStatMapper
    {
        public enum ContributionType
        {
            FlatAdd,
            PercentMult,
        }

        public readonly struct StatMapping
        {
            public readonly string DevionStatName;
            public readonly ContributionType Contribution;

            public StatMapping(string devionStatName, ContributionType contribution)
            {
                DevionStatName = devionStatName;
                Contribution = contribution;
            }
        }

        // Devion stat names must match exactly what is in Curve Dash_Character_Stats StatDatabase.
        private static readonly Dictionary<StatType, StatMapping[]> Map = new Dictionary<StatType, StatMapping[]>
        {
            {
                StatType.AddedPhysicalDamage, new[]
                {
                    new StatMapping("Min Damage", ContributionType.FlatAdd),
                    new StatMapping("Max Damage", ContributionType.FlatAdd),
                }
            },
            {
                StatType.AddedFireDamage, new[]
                {
                    new StatMapping("Min Damage", ContributionType.FlatAdd),
                    new StatMapping("Max Damage", ContributionType.FlatAdd),
                }
            },
            {
                StatType.AddedColdDamage, new[]
                {
                    new StatMapping("Min Damage", ContributionType.FlatAdd),
                    new StatMapping("Max Damage", ContributionType.FlatAdd),
                }
            },
            {
                StatType.IncreasedPhysicalDamage, new[]
                {
                    new StatMapping("Min Damage", ContributionType.PercentMult),
                    new StatMapping("Max Damage", ContributionType.PercentMult),
                }
            },
            // IncreasedAttackSpeed has no corresponding Devion stat — it is consumed by CombatSystem
            // via WeaponInstance.FinalAttackSpeed. Mapping it to "Melee Attack"/"Ranged Attack" would
            // cause EquipmentHandler's PercentAdd modifier to multiply the avgDmg BaseValue set by
            // SyncCharacterInfoStats, incorrectly making attack speed scale damage.
            {
                StatType.IncreasedCriticalChance, new[]
                {
                    new StatMapping("Critical Strike", ContributionType.FlatAdd),
                }
            },
            // LifeStealPercentage and KnockbackForce are handled in ECS combat — no Devion stat mapping.

            // --- Defensive / character-sheet stats (apply to CurveDash_Character_Stats via EquipmentHandler) ---
            { StatType.AddedLife,               new[] { new StatMapping("Heart", ContributionType.FlatAdd) } },
            { StatType.AddedMana,               new[] { new StatMapping("Mana", ContributionType.FlatAdd) } },
            { StatType.AddedArmour,             new[] { new StatMapping("Armor", ContributionType.FlatAdd) } },
            { StatType.AddedEvasion,            new[] { new StatMapping("Evasion Rating", ContributionType.FlatAdd) } },
            { StatType.AddedAccuracy,           new[] { new StatMapping("Accuracy Rating", ContributionType.FlatAdd) } },
            { StatType.AddedStrength,           new[] { new StatMapping("Strength", ContributionType.FlatAdd) } },
            { StatType.AddedDexterity,          new[] { new StatMapping("Dexterity", ContributionType.FlatAdd) } },
            { StatType.AddedIntelligence,       new[] { new StatMapping("Intelligence", ContributionType.FlatAdd) } },
            { StatType.AddedCriticalMultiplier, new[] { new StatMapping("Critical Multiplier", ContributionType.FlatAdd) } },
            { StatType.AddedMovementSpeed,      new[] { new StatMapping("Movement Speed", ContributionType.FlatAdd) } },
            { StatType.AddedLifeRegen,          new[] { new StatMapping("Life Regeneration", ContributionType.FlatAdd) } },
            { StatType.AddedBlockChance,        new[] { new StatMapping("Block Chance", ContributionType.FlatAdd) } },
            { StatType.AddedFireResistance,     new[] { new StatMapping("Fire Resistance", ContributionType.FlatAdd) } },
            { StatType.AddedColdResistance,     new[] { new StatMapping("Cold Resistance", ContributionType.FlatAdd) } },
            { StatType.AddedLightningResistance,new[] { new StatMapping("Lightning Resistance", ContributionType.FlatAdd) } },
            { StatType.AddedChaosResistance,    new[] { new StatMapping("Chaos Resistance", ContributionType.FlatAdd) } },
            {
                StatType.AddedAllResistances, new[]
                {
                    new StatMapping("Fire Resistance", ContributionType.FlatAdd),
                    new StatMapping("Cold Resistance", ContributionType.FlatAdd),
                    new StatMapping("Lightning Resistance", ContributionType.FlatAdd),
                    new StatMapping("Chaos Resistance", ContributionType.FlatAdd),
                }
            },
        };

        public static IReadOnlyList<StatMapping> GetMappings(StatType statType)
        {
            return Map.TryGetValue(statType, out var result)
                ? result
                : System.Array.Empty<StatMapping>();
        }
    }
}
