using UnityEngine;

namespace STG.CurveDash
{
    [CreateAssetMenu(fileName = "New Weapon", menuName = "Curve Dash/Items/Weapon Data")]
    public class WeaponData : ItemData
    {
        public WeaponType WeaponType;
        
        [Header("Stats")]
        public int Damage = 1;
        public float AttackRange = 5f;
        public float AttackCooldown = 0.5f;
        
        [Header("Special Traits")]
        [Range(0f, 1f)]
        public float CritChance = 0.1f;
        public float CritMultiplier = 2.0f;
        public int MaxTargets = 1;
        
        [Header("Visuals")]
        public GameObject RightHandModel; 
        public GameObject LeftHandModel;  
        
        [Header("Grip Offsets - Right Hand")]
        public Vector3 RightHandPositionOffset;
        public Vector3 RightHandRotationOffset;

        [Header("Grip Offsets - Left Hand")]
        public Vector3 LeftHandPositionOffset;
        public Vector3 LeftHandRotationOffset;

        
        [Header("Animation")]
        public RuntimeAnimatorController AnimatorController;
        
        [Header("Ability")]
        public AbilityData SpecialAbility;

        public WeaponData()
        {
            Type = ItemType.Weapon;
        }
    }
}

