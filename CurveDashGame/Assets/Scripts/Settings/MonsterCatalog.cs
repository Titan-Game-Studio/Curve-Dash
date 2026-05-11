using System.Collections.Generic;
using UnityEngine;

namespace STG.CurveDash
{
    [CreateAssetMenu(fileName = "MonsterCatalog", menuName = "Curve-Dash/Catalogs/Monster Catalog")]
    public class MonsterCatalog : ScriptableObject
    {
        public List<MonsterData> Monsters;

        public MonsterData GetRandomMonster()
        {
            if (Monsters == null || Monsters.Count == 0) return null;
            return Monsters[Random.Range(0, Monsters.Count)];
        }
    }
}

