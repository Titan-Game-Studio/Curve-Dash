using UnityEngine;

namespace STG.CurveDash
{
    [CreateAssetMenu(fileName = "NewTwoHandedWeapon", menuName = "Curve-Dash/Weapons/Two-Handed (GreatSword-Hammer)")]
    public class TwoHandedWeaponData : WeaponData
    {
        public override int MaxSockets => 6;
    }


}
