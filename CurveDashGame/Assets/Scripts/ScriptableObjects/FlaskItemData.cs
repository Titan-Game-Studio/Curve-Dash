using System.Collections.Generic;
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

        [Header("Active-Effect Buffs (data-driven)")]
        [Tooltip("Stat modifiers granted ONLY while this flask's effect is active. Reuses the shared StatType " +
                 "system so ANY stat works with no new code: sheet stats (Armor, Resistances, Crit, Life Regen, …) " +
                 "route to the character sheet via AffixStatMapper; Movement Speed (AddedMovementSpeed) and Attack " +
                 "Speed (IncreasedAttackSpeed) are read by the movement/combat systems. Add entries here to define " +
                 "new flask effects — e.g. Granite = AddedArmour 1000, Quicksilver = AddedMovementSpeed 40.")]
        public List<StatModifier> Buffs = new List<StatModifier>();

        private void Reset()
        {
            Type = ItemType.Flask;
        }
    }
}
