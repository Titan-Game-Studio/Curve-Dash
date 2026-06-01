using Zenject;
using Leopotam.EcsLite;
using STG.CurveDash.Views;
using TGS.Ads;
using DevionGames.InventorySystem;
using DevionGames.UIWidgets;

namespace STG.CurveDash
{
    public class GameInstaller : MonoInstaller
    {
        public static DiContainer GlobalContainer;
        public PrefabsSettings Prefabs;

        public override void InstallBindings()
        {
            GlobalContainer = Container;
            UnityEngine.Assertions.Assert.IsNotNull(Prefabs, "[LỖI SETUP] Bạn chưa kéo file GamePrefabs (ScriptableObject) vào ô 'Prefabs' của GameInstaller trong Unity Inspector!");

            // ecs

            Container.BindInstance(new EcsWorld());
            Container.BindInterfacesAndSelfTo<EcsSystems>().AsSingle();
            Container.BindInterfacesTo<EcsStartup>().AsSingle();

            // settings

            Container.BindInstance(Prefabs);

            // Bind named Devion containers for GemUseHandler (Equipment + Inventory)
            Container.Bind<ItemContainer>().WithId("Equipment")
                .FromMethod(ctx => WidgetUtility.Find<ItemContainer>("Equipment"))
                .WhenInjectedInto<GemUseHandler>();
            Container.Bind<ItemContainer>().WithId("Inventory")
                .FromMethod(ctx => WidgetUtility.Find<ItemContainer>("Inventory"))
                .WhenInjectedInto<GemUseHandler>();

            // systems
            Container.BindInterfacesAndSelfTo<GemSocketService>().AsSingle();
            Container.Bind<GemLevelService>().AsSingle();
            Container.Bind<BeltFlaskService>().AsSingle();


            Container.BindInstance(Prefabs.GameAssetCatalog).AsSingle();
            Container.BindInstance(Prefabs.MonsterCatalog).AsSingle();
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
            Container.BindInterfacesAndSelfTo<ItemPickupSystem>().AsSingle();
            Container.BindInterfacesAndSelfTo<TGS.Core.IAP.UnityIapService>().AsSingle();
            Container.BindInterfacesAndSelfTo<LevelPlayAdService>().AsSingle();
            Container.Bind<AudioPlayer>().AsSingle();
            Container.Bind<PlayerStatService>().AsSingle();
            Container.BindInterfacesAndSelfTo<GameSystem>().AsSingle();
            Container.BindInterfacesAndSelfTo<CrystalSystem>().AsSingle();
            Container.BindInterfacesAndSelfTo<ObstacleSystem>().AsSingle();
            Container.BindInterfacesAndSelfTo<CloudSystem>().AsSingle();
            Container.BindInterfacesAndSelfTo<ShieldSystem>().AsSingle();
            Container.BindInterfacesAndSelfTo<VfxSystem>().AsSingle();
            Container.BindInterfacesAndSelfTo<EnemyMovementSystem>().AsSingle();




            Container.BindInterfacesTo<DeleteEventsSystem<PlayerPassedComponent>>().AsSingle();
            Container.BindInterfacesTo<DeleteEventsSystem<PlayerHitCrystalEvent>>().AsSingle();
            Container.BindInterfacesTo<DeleteEventsSystem<PlayerHitObstacleEvent>>().AsSingle();
            Container.BindInterfacesTo<DeleteEventsSystem<PlayerHitShieldEvent>>().AsSingle();
            Container.BindInterfacesTo<DeleteEventsSystem<PlayerLevelUpComponent>>().AsSingle();
            Container.BindInterfacesTo<DeleteEventsSystem<PlayerHitItemEvent>>().AsSingle();

            // factories

            Container.BindFactory<PlayerView, PlayerViewFactory>()
                .FromComponentInNewPrefab(GetValidPrefab<PlayerView>(Prefabs.PlayerPrefab, "Player_Fallback"));
            Container.BindFactory<BlockPartView, BlockPartViewFactory>()
                .FromComponentInNewPrefab(GetValidPrefab<BlockPartView>(Prefabs.BlockPartPrefab, "BlockPart_Fallback"));

            // pools

            Container.BindMemoryPool<BlockView, BlockViewPool>()
                .WithInitialSize(30).FromComponentInNewPrefab(GetValidPrefab<BlockView>(Prefabs.BlockPrefab, "Block_Fallback"))
                .UnderTransformGroup("ObjectsPool");
            Container.BindMemoryPool<CrystalView, CrystalViewPool>()
                .WithInitialSize(5).FromComponentInNewPrefab(GetValidPrefab<CrystalView>(Prefabs.CrystalPrefab, "Crystal_Fallback"))
                .UnderTransformGroup("ObjectsPool");
            Container.BindMemoryPool<ObstacleView, ObstacleViewPool>()
                .WithInitialSize(5).FromComponentInNewPrefab(GetValidPrefab<ObstacleView>(Prefabs.ObstaclePrefab, "Obstacle_Fallback"))
                .UnderTransformGroup("ObjectsPool");
            Container.BindMemoryPool<ShieldView, ShieldViewPool>()
                .WithInitialSize(2).FromComponentInNewPrefab(GetValidPrefab<ShieldView>(Prefabs.ShieldPrefab, "Shield_Fallback"))
                .UnderTransformGroup("ObjectsPool");
            Container.BindMemoryPool<CloudView, CloudViewPool>()
                .WithInitialSize(5).FromComponentInNewPrefab(GetValidPrefab<CloudView>(Prefabs.CloudPrefab, "Cloud_Fallback"))
                .UnderTransformGroup("ObjectsPool");
            Container.BindMemoryPool<ItemPickupView, ItemPickupViewPool>()
                .WithInitialSize(2).FromComponentInNewPrefab(GetValidPrefab<ItemPickupView>(Prefabs.ItemPickupPrefab, "ItemPickup_Fallback"))
                .UnderTransformGroup("ObjectsPool");
            Container.BindMemoryPool<MonsterView, MonsterViewPool>()
                .WithInitialSize(10).FromComponentInNewPrefab(GetValidPrefab<MonsterView>(Prefabs.MonsterBasePrefab, "MonsterBase_Fallback"))
                .UnderTransformGroup("ObjectsPool");
        }

        private UnityEngine.GameObject GetValidPrefab<T>(UnityEngine.GameObject prefab, string fallbackName) where T : UnityEngine.Component
        {
            if (prefab != null)
            {
                return prefab;
            }

            UnityEngine.Debug.LogWarning($"[GameInstaller] Prefab for {typeof(T).Name} is missing in GamePrefabs (ScriptableObject). Creating a temporary fallback prefab '{fallbackName}'.");
            var fallback = new UnityEngine.GameObject(fallbackName);
            fallback.AddComponent<T>();
            fallback.SetActive(false);
            return fallback;
        }
    }
}
