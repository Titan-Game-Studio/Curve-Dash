using System.Collections.Generic;
using System.Linq;
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
        private readonly MonsterViewPool monsterViewPool;
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

        private readonly EcsPool<EnemyComponent> enemyPool;
        private readonly EcsPool<ViewLinkComponent> viewLinkPool;
        private readonly EcsFilter viewLinkFilter;

        public ObjectSpawner(EcsWorld world, BlockViewPool blockViewPool, BlockPartViewFactory blockPartViewFactory,
            CrystalViewPool crystalViewPool, ObstacleViewPool obstacleViewPool, ShieldViewPool shieldViewPool, CloudViewPool cloudViewPool, 
            PlayerViewFactory playerViewFactory, MonsterViewPool monsterViewPool,
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
            this.monsterViewPool = monsterViewPool;
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

            enemyPool = world.GetPool<EnemyComponent>();
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
            monsterViewPool.Clear();
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

            Assert.IsTrue(gameObject.transform.childCount >= count);
            return entity;
        }

        private void AddChildBlocks(int blocks, int count, bool rightSide, Vector3 spawnPos, Color color)
        {
            Vector3 turnDirection = rightSide ? Vector3.forward : Vector3.right;

            ref var viewLinkComponent = ref viewLinkPool.Get(blocks);
            var parent = viewLinkComponent.Transform;
            int childCount = parent.childCount;

            // Spawn or reuse up to count
            for (int i = 0; i < count; i++)
            {
                BlockPartView cachedChild = (i < childCount) ? parent.GetChild(i).GetComponent<BlockPartView>() : null;
                AddBlockChild(spawnPos + turnDirection * -i, blocks, color, cachedChild);
            }

            // Deactivate any extra children if the pooled parent has more parts than needed
            for (int i = count; i < childCount; i++)
            {
                parent.GetChild(i).gameObject.SetActive(false);
            }
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

            obstacleView.transform.position = blockPosition + ObstacleOffset;

            obstacleView.GetComponent<Rigidbody>().isKinematic = true;
            obstacleView.GetComponent<Rigidbody>().position = obstacleView.transform.position;

            return entity;
        }

        public int SpawnShield(Vector3 blockPosition)
        {
            // Tạm thời vô hiệu hóa
            return -1;
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

            // Select random weapon from MasterItemCatalog
            if (assetCatalog != null && assetCatalog.MasterItemCatalog != null)
            {
                var weapons = assetCatalog.MasterItemCatalog.Items.OfType<WeaponData>().ToList();
                if (weapons.Count > 0)
                {
                    var randomWeapon = weapons[Random.Range(0, weapons.Count)];
                    weaponView.Setup(randomWeapon);
                }
            }


            weaponView.transform.position = blockPosition + CrystalOffset;
            weaponView.GetComponent<Rigidbody>().isKinematic = true;
            weaponView.GetComponent<Rigidbody>().position = weaponView.transform.position;

            return entity;
        }

        public int SpawnMountPickup(Vector3 blockPosition)
        {
            // Tạm thời vô hiệu hóa
            return -1;
        }
        
        public int SpawnAuraPickup(Vector3 blockPosition)
        {
            // Tạm thời vô hiệu hóa
            return -1;
        }


        private int CreateEntity<T>(GameObject gameObject) where T : struct
        {
            var entity = world.NewEntity();
            world.GetPool<T>().Add(entity);

            ref var viewLinkComponent = ref world.GetPool<ViewLinkComponent>().Add(entity);
            viewLinkComponent.View = gameObject;
            
            var link = gameObject.GetComponent<EntityLinkView>();
            if (link == null) link = gameObject.AddComponent<EntityLinkView>();
            link.Entity = world.PackEntity(entity);
            
            viewLinkComponent.Transform = gameObject.transform;


            return entity;
        }

        private Transform GetPoolTransform()
        {
            var pool = GameObject.Find("ObjectsPool");
            return pool != null ? pool.transform : null;
        }

        public int SpawnMonster(MonsterData data, Vector3 position)
        {
            if (data == null) return -1;
            
            var monsterView = monsterViewPool.Spawn();
            monsterView.transform.position = position;
            monsterView.Setup(data);
            
            var entity = CreateEntity<EnemyComponent>(monsterView.gameObject);



            
            ref var enemy = ref enemyPool.Get(entity);
            enemy.Data = data;
            enemy.MoveSpeed = data.MovementSpeed;

            ref var health = ref enemyHealthPool.Add(entity);
            health.MaxHealth = data.MaxHealth;
            health.CurrentHealth = data.MaxHealth;

            return entity;
        }

        public void DespawnObject(int entity)
        {
            if (!viewLinkPool.Has(entity)) 
            {
                return;
            }

            ref var viewLinkComponent = ref viewLinkPool.Get(entity);
            var view = viewLinkComponent.View;

            if (view == null)
            {
                world.DelEntity(entity);
                return;
            }

            // Safe despawn logic
            if (crystalPool.Has(entity))
            {
                var component = view.GetComponent<CrystalView>();
                if (component != null) crystalViewPool.Despawn(component);
            }
            else if (obstaclePool.Has(entity))
            {
                var component = view.GetComponent<ObstacleView>();
                if (component != null) obstacleViewPool.Despawn(component);
            }
            else if (enemyPool.Has(entity))
            {
                var component = view.GetComponent<MonsterView>();
                if (component != null) monsterViewPool.Despawn(component);
            }
            else if (blockPool.Has(entity))
            {
                if (viewLinkComponent.Transform != null && viewLinkComponent.Transform.childCount > 3) // start block
                    Object.Destroy(view);
                else
                {
                    var component = view.GetComponent<BlockView>();
                    if (component != null) blockViewPool.Despawn(component);
                }
            }
            else if (weaponPickupPool.Has(entity))
            {
                var component = view.GetComponent<WeaponPickupView>();
                if (component != null) weaponPickupViewPool.Despawn(component);
            }
            else if (mountPickupPool.Has(entity))
            {
                var component = view.GetComponent<MountPickupView>();
                if (component != null) mountPickupViewPool.Despawn(component);
            }
            else if (auraPickupPool.Has(entity))
            {
                var component = view.GetComponent<AuraPickupView>();
                if (component != null) auraPickupViewPool.Despawn(component);
            }
            else if (shieldPool.Has(entity))
            {
                var component = view.GetComponent<ShieldView>();
                if (component != null) shieldViewPool.Despawn(component);
            }
            else if (cloudPool.Has(entity))
            {
                var component = view.GetComponent<CloudView>();
                if (component != null) cloudViewPool.Despawn(component);
            }
            else if (playerPool.Has(entity))
            {
                Object.Destroy(view);
            }
            
            world.DelEntity(entity);
        }


    }
}
