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
        };

        public static IReadOnlyList<StatMapping> GetMappings(StatType statType)
        {
            return Map.TryGetValue(statType, out var result)
                ? result
                : System.Array.Empty<StatMapping>();
        }
    }
}
