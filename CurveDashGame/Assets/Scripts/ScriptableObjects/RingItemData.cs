using UnityEngine;

namespace STG.CurveDash
{
    // No 3D model needed — equipped in Ring1 or Ring2 slot for stat bonuses only.
    [CreateAssetMenu(fileName = "New Ring", menuName = "Curve-Dash/Items/Ring Data")]
    public class RingItemData : ItemData
    {
        [Header("Slot Assignment")]
        [Tooltip("Which ring slot this item occupies (Ring1 = left, Ring2 = right).")]
        public EquipmentSlot Slot = EquipmentSlot.Ring1;

        [Header("Stats")]
        public int HealthBonus;
        public float AddedFlatDamage;
        public float AttackSpeedBonus;
        public float AllResistances;

        public RingItemData()
        {
            Type = ItemType.Accessory;
        }

        public override System.Collections.Generic.List<StatModifier> GetStatModifiers()
        {
            var list = base.GetStatModifiers();
            if (HealthBonus != 0)     list.Add(new StatModifier(StatType.AddedLife, HealthBonus));
            if (AddedFlatDamage != 0) list.Add(new StatModifier(StatType.AddedPhysicalDamage, AddedFlatDamage));
            if (AllResistances != 0)  list.Add(new StatModifier(StatType.AddedAllResistances, AllResistances));
            // AttackSpeedBonus stays display-only (consumed by CombatSystem, no Devion sheet mapping).
            return list;
        }
    }
}
