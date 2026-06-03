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

        [Header("Animation Synchronization")]
        [Tooltip("If true, the weapon will wait for an 'OnAttackHit' Unity Animation Event to deal damage/cast skills. If false, it will use the AttackHitDelay timer.")]
        public bool UseAnimationEvent = false;

        [Tooltip("Delay in seconds before the attack actually hits/casts (at 1.0 Attack Speed). Used if UseAnimationEvent is false.")]
        public float AttackHitDelay = 0.3f;

        [Header("Sockets")]
        public List<AbilityData> Abilities = new List<AbilityData>();
        public virtual int MaxSockets => 3;

        public override List<ItemTag> GetAffixTags() => new List<ItemTag> { ItemTag.Weapon };

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
