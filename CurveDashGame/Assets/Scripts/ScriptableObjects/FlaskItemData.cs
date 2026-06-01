using UnityEngine;

namespace STG.CurveDash
{
    public enum FlaskType
    {
        Life,
        Mana,
        Utility
    }

    public enum FlaskAutoUseCondition
    {
        None,
        WhenFullCharges,
        WhenHitRareOrUnique,
        WhenInjured,        // Health drops below a threshold
        WhenManaLow         // Mana drops below a threshold
    }

    [CreateAssetMenu(fileName = "New Flask Item", menuName = "Curve-Dash/Items/Flask")]
    public class FlaskItemData : ItemData
    {
        [Header("Auto-Use (POE Style)")]
        public FlaskAutoUseCondition AutoUseCondition = FlaskAutoUseCondition.None;
        [Header("Flask Recoveries")]
        public FlaskType FlaskType;
        public float RecoveryAmount = 50f;
        public float Duration = 5.0f;
        public int MaxCharges = 60;
        public int ChargesUsedPerUse = 20;
        
        [Header("Charge Refill")]
        [Tooltip("Charges restored each time the player kills an enemy (POE-style).")]
        public int ChargesGainedOnKill = 3;

        [Header("Utility Buffs (Optional)")]
        public float SpeedModifier = 1.0f;
        public float AttackSpeedModifier = 1.0f;

        private void Reset()
        {
            Type = ItemType.Flask;
        }
    }
}
