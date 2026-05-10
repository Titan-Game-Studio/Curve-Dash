using UnityEngine;

namespace STG.CurveDash
{
    public struct PlayerComponent
    {
        public int? PreviousHitEntity;
        public Vector3 Direction;
        public Vector3 LastNonZeroDirection; // Stores the last active movement direction to handle toggles from a stopped state
        public float Speed;
        public float Size;
    }
}
