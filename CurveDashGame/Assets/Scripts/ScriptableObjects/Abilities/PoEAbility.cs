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

    [CreateAssetMenu(fileName = "NewPoEAbility", menuName = "Curve-Dash/Abilities/PoE Skill")]
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

        [Header("Aura Settings")]
        [Tooltip("Aura only: how long (seconds) the aura effect lasts before it is automatically recast. The AuraSystem casts the aura, waits this duration, then casts it again.")]
        public float Duration = 5f;

        [Header("Self Modifiers (data-driven — extend the skill without new fields)")]
        [Tooltip("Built-in modifiers this skill always applies to itself, same vocabulary as support gems. " +
                 "Use this to express extra stats (more damage, extra projectiles, etc.) as data instead of code.")]
        public System.Collections.Generic.List<SupportModifier> SelfModifiers = new System.Collections.Generic.List<SupportModifier>();


        [Header("Visual Effects")]
        public GameObject CastVFX;
        public GameObject ImpactVFX;

        public override void Execute(GameObject user, Vector3 targetPosition)
        {
            // Initialize final stats with default values from this ability
            float finalDamageMultiplier = DamageMultiplier;
            float finalAddedFlatDamage = AddedFlatDamage;
            float finalCritBonus = CriticalChanceBonus;
            int finalProjectileCount = ProjectileCount;
            float finalSpeed = Speed;

            // Apply this skill's own data-driven SelfModifiers (same vocabulary as support gems).
            finalDamageMultiplier *= SupportAbilityData.Multiplier(SelfModifiers, SupportStat.Damage);
            finalAddedFlatDamage  += SupportAbilityData.Flat(SelfModifiers, SupportStat.AddedFlatDamage);
            finalCritBonus        += SupportAbilityData.Flat(SelfModifiers, SupportStat.CriticalChance);
            finalProjectileCount  += Mathf.RoundToInt(SupportAbilityData.Flat(SelfModifiers, SupportStat.ExtraProjectiles));
            finalSpeed            *= SupportAbilityData.Multiplier(SelfModifiers, SupportStat.Speed);

            System.Collections.Generic.List<string> appliedSupportsList = new System.Collections.Generic.List<string>();
            System.Collections.Generic.List<GameObject> extraCastVFXs = new System.Collections.Generic.List<GameObject>();
            System.Collections.Generic.List<GameObject> extraImpactVFXs = new System.Collections.Generic.List<GameObject>();

            // Query socketed support gems from the PlayerView's CurrentWeaponInstance and equipped armor
            var playerView = user.GetComponent<PlayerView>();
            if (playerView != null)
            {
                var allAbilities = new System.Collections.Generic.List<AbilityData>();
                if (playerView.CurrentWeaponInstance != null)
                {
                    allAbilities.AddRange(playerView.CurrentWeaponInstance.GetAbilities());
                }
                allAbilities.AddRange(playerView.GetEquippedArmorAbilities());

                foreach (var ab in allAbilities)
                {
                    if (ab is SupportAbilityData support && support.IsCompatible(this))
                    {
                        finalDamageMultiplier *= support.GetDamageMultiplier();
                        finalAddedFlatDamage += support.GetAddedFlatDamage();
                        finalCritBonus += support.GetCriticalChanceBonus();
                        finalProjectileCount += support.GetExtraProjectiles();
                        finalSpeed *= support.GetSpeedMultiplier();

                        appliedSupportsList.Add(support.AbilityName);

                        if (support.ExtraCastVFX != null) extraCastVFXs.Add(support.ExtraCastVFX);
                        if (support.ExtraImpactVFX != null) extraImpactVFXs.Add(support.ExtraImpactVFX);
                    }
                }
            }

            string supportsInfo = appliedSupportsList.Count > 0 
                ? string.Join(", ", appliedSupportsList) 
                : "None";

            Debug.Log($"<color=lime>[PoE Ability] Casted {AbilityName} ({SkillType} - {Element})!</color> " +
                      $"Cooldown: {Cooldown}s | " +
                      $"Final Damage Multiplier: {finalDamageMultiplier * 100:F0}% (Flat: +{finalAddedFlatDamage:F1}) | " +
                      $"Proj Count: {finalProjectileCount} | " +
                      $"Proj Speed: {finalSpeed:F1} | " +
                      $"Supports Applied: <color=yellow>{supportsInfo}</color>");
            
            if (SkillType == PoEAbilityType.Aura)
            {
                Vector3 auraPos = user.transform.position;
                if (CastVFX != null)
                    VfxSystem.RequestSpawn(CastVFX, auraPos, Quaternion.identity);
                foreach (var extraVfx in extraCastVFXs)
                    VfxSystem.RequestSpawn(extraVfx, auraPos, Quaternion.identity);
                if (ImpactVFX != null)
                    VfxSystem.RequestSpawn(ImpactVFX, auraPos, Quaternion.identity);
                foreach (var extraVfx in extraImpactVFXs)
                    VfxSystem.RequestSpawn(extraVfx, auraPos, Quaternion.identity);
                return;
            }

            // Calculate horizontal rotation towards target
            Vector3 direction = (targetPosition - user.transform.position);
            direction.y = 0; // Lock Y axis
            Quaternion baseRotation = direction.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(direction.normalized)
                : user.transform.rotation;

            // Spawn Cast VFX (supports shooting multiple projectiles in a gorgeous cone spread if count > 1)
            if (CastVFX != null)
            {
                if (finalProjectileCount <= 1)
                {
                    VfxSystem.RequestSpawn(CastVFX, user.transform.position + Vector3.up * 1.0f, baseRotation);
                }
                else
                {
                    // Spread projectiles in a cone (LMP / Multi-Proj style!)
                    float spreadAngle = 15f; // degrees between projectiles
                    float startAngle = -spreadAngle * (finalProjectileCount - 1) / 2f;

                    for (int i = 0; i < finalProjectileCount; i++)
                    {
                        float currentAngle = startAngle + i * spreadAngle;
                        Quaternion rot = baseRotation * Quaternion.Euler(0, currentAngle, 0);
                        VfxSystem.RequestSpawn(CastVFX, user.transform.position + Vector3.up * 1.0f, rot);
                    }
                }
            }

            // Spawn any extra Cast VFX from support gems
            foreach (var extraVfx in extraCastVFXs)
            {
                VfxSystem.RequestSpawn(extraVfx, user.transform.position + Vector3.up * 1.0f, baseRotation);
            }

            // Spawn Impact VFX at target position
            if (ImpactVFX != null)
            {
                VfxSystem.RequestSpawn(ImpactVFX, targetPosition, baseRotation);
            }

            // Spawn any extra Impact VFX from support gems
            foreach (var extraVfx in extraImpactVFXs)
            {
                VfxSystem.RequestSpawn(extraVfx, targetPosition, baseRotation);
            }
        }
    }
}
