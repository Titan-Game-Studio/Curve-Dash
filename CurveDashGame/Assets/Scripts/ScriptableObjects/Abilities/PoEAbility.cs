using UnityEngine;

namespace STG.CurveDash
{
    public enum PoEElementType
    {
        Physical,
        Fire,
        Cold,
        Lightning,
        Chaos
    }

    public enum PoEAbilityType
    {
        Melee,
        Spell,
        Ranged,
        Aura
    }

    [CreateAssetMenu(fileName = "NewPoEAbility", menuName = "Curve Dash/Abilities/PoE Skill")]
    public class PoEAbility : AbilityData
    {
        [Header("PoE Skill Settings")]
        public PoEElementType Element;
        public PoEAbilityType SkillType;
        
        [TextArea]
        public string SkillDescription;
        
        [Header("Offensive Stats")]
        public float DamageMultiplier = 1.0f;
        public float AddedFlatDamage = 0f;
        public float CriticalChanceBonus = 5.0f; // in percent
        
        [Header("Projectile/AOE Settings")]
        public float AttackRange = 2.0f;
        public int ProjectileCount = 1;
        public float Speed = 10f;
        
        [Header("Visual Effects")]
        public GameObject CastVFX;
        public GameObject ImpactVFX;

        public override void Execute(GameObject user, Vector3 targetPosition)
        {
            Debug.Log($"[PoE Ability] Casted {AbilityName} ({SkillType} - {Element})! Cooldown: {Cooldown}s | Damage Multiplier: {DamageMultiplier * 100}%");
            
            if (CastVFX != null)
            {
                Instantiate(CastVFX, user.transform.position + Vector3.up * 1.0f, Quaternion.identity);
            }
            
            if (ImpactVFX != null)
            {
                Instantiate(ImpactVFX, targetPosition, Quaternion.identity);
            }
        }
    }
}
