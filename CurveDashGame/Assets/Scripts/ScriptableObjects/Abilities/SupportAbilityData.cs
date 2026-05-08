using System.Collections.Generic;
using UnityEngine;

namespace STG.CurveDash
{
    [CreateAssetMenu(fileName = "NewSupportAbility", menuName = "Curve Dash/Abilities/Support Ability")]
    public class SupportAbilityData : AbilityData
    {
        [Header("Support Gem Settings")]
        [TextArea]
        public string SupportDescription;

        [Header("Compatibility Filter")]
        [Tooltip("The support will only apply if the active skill matches at least one of these skill types. Leave empty to support ALL types.")]
        public List<PoEAbilityType> SupportedSkillTypes = new List<PoEAbilityType>();

        [Tooltip("The support will only apply if the active skill matches at least one of these element types. Leave empty to support ALL elements.")]
        public List<PoEElementType> SupportedElements = new List<PoEElementType>();

        [Header("Stat Modifiers")]
        [Tooltip("Damage multiplier percentage. E.g. 30 means 30% more damage (x1.30), -15 means 15% less damage (x0.85).")]
        public float DamageMultiplierPercent = 0f;
        
        [Tooltip("Flat damage added to the skill.")]
        public float AddedFlatDamageBonus = 0f;

        [Tooltip("Critical hit chance bonus percentage. E.g. +5% crit chance.")]
        public float CriticalChanceBonus = 0f;

        [Tooltip("Extra projectiles added. E.g. Lesser Multiple Projectiles adds +2.")]
        public int ExtraProjectiles = 0;

        [Tooltip("Speed multiplier percentage. E.g. +30% projectile speed.")]
        public float SpeedMultiplierPercent = 0f;

        [Header("Visual Effects Overrides")]
        [Tooltip("Optional Cast VFX to override or overlay on the active ability.")]
        public GameObject ExtraCastVFX;

        [Tooltip("Optional Impact VFX to override or overlay on the active ability.")]
        public GameObject ExtraImpactVFX;

        public override void Execute(GameObject user, Vector3 targetPosition)
        {
            // Support abilities cannot be executed directly by themselves.
            // They modify socketed active abilities instead.
            Debug.LogWarning($"[Support] Support Ability '{AbilityName}' cannot be cast directly! Link it with an active skill on your weapon.");
        }

        /// <summary>
        /// Checks if this support gem is compatible with the given active PoEAbility.
        /// </summary>
        public bool IsCompatible(PoEAbility activeAbility)
        {
            if (activeAbility == null) return false;

            // 1. Check Skill Type Compatibility (Melee, Spell, Ranged, Aura)
            if (SupportedSkillTypes != null && SupportedSkillTypes.Count > 0)
            {
                if (!SupportedSkillTypes.Contains(activeAbility.SkillType))
                {
                    return false;
                }
            }

            // 2. Check Element Type Compatibility (Physical, Fire, Cold, Lightning, Chaos)
            if (SupportedElements != null && SupportedElements.Count > 0)
            {
                if (!SupportedElements.Contains(activeAbility.Element))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
