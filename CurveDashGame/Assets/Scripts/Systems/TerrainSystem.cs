using Leopotam.EcsLite;
using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class TerrainSystem : ITickable
    {
        private const float TerrainLerpRateOffset = -0.05f;
        
        private readonly TerrainView terrainView;
        private readonly GameSettings gameSettings;

        private readonly Vector3 initialPosition = new Vector3(0, -5, 0);

        private readonly EcsFilter followingPlayerFilter;
        
        private readonly EcsPool<ViewLinkComponent> viewLinkPool;
        
        private MeshRenderer meshRenderer;
        
        public TerrainSystem(TerrainView terrainView, EcsWorld world, GameSettings gameSettings)
        {
            this.terrainView = terrainView;
            this.gameSettings = gameSettings;
            
            meshRenderer = terrainView.GetComponent<MeshRenderer>();

            viewLinkPool = world.GetPool<ViewLinkComponent>();

            followingPlayerFilter = world.Filter<PlayerComponent>().Exc<FallingComponent>().End();
            
            terrainView.transform.position = initialPosition;
        }
        
        public void Tick()
        {
            foreach (var followingBall in followingPlayerFilter)
            {
                ref var viewLinkComponent = ref viewLinkPool.Get(followingBall);
            
                var targetPos = viewLinkComponent.Transform.position + initialPosition;
                targetPos.y = -5f;
                terrainView.transform.position = targetPos;
                
                var pos = terrainView.transform.position;
                pos = Vector3.Lerp(pos, targetPos, gameSettings.TerrainLerpRate * Time.deltaTime);
                meshRenderer.material.mainTextureOffset = new Vector2(pos.x, pos.z) * TerrainLerpRateOffset;
            }
        }
    }
}
