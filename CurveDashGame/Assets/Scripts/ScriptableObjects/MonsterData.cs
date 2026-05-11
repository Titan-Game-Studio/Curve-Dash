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

    }
}

