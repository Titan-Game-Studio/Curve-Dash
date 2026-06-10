using UnityEngine;

namespace STG.CurveDash
{
    /// <summary>
    /// Central, reusable formatting for <see cref="StatType"/> → human-readable text. Keep ALL stat display
    /// strings here so tooltips, the character sheet and item descriptions stay consistent and a new stat is
    /// labelled in exactly one place.
    /// </summary>
    public static class StatTypeFormatter
    {
        /// <summary>Readable name of the stat, e.g. <c>AddedArmour → "Armour"</c>.</summary>
        public static string DisplayName(StatType type)
        {
            switch (type)
            {
                case StatType.AddedPhysicalDamage:      return "Physical Damage";
                case StatType.AddedFireDamage:          return "Fire Damage";
                case StatType.AddedColdDamage:          return "Cold Damage";
                case StatType.IncreasedPhysicalDamage:  return "Physical Damage";
                case StatType.IncreasedAttackSpeed:     return "Attack Speed";
                case StatType.IncreasedCriticalChance:  return "Critical Strike Chance";
                case StatType.LifeStealPercentage:      return "Life Leech";
                case StatType.KnockbackForce:           return "Knockback";
                case StatType.AddedLife:                return "Maximum Life";
                case StatType.AddedMana:                return "Maximum Mana";
                case StatType.AddedArmour:              return "Armour";
                case StatType.AddedEvasion:             return "Evasion Rating";
                case StatType.AddedAccuracy:            return "Accuracy Rating";
                case StatType.AddedStrength:            return "Strength";
                case StatType.AddedDexterity:           return "Dexterity";
                case StatType.AddedIntelligence:        return "Intelligence";
                case StatType.AddedCriticalMultiplier:  return "Critical Strike Multiplier";
                case StatType.AddedMovementSpeed:       return "Movement Speed";
                case StatType.AddedLifeRegen:           return "Life Regeneration";
                case StatType.AddedBlockChance:         return "Block Chance";
                case StatType.AddedFireResistance:      return "Fire Resistance";
                case StatType.AddedColdResistance:      return "Cold Resistance";
                case StatType.AddedLightningResistance: return "Lightning Resistance";
                case StatType.AddedChaosResistance:     return "Chaos Resistance";
                case StatType.AddedAllResistances:      return "All Elemental Resistances";
                default:                                return type.ToString();
            }
        }

        /// <summary>True when the stat's value is a percentage (shown with a trailing %).</summary>
        public static bool IsPercent(StatType type)
        {
            switch (type)
            {
                case StatType.IncreasedPhysicalDamage:
                case StatType.IncreasedAttackSpeed:
                case StatType.IncreasedCriticalChance:
                case StatType.LifeStealPercentage:
                case StatType.AddedCriticalMultiplier:
                case StatType.AddedMovementSpeed:
                case StatType.AddedBlockChance:
                case StatType.AddedFireResistance:
                case StatType.AddedColdResistance:
                case StatType.AddedLightningResistance:
                case StatType.AddedChaosResistance:
                case StatType.AddedAllResistances:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Signed value with its unit, e.g. <c>"+40%"</c>, <c>"+1000"</c>, <c>"-25%"</c>.</summary>
        public static string FormatValue(StatType type, float value)
        {
            string num = Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.##");
            string signed = value >= 0f ? "+" + num : num; // negatives already carry '-'
            return IsPercent(type) ? signed + "%" : signed;
        }

        /// <summary>One readable line, e.g. <c>"+40% Movement Speed"</c> or <c>"+1000 Armour"</c>.</summary>
        public static string Describe(StatType type, float value)
        {
            return FormatValue(type, value) + " " + DisplayName(type);
        }
    }
}
