using UnityEngine;

namespace STG.CurveDash
{
    public enum FlaskType
    {
        Life,
        Mana,
        Utility
    }

    [CreateAssetMenu(fileName = "New Flask Item", menuName = "Curve-Dash/Items/Flask")]
    public class FlaskItemData : ItemData
    {
        [Header("Flask Recoveries")]
        public FlaskType FlaskType;
        public float RecoveryAmount = 50f;
        public float Duration = 5.0f;
        public int MaxCharges = 60;
        public int ChargesUsedPerUse = 20;
        
        [Header("Utility Buffs (Optional)")]
        public float SpeedModifier = 1.0f; // e.g. 1.4f for Quicksilver Flask (40% speed boost)
        public float AttackSpeedModifier = 1.0f;

        private void Reset()
        {
            Type = ItemType.Flask;
        }
    }
}
