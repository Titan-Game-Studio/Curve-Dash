using UnityEngine;

namespace STG.CurveDash
{
    public class RandomCloudSpawnStrategy
    {
        // Adjust the chance as needed
        private float chance = 0.2f;

        public bool ShouldSpawn()
        {
            return Random.value <= chance;
        }
    }
}
