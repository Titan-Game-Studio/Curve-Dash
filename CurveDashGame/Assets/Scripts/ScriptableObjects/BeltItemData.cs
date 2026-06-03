using UnityEngine;
using System.Collections.Generic;

namespace STG.CurveDash
{
    [CreateAssetMenu(fileName = "New Belt", menuName = "Curve-Dash/Items/Belt Data")]
    public class BeltItemData : ItemData
    {
        [Header("Visual (Uses Legs/Pants modular part from GanzSe)")]
        public int ModularPartIndex;
        public string MeshPartName;

        [Header("Stats")]
        public int HealthBonus;
        public int LifeRegeneration;

        [Header("Flask Slots (Max 3, Auto-Use like POE)")]
        [Tooltip("Up to 3 flasks. Each auto-uses based on its AutoUseCondition.")]
        public List<FlaskItemData> FlaskSlots = new List<FlaskItemData>();

        public int MaxFlaskSlots => 3;

        public BeltItemData()
        {
            Type = ItemType.Accessory;
        }

        public override List<StatModifier> GetStatModifiers()
        {
            var list = base.GetStatModifiers();
            if (HealthBonus != 0)      list.Add(new StatModifier(StatType.AddedLife, HealthBonus));
            if (LifeRegeneration != 0) list.Add(new StatModifier(StatType.AddedLifeRegen, LifeRegeneration));
            return list;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (FlaskSlots != null && FlaskSlots.Count > MaxFlaskSlots)
                FlaskSlots.RemoveRange(MaxFlaskSlots, FlaskSlots.Count - MaxFlaskSlots);
        }
#endif
    }
}
