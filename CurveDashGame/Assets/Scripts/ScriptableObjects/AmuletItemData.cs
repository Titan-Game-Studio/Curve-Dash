using UnityEngine;

namespace STG.CurveDash
{
    // No 3D model needed — equipped in the Amulet slot for stat bonuses only.
    [CreateAssetMenu(fileName = "New Amulet", menuName = "Curve-Dash/Items/Amulet Data")]
    public class AmuletItemData : ItemData
    {
        [Header("Stats")]
        public int HealthBonus;
        public int ManaBonus;
        public float CritChanceBonus;
        public float AllResistances;

        public AmuletItemData()
        {
            Type = ItemType.Accessory;
        }

        public override System.Collections.Generic.List<StatModifier> GetStatModifiers()
        {
            var list = base.GetStatModifiers();
            if (HealthBonus != 0)     list.Add(new StatModifier(StatType.AddedLife, HealthBonus));
            if (ManaBonus != 0)       list.Add(new StatModifier(StatType.AddedMana, ManaBonus));
            if (CritChanceBonus != 0) list.Add(new StatModifier(StatType.IncreasedCriticalChance, CritChanceBonus));
            if (AllResistances != 0)  list.Add(new StatModifier(StatType.AddedAllResistances, AllResistances));
            return list;
        }
    }
}
