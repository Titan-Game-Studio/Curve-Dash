using System.Collections.Generic;
using UnityEngine;

namespace STG.CurveDash
{
    /// <summary>
    /// The stat a support gem modifier touches. Add new entries here to clone more PoE supports
    /// without writing new fields/classes — only the combat code that *consumes* a stat needs work.
    /// </summary>
    public enum SupportStat
    {
        Damage,            // general damage (works with Increased / More)
        AddedFlatDamage,   // flat damage added to the hit (Flat)
        CriticalChance,    // crit chance % added (Flat)
        ExtraProjectiles,  // +N projectiles (Flat)
        Speed,             // projectile / attack speed (Increased / More)
        // --- Data-ready, not yet applied by combat (future systems can query via GetMultiplier/GetFlat) ---
        AreaOfEffect,
        Pierce,
        Chain,
        Duration,
    }

    /// <summary>
    /// How a modifier combines, mirroring PoE's wording:
    /// Flat = add the raw value; Increased = additive %, pooled then applied once; More = multiplicative, compounds.
    /// </summary>
    public enum SupportForm
    {
        Flat,
        Increased,
        More,
    }

    [System.Serializable]
    public class SupportModifier
    {
        public SupportStat Stat;
        public SupportForm Form = SupportForm.More;
        [Tooltip("Flat → raw value (+2 proj, +5% crit). Increased/More → percent (35 = 35%, -26 = 26% less).")]
        public float Value;
    }

    [CreateAssetMenu(fileName = "NewSupportAbility", menuName = "Curve-Dash/Abilities/Support Ability")]
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

        [Header("Modifiers (data-driven — clone any PoE support here)")]
        [Tooltip("Each entry tweaks one stat. Stack as many as needed instead of adding new fields.")]
        public List<SupportModifier> Modifiers = new List<SupportModifier>();

        [Header("Legacy Stat Modifiers (auto-folded into the accessors below)")]
        [Tooltip("Kept for backward-compat with existing assets. Prefer adding entries to 'Modifiers' for new gems.")]
        public float DamageMultiplierPercent = 0f;
        [Tooltip("Legacy: flat damage added to the skill.")]
        public float AddedFlatDamageBonus = 0f;
        [Tooltip("Legacy: critical hit chance bonus percentage.")]
        public float CriticalChanceBonus = 0f;
        [Tooltip("Legacy: extra projectiles added.")]
        public int ExtraProjectiles = 0;
        [Tooltip("Legacy: projectile/attack speed multiplier percentage.")]
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

        // ---------------------------------------------------------------------
        // Aggregation API — consumers read these so legacy fields and the new
        // Modifiers list are combined transparently.
        // ---------------------------------------------------------------------

        /// <summary>
        /// Combined multiplicative factor for a stat over any modifier list: pools all Increased values
        /// additively (1 + sum/100) and compounds every More value, then folds in an optional legacy "more" %.
        /// Static so skills (SelfModifiers) and gems share one implementation.
        /// </summary>
        public static float Multiplier(IEnumerable<SupportModifier> mods, SupportStat stat, float legacyMorePercent = 0f)
        {
            float increasedPool = 0f;
            float moreFactor = 1f + legacyMorePercent / 100f;

            if (mods != null)
            {
                foreach (var m in mods)
                {
                    if (m == null || m.Stat != stat) continue;
                    if (m.Form == SupportForm.Increased) increasedPool += m.Value;
                    else if (m.Form == SupportForm.More) moreFactor *= (1f + m.Value / 100f);
                    // Flat is ignored for multiplier-style stats.
                }
            }

            return (1f + increasedPool / 100f) * moreFactor;
        }

        /// <summary>Sum of all Flat modifiers for a stat over any list, plus an optional legacy flat value.</summary>
        public static float Flat(IEnumerable<SupportModifier> mods, SupportStat stat, float legacyFlat = 0f)
        {
            float total = legacyFlat;
            if (mods != null)
            {
                foreach (var m in mods)
                {
                    if (m != null && m.Stat == stat && m.Form == SupportForm.Flat)
                        total += m.Value;
                }
            }
            return total;
        }

        public float GetMultiplier(SupportStat stat, float legacyMorePercent = 0f) => Multiplier(Modifiers, stat, legacyMorePercent);
        public float GetFlat(SupportStat stat, float legacyFlat = 0f) => Flat(Modifiers, stat, legacyFlat);

        // Convenience accessors the combat/ability code uses today.
        public float GetDamageMultiplier()    => GetMultiplier(SupportStat.Damage, DamageMultiplierPercent);
        public float GetSpeedMultiplier()     => GetMultiplier(SupportStat.Speed, SpeedMultiplierPercent);
        public float GetAddedFlatDamage()     => GetFlat(SupportStat.AddedFlatDamage, AddedFlatDamageBonus);
        public float GetCriticalChanceBonus() => GetFlat(SupportStat.CriticalChance, CriticalChanceBonus);
        public int   GetExtraProjectiles()    => Mathf.RoundToInt(GetFlat(SupportStat.ExtraProjectiles, ExtraProjectiles));
    }
}
