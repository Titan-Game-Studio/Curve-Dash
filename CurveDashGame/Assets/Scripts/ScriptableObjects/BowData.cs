using UnityEngine;

namespace STG.CurveDash
{
    [CreateAssetMenu(fileName = "NewBow", menuName = "Curve-Dash/Weapons/Great Bow")]
    public class BowData : WeaponData
    {
        public override int MaxSockets => 6;

        [Header("Ammo")]


        public OffHandData DefaultArrow;
    }
}


