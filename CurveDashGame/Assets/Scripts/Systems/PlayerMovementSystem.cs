using Leopotam.EcsLite;
using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class PlayerMovementSystem : ITickable
    {
        const float PlayerOverlapRadius = 0.4f;

        private readonly EcsWorld world;
        private readonly AudioPlayer audioPlayer;
        private readonly AudioSettings audioSettings;
        private readonly GameSettings gameSettings;

        private readonly EcsPool<PlayerComponent> playerPool;
        private readonly EcsPool<BlockComponent> blockPool;
        private readonly EcsPool<CrystalComponent> crystalPool;
        private readonly EcsPool<ObstacleComponent> obstaclePool;
        private readonly EcsPool<ShieldComponent> shieldPool;
        private readonly EcsPool<ViewLinkComponent> viewLinkPool;
        private readonly EcsPool<FallingComponent> fallingPool;
        private readonly EcsPool<PlayerPassedComponent> playerPassedPool;
        private readonly EcsPool<PlayerHitCrystalEvent> playerHitCrystalPool;
        private readonly EcsPool<PlayerHitShieldEvent> playerHitShieldPool;
        private readonly EcsPool<PlayerHitWeaponEvent> playerHitWeaponPool;
        private readonly EcsPool<PlayerHitMountEvent> playerHitMountPool;
        private readonly EcsPool<PlayerHitAuraEvent> playerHitAuraPool;
        private readonly EcsPool<WeaponPickupComponent> weaponPickupPool;
        private readonly EcsPool<MountPickupComponent> mountPickupPool;
        private readonly EcsPool<AuraPickupComponent> auraPickupPool;
        private readonly EcsPool<PlayerHitObstacleEvent> playerHitObstaclePool;
        private readonly EcsFilter playerFilter;

        private readonly Collider[] hitColliders = new Collider[1];
        private readonly Collider[] hitColliders1 = new Collider[1];

        private float playerSize = 1f;

        public PlayerMovementSystem(EcsWorld world, AudioPlayer audioPlayer, AudioSettings audioSettings,
            GameSettings gameSettings)
        {
            this.world = world;
            this.audioPlayer = audioPlayer;
            this.audioSettings = audioSettings;
            this.gameSettings = gameSettings;

            playerPool = world.GetPool<PlayerComponent>();
            blockPool = world.GetPool<BlockComponent>();
            crystalPool = world.GetPool<CrystalComponent>();
            obstaclePool = world.GetPool<ObstacleComponent>();
            shieldPool = world.GetPool<ShieldComponent>();
            viewLinkPool = world.GetPool<ViewLinkComponent>();
            fallingPool = world.GetPool<FallingComponent>();
            playerPassedPool = world.GetPool<PlayerPassedComponent>();
            playerHitCrystalPool = world.GetPool<PlayerHitCrystalEvent>();
            playerHitShieldPool = world.GetPool<PlayerHitShieldEvent>();
            playerHitWeaponPool = world.GetPool<PlayerHitWeaponEvent>();
            playerHitMountPool = world.GetPool<PlayerHitMountEvent>();
            playerHitAuraPool = world.GetPool<PlayerHitAuraEvent>();
            weaponPickupPool = world.GetPool<WeaponPickupComponent>();
            mountPickupPool = world.GetPool<MountPickupComponent>();
            auraPickupPool = world.GetPool<AuraPickupComponent>();
            playerHitObstaclePool = world.GetPool<PlayerHitObstacleEvent>();
            playerFilter = world.Filter<PlayerComponent>().End();
        }

        public void ChangeDirection(int ball)
        {
            ref var viewLinkComponent = ref viewLinkPool.Get(ball);
            var position = viewLinkComponent.Transform.position;

            if (!CheckEntityUnder(position, out _))
                return; // can't change direction when fall

            ref var playerComponent = ref playerPool.Get(ball);
            playerComponent.Direction = playerComponent.Direction == Vector3.forward ? -Vector3.left : Vector3.forward;

            audioPlayer.Play(audioSettings.BallTurnSound);
        }

        public void Tick()
        {
            foreach (var ball in playerFilter)
                Update(ball);
        }

        private void Update(int ball)
        {
            ref var playerComponent = ref playerPool.Get(ball);

            if (playerComponent.Direction == Vector3.zero)
                return;

            ref var viewLinkComponent = ref viewLinkPool.Get(ball);
            Transform playerTransform = viewLinkComponent.Transform;

            if (!fallingPool.Has(ball))
            {
                var position = viewLinkComponent.Transform.position;

                if (CheckCollisionWithPickup(position, out int pickupEntity))
                {
                    if (crystalPool.Has(pickupEntity))
                        playerHitCrystalPool.Add(pickupEntity);
                    else if (shieldPool.Has(pickupEntity))
                        playerHitShieldPool.Add(pickupEntity);
                    else if (weaponPickupPool.Has(pickupEntity))
                        playerHitWeaponPool.Add(pickupEntity);
                    else if (mountPickupPool.Has(pickupEntity))
                        playerHitMountPool.Add(pickupEntity);
                    else if (auraPickupPool.Has(pickupEntity))
                        playerHitAuraPool.Add(pickupEntity);
                }
                
                if (CheckCollisionWithObstacle(position, out int obstacle) && obstaclePool.Has(obstacle))
                    playerHitObstaclePool.Add(obstacle);

                if (CheckEntityUnder(position, out var block) && blockPool.Has(block))
                {
                    if (block != playerComponent.PreviousHitEntity)
                    {
                        if (playerComponent.PreviousHitEntity != null)
                            playerPassedPool.Add(playerComponent.PreviousHitEntity.Value);
                        playerComponent.PreviousHitEntity = block;
                    }
                }
                else
                {
                    fallingPool.Add(ball) = new FallingComponent { FallingDelay = 0.0f };
                }
            }

            float speed = !fallingPool.Has(ball) ? playerComponent.Speed : 1;

            playerTransform.Translate(playerComponent.Direction * speed * Time.deltaTime, Space.World);

            if (playerComponent.Direction != Vector3.zero)
            {
                playerTransform.rotation = Quaternion.LookRotation(playerComponent.Direction, Vector3.up);
            }

            if (!Mathf.Approximately(playerSize, playerComponent.Size))
            {
                playerSize = playerComponent.Size;
                playerTransform.GetChild(0).localScale = Vector3.one + (Vector3.one * (playerComponent.Size / 5f));
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

        private bool CheckCollisionWithPickup(Vector3 position, out int hitEntity)
        {
            if (Physics.OverlapSphereNonAlloc(position, PlayerOverlapRadius, hitColliders, gameSettings.PickupMask) != 0)
            {
                var linkView = hitColliders[0].transform.GetComponent<EntityLinkView>();
                return linkView.Entity.Unpack(world, out hitEntity);
            }

            hitEntity = -1;
            return false;
        }

        private bool CheckCollisionWithObstacle(Vector3 position, out int hitEntity)
        {
            if (Physics.OverlapSphereNonAlloc(position, PlayerOverlapRadius, hitColliders1, gameSettings.ObstacleMask) !=
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
