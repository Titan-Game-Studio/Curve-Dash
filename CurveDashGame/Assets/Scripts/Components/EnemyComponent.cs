namespace STG.CurveDash
{
    public struct EnemyComponent
    {
        public MonsterData Data;
        public int Level;        // area level at spawn — scales HP and (future) loot item level
        public float MoveSpeed;

        public float AttackTimer;
        public float AttackCooldown;
        public bool IsChargingAttack;

        public UnityEngine.Vector3 CurrentDirection;
        public float ZigzagTimer;
    }
}

