using UnityEngine;

namespace STG.CurveDash
{
    public abstract class AbilityData : ScriptableObject
    {
        public string AbilityName;
        public Sprite Icon;
        public float Cooldown;
        public float ManaCost; // If we add mana later

        [Header("Ability Animation Overrides")]
        [Tooltip("Optional: If specified, this trigger will be set on the animator instead of the weapon's default 'Attack' trigger.")]
        public string AnimationTriggerName;
        
        // This will be called by the CombatSystem
        public abstract void Execute(GameObject user, Vector3 targetPosition);
    }
}
