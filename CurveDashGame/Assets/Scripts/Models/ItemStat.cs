namespace STG.CurveDash
{
    public enum StatType
    {
        AddedPhysicalDamage,
        AddedFireDamage,
        AddedColdDamage,
        IncreasedPhysicalDamage,
        IncreasedAttackSpeed,
        IncreasedCriticalChance,
        LifeStealPercentage,
        KnockbackForce,

        // --- Defensive / character-sheet stats (appended; DO NOT reorder — assets store these by index) ---
        AddedLife,
        AddedMana,
        AddedArmour,
        AddedEvasion,
        AddedAccuracy,
        AddedStrength,
        AddedDexterity,
        AddedIntelligence,
        AddedCriticalMultiplier,
        AddedMovementSpeed,
        AddedLifeRegen,
        AddedBlockChance,
        AddedFireResistance,
        AddedColdResistance,
        AddedLightningResistance,
        AddedChaosResistance,
        AddedAllResistances,
    }

    [System.Serializable]
    public class StatModifier
    {
        public StatType Type;
        public float Value;
        
        // Thông tin để hiển thị tên
        public string AffixName;
        public AffixType AffixType;

        public StatModifier(StatType type, float value, string name = "", AffixType affixType = AffixType.Prefix)
        {
            Type = type;
            Value = value;
            AffixName = name;
            AffixType = affixType;
        }
    }
}
