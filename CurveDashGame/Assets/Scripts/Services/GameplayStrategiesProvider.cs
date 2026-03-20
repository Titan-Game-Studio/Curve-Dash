namespace STG.CurveDash
{
    public class GameplayStrategiesProvider
    {
        private readonly GameSettings gameSettings;
        private CrystalSpawnStrategy crystalSpawnStrategy;
        private ObstacleSpawnStrategy obstacleSpawnStrategy;
        private BlockHolesStrategy blockHolesStrategy;
        private RandomShieldSpawnStrategy shieldSpawnStrategy;
        private RandomCloudSpawnStrategy cloudSpawnStrategy;

        public GameplayStrategiesProvider(GameSettings gameSettings)
        {
            this.gameSettings = gameSettings;
        }

        public CrystalSpawnStrategy GetCrystalSpawnStrategy()
        {
            if (crystalSpawnStrategy == null)
            {
                if (gameSettings.RandomCrystals)
                    crystalSpawnStrategy = new RandomCrystalSpawnStrategy();
                else
                    crystalSpawnStrategy = new ProgressiveCrystalSpawnStrategy();
            }

            return crystalSpawnStrategy;
        }

        public BlockHolesStrategy GetBlockHolesStrategy()
        {
            return blockHolesStrategy ??= new BlockHolesStrategy();
        }

        public ObstacleSpawnStrategy GetObstacleSpawnStrategy()
        {
            return obstacleSpawnStrategy ??= new RandomObstacleSpawnStrategy();
        }

        public RandomShieldSpawnStrategy GetShieldSpawnStrategy()
        {
            return shieldSpawnStrategy ??= new RandomShieldSpawnStrategy();
        }

        public RandomCloudSpawnStrategy GetCloudSpawnStrategy()
        {
            return cloudSpawnStrategy ??= new RandomCloudSpawnStrategy();
        }

        public void Reset()
        {
            crystalSpawnStrategy = null;
            blockHolesStrategy = null;
            shieldSpawnStrategy = null;
            cloudSpawnStrategy = null;
        }
    }
}