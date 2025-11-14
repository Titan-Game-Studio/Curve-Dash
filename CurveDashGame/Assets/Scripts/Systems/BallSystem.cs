using Leopotam.EcsLite;
using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class BallSystem : ITickable
    {
        const float BallOverlapRadius = 0.4f;

        private readonly EcsWorld world;
        private readonly AudioPlayer audioPlayer;
        private readonly AudioSettings audioSettings;
        private readonly GameSettings gameSettings;

        private readonly EcsPool<BallComponent> ballPool;
        private readonly EcsPool<BlockComponent> blockPool;
        private readonly EcsPool<CrystalComponent> crystalPool;
        private readonly EcsPool<ObstacleComponent> obstaclePool;
        private readonly EcsPool<ViewLinkComponent> viewLinkPool;
        private readonly EcsPool<FallingComponent> fallingPool;
        private readonly EcsPool<BallPassedComponent> ballPassedPool;
        private readonly EcsPool<BallHitCrystalEvent> ballHitCrystalPool;
        private readonly EcsPool<BallHitObstacleEvent> ballHitObstaclePool;
        private readonly EcsFilter ballFilter;

        private readonly Collider[] hitColliders = new Collider[1];
        private readonly Collider[] hitColliders1 = new Collider[1];

        private float ballSize = 1f;

        public BallSystem(EcsWorld world, AudioPlayer audioPlayer, AudioSettings audioSettings,
            GameSettings gameSettings)
        {
            this.world = world;
            this.audioPlayer = audioPlayer;
            this.audioSettings = audioSettings;
            this.gameSettings = gameSettings;

            ballPool = world.GetPool<BallComponent>();
            blockPool = world.GetPool<BlockComponent>();
            crystalPool = world.GetPool<CrystalComponent>();
            obstaclePool = world.GetPool<ObstacleComponent>();
            viewLinkPool = world.GetPool<ViewLinkComponent>();
            fallingPool = world.GetPool<FallingComponent>();
            ballPassedPool = world.GetPool<BallPassedComponent>();
            ballHitCrystalPool = world.GetPool<BallHitCrystalEvent>();
            ballHitObstaclePool = world.GetPool<BallHitObstacleEvent>();
            ballFilter = world.Filter<BallComponent>().End();
        }

        public void ChangeDirection(int ball)
        {
            ref var viewLinkComponent = ref viewLinkPool.Get(ball);
            var position = viewLinkComponent.Transform.position;

            if (!CheckEntityUnder(position, out _))
                return; // can't change direction when fall

            ref var ballComponent = ref ballPool.Get(ball);
            ballComponent.Direction = ballComponent.Direction == Vector3.forward ? -Vector3.left : Vector3.forward;

            audioPlayer.Play(audioSettings.BallTurnSound);
        }

        public void Tick()
        {
            foreach (var ball in ballFilter)
                Update(ball);
        }

        private void Update(int ball)
        {
            ref var ballComponent = ref ballPool.Get(ball);

            if (ballComponent.Direction == Vector3.zero)
                return;

            ref var viewLinkComponent = ref viewLinkPool.Get(ball);
            Transform ballTransform = viewLinkComponent.Transform;

            if (!fallingPool.Has(ball))
            {
                var position = viewLinkComponent.Transform.position;

                if (CheckCollisionWithCrystal(position, out int crystal) && crystalPool.Has(crystal))
                    ballHitCrystalPool.Add(crystal);
                
                if (CheckCollisionWithObstacle(position, out int obstacle) && obstaclePool.Has(obstacle))
                    ballHitObstaclePool.Add(obstacle);

                if (CheckEntityUnder(position, out var block) && blockPool.Has(block))
                {
                    if (block != ballComponent.PreviousHitEntity)
                    {
                        if (ballComponent.PreviousHitEntity != null)
                            ballPassedPool.Add(ballComponent.PreviousHitEntity.Value);
                        ballComponent.PreviousHitEntity = block;
                    }
                }
                else
                {
                    fallingPool.Add(ball) = new FallingComponent { FallingDelay = 0.0f };
                }
            }

            float speed = !fallingPool.Has(ball) ? ballComponent.Speed : 1;

            ballTransform.Translate(ballComponent.Direction * speed * Time.deltaTime, Space.World);

            if (ballComponent.Direction != Vector3.zero)
            {
                ballTransform.rotation = Quaternion.LookRotation(ballComponent.Direction, Vector3.up);
            }

            if (!Mathf.Approximately(ballSize, ballComponent.Size))
            {
                ballSize = ballComponent.Size;
                ballTransform.GetChild(0).localScale = Vector3.one + (Vector3.one * (ballComponent.Size / 5f));
            }
        }

        private bool CheckEntityUnder(Vector3 position, out int hitEntity)
        {
            const float SphereCastRadius = 0.1f;
            if (Physics.SphereCast(position, SphereCastRadius, Vector3.down, out var hit, 0.3f))
            {
                var linkView = hit.transform.parent.GetComponent<EntityLinkView>();
                if (linkView != null)
                    return linkView.Entity.Unpack(world, out hitEntity);
            }

            hitEntity = -1;
            return false;
        }

        private bool CheckCollisionWithCrystal(Vector3 position, out int hitEntity)
        {
            if (Physics.OverlapSphereNonAlloc(position, BallOverlapRadius, hitColliders, gameSettings.CrystalMask) != 0)
            {
                var linkView = hitColliders[0].transform.GetComponent<EntityLinkView>();
                return linkView.Entity.Unpack(world, out hitEntity);
            }

            hitEntity = -1;
            return false;
        }

        private bool CheckCollisionWithObstacle(Vector3 position, out int hitEntity)
        {
            if (Physics.OverlapSphereNonAlloc(position, BallOverlapRadius, hitColliders1, gameSettings.ObstacleMask) !=
                0)
            {
                var linkView = hitColliders1[0].transform.GetComponent<EntityLinkView>();
                return linkView.Entity.Unpack(world, out hitEntity);
            }

            hitEntity = -1;
            return false;
        }
    }
}