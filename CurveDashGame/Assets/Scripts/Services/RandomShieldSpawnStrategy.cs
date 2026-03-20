using UnityEngine;

namespace STG.CurveDash
{
    public class RandomShieldSpawnStrategy
    {
        public bool ShouldSpawn()
        {
            return Random.value < 0.1f;
        }
    }
}
