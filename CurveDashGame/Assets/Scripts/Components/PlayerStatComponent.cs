namespace STG.CurveDash
{
    public struct PlayerStatComponent
    {
        // --- Core resources ---
        public int Score;       // per-run score (resets each run; drives HighScore)
        public int HighScore;
        public int Level;       // persistent character level (carries across runs)
        public int Exp;         // persistent XP into the CURRENT level (0..ScoreForNextLevel)
        public int Gold;
        public float CurrentLife;
        public float MaxLife;
        public float CurrentMana;
        public float MaxMana;
        public float CurrentEnergyShield;
        public float MaxEnergyShield;
        public float InvincibleTimer;

        // --- PoE Attributes ---
        public float Strength;
        public float Dexterity;
        public float Intelligence;

        // --- PoE Defences ---
        public float Armour;
        public float EvasionRating;
        public float AccuracyRating;

        // --- PoE Resistances (0–75%, Chaos starts at -60%) ---
        public float FireResistance;
        public float ColdResistance;
        public float LightningResistance;
        public float ChaosResistance;

        // --- PoE Offence ---
        public float CritChance;        // %
        public float CritMultiplier;    // % (default 150)
        public float LifeRegen;         // flat HP/s

        // --- Movement ---
        public float MovementSpeed;     // base 100
        public float BlockChance;       // % cap 75
    }
}