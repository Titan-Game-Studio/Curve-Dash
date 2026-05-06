using System.Collections.Generic;
using UnityEngine;

namespace STG.CurveDash
{
    public abstract class WeaponData : EquippableData
    {
        [Header("Base Stats (White Item)")]
        public float BaseMinDamage = 10f;
        public float BaseMaxDamage = 15f;
        public float BaseAttackSpeed = 1.0f; // Số đòn đánh mỗi giây
        public float BaseAttackRange = 2f;

        [Header("Sockets")]
        public List<AbilityData> Abilities = new List<AbilityData>();
        public virtual int MaxSockets => 3;

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (Abilities != null && Abilities.Count > MaxSockets)
            {
                Abilities.RemoveRange(MaxSockets, Abilities.Count - MaxSockets);
            }
        }
#endif
    }
}
