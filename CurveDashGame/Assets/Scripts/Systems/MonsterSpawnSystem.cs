using Leopotam.EcsLite;
using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class MonsterSpawnSystem : ITickable
    {
        private readonly ObjectSpawner _spawner;
        private readonly MonsterCatalog _catalog;
        private readonly EcsFilter _playerFilter;
        private readonly EcsPool<ViewLinkComponent> _viewLinkPool;
        
        private float _spawnTimer;
        private const float SpawnInterval = 2.5f; // Sinh quái mỗi 2.5 giây
        private const float SpawnDistance = 40f; // Sinh ở phía trước 40m
        private const float RandomXRange = 10f; // Phạm vi ngẫu nhiên theo chiều ngang

        public MonsterSpawnSystem(EcsWorld world, ObjectSpawner spawner, MonsterCatalog catalog)
        {
            _spawner = spawner;
            _catalog = catalog;
            _playerFilter = world.Filter<PlayerComponent>().Inc<ViewLinkComponent>().End();
            _viewLinkPool = world.GetPool<ViewLinkComponent>();
        }

        public void Tick()
        {
            _spawnTimer += Time.deltaTime;

            if (_spawnTimer >= SpawnInterval)
            {
                _spawnTimer = 0f;
                SpawnMonster();
            }
        }

        private void SpawnMonster()
        {
            foreach (var entity in _playerFilter)
            {
                ref var viewLink = ref _viewLinkPool.Get(entity);
                Vector3 playerPos = viewLink.Transform.position;
                Vector3 playerForward = viewLink.Transform.forward;

                // Tính toán vị trí Spawn
                Vector3 spawnPos = playerPos + playerForward * SpawnDistance;
                spawnPos.x += Random.Range(-RandomXRange, RandomXRange);
                spawnPos.y = 0.5f; // Độ cao cơ bản

                var data = _catalog.GetRandomMonster();
                if (data != null)
                {
                    _spawner.SpawnMonster(data, spawnPos);
                    Debug.Log($"[MonsterSpawn] Spawned {data.MonsterName} at {spawnPos}");
                }
            }
        }
    }
}
