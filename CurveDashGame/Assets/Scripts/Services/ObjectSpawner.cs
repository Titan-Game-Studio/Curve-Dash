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
        private readonly ItemPickupViewPool itemPickupViewPool;
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
        private readonly EcsPool<ItemPickupComponent> itemPickupPool;

        private readonly EcsPool<EnemyComponent> enemyPool;
        private readonly EcsPool<ViewLinkComponent> viewLinkPool;
        private readonly EcsFilter viewLinkFilter;

        public ObjectSpawner(EcsWorld world, BlockViewPool blockViewPool, BlockPartViewFactory blockPartViewFactory,
            CrystalViewPool crystalViewPool, ObstacleViewPool obstacleViewPool, ShieldViewPool shieldViewPool, CloudViewPool cloudViewPool, 
            PlayerViewFactory playerViewFactory, MonsterViewPool monsterViewPool,
            ItemPickupViewPool itemPickupViewPool, 
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
            this.itemPickupViewPool = itemPickupViewPool;

            playerPool = world.GetPool<PlayerComponent>();
            crystalPool = world.GetPool<CrystalComponent>();
            obstaclePool = world.GetPool<ObstacleComponent>();
            shieldPool = world.GetPool<ShieldComponent>();
            cloudPool = world.GetPool<CloudComponent>();
            blockPool = world.GetPool<BlockComponent>();
            
            enemyHealthPool = world.GetPool<EnemyHealthComponent>();
            combatPool = world.GetPool<PlayerCombatComponent>();
            itemPickupPool = world.GetPool<ItemPickupComponent>();

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
            itemPickupViewPool.Clear();
        }

        public int SpawnPlayer(Vector3 pos, float speed)
        {
            var gameObject = playerViewFactory.Create().gameObject;

            // Dynamically attach and configure the Devion Games StatsHandler so stats are registered and visible to the UI
            var handler = gameObject.GetComponent<DevionGames.StatSystem.StatsHandler>();
            if (handler == null)
            {
                handler = gameObject.AddComponent<DevionGames.StatSystem.StatsHandler>();
                
                // Use reflection to set private field m_HandlerName to "Player Stats"
                var nameField = typeof(DevionGames.StatSystem.StatsHandler).GetField("m_HandlerName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (nameField != null)
                {
                    nameField.SetValue(handler, "Player Stats");
                }

                // Populate stats list with all stats defined in the Stat Database
                if (DevionGames.StatSystem.StatsManager.Database != null)
                {
                    handler.m_Stats = new List<DevionGames.StatSystem.Stat>();
                    foreach (var dbStat in DevionGames.StatSystem.StatsManager.Database.items)
                    {
                        if (dbStat != null)
                        {
                            handler.m_Stats.Add(dbStat);
                        }
                    }
                    UnityEngine.Debug.Log($"<color=green>[ObjectSpawner] Programmatically attached StatsHandler to Player and populated {handler.m_Stats.Count} stats from Database.</color>");
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[ObjectSpawner] DevionGames StatsManager.Database is currently NULL! Cannot populate player stats.");
                }

                // Force immediate initialization and registration via Reflection to eliminate 1-frame async delay
                var startMethod = typeof(DevionGames.StatSystem.StatsHandler).GetMethod("Start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (startMethod != null)
                {
                    startMethod.Invoke(handler, null);
                }
            }

            var entity = CreateEntity<PlayerComponent>(gameObject);

            ref var playerComponent = ref world.GetPool<PlayerComponent>().Get(entity);
            playerComponent.Speed = speed;
            
            ref var combat = ref combatPool.Add(entity);
            combat.CurrentWeapon = null;
            combat.CooldownTimer = 0f;

            // 1. Trang bị Vũ khí Khởi đầu & Kỹ năng Active
            if (assetCatalog != null && assetCatalog.MasterItemCatalog != null)
            {
                var weapons = assetCatalog.MasterItemCatalog.Items.OfType<WeaponData>().ToList();
                if (weapons.Count > 0)
                {
                    var starterWeapon = weapons[0]; // Chọn vũ khí cơ bản đầu tiên
                    var instance = new WeaponInstance(starterWeapon, starterWeapon.Rarity);
                    
                    // Tìm một kỹ năng Active để gắn vào vũ khí
                    var loadedAbilities = Resources.LoadAll<AbilityData>("");
                    PoEAbility starterActive = null;
                    foreach (var ab in loadedAbilities)
                    {
                        if (ab is PoEAbility active)
                        {
                            starterActive = active;
                            break;
                        }
                    }
                    if (starterActive != null)
                    {
                        instance.DynamicAbilities.Add(starterActive);
                    }

                    combat.CurrentWeapon = instance;

                    var playerView = gameObject.GetComponent<PlayerView>();
                    if (playerView != null)
                    {
                        playerView.Equip(starterWeapon);
                    }

                    // Tích hợp Devion Games Inventory System cho Actionbar & Inventory
                    try
                    {
                        if (DevionGames.InventorySystem.InventoryManager.Database != null)
                        {
                            // Thêm vũ khí vào Equipment / Inventory
                            var devionAdapter = starterWeapon.DevionAdapter;
                            if (devionAdapter == null)
                            {
                                string targetName = starterWeapon.name + "_Adapter";
                                foreach (var dbItem in DevionGames.InventorySystem.InventoryManager.Database.items)
                                {
                                    if (dbItem != null && dbItem.name == targetName)
                                    {
                                        devionAdapter = dbItem;
                                        starterWeapon.DevionAdapter = dbItem;
                                        break;
                                    }
                                }
                            }
                            if (devionAdapter != null)
                            {
                                var devionInstance = DevionGames.InventorySystem.InventoryManager.CreateInstance(devionAdapter);
                                if (devionInstance != null)
                                {
                                    DevionGames.InventorySystem.ItemContainer.AddItem("Equipment", devionInstance);
                                    DevionGames.InventorySystem.ItemContainer.AddItem("Inventory", devionInstance);
                                }
                            }

                            // 2. Thêm 2 bình Flask (Máu và Mana) vào Actionbar
                            var flasks = assetCatalog.MasterItemCatalog.Items.OfType<FlaskItemData>().ToList();
                            foreach (var flask in flasks)
                            {
                                var fAdapter = flask.DevionAdapter;
                                if (fAdapter == null)
                                {
                                    string targetName = flask.name + "_Adapter";
                                    foreach (var dbItem in DevionGames.InventorySystem.InventoryManager.Database.items)
                                    {
                                        if (dbItem != null && dbItem.name == targetName)
                                        {
                                            fAdapter = dbItem;
                                            flask.DevionAdapter = dbItem;
                                            break;
                                        }
                                    }
                                }
                                if (fAdapter != null)
                                {
                                    var fInstance = DevionGames.InventorySystem.InventoryManager.CreateInstance(fAdapter);
                                    if (fInstance != null)
                                    {
                                        DevionGames.InventorySystem.ItemContainer.AddItem("Actionbar", fInstance);
                                        DevionGames.InventorySystem.ItemContainer.AddItem("Inventory", fInstance);
                                    }
                                }
                            }

                            // 3. Thêm kỹ năng Active vào Actionbar
                            if (starterActive != null)
                            {
                                foreach (var dbItem in DevionGames.InventorySystem.InventoryManager.Database.items)
                                {
                                    if (dbItem != null && (dbItem.name.Contains("Cleave") || dbItem.name.Contains(starterActive.AbilityName)))
                                    {
                                        var skillInstance = DevionGames.InventorySystem.InventoryManager.CreateInstance(dbItem);
                                        if (skillInstance != null)
                                        {
                                            DevionGames.InventorySystem.ItemContainer.AddItem("Actionbar", skillInstance);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        UnityEngine.Debug.LogError($"[StarterEquipment] Exception during starter equipment setup: {ex.Message}");
                    }
                }
            }

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

        public int SpawnItemPickup(Vector3 blockPosition, ItemData item)
        {
            if (item == null) return -1;
            var itemView = itemPickupViewPool.Spawn();
            var entity = CreateEntity<ItemPickupComponent>(itemView.gameObject);

            itemView.Setup(item);

            itemView.transform.position = blockPosition + CrystalOffset;
            var rb = itemView.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.position = itemView.transform.position;
            }

            return entity;
        }

        public int RegisterDroppedItemEntity(ItemPickupView itemView)
        {
            if (itemView == null || world == null) return -1;
            var entity = CreateEntity<ItemPickupComponent>(itemView.gameObject);
            return entity;
        }

        private List<T> GetAllItemsOfType<T>() where T : ItemData
        {
            List<T> result = new List<T>();
            if (assetCatalog != null && assetCatalog.MasterItemCatalog != null && assetCatalog.MasterItemCatalog.Items != null)
            {
                result.AddRange(assetCatalog.MasterItemCatalog.Items.OfType<T>());
            }

            if (result.Count == 0)
            {
#if UNITY_EDITOR
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:" + typeof(T).Name);
                foreach (var guid in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var item = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                    if (item != null) result.Add(item);
                }
#else
                result.AddRange(Resources.LoadAll<T>(""));
#endif
            }

            return result;
        }

        public int SpawnItemPickup(Vector3 blockPosition)
        {
            var items = GetAllItemsOfType<ItemData>();
            if (items.Count > 0)
            {
                var randomItem = items[Random.Range(0, items.Count)];
                return SpawnItemPickup(blockPosition, randomItem);
            }
            return -1;
        }

        public int SpawnWeaponPickup(Vector3 blockPosition)
        {
            var weapons = GetAllItemsOfType<WeaponData>();
            if (weapons.Count > 0)
            {
                var randomWeapon = weapons[Random.Range(0, weapons.Count)];
                return SpawnItemPickup(blockPosition, randomWeapon);
            }
            return -1;
        }

        public int SpawnArmorPickup(Vector3 blockPosition)
        {
            var armors = GetAllItemsOfType<ArmorItemData>();
            if (armors.Count > 0)
            {
                var randomArmor = armors[Random.Range(0, armors.Count)];
                return SpawnItemPickup(blockPosition, randomArmor);
            }
            return -1;
        }

        public int SpawnGemPickup(Vector3 blockPosition)
        {
            var gems = GetAllItemsOfType<GemItemData>();
            if (gems.Count > 0)
            {
                var randomGem = gems[Random.Range(0, gems.Count)];
                return SpawnItemPickup(blockPosition, randomGem);
            }
            return -1;
        }

        public int SpawnFlaskPickup(Vector3 blockPosition)
        {
            var flasks = GetAllItemsOfType<FlaskItemData>();
            if (flasks.Count > 0)
            {
                var randomFlask = flasks[Random.Range(0, flasks.Count)];
                return SpawnItemPickup(blockPosition, randomFlask);
            }
            return -1;
        }

        public int SpawnCurrencyPickup(Vector3 blockPosition)
        {
            var currencies = GetAllItemsOfType<CurrencyItemData>();
            if (currencies.Count > 0)
            {
                var randomCurrency = currencies[Random.Range(0, currencies.Count)];
                return SpawnItemPickup(blockPosition, randomCurrency);
            }
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
            else if (itemPickupPool.Has(entity))
            {
                var component = view.GetComponent<ItemPickupView>();
                if (component != null) itemPickupViewPool.Despawn(component);
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
                var handler = view.GetComponent<DevionGames.StatSystem.StatsHandler>();
                if (handler != null && DevionGames.StatSystem.StatsManager.current != null)
                {
                    var field = typeof(DevionGames.StatSystem.StatsManager).GetField("m_StatsHandler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        var list = field.GetValue(DevionGames.StatSystem.StatsManager.current) as List<DevionGames.StatSystem.StatsHandler>;
                        if (list != null)
                        {
                            list.Remove(handler);
                        }
                    }
                }
                Object.Destroy(view);
            }
            
            world.DelEntity(entity);
        }


    }
}
