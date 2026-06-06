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

            // Hand the starter kit to a player who has no weapon. The routine itself confirms (after
            // Devion finishes loading) that no weapon exists in Equipment or the bag before granting,
            // so it never overwrites looted gear and self-heals if the player ends up weaponless.
            if (assetCatalog != null && assetCatalog.MasterItemCatalog != null)
            {
                var weapons = assetCatalog.MasterItemCatalog.Items.OfType<WeaponData>().ToList();
                if (weapons.Count > 0)
                {
                    var normalWeapons = weapons.Where(w => w.Rarity == ItemRarity.Normal).ToList();
                    var starterWeapon = normalWeapons.Count > 0 ? normalWeapons[UnityEngine.Random.Range(0, normalWeapons.Count)] : weapons[0];

                    var playerView = gameObject.GetComponent<PlayerView>();
                    if (playerView != null)
                        playerView.StartCoroutine(GiveStarterEquipmentRoutine(starterWeapon));
                }
            }

            gameObject.transform.position = pos;
            return entity;
        }

        // True if the player already has a weapon (equipped OR in the bag). The starter kit is only
        // granted when there is none, so a weaponless player always gets something to fight with —
        // while any looted gear they already own (amulets, armor, boots, etc.) is left untouched.
        private static bool HasAnyWeapon()
        {
            return ContainerHasWeapon("Equipment") || ContainerHasWeapon("Inventory");
        }

        private static bool ContainerHasWeapon(string containerName)
        {
            foreach (var c in DevionGames.UIWidgets.WidgetUtility.FindAll<DevionGames.InventorySystem.ItemContainer>(containerName))
            {
                if (c == null) continue;
                foreach (var s in c.Slots)
                {
                    if (s == null || s.IsEmpty || s.ObservedItem == null) continue;
                    if (s.ObservedItem is CurveDashEquipmentAdapter adapter && adapter.OriginalEquipmentData is WeaponData)
                    {
                        UnityEngine.Debug.Log($"<color=yellow>[StarterEquipment] Existing weapon '{s.ObservedItem.Name}' found in '{containerName}' — starter kit not needed.</color>");
                        return true;
                    }
                }
            }
            return false;
        }

        // Hands out the one-time, first-run starter kit — exactly ONE weapon, a small Life flask, a
        // Mana flask and one active skill gem — into the Inventory bag (not auto-equipped). NON-destructive:
        // waits for Devion to finish loading, bails (just flagging) if a kit was already granted or any
        // gear already exists, and calls InventoryManager.Save() right after so Devion's own load/save
        // cycle can't overwrite the freshly granted items.
        private System.Collections.IEnumerator GiveStarterEquipmentRoutine(WeaponData starterWeapon)
        {
            UnityEngine.Debug.Log($"<color=orange>[StarterEquipment] Routine started for weapon '{starterWeapon?.name ?? "null"}'. Waiting for Devion to load…</color>");

            // Wait (capped) until Devion has woken up and finished loading. IsLoaded is set to
            // !HasSavedData() on init, so when saved data exists it only flips true after a Load()
            // fires onDataLoaded. In a single-scene boot no Load may ever fire, which would hang this
            // routine forever — so we time out and proceed; HasAnySavedGear() below still guards gear.
            const float timeout = 5f;
            float t = 0f;
            while ((DevionGames.InventorySystem.InventoryManager.current == null
                    || !DevionGames.InventorySystem.InventoryManager.IsLoaded)
                   && t < timeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            t = 0f;
            while ((DevionGames.UIWidgets.WidgetUtility.FindAll<DevionGames.InventorySystem.ItemContainer>("Equipment").Length == 0 ||
                    DevionGames.UIWidgets.WidgetUtility.FindAll<DevionGames.InventorySystem.ItemContainer>("Inventory").Length == 0)
                   && t < timeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // Let any in-flight Devion load / scene-change settle before we touch the containers,
            // otherwise its load can land AFTER our additions and wipe them.
            yield return new UnityEngine.WaitForSecondsRealtime(0.35f);

            UnityEngine.Debug.Log($"<color=orange>[StarterEquipment] After wait: IsLoaded={DevionGames.InventorySystem.InventoryManager.IsLoaded}, HasAnyWeapon={HasAnyWeapon()}, Database={(DevionGames.InventorySystem.InventoryManager.Database != null)}</color>");

            // Grant only when the player has no weapon at all — leaves any looted gear untouched.
            if (HasAnyWeapon())
            {
                UnityEngine.Debug.Log("<color=orange>[StarterEquipment] Player already has a weapon — starter kit skipped.</color>");
                yield break;
            }

            if (starterWeapon == null || DevionGames.InventorySystem.InventoryManager.Database == null)
            {
                UnityEngine.Debug.LogWarning("<color=red>[StarterEquipment] Aborted: starterWeapon or Devion Database is null.</color>");
                yield break;
            }

            UnityEngine.Debug.Log("<color=lime>[StarterEquipment] First run — granting minimal starter kit (1 weapon + Life/Mana flask + 1 active gem).</color>");

            // 1 weapon.
            HelperAddStarterItemToInventory(starterWeapon);

            // 1 small Life flask + 1 Mana flask.
            var allFlasks = GetAllItemsOfType<FlaskItemData>();
            var lifeFlask = PickSmallestFlask(allFlasks, FlaskType.Life);
            var manaFlask = PickSmallestFlask(allFlasks, FlaskType.Mana);
            if (lifeFlask != null) HelperAddStarterItemToInventory(lifeFlask);
            if (manaFlask != null) HelperAddStarterItemToInventory(manaFlask);

            // 1 active skill gem, matched to the weapon's combat style when possible.
            var activeGem = PickStarterActiveGem(starterWeapon);
            if (activeGem != null) HelperAddStarterItemToInventory(activeGem);

            // Persist immediately so Devion's autosave/load cycle can't overwrite the granted items.
            DevionGames.InventorySystem.InventoryManager.Save();

            UnityEngine.Debug.Log("<color=green>[StarterEquipment] Minimal starter kit granted and saved.</color>");
        }

        // Picks the smallest/lowest-tier flask of a type, preferring names hinting at a minor tier.
        private FlaskItemData PickSmallestFlask(List<FlaskItemData> flasks, FlaskType type)
        {
            if (flasks == null) return null;
            var ofType = flasks.Where(f => f != null && f.FlaskType == type).ToList();
            if (ofType.Count == 0) return null;

            string[] smallHints = { "small", "minor", "lesser", "tiny" };
            foreach (var hint in smallHints)
            {
                var match = ofType.FirstOrDefault(f =>
                    f.name.ToLower().Contains(hint) ||
                    (f.ItemName != null && f.ItemName.ToLower().Contains(hint)));
                if (match != null) return match;
            }
            return ofType[0];
        }

        // Picks one active (Skill) gem, preferring one whose embedded ability matches the weapon's range.
        private GemItemData PickStarterActiveGem(WeaponData weapon)
        {
            var skillGems = GetAllItemsOfType<GemItemData>().Where(g => g != null && g.GemType == GemType.Skill).ToList();
            if (skillGems.Count == 0) return null;

            bool isRanged = (weapon is BowData);
            var matched = skillGems.Where(g =>
            {
                if (g.EmbeddedAbility is PoEAbility poeAb)
                    return isRanged
                        ? (poeAb.SkillType == PoEAbilityType.Ranged || poeAb.SkillType == PoEAbilityType.Spell)
                        : (poeAb.SkillType == PoEAbilityType.Melee  || poeAb.SkillType == PoEAbilityType.Spell);
                return true;
            }).ToList();

            var pool = matched.Count > 0 ? matched : skillGems;
            return pool[UnityEngine.Random.Range(0, pool.Count)];
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

        public int SpawnItemPickup(Vector3 blockPosition, ItemData item, Transform parent = null)
        {
            if (item == null) return -1;
            var itemView = itemPickupViewPool.Spawn();
            var entity = CreateEntity<ItemPickupComponent>(itemView.gameObject);

            itemView.Setup(item);

            if (parent != null)
            {
                itemView.transform.SetParent(parent);
                // Tâm X,Z của block, Y ngang mặt đất (kèm offset nhỏ để không lún mesh)
                itemView.transform.position = new Vector3(parent.position.x, parent.position.y + 0.75f, parent.position.z);
            }
            else
            {
                itemView.transform.position = blockPosition + new Vector3(0, 0.75f, 0);
            }
            
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

        private void HelperAddStarterItemToInventory(ItemData itemData)
        {
            if (itemData == null || DevionGames.InventorySystem.InventoryManager.Database == null) return;

            var devionAdapter = itemData.DevionAdapter;
            if (devionAdapter == null)
            {
                string targetName = itemData.name + "_Adapter";
                foreach (var dbItem in DevionGames.InventorySystem.InventoryManager.Database.items)
                {
                    if (dbItem != null && dbItem.name == targetName)
                    {
                        devionAdapter = dbItem;
                        itemData.DevionAdapter = dbItem;
                        break;
                    }
                }
            }

            if (devionAdapter != null)
            {
                var devionInstance = DevionGames.InventorySystem.InventoryManager.CreateInstance(devionAdapter);
                if (devionInstance != null)
                {
                    if (devionInstance is CurveDashEquipmentAdapter equipAdapter)
                    {
                        equipAdapter.SyncData();
                    }
                    bool added = DevionGames.InventorySystem.ItemContainer.AddItem("Inventory", devionInstance);
                    UnityEngine.Debug.Log($"<color=lime>[StarterEquipment] Extra starter item '{itemData.name}' added to Inventory bag: {added}</color>");
                }
            }
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

        public int SpawnItemPickup(Vector3 blockPosition, Transform parent = null)
        {
            var items = GetAllItemsOfType<ItemData>();
            if (items.Count > 0)
            {
                var randomItem = items[Random.Range(0, items.Count)];
                return SpawnItemPickup(blockPosition, randomItem, parent);
            }
            return -1;
        }

        public int SpawnWeaponPickup(Vector3 blockPosition, Transform parent = null, int itemLevel = 1)
        {
            var weapons = GetAllItemsOfType<WeaponData>();
            if (weapons.Count == 0) return -1;

            var baseWeapon = weapons[Random.Range(0, weapons.Count)];

            // Roll the weapon at the dropping monster's level: rarity decides affix count,
            // item level decides which affix tiers are reachable.
            var rarity = RollDropRarity();
            var rolled = new WeaponInstance(baseWeapon, rarity, true, Mathf.Max(1, itemLevel));

            int entity = SpawnItemPickup(blockPosition, baseWeapon, parent);
            AttachRolledLoot(entity, rarity, rolled.ItemLevel, rolled.Affixes);
            return entity;
        }

        public int SpawnArmorPickup(Vector3 blockPosition, Transform parent = null, int itemLevel = 1)
        {
            var armors = GetAllItemsOfType<ArmorItemData>();
            if (armors.Count == 0) return -1;

            var baseArmor = armors[Random.Range(0, armors.Count)];
            int lvl = Mathf.Max(1, itemLevel);

            // Armor rolls affixes too — gated by its Armour tag + item level (no WeaponInstance needed).
            var rarity = RollDropRarity();
            var affixes = AffixRoller.RollFor(baseArmor, lvl, rarity);

            int entity = SpawnItemPickup(blockPosition, baseArmor, parent);
            AttachRolledLoot(entity, rarity, lvl, affixes);
            return entity;
        }

        public int SpawnAccessoryPickup(Vector3 blockPosition, Transform parent = null, int itemLevel = 1)
        {
            // Accessories have no shared base type — gather amulets, rings and belts into one pool.
            var accessories = new List<ItemData>();
            accessories.AddRange(GetAllItemsOfType<AmuletItemData>());
            accessories.AddRange(GetAllItemsOfType<RingItemData>());
            accessories.AddRange(GetAllItemsOfType<BeltItemData>());
            if (accessories.Count == 0) return -1;

            var baseAccessory = accessories[Random.Range(0, accessories.Count)];
            int lvl = Mathf.Max(1, itemLevel);

            // Accessories roll affixes gated by their Accessory(/Ring/Amulet/Belt) tags + item level.
            var rarity = RollDropRarity();
            var affixes = AffixRoller.RollFor(baseAccessory, lvl, rarity);

            int entity = SpawnItemPickup(blockPosition, baseAccessory, parent);
            AttachRolledLoot(entity, rarity, lvl, affixes);
            return entity;
        }

        // Attaches a rolled-loot payload (rarity + item level + affixes) to the spawned pickup's view.
        private void AttachRolledLoot(int entity, ItemRarity rarity, int itemLevel, List<StatModifier> affixes)
        {
            if (entity < 0 || !viewLinkPool.Has(entity)) return;
            ref var vl = ref viewLinkPool.Get(entity);
            var view = vl.View != null ? vl.View.GetComponent<ItemPickupView>() : null;
            if (view != null) view.SetRolledLoot(rarity, itemLevel, affixes);
        }

        // Drop-rarity weights. Tune freely; affix counts follow rarity in AffixRoller.GetAffixCounts.
        private ItemRarity RollDropRarity()
        {
            float r = Random.value;
            if (r < 0.45f) return ItemRarity.Normal;
            if (r < 0.80f) return ItemRarity.Magic;
            if (r < 0.97f) return ItemRarity.Rare;
            return ItemRarity.Unique;
        }


        public int SpawnGemPickup(Vector3 blockPosition, Transform parent = null)
        {
            var gems = GetAllItemsOfType<GemItemData>();
            if (gems.Count > 0)
            {
                var randomGem = gems[Random.Range(0, gems.Count)];
                return SpawnItemPickup(blockPosition, randomGem, parent);
            }
            return -1;
        }

        public int SpawnFlaskPickup(Vector3 blockPosition, Transform parent = null)
        {
            var flasks = GetAllItemsOfType<FlaskItemData>();
            if (flasks.Count > 0)
            {
                var randomFlask = flasks[Random.Range(0, flasks.Count)];
                return SpawnItemPickup(blockPosition, randomFlask, parent);
            }
            return -1;
        }

        public int SpawnCurrencyPickup(Vector3 blockPosition, Transform parent = null)
        {
            var currencies = GetAllItemsOfType<CurrencyItemData>();
            if (currencies.Count > 0)
            {
                var randomCurrency = currencies[Random.Range(0, currencies.Count)];
                return SpawnItemPickup(blockPosition, randomCurrency, parent);
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

        // Additional max-health fraction granted per area level above 1 (e.g. 0.12 = +12% HP per level).
        private const float MonsterHpPerLevel = 0.12f;

        public int SpawnMonster(MonsterData data, Vector3 position, int level = 1)
        {
            if (data == null) return -1;

            var monsterView = monsterViewPool.Spawn();
            monsterView.transform.position = position;
            monsterView.Setup(data);

            var entity = CreateEntity<EnemyComponent>(monsterView.gameObject);

            int areaLevel = Mathf.Max(1, level);

            ref var enemy = ref enemyPool.Get(entity);
            enemy.Data = data;
            enemy.Level = areaLevel;
            enemy.MoveSpeed = data.MovementSpeed;

            // Scale max health with the area level so deeper runs feel tougher.
            float scaledMaxHealth = data.MaxHealth * (1f + (areaLevel - 1) * MonsterHpPerLevel);

            ref var health = ref enemyHealthPool.Add(entity);
            health.MaxHealth = scaledMaxHealth;
            health.CurrentHealth = scaledMaxHealth;

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
