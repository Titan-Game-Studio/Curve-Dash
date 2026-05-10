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
            // Initialize final stats with default values from this ability
            float finalDamageMultiplier = DamageMultiplier;
            float finalAddedFlatDamage = AddedFlatDamage;
            float finalCritBonus = CriticalChanceBonus;
            int finalProjectileCount = ProjectileCount;
            float finalSpeed = Speed;

            System.Collections.Generic.List<string> appliedSupportsList = new System.Collections.Generic.List<string>();
            System.Collections.Generic.List<GameObject> extraCastVFXs = new System.Collections.Generic.List<GameObject>();
            System.Collections.Generic.List<GameObject> extraImpactVFXs = new System.Collections.Generic.List<GameObject>();

            // Query socketed support gems from the PlayerView's CurrentWeaponInstance
            var playerView = user.GetComponent<PlayerView>();
            if (playerView != null && playerView.CurrentWeaponInstance != null)
            {
                var weaponAbilities = playerView.CurrentWeaponInstance.GetAbilities();
                foreach (var ab in weaponAbilities)
                {
                    if (ab is SupportAbilityData support && support.IsCompatible(this))
                    {
                        finalDamageMultiplier *= (1f + support.DamageMultiplierPercent / 100f);
                        finalAddedFlatDamage += support.AddedFlatDamageBonus;
                        finalCritBonus += support.CriticalChanceBonus;
                        finalProjectileCount += support.ExtraProjectiles;
                        finalSpeed *= (1f + support.SpeedMultiplierPercent / 100f);

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
