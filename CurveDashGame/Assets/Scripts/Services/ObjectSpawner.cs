using Leopotam.EcsLite;
using STG.CurveDash.Views;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace STG.CurveDash
{
    public class ObjectSpawner
    {
        private readonly EcsWorld world;
        private readonly CrystalViewPool crystalViewPool;
        private readonly ObstacleViewPool obstacleViewPool;
        private readonly ShieldViewPool shieldViewPool;
        private readonly CloudViewPool cloudViewPool;
        private readonly BlockViewPool blockViewPool;
        private readonly BlockPartViewFactory blockPartViewFactory;
        private readonly PlayerViewFactory playerViewFactory;
        private readonly WeaponPickupViewPool weaponPickupViewPool;
        private readonly MountPickupViewPool mountPickupViewPool;
        private readonly AuraPickupViewPool auraPickupViewPool;
        private readonly GameAssetCatalog assetCatalog;
        private readonly AssetManager assetManager;

        private readonly Vector3 CrystalOffset = new Vector3(0, 0.8f, 0);
        private readonly Vector3 ObstacleOffset = new Vector3(0, 0.5f, 0);

        private readonly EcsPool<PlayerComponent> playerPool;
        private readonly EcsPool<CrystalComponent> crystalPool;
        private readonly EcsPool<ObstacleComponent> obstaclePool;
        private readonly EcsPool<ShieldComponent> shieldPool;
        private readonly EcsPool<CloudComponent> cloudPool;
        private readonly EcsPool<BlockComponent> blockPool;
        
        private readonly EcsPool<EnemyHealthComponent> enemyHealthPool;
        private readonly EcsPool<PlayerCombatComponent> combatPool;
        private readonly EcsPool<WeaponPickupComponent> weaponPickupPool;
        private readonly EcsPool<MountPickupComponent> mountPickupPool;
        private readonly EcsPool<AuraPickupComponent> auraPickupPool;

        private readonly EcsPool<ViewLinkComponent> viewLinkPool;
        private readonly EcsFilter viewLinkFilter;

        public ObjectSpawner(EcsWorld world, BlockViewPool blockViewPool, BlockPartViewFactory blockPartViewFactory,
            CrystalViewPool crystalViewPool, ObstacleViewPool obstacleViewPool, ShieldViewPool shieldViewPool, CloudViewPool cloudViewPool, PlayerViewFactory playerViewFactory, 
            WeaponPickupViewPool weaponPickupViewPool, MountPickupViewPool mountPickupViewPool, AuraPickupViewPool auraPickupViewPool, 
            GameAssetCatalog assetCatalog, AssetManager assetManager)
        {
            this.world = world;
            this.assetCatalog = assetCatalog;
            this.assetManager = assetManager;
            this.blockViewPool = blockViewPool;
            this.blockPartViewFactory = blockPartViewFactory;
            this.crystalViewPool = crystalViewPool;
            this.obstacleViewPool = obstacleViewPool;
            this.shieldViewPool = shieldViewPool;
            this.cloudViewPool = cloudViewPool;
            this.playerViewFactory = playerViewFactory;
            this.weaponPickupViewPool = weaponPickupViewPool;
            this.mountPickupViewPool = mountPickupViewPool;
            this.auraPickupViewPool = auraPickupViewPool;

            playerPool = world.GetPool<PlayerComponent>();
            crystalPool = world.GetPool<CrystalComponent>();
            obstaclePool = world.GetPool<ObstacleComponent>();
            shieldPool = world.GetPool<ShieldComponent>();
            cloudPool = world.GetPool<CloudComponent>();
            blockPool = world.GetPool<BlockComponent>();
            
            enemyHealthPool = world.GetPool<EnemyHealthComponent>();
            combatPool = world.GetPool<PlayerCombatComponent>();
            weaponPickupPool = world.GetPool<WeaponPickupComponent>();
            mountPickupPool = world.GetPool<MountPickupComponent>();
            auraPickupPool = world.GetPool<AuraPickupComponent>();

            viewLinkPool = world.GetPool<ViewLinkComponent>();
            viewLinkFilter = world.Filter<ViewLinkComponent>().End();
        }

        public void Clear()
        {
            foreach (var entity in viewLinkFilter)
                DespawnObject(entity);

            crystalViewPool.Clear();
            obstacleViewPool.Clear();
            shieldViewPool.Clear();
            cloudViewPool.Clear();
            blockViewPool.Clear();
            weaponPickupViewPool.Clear();
            mountPickupViewPool.Clear();
            auraPickupViewPool.Clear();
        }

        public int SpawnPlayer(Vector3 pos, float speed)
        {
            var gameObject = playerViewFactory.Create().gameObject;
            var entity = CreateEntity<PlayerComponent>(gameObject);

            ref var playerComponent = ref world.GetPool<PlayerComponent>().Get(entity);
            playerComponent.Speed = speed;
            
            ref var combat = ref combatPool.Add(entity);
            combat.CurrentWeapon = null;
            combat.CooldownTimer = 0f;

            gameObject.transform.position = pos;
            return entity;
        }

        public int SpawnStartPlatform(Color color)
        {
            var gameObject = new GameObject("StartBlocks");
            gameObject.AddComponent<BlockView>();
            gameObject.AddComponent<EntityLinkView>();

            var entity = CreateEntity<BlockComponent>(gameObject);

            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    AddBlockChild(new Vector3(i, 0f, j), entity, color);
                }
            }

            return entity;
        }

        public int SpawnBlock(int count, Vector3 spawnPos, bool rightSide, Color color)
        {
            var gameObject = blockViewPool.Spawn().gameObject;
            var entity = CreateEntity<BlockComponent>(gameObject);

            AddChildBlocks(entity, count, rightSide, spawnPos, color);

            Assert.IsTrue(gameObject.transform.childCount == count);
            return entity;
        }

        private void AddChildBlocks(int blocks, int count, bool rightSide, Vector3 spawnPos, Color color)
        {
            Vector3 turnDirection = rightSide ? Vector3.forward : Vector3.right;

            ref var viewLinkComponent = ref viewLinkPool.Get(blocks);
            var parent = viewLinkComponent.Transform;
            bool childExists = parent.childCount > 0;

            for (int i = 0; i < count; i++)
                AddBlockChild(spawnPos + turnDirection * -i, blocks, color,
                    childExists ? parent.GetChild(i).GetComponent<BlockPartView>() : null);
        }

        private void AddBlockChild(Vector3 position, int blocks, Color color, BlockPartView cachedBlockPartView = null)
        {
            ref var viewLinkComponent = ref viewLinkPool.Get(blocks);

            var blockView = cachedBlockPartView != null ? cachedBlockPartView : blockPartViewFactory.Create();

            blockView.GetComponent<Rigidbody>().isKinematic = true;
            blockView.SetColor(color);
            blockView.transform.parent = viewLinkComponent.Transform;
            blockView.transform.SetPositionAndRotation(position, Quaternion.identity);
            blockView.gameObject.SetActive(true);
        }

        public int SpawnCrystal(Vector3 blockPosition)
        {
            var crystalView = crystalViewPool.Spawn();
            var entity = CreateEntity<CrystalComponent>(crystalView.gameObject);

            crystalView.transform.position = blockPosition + CrystalOffset;
            crystalView.GetComponent<Rigidbody>().isKinematic = true;
            crystalView.GetComponent<Rigidbody>().position = crystalView.transform.position;

            return entity;
        }

        public int SpawnObstacle(Vector3 blockPosition)
        {
            var obstacleView = obstacleViewPool.Spawn();
            var entity = CreateEntity<ObstacleComponent>(obstacleView.gameObject);

            ref var health = ref enemyHealthPool.Add(entity);
            health.MaxHealth = 3;
            health.CurrentHealth = 3;

            obstacleView.transform.position = blockPosition + ObstacleOffset;
            obstacleView.GetComponent<Rigidbody>().isKinematic = true;
            obstacleView.GetComponent<Rigidbody>().position = obstacleView.transform.position;

            return entity;
        }

        public int SpawnShield(Vector3 blockPosition)
        {
            var shieldView = shieldViewPool.Spawn();
            var entity = CreateEntity<ShieldComponent>(shieldView.gameObject);

            shieldView.transform.position = blockPosition + CrystalOffset; // Using same offset as Crystal
            shieldView.GetComponent<Rigidbody>().isKinematic = true;
            shieldView.GetComponent<Rigidbody>().position = shieldView.transform.position;

            return entity;
        }

        public int SpawnCloud(Vector3 blockPosition)
        {
            var cloudView = cloudViewPool.Spawn();
            var entity = CreateEntity<CloudComponent>(cloudView.gameObject);
            
            // Randomize cloud speed and offset
            float heightOffset = Random.Range(1.5f, 3.5f);
            float sideOffset = Random.Range(-2f, 2f);
            
            cloudView.transform.position = blockPosition + new Vector3(sideOffset, heightOffset, 0);
            
            ref var cloud = ref cloudPool.Get(entity);
            cloud.Speed = Random.Range(1f, 3f);
            cloud.Lifetime = 15f; // Clouds live for 15 seconds before despawning

            return entity;
        }

        public int SpawnWeaponPickup(Vector3 blockPosition)
        {
            var weaponView = weaponPickupViewPool.Spawn();
            var entity = CreateEntity<WeaponPickupComponent>(weaponView.gameObject);

            // Select random weapon
            if (assetCatalog != null && assetCatalog.Weapons != null && assetCatalog.Weapons.Weapons != null && assetCatalog.Weapons.Weapons.Count > 0)
            {
                var weaponList = assetCatalog.Weapons.Weapons;
                var randomWeapon = weaponList[Random.Range(0, weaponList.Count)];
                weaponView.Setup(randomWeapon);
            }

            weaponView.transform.position = blockPosition + CrystalOffset;
            weaponView.GetComponent<Rigidbody>().isKinematic = true;
            weaponView.GetComponent<Rigidbody>().position = weaponView.transform.position;

            return entity;
        }

        public int SpawnMountPickup(Vector3 blockPosition)
        {
            var mountView = mountPickupViewPool.Spawn();
            var entity = CreateEntity<MountPickupComponent>(mountView.gameObject);

            if (assetCatalog != null && assetCatalog.MountSkins != null && assetCatalog.MountSkins.Items != null && assetCatalog.MountSkins.Items.Count > 0)
            {
                var randomMountIndex = Random.Range(0, assetCatalog.MountSkins.Items.Count);
                mountView.Setup(randomMountIndex, assetManager);
            }

            mountView.transform.position = blockPosition + CrystalOffset;
            mountView.GetComponent<Rigidbody>().isKinematic = true;
            mountView.GetComponent<Rigidbody>().position = mountView.transform.position;

            return entity;
        }

        public int SpawnAuraPickup(Vector3 blockPosition)
        {
            var auraView = auraPickupViewPool.Spawn();
            var entity = CreateEntity<AuraPickupComponent>(auraView.gameObject);

            if (assetCatalog != null && assetCatalog.Auras != null && assetCatalog.Auras.Items != null && assetCatalog.Auras.Items.Count > 0)
            {
                var randomAuraIndex = Random.Range(0, assetCatalog.Auras.Items.Count);
                auraView.Setup(randomAuraIndex, assetManager);
            }

            auraView.transform.position = blockPosition + CrystalOffset;
            auraView.GetComponent<Rigidbody>().isKinematic = true;
            auraView.GetComponent<Rigidbody>().position = auraView.transform.position;

            return entity;
        }

        private int CreateEntity<T>(GameObject gameObject) where T : struct
        {
            var entity = world.NewEntity();
            world.GetPool<T>().Add(entity);

            ref var viewLinkComponent = ref world.GetPool<ViewLinkComponent>().Add(entity);
            viewLinkComponent.View = gameObject;
            viewLinkComponent.View.GetComponent<EntityLinkView>().Entity = world.PackEntity(entity);
            viewLinkComponent.Transform = gameObject.transform;

            return entity;
        }

        public void DespawnObject(int entity)
        {
            ref var viewLinkComponent = ref viewLinkPool.Get(entity);

            if (playerPool.Has(entity))
            {
                Object.Destroy(viewLinkComponent.View);
            }

            if (crystalPool.Has(entity))
            {
                crystalViewPool.Despawn(viewLinkComponent.View.GetComponent<CrystalView>());
            }

            if (obstaclePool.Has(entity))
            {
                obstacleViewPool.Despawn(viewLinkComponent.View.GetComponent<ObstacleView>());
            }

            if (shieldPool.Has(entity))
            {
                shieldViewPool.Despawn(viewLinkComponent.View.GetComponent<ShieldView>());
            }

            if (cloudPool.Has(entity))
            {
                cloudViewPool.Despawn(viewLinkComponent.View.GetComponent<CloudView>());
            }

            if (blockPool.Has(entity))
            {
                if (viewLinkComponent.Transform.childCount > 3) // start block
                    Object.Destroy(viewLinkComponent.View);
                else
                    blockViewPool.Despawn(viewLinkComponent.View.GetComponent<BlockView>());
            }

            if (weaponPickupPool.Has(entity))
            {
                weaponPickupViewPool.Despawn(viewLinkComponent.View.GetComponent<WeaponPickupView>());
            }

            if (mountPickupPool.Has(entity))
            {
                mountPickupViewPool.Despawn(viewLinkComponent.View.GetComponent<MountPickupView>());
            }

            if (auraPickupPool.Has(entity))
            {
                auraPickupViewPool.Despawn(viewLinkComponent.View.GetComponent<AuraPickupView>());
            }

            world.DelEntity(entity);
        }
    }
}
