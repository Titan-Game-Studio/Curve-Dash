using UnityEngine;

namespace STG.CurveDash
{
    [CreateAssetMenu(fileName = "NewOneHandedWeapon", menuName = "Curve Dash/Weapons/One-Handed Sword")]
    public class OneHandedWeaponData : WeaponData
    {
        public override int MaxSockets => 3;

        [Header("Combo Animations")]


        public RuntimeAnimatorController DualWieldController;
        public RuntimeAnimatorController SwordShieldController;
    }
}
