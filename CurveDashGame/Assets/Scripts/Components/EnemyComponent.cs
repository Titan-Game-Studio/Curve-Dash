namespace STG.CurveDash
{
    public struct EnemyComponent
    {
        public MonsterData Data;
        public float MoveSpeed;

        public float AttackTimer;
        public float AttackCooldown;
        public bool IsChargingAttack;

        public UnityEngine.Vector3 CurrentDirection;
        public float ZigzagTimer;
    }
}

