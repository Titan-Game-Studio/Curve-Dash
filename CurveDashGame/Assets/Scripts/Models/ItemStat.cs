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
        KnockbackForce
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
