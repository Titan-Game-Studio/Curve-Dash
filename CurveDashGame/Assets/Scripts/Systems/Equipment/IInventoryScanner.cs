using System;

namespace STG.CurveDash
{
    public interface IInventoryScanner
    {
        OffHandData FindFirstArrow();
        OffHandData FindFirstShield();
        OneHandedWeaponData FindFirstOneHandedWeapon();
        BowData FindFirstBow();
    }
}
