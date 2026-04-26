using UnityEngine;

namespace STG.CurveDash
{
    [CreateAssetMenu(fileName = "New Weapon", menuName = "Curve Dash/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        public string WeaponName = "Sword";
        public WeaponType Type = WeaponType.Sword;
        
        [Header("Stats")]
        public int Damage = 1;
        public float AttackRange = 5f;
        public float AttackCooldown = 0.5f;
        
        [Header("Special Traits")]
        [Range(0f, 1f)]
        public float CritChance = 0.1f;
        public float CritMultiplier = 2.0f;
        public int MaxTargets = 1; // Số lượng quái bị chém cùng lúc (Đánh lan)
        
        [Header("Visuals")]
        public GameObject RightHandModel; // Model cầm tay phải (và hiển thị dưới đất)
        public GameObject LeftHandModel;  // Model cầm tay trái (Dành riêng cho Dagger)
    }
}
