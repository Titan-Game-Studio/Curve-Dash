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
        private readonly EcsPool<ItemPickupComponent> itemPickupPool;
        private readonly EcsPool<ViewLinkComponent> viewLinkPool;
        private readonly EcsPool<FallingComponent> fallingPool;
        private readonly EcsPool<PlayerPassedComponent> playerPassedPool;
        private readonly EcsPool<PlayerHitCrystalEvent> playerHitCrystalPool;
        private readonly EcsPool<PlayerHitShieldEvent> playerHitShieldPool;
        private readonly EcsPool<PlayerHitItemEvent> playerHitItemPool;
        private readonly EcsPool<PlayerHitObstacleEvent> playerHitObstaclePool;
        private readonly EcsPool<PlayerHitByEnemyEvent> playerHitByEnemyPool;
        private readonly EcsPool<ObstacleHitPlayerTag> obstacleHitTagPool;

        private readonly EcsFilter playerFilter;

        private readonly Collider[] hitColliders = new Collider[8];
        private readonly Collider[] hitColliders1 = new Collider[8];

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
            itemPickupPool = world.GetPool<ItemPickupComponent>();
            viewLinkPool = world.GetPool<ViewLinkComponent>();
            fallingPool = world.GetPool<FallingComponent>();
            playerPassedPool = world.GetPool<PlayerPassedComponent>();
            playerHitCrystalPool = world.GetPool<PlayerHitCrystalEvent>();
            playerHitShieldPool = world.GetPool<PlayerHitShieldEvent>();
            playerHitItemPool = world.GetPool<PlayerHitItemEvent>();
            playerHitObstaclePool = world.GetPool<PlayerHitObstacleEvent>();
            playerHitByEnemyPool = world.GetPool<PlayerHitByEnemyEvent>();
            obstacleHitTagPool = world.GetPool<ObstacleHitPlayerTag>();

            playerFilter = world.Filter<PlayerComponent>().End();
        }

        public void ChangeDirection(int ball)
        {
            ref var viewLinkComponent = ref viewLinkPool.Get(ball);
            var position = viewLinkComponent.Transform.position;

            // Use a highly forgiving radius (0.6f) when changing direction so slight animation/combat offsets don't softlock tap controls!
            if (!CheckEntityUnder(position, out var blockEntity, 0.6f))
            {
                Debug.LogWarning($"[Movement] ChangeDirection ignored! CheckEntityUnder returned FALSE at position {position}");
                return; // can't change direction when fall
            }

            ref var playerComponent = ref playerPool.Get(ball);
            
            // Log when character starts moving (Direction transitions from zero)
            if (playerComponent.Direction == Vector3.zero)
            {
                // Debug.Log($"[Movement] Character started moving! Initial tap on block entity {blockEntity}.");
                Vector3 lastDir = playerComponent.LastNonZeroDirection != Vector3.zero 
                    ? playerComponent.LastNonZeroDirection 
                    : Vector3.forward;
                playerComponent.Direction = lastDir == Vector3.forward ? -Vector3.left : Vector3.forward;
            }
            else
            {
                // Debug.Log($"[Movement] Direction changed on block entity {blockEntity}.");
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
                        {
                            playerHitCrystalPool.Add(pickupEntity);
                        }
                        else if (shieldPool.Has(pickupEntity) && !playerHitShieldPool.Has(pickupEntity))
                        {
                            playerHitShieldPool.Add(pickupEntity);
                        }
                        else if (itemPickupPool.Has(pickupEntity) && !playerHitItemPool.Has(pickupEntity))
                        {
                            playerHitItemPool.Add(pickupEntity);
                        }
                    }

                    if (CheckCollisionWithObstacle(position, out int obstacle) && obstaclePool.Has(obstacle))
                    {
                        if (!obstacleHitTagPool.Has(obstacle))
                        {
                            obstacleHitTagPool.Add(obstacle);
                            playerHitObstaclePool.Add(obstacle);
                        }
                    }

                    if (CheckCollisionWithEnemy(position, out int enemy) && enemyPool.Has(enemy))
                    {
                        ref var enemyComp = ref enemyPool.Get(enemy);
                        if (enemyComp.AttackCooldown <= 0f && !enemyComp.IsChargingAttack)
                        {
                            enemyComp.IsChargingAttack = true;
                            enemyComp.AttackTimer = 0.5f; // 0.5 seconds of charging attack
                            UnityEngine.Debug.Log($"<color=orange>[EnemyAttack] Enemy {enemy} started charging attack! Hits in 0.5s...</color>");
                        }
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

        private bool CheckEntityUnder(Vector3 position, out int hitEntity, float radius = 0.1f)
        {
            // Spherecast from a safe height above the position to ensure it hits the block even if jumping, falling, or performing animation-driven root motion
            var hits = Physics.SphereCastAll(new Vector3(position.x, position.y + 5.0f, position.z), radius, Vector3.down, 10.0f);
            
            // if (hits.Length > 0)
            // {
            //     Debug.Log($"[CheckEntityUnder] Position: {position}, Hit count: {hits.Length}");
            // }
            
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
                
                // Debug.Log($"[CheckEntityUnder] Hit: {hit.transform.name} | Parent: {(hit.transform.parent != null ? hit.transform.parent.name : "null")} | Unpacked: {unpacked} | Entity: {entity} | IsBlock: {isBlock}");

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
            // Center the sphere at Y = 0.5f and expand radius to 1.2f to cover vertical offset.
            // Use ~0 (All Layers) and QueryTriggerInteraction.Collide unconditionally to bypass any layer mask mismatches
            // or disabled "Queries Hit Triggers" global settings in Unity Project settings.
            int numHits = Physics.OverlapSphereNonAlloc(position + Vector3.up * 0.5f, 1.2f, hitColliders, ~0, QueryTriggerInteraction.Collide);

            for (int i = 0; i < numHits; i++)
            {
                var hitCollider = hitColliders[i];
                var linkView = hitCollider.transform.GetComponentInParent<EntityLinkView>();

                if (linkView != null)
                {
                    if (linkView.Entity.Unpack(world, out hitEntity))
                    {
                        bool isCrystal = crystalPool.Has(hitEntity);
                        bool isShield = shieldPool.Has(hitEntity);
                        bool isItem = itemPickupPool.Has(hitEntity);

                        // Filter out non-pickup entities to prevent player self-selection or duplicate checks
                        if (isCrystal || isShield || isItem)
                        {
                            return true;
                        }
                    }
                }
            }

            hitEntity = -1;
            return false;
        }

        private bool CheckCollisionWithEnemy(Vector3 position, out int hitEntity)
        {
            int numHits = Physics.OverlapSphereNonAlloc(position + Vector3.up * 0.5f, 1.2f, hitColliders, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < numHits; i++)
            {
                var linkView = hitColliders[i].transform.GetComponentInParent<EntityLinkView>();
                if (linkView != null)
                {
                    if (linkView.Entity.Unpack(world, out hitEntity))
                    {
                        if (enemyPool.Has(hitEntity))
                        {
                            return true;
                        }
                    }
                }
            }

            hitEntity = -1;
            return false;
        }

        private bool CheckCollisionWithObstacle(Vector3 position, out int hitEntity)
        {
            int numHits = Physics.OverlapSphereNonAlloc(position + Vector3.up * 0.5f, 1.2f, hitColliders1, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < numHits; i++)
            {
                var linkView = hitColliders1[i].transform.GetComponentInParent<EntityLinkView>();
                if (linkView != null)
                {
                    if (linkView.Entity.Unpack(world, out hitEntity))
                    {
                        if (obstaclePool.Has(hitEntity))
                        {
                            return true;
                        }
                    }
                }
            }

            hitEntity = -1;
            return false;
        }
    }
}
