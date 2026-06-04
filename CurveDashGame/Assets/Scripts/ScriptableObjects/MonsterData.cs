using UnityEngine;

namespace STG.CurveDash
{
    [CreateAssetMenu(fileName = "NewMonster", menuName = "Curve-Dash/Monsters/Monster Data")]
    public class MonsterData : ScriptableObject
    {
        public string MonsterName;
        public GameObject Prefab; // Hoặc dùng Addressable Reference
        public float MaxHealth = 50f;
        public float MovementSpeed = 2f;
        public float ExperienceReward = 10f;
        public float AttackRange = 1.5f;

        [Header("Elemental Resistances (%, capped at 75; negative = takes extra damage)")]
        public float FireResistance = 0f;
        public float ColdResistance = 0f;
        public float LightningResistance = 0f;
        public float ChaosResistance = 0f;
    }
}

