using UnityEngine;

namespace STG.CurveDash
{
    public abstract class EquippableData : ItemData
    {
        [Header("Visuals")]
        public GameObject VisualModel;

        
        [Header("Grip Offsets")]
        public Vector3 PositionOffset;
        public Vector3 RotationOffset;

        [Header("Base Animation")]
        public RuntimeAnimatorController MainAnimator;
    }
}
