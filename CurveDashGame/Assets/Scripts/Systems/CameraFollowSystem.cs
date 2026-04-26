using Leopotam.EcsLite;
using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class CameraFollowSystem : ITickable
    {
        private readonly CameraView cameraView;
        private readonly GameSettings gameSettings;

        private readonly Vector3 initialPosition;

        private readonly EcsFilter followingPlayerFilter;
        
        private readonly EcsPool<ViewLinkComponent> viewLinkPool;
        
        public CameraFollowSystem(CameraView cameraView, EcsWorld world, GameSettings gameSettings)
        {
            this.cameraView = cameraView;
            this.gameSettings = gameSettings;

            viewLinkPool = world.GetPool<ViewLinkComponent>();

            followingPlayerFilter = world.Filter<PlayerComponent>().Exc<FallingComponent>().End();
            
            initialPosition = cameraView.transform.position;
        }
        
        public void Tick()
        {
            foreach (var followingBall in followingPlayerFilter)
            {
                ref var viewLinkComponent = ref viewLinkPool.Get(followingBall);
            
                var pos = cameraView.transform.position;
                var targetPos = viewLinkComponent.Transform.position + initialPosition;
                pos = Vector3.Lerp(pos, targetPos, gameSettings.CameraLerpRate * Time.deltaTime);
                cameraView.transform.position = pos;
            }
        }
    }
}
