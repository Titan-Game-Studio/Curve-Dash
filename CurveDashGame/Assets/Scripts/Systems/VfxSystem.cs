using Leopotam.EcsLite;
using Zenject;
using UnityEngine;

namespace STG.CurveDash
{
    /// <summary>
    /// ECS System that processes VfxSpawnRequest entities.
    /// It consumes these requests, spawns them from the VFX pool, and disposes of the temporary ECS entities.
    /// </summary>
    public class VfxSystem : ITickable
    {
        private static VfxSystem _instance;

        private readonly EcsWorld world;
        private readonly EcsFilter filter;
        private readonly EcsPool<VfxSpawnRequest> requestPool;

        public VfxSystem(EcsWorld world)
        {
            this.world = world;
            filter = world.Filter<VfxSpawnRequest>().End();
            requestPool = world.GetPool<VfxSpawnRequest>();
            _instance = this;
        }

        /// <summary>
        /// Global bridge allowing ScriptableObjects, views, and external assets to issue 
        /// an ECS-compliant VfxSpawnRequest.
        /// </summary>
        public static void RequestSpawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return;

            if (_instance == null || _instance.world == null)
            {
                // Safety fallback if the ECS world or system is not initialized yet
                VfxPoolManager.Instance.Spawn(prefab, position, rotation);
                return;
            }

            // Create a clean, temporary ECS event/request entity
            int entity = _instance.world.NewEntity();
            ref var request = ref _instance.requestPool.Add(entity);
            request.Prefab = prefab;
            request.Position = position;
            request.Rotation = rotation;
        }

        public void Tick()
        {
            // Process and consume all spawn requests
            foreach (var entity in filter)
            {
                ref var request = ref requestPool.Get(entity);
                if (request.Prefab != null)
                {
                    // Spawn using the central pool manager
                    VfxPoolManager.Instance.Spawn(request.Prefab, request.Position, request.Rotation);
                }

                // Delete the temporary event entity
                world.DelEntity(entity);
            }
        }
    }
}
