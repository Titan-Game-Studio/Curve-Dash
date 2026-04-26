using Zenject;
using Leopotam.EcsLite;
using STG.CurveDash.Views;
using TGS.Ads;

namespace STG.CurveDash
{
    public class GameInstaller : MonoInstaller
    {
        public PrefabsSettings Prefabs;

        public override void InstallBindings()
        {
            UnityEngine.Assertions.Assert.IsNotNull(Prefabs, "[LỖI SETUP] Bạn chưa kéo file GamePrefabs (ScriptableObject) vào ô 'Prefabs' của GameInstaller trong Unity Inspector!");

            // ecs

            Container.BindInstance(new EcsWorld());
            Container.BindInterfacesAndSelfTo<EcsSystems>().AsSingle();
            Container.BindInterfacesTo<EcsStartup>().AsSingle();

            // settings

            Container.BindInstance(Prefabs);

            // systems

            Container.BindInstance(Prefabs.GameAssetCatalog).AsSingle();
            Container.Bind<AddressablesController>().AsSingle().NonLazy();
            Container.Bind<AssetManager>().AsSingle();
            Container.BindInterfacesAndSelfTo<DataManager>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<CloudSaveManager>().AsSingle().NonLazy();
            Container.Bind<ShopService>().AsSingle();
            Container.BindInterfacesTo<AndroidRefreshRateFix>().AsSingle();
            Container.BindInterfacesTo<CameraFollowSystem>().AsSingle();
            Container.BindInterfacesTo<TerrainSystem>().AsSingle();
            Container.BindInterfacesTo<BackgroundColorSystem>().AsSingle();
            Container.BindInterfacesAndSelfTo<ObjectSpawner>().AsSingle();
            Container.Bind<GameplayStrategiesProvider>().AsSingle();
            Container.BindInterfacesAndSelfTo<PlayerMovementSystem>().AsSingle();
            Container.BindInterfacesAndSelfTo<BlockSystem>().AsSingle();
            Container.BindInterfacesTo<FallingSystem>().AsSingle();
            Container.BindInterfacesAndSelfTo<CombatSystem>().AsSingle();
            Container.BindInterfacesAndSelfTo<WeaponPickupSystem>().AsSingle();
            Container.BindInterfacesAndSelfTo<PowerUpPickupSystem>().AsSingle();
            Container.BindInterfacesAndSelfTo<TGS.Core.IAP.UnityIapService>().AsSingle();
            Container.BindInterfacesAndSelfTo<LevelPlayAdService>().AsSingle();
            Container.Bind<AudioPlayer>().AsSingle();
            Container.Bind<PlayerStatService>().AsSingle();
            Container.BindInterfacesAndSelfTo<GameSystem>().AsSingle();
            Container.BindInterfacesAndSelfTo<CrystalSystem>().AsSingle();
            Container.BindInterfacesAndSelfTo<ObstacleSystem>().AsSingle();
            Container.BindInterfacesAndSelfTo<CloudSystem>().AsSingle();
            Container.BindInterfacesAndSelfTo<ShieldSystem>().AsSingle();

            Container.BindInterfacesTo<DeleteEventsSystem<PlayerPassedComponent>>().AsSingle();
            Container.BindInterfacesTo<DeleteEventsSystem<PlayerHitCrystalEvent>>().AsSingle();
            Container.BindInterfacesTo<DeleteEventsSystem<PlayerHitObstacleEvent>>().AsSingle();
            Container.BindInterfacesTo<DeleteEventsSystem<PlayerHitShieldEvent>>().AsSingle();
            Container.BindInterfacesTo<DeleteEventsSystem<PlayerLevelUpComponent>>().AsSingle();
            Container.BindInterfacesTo<DeleteEventsSystem<PlayerHitWeaponEvent>>().AsSingle();
            Container.BindInterfacesTo<DeleteEventsSystem<PlayerHitMountEvent>>().AsSingle();
            Container.BindInterfacesTo<DeleteEventsSystem<PlayerHitAuraEvent>>().AsSingle();

            // factories

            Container.BindFactory<PlayerView, PlayerViewFactory>().FromComponentInNewPrefab(Prefabs.PlayerPrefab);
            Container.BindFactory<BlockPartView, BlockPartViewFactory>()
                .FromComponentInNewPrefab(Prefabs.BlockPartPrefab);

            // pools

            Container.BindMemoryPool<BlockView, BlockViewPool>()
                .WithInitialSize(30).FromComponentInNewPrefab(Prefabs.BlockPrefab)
                .UnderTransformGroup("ObjectsPool");
            Container.BindMemoryPool<CrystalView, CrystalViewPool>()
                .WithInitialSize(5).FromComponentInNewPrefab(Prefabs.CrystalPrefab)
                .UnderTransformGroup("ObjectsPool");
            Container.BindMemoryPool<ObstacleView, ObstacleViewPool>()
                .WithInitialSize(5).FromComponentInNewPrefab(Prefabs.ObstaclePrefab)
                .UnderTransformGroup("ObjectsPool");
            Container.BindMemoryPool<ShieldView, ShieldViewPool>()
                .WithInitialSize(2).FromComponentInNewPrefab(Prefabs.ShieldPrefab)
                .UnderTransformGroup("ObjectsPool");
            Container.BindMemoryPool<CloudView, CloudViewPool>()
                .WithInitialSize(5).FromComponentInNewPrefab(Prefabs.CloudPrefab)
                .UnderTransformGroup("ObjectsPool");
            Container.BindMemoryPool<WeaponPickupView, WeaponPickupViewPool>()
                .WithInitialSize(2).FromComponentInNewPrefab(Prefabs.WeaponPickupPrefab)
                .UnderTransformGroup("ObjectsPool");
            Container.BindMemoryPool<MountPickupView, MountPickupViewPool>()
                .WithInitialSize(2).FromComponentInNewPrefab(Prefabs.MountPickupPrefab)
                .UnderTransformGroup("ObjectsPool");
            Container.BindMemoryPool<AuraPickupView, AuraPickupViewPool>()
                .WithInitialSize(2).FromComponentInNewPrefab(Prefabs.AuraPickupPrefab)
                .UnderTransformGroup("ObjectsPool");
        }
    }
}
