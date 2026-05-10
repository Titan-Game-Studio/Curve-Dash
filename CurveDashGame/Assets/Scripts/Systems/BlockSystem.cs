using System.Collections.Generic;
using Leopotam.EcsLite;
using UnityEngine;
using UnityEngine.Assertions;
using Zenject;

namespace STG.CurveDash
{
    public class BlockSystem : ITickable
    {
        private const int BlocksOnScreen = 30;

        private readonly EcsWorld world;
        private readonly ObjectSpawner spawner;
        private readonly GameplayStrategiesProvider gameplayStrategies;
     
        private readonly GameSettings gameSettings;
        private readonly MonsterCatalog monsterCatalog;

        private Vector3 lastSpawnPos;
        private readonly Queue<int> liveBlocksEntities = new Queue<int>();
        private readonly Queue<int> passedBlocksEntities = new Queue<int>();
        
        private readonly EcsPool<BlockComponent> blockPool;
        private readonly EcsPool<FallingComponent> fallingPool;
        private readonly EcsPool<ViewLinkComponent> viewLinkPool;

        private readonly EcsFilter playerPassedFilter;

        private int BlockPartsCount = 1;

        public BlockSystem(EcsWorld world, ObjectSpawner spawner,
            GameplayStrategiesProvider gameplayStrategies, GameSettings gameSettings, MonsterCatalog monsterCatalog)
        {
            this.world = world;
            this.spawner = spawner;
            this.gameplayStrategies = gameplayStrategies;
            this.gameSettings = gameSettings;
            this.monsterCatalog = monsterCatalog;


            blockPool = world.GetPool<BlockComponent>();
            fallingPool = world.GetPool<FallingComponent>();
            viewLinkPool = world.GetPool<ViewLinkComponent>();
            
            playerPassedFilter = world.Filter<PlayerPassedComponent>()
                .Exc<FallingComponent>()
                .End();
        }
        
        public void CreateStartBlocks(int blockPartsCount)
        {
            BlockPartsCount = blockPartsCount;

            var startPlatform = spawner.SpawnStartPlatform(Color.gray);
            liveBlocksEntities.Enqueue(startPlatform);

            lastSpawnPos = new Vector3(1f, 0f, 1f);

            for (int i = 0; i < BlocksOnScreen; i++)
                SpawnNextBlocks();
        }
        
        public void ClearBlocks()
        {
            liveBlocksEntities.Clear();
            passedBlocksEntities.Clear();
            gameplayStrategies.Reset();
        }
        
        public void Tick()
        {
            foreach (var block in playerPassedFilter)
                OnMovedToNextBlock(block);
        }
        
        private void OnMovedToNextBlock(int block)
        {
            if (!liveBlocksEntities.Contains(block))
                return;

            int block1 = -1;
            while (block1 != block)
            {
                block1 = liveBlocksEntities.Dequeue();
                
                // Enqueue the block that was just passed into our trailing queue
                passedBlocksEntities.Enqueue(block1);
                
                // If we have more than 10 blocks behind the player, despawn the oldest one
                if (passedBlocksEntities.Count > 10)
                {
                    int oldestBlock = passedBlocksEntities.Dequeue();
                    FallDownBlock(oldestBlock);
                }

                SpawnNextBlocks();
            }
        }

        private void SpawnNextBlocks()
        {
            bool rightDirection = Random.value > 0.5f;

            lastSpawnPos += rightDirection ? Vector3.right : Vector3.forward;
            
            float hue = Random.Range(0f, 1f);
            Color color = Color.HSVToRGB(hue, 1f, 1f);
            var group = SpawnBlockWithCrystalAndHole(lastSpawnPos, rightDirection, color);
            liveBlocksEntities.Enqueue(group);
        }

        private int SpawnBlockWithCrystalAndHole(Vector3 spawnPos, bool rightSide, Color color)
        {
            var block = spawner.SpawnBlock(BlockPartsCount, spawnPos, rightSide, color);

            if (gameSettings.GameMode == GameMode.Holes)
            {
                if (gameplayStrategies.GetBlockHolesStrategy().IsTimeToHole())
                    MakeHole(block);
            }

            float randomVal = Random.value;
            if (randomVal < 0.03f) // 3% chance for Weapon
            {
                SpawnWeaponPickup(block);
            }
            /* TẠM THỜI TẮT
            else if (randomVal < 0.05f) // 2% chance for Mount
            {
                SpawnMountPickup(block);
            }
            else if (randomVal < 0.07f) // 2% chance for Aura
            {
                SpawnAuraPickup(block);
            }
            else if (gameplayStrategies.GetShieldSpawnStrategy().ShouldSpawn())
            {
                SpawnShield(block);
            }
            */
            else if (gameplayStrategies.GetCrystalSpawnStrategy().ShouldSpawn())
            {
                SpawnCrystal(block);
            }
            else if (gameplayStrategies.GetObstacleSpawnStrategy().ShouldSpawn())
            {
                SpawnObstacle(block);
            }
            else if (Random.value < 0.15f) // 15% chance to spawn a monster on any block that doesn't have other items
            {
                SpawnMonster(block);
            }



            // Clouds can spawn independently from other objects
            if (gameplayStrategies.GetCloudSpawnStrategy().ShouldSpawn())
            {
                SpawnCloud(block);
            }

            return block;
        }
        
        private void MakeHole(int block)
        {
            int childIndex = Random.Range(0, 1) == 0 ? 0 : 2;
            ref var viewLinkComponent = ref viewLinkPool.Get(block);
            var child = viewLinkComponent.Transform.GetChild(childIndex);
            child.gameObject.SetActive(false);
        }

        private void SpawnCrystal(int block)
        {
            ref var viewLinkComponent = ref viewLinkPool.Get(block);
            var child = viewLinkComponent.Transform.GetChild(Random.Range(0, BlockPartsCount - 1));
            if (child.gameObject.activeSelf)
            {
                int crystal = spawner.SpawnCrystal(child.position);
                ref var blockComponent = ref blockPool.Get(block);
                blockComponent.Crystal = world.PackEntity(crystal);
            }
        }

        private void SpawnShield(int block)
        {
            ref var viewLinkComponent = ref viewLinkPool.Get(block);
            var child = viewLinkComponent.Transform.GetChild(Random.Range(0, BlockPartsCount - 1));
            if (child.gameObject.activeSelf)
            {
                int shield = spawner.SpawnShield(child.position);
                ref var blockComponent = ref blockPool.Get(block);
                blockComponent.Crystal = world.PackEntity(shield);
            }
        }

        private void SpawnWeaponPickup(int block)
        {
            ref var viewLinkComponent = ref viewLinkPool.Get(block);
            var child = viewLinkComponent.Transform.GetChild(Random.Range(0, BlockPartsCount - 1));
            if (child.gameObject.activeSelf)
            {
                int weapon = spawner.SpawnWeaponPickup(child.position);
                ref var blockComponent = ref blockPool.Get(block);
                blockComponent.Crystal = world.PackEntity(weapon);
            }
        }

        private void SpawnMountPickup(int block)
        {
            ref var viewLinkComponent = ref viewLinkPool.Get(block);
            var child = viewLinkComponent.Transform.GetChild(Random.Range(0, BlockPartsCount - 1));
            if (child.gameObject.activeSelf)
            {
                int mount = spawner.SpawnMountPickup(child.position);
                ref var blockComponent = ref blockPool.Get(block);
                blockComponent.Crystal = world.PackEntity(mount);
            }
        }

        private void SpawnAuraPickup(int block)
        {
            ref var viewLinkComponent = ref viewLinkPool.Get(block);
            var child = viewLinkComponent.Transform.GetChild(Random.Range(0, BlockPartsCount - 1));
            if (child.gameObject.activeSelf)
            {
                int aura = spawner.SpawnAuraPickup(child.position);
                ref var blockComponent = ref blockPool.Get(block);
                blockComponent.Crystal = world.PackEntity(aura);
            }
        }
        private void SpawnObstacle(int block)
        {
            ref var viewLinkComponent = ref viewLinkPool.Get(block);
            var child = viewLinkComponent.Transform.GetChild(Random.Range(0, BlockPartsCount - 1));
            if (child.gameObject.activeSelf)
            {
                int crystal = spawner.SpawnObstacle(child.position);
                ref var blockComponent = ref blockPool.Get(block);
                blockComponent.Crystal = world.PackEntity(crystal);
            }
        }

        private void SpawnCloud(int block)
        {
            ref var viewLinkComponent = ref viewLinkPool.Get(block);
            var child = viewLinkComponent.Transform.GetChild(Random.Range(0, BlockPartsCount - 1));
            if (child.gameObject.activeSelf)
            {
                // Clouds do not attach uniquely to blockComponent.Crystal slot.
                // They just spawn at the location and fly away.
                spawner.SpawnCloud(child.position);
            }
        }

        private void SpawnMonster(int block)
        {
            if (monsterCatalog == null) return;

            ref var viewLinkComponent = ref viewLinkPool.Get(block);

            var child = viewLinkComponent.Transform.GetChild(Random.Range(0, BlockPartsCount - 1));
            if (child.gameObject.activeSelf)
            {
                var data = monsterCatalog.GetRandomMonster();
                if (data != null)
                {
                    // Sinh quái vật làm con của ô đường đi (child) để nó không bị "trôi"
                    int monster = spawner.SpawnMonster(data, child.position);
                    
                    // Cập nhật: Tìm GameObject vừa sinh và gán Parent
                    ref var monsterLink = ref world.GetPool<ViewLinkComponent>().Get(monster);
                    if (monsterLink.Transform != null)
                    {
                        monsterLink.Transform.SetParent(child);
                    }

                    ref var blockComponent = ref blockPool.Get(block);

                    blockComponent.Crystal = world.PackEntity(monster);
                }
            }
        }

        private void FallDownBlock(int block)
        {
            fallingPool.Add(block) = new FallingComponent { FallingDelay = 0.3f };

            ref var blockComponent = ref blockPool.Get(block);
            if (blockComponent.Crystal != null && blockComponent.Crystal.Value.Unpack(world, out int crystal))
                fallingPool.Add(crystal) = new FallingComponent { FallingDelay = 0.4f };
        }
    }
}
