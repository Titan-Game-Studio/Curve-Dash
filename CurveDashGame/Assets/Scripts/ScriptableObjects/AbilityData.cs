using UnityEngine;

namespace STG.CurveDash
{
    public abstract class AbilityData : ScriptableObject
    {
        public string AbilityName;
        public float Cooldown;
        public float ManaCost; // If we add mana later
        
        // This will be called by the CombatSystem
        public abstract void Execute(GameObject user, Vector3 targetPosition);
    }
}
