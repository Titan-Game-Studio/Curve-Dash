using UnityEngine;

namespace STG.CurveDash
{
    public enum AffixType
    {
        Prefix,
        Suffix
    }

    [CreateAssetMenu(fileName = "NewAffix", menuName = "Curve Dash/Items/Affix Template")]
    public class AffixData : ScriptableObject
    {
        public string AffixName; 
        public AffixType TypeOfAffix; // Tiền tố hay Hậu tố
        public StatType Stat;
        
        [Header("Roll Range")]
        public float MinValue;
        public float MaxValue;

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
