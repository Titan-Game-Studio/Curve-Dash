using UnityEngine;

namespace STG.CurveDash
{
    public abstract class ObstacleSpawnStrategy
    {
        public abstract bool ShouldSpawn();
    }

    public class RandomObstacleSpawnStrategy : ObstacleSpawnStrategy
    {
        private const int Chance = 5;

        public override bool ShouldSpawn()
        {
            return Random.Range(0, Chance) == 0;
        }
    }

    public class ProgressiveObstacleSpawnStrategy : ObstacleSpawnStrategy
    {
        private const int ProgressiveStep = 5;

        private int blockCounter = -1;
        private int blockWithObstacleCounter = 0;

        public override bool ShouldSpawn()
        {
            blockCounter++;
            if (blockCounter == ProgressiveStep)
            {
                blockCounter = 0;

                blockWithObstacleCounter++;
                if (blockWithObstacleCounter == ProgressiveStep)
                    blockWithObstacleCounter = 0;
            }

            return blockCounter == blockWithObstacleCounter;
        }
    }
}