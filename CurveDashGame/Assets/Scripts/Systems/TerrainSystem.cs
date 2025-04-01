using Leopotam.EcsLite;
using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class TerrainSystem : ITickable
    {
        private const float TerrainLerpRateOffset = -0.01f;
        
        private readonly TerrainView terrainView;
        private readonly GameSettings gameSettings;

        private readonly Vector3 initialPosition;

        private readonly EcsFilter followingBallFilter;
        
        private readonly EcsPool<ViewLinkComponent> viewLinkPool;
        
        private MeshRenderer meshRenderer;
        
        public TerrainSystem(TerrainView terrainView, EcsWorld world, GameSettings gameSettings)
        {
            this.terrainView = terrainView;
            this.gameSettings = gameSettings;
            
            meshRenderer = terrainView.GetComponent<MeshRenderer>();

            viewLinkPool = world.GetPool<ViewLinkComponent>();

            followingBallFilter = world.Filter<BallComponent>().Exc<FallingComponent>().End();
            
            initialPosition = terrainView.transform.position;
        }
        
        public void Tick()
        {
            foreach (var followingBall in followingBallFilter)
            {
                ref var viewLinkComponent = ref viewLinkPool.Get(followingBall);
            
                var pos = terrainView.transform.position;
                var targetPos = viewLinkComponent.Transform.position + initialPosition;
                targetPos.y = -5f;
                pos = Vector3.Lerp(pos, targetPos, gameSettings.TerrainLerpRate * Time.deltaTime);
                terrainView.transform.position = pos;
                meshRenderer.material.mainTextureOffset = new Vector2(pos.x, pos.z) * TerrainLerpRateOffset;
            }
        }
    }
}