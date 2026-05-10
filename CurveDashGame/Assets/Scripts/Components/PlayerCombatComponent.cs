namespace STG.CurveDash
{
    public struct PlayerCombatComponent
    {
        public WeaponInstance CurrentWeapon;
        public float CooldownTimer;

        // Sequential Combo System
        public int ComboIndex;
        public float ComboTimer;
    }
}
