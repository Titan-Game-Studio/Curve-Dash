namespace STG.CurveDash
{
    /// <summary>
    /// Converts a stat value into abstract "power points" so the affix roller can balance how much
    /// total power an item carries. Tune these weights to set how stats trade off against each other
    /// (e.g. 1 point of all-resistance is worth more than 1 point of armour). Power is linear in value,
    /// so scaling a value by k scales its power by k — which is what the budget normalizer relies on.
    /// </summary>
    public static class StatPowerTable
    {
        private static float Weight(StatType type)
        {
            switch (type)
            {
                // Offence
                case StatType.AddedPhysicalDamage:     return 1.0f;
                case StatType.AddedFireDamage:         return 1.0f;
                case StatType.AddedColdDamage:         return 1.0f;
                case StatType.IncreasedPhysicalDamage: return 0.6f;
                case StatType.IncreasedAttackSpeed:    return 1.5f;
                case StatType.IncreasedCriticalChance: return 1.0f;
                case StatType.AddedCriticalMultiplier: return 0.5f;
                case StatType.LifeStealPercentage:     return 3.0f;
                case StatType.KnockbackForce:          return 0.3f;

                // Attributes
                case StatType.AddedStrength:
                case StatType.AddedDexterity:
                case StatType.AddedIntelligence:       return 1.0f;

                // Defence / utility
                case StatType.AddedLife:               return 0.4f;
                case StatType.AddedMana:               return 0.3f;
                case StatType.AddedArmour:             return 0.25f;
                case StatType.AddedEvasion:            return 0.25f;
                case StatType.AddedAccuracy:           return 0.2f;
                case StatType.AddedLifeRegen:          return 1.0f;
                case StatType.AddedBlockChance:        return 1.5f;
                case StatType.AddedMovementSpeed:      return 2.0f;

                // Resistances
                case StatType.AddedFireResistance:
                case StatType.AddedColdResistance:
                case StatType.AddedLightningResistance:
                case StatType.AddedChaosResistance:    return 1.0f;
                case StatType.AddedAllResistances:     return 4.0f; // applies to all four

                default:                               return 1.0f;
            }
        }

        public static float Power(StatType type, float value) => value * Weight(type);
    }
}
