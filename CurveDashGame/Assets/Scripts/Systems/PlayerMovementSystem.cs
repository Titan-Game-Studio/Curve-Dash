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
        private readonly EcsPool<EnemyComponent> enemyPool;
        private readonly EcsPool<WeaponPickupComponent> weaponPickupPool;
        private readonly EcsPool<ViewLinkComponent> viewLinkPool;
        private readonly EcsPool<FallingComponent> fallingPool;
        private readonly EcsPool<PlayerPassedComponent> playerPassedPool;
        private readonly EcsPool<PlayerHitCrystalEvent> playerHitCrystalPool;
        private readonly EcsPool<PlayerHitShieldEvent> playerHitShieldPool;
        private readonly EcsPool<PlayerHitWeaponEvent> playerHitWeaponPool;
        private readonly EcsPool<PlayerHitMountEvent> playerHitMountPool;
        private readonly EcsPool<PlayerHitAuraEvent> playerHitAuraPool;
        private readonly EcsPool<MountPickupComponent> mountPickupPool;
        private readonly EcsPool<AuraPickupComponent> auraPickupPool;
        private readonly EcsPool<PlayerHitObstacleEvent> playerHitObstaclePool;
        private readonly EcsPool<PlayerHitByEnemyEvent> playerHitByEnemyPool;

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
            enemyPool = world.GetPool<EnemyComponent>();
            weaponPickupPool = world.GetPool<WeaponPickupComponent>();
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
            playerHitByEnemyPool = world.GetPool<PlayerHitByEnemyEvent>();

            playerFilter = world.Filter<PlayerComponent>().End();
        }

        public void ChangeDirection(int ball)
        {
            ref var viewLinkComponent = ref viewLinkPool.Get(ball);
            var position = viewLinkComponent.Transform.position;

            if (!CheckEntityUnder(position, out var blockEntity))
            {
                Debug.LogWarning($"[Movement] ChangeDirection ignored! CheckEntityUnder returned FALSE at position {position}");
                return; // can't change direction when fall
            }

            ref var playerComponent = ref playerPool.Get(ball);
            
            // Log when character starts moving (Direction transitions from zero)
            if (playerComponent.Direction == Vector3.zero)
            {
                Debug.Log($"[Movement] Character started moving! Initial tap on block entity {blockEntity}.");
                Vector3 lastDir = playerComponent.LastNonZeroDirection != Vector3.zero 
                    ? playerComponent.LastNonZeroDirection 
                    : Vector3.forward;
                playerComponent.Direction = lastDir == Vector3.forward ? -Vector3.left : Vector3.forward;
            }
            else
            {
                Debug.Log($"[Movement] Direction changed on block entity {blockEntity}.");
                playerComponent.Direction = playerComponent.Direction == Vector3.forward ? -Vector3.left : Vector3.forward;
            }

            playerComponent.LastNonZeroDirection = playerComponent.Direction;

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
            ref var viewLinkComponent = ref viewLinkPool.Get(ball);
            
            // Lấy PlayerView từ Transform để điều khiển animation dựa theo di chuyển thực tế
            var ballView = viewLinkComponent.Transform != null ? viewLinkComponent.Transform.GetComponent<PlayerView>() : null;
            if (ballView != null)
            {
                ballView.MovementSpeed = playerComponent.Speed;
            }

            if (playerComponent.Direction == Vector3.zero)
            {
                // Nhân vật đứng yên -> Idle animation
                if (ballView != null) ballView.SetRunning(false);
                return;
            }
            else
            {
                // Nhân vật di chuyển -> Run animation
                if (ballView != null) ballView.SetRunning(true);
            }

            Transform playerTransform = viewLinkComponent.Transform;


            if (!fallingPool.Has(ball))
            {
                var position = viewLinkComponent.Transform.position;

                // Predict if the next step would overshoot and run off the road
                if (playerComponent.Direction != Vector3.zero)
                {
                    Vector3 nextPosition = position + playerComponent.Direction * playerComponent.Speed * Time.deltaTime;
                    if (!CheckEntityUnder(nextPosition, out var nextBlock) || !blockPool.Has(nextBlock))
                    {
                        // Snap precisely to the current block part's center to prevent overshooting
                        if (CheckEntityUnder(position, out var currentBlock) && blockPool.Has(currentBlock))
                        {
                            ref var blockLink = ref viewLinkPool.Get(currentBlock);
                            Vector3 closestPartPos = position;
                            float minDistance = float.MaxValue;
                            for (int i = 0; i < blockLink.Transform.childCount; i++)
                            {
                                var child = blockLink.Transform.GetChild(i);
                                if (child.gameObject.activeSelf)
                                {
                                    float dist = Vector3.Distance(position, child.position);
                                    if (dist < minDistance)
                                    {
                                        minDistance = dist;
                                        closestPartPos = child.position;
                                    }
                                }
                            }
                            playerTransform.position = new Vector3(closestPartPos.x, position.y, closestPartPos.z);
                            position = playerTransform.position; // update local position reference
                        }

                        // Stop the player smoothly
                        playerComponent.Direction = Vector3.zero;
                        if (ballView != null) ballView.SetRunning(false);
                        Debug.Log("[Movement] Edge reached! Stopped player safely. Tap to turn!");
                    }
                }

                // Process collisions and triggers only if the player is actively moving
                if (playerComponent.Direction != Vector3.zero)
                {
                    if (CheckCollisionWithPickup(position, out int pickupEntity))
                    {
                        if (crystalPool.Has(pickupEntity) && !playerHitCrystalPool.Has(pickupEntity))
                            playerHitCrystalPool.Add(pickupEntity);
                        else if (shieldPool.Has(pickupEntity) && !playerHitShieldPool.Has(pickupEntity))
                            playerHitShieldPool.Add(pickupEntity);
                        else if (weaponPickupPool.Has(pickupEntity) && !playerHitWeaponPool.Has(pickupEntity))
                            playerHitWeaponPool.Add(pickupEntity);
                        else if (mountPickupPool.Has(pickupEntity) && !playerHitMountPool.Has(pickupEntity))
                            playerHitMountPool.Add(pickupEntity);
                        else if (auraPickupPool.Has(pickupEntity) && !playerHitAuraPool.Has(pickupEntity))
                            playerHitAuraPool.Add(pickupEntity);
                    }

                    if (CheckCollisionWithObstacle(position, out int obstacle) && obstaclePool.Has(obstacle))
                        playerHitObstaclePool.Add(obstacle);

                    if (CheckCollisionWithEnemy(position, out int enemy) && enemyPool.Has(enemy))
                    {
                        if (!world.GetPool<PlayerHitByEnemyEvent>().Has(ball))
                            world.GetPool<PlayerHitByEnemyEvent>().Add(ball);
                    }

                    if (CheckEntityUnder(position, out var block) && blockPool.Has(block))
                    {
                        if (block != playerComponent.PreviousHitEntity)
                        {
                            if (playerComponent.PreviousHitEntity != null)
                                playerPassedPool.Add(playerComponent.PreviousHitEntity.Value);
                            playerComponent.PreviousHitEntity = block;
                        }
                    }
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
                playerTransform.GetChild(0).localScale = Vector3.one;
            }
        }

        private bool CheckEntityUnder(Vector3 position, out int hitEntity)
        {
            const float SphereCastRadius = 0.1f;
            var hits = Physics.SphereCastAll(position + Vector3.up * 0.5f, SphereCastRadius, Vector3.down, 1.5f);
            
            if (hits.Length > 0)
            {
                Debug.Log($"[CheckEntityUnder] Position: {position}, Hit count: {hits.Length}");
            }
            
            foreach (var hit in hits)
            {
                var linkView = hit.transform.GetComponent<EntityLinkView>();
                int entity = -1;
                bool unpacked = false;
                bool isBlock = false;
                
                if (linkView != null)
                {
                    unpacked = linkView.Entity.Unpack(world, out entity);
                }

                // Nếu bản thân đối tượng va chạm không có LinkView hoặc có nhưng không unpack được thực thể hợp lệ (do prefab có sẵn LinkView trống)
                // Ta sẽ tìm kiếm trên đối tượng Cha để giải mã thực thể đúng
                if (!unpacked && hit.transform.parent != null)
                {
                    var parentLinkView = hit.transform.parent.GetComponent<EntityLinkView>();
                    if (parentLinkView != null)
                    {
                        unpacked = parentLinkView.Entity.Unpack(world, out entity);
                    }
                }
                
                if (unpacked)
                {
                    isBlock = blockPool.Has(entity);
                }
                
                Debug.Log($"[CheckEntityUnder] Hit: {hit.transform.name} | Parent: {(hit.transform.parent != null ? hit.transform.parent.name : "null")} | Unpacked: {unpacked} | Entity: {entity} | IsBlock: {isBlock}");

                if (unpacked && isBlock)
                {
                    hitEntity = entity;
                    return true;
                }
            }

            hitEntity = -1;
            return false;
        }

        private bool CheckCollisionWithPickup(Vector3 position, out int hitEntity)
        {
            if (Physics.OverlapSphereNonAlloc(position, PlayerOverlapRadius, hitColliders, gameSettings.PickupMask) != 0)
            {
                var linkView = hitColliders[0].transform.GetComponent<EntityLinkView>();
                if (linkView != null)
                    return linkView.Entity.Unpack(world, out hitEntity);
            }

            hitEntity = -1;
            return false;
        }

        private bool CheckCollisionWithEnemy(Vector3 position, out int hitEntity)
        {
            if (Physics.OverlapSphereNonAlloc(position, PlayerOverlapRadius, hitColliders, gameSettings.EnemyMask) != 0)
            {
                var linkView = hitColliders[0].transform.GetComponent<EntityLinkView>();
                if (linkView != null)
                    return linkView.Entity.Unpack(world, out hitEntity);
            }

            hitEntity = -1;
            return false;
        }

        private bool CheckCollisionWithObstacle(Vector3 position, out int hitEntity)

        {
            if (Physics.OverlapSphereNonAlloc(position, PlayerOverlapRadius, hitColliders1, gameSettings.ObstacleMask) != 0)
            {
                var linkView = hitColliders1[0].transform.GetComponent<EntityLinkView>();
                if (linkView != null)
                    return linkView.Entity.Unpack(world, out hitEntity);
            }

            hitEntity = -1;
            return false;
        }
    }
}
