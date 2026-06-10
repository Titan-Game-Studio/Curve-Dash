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
        private readonly GameStateService gameState;
        private readonly BeltFlaskService beltFlaskService;

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
        // Frames to skip edge-stop after resuming from a standstill, preventing the check
        // from firing in the same (or next) frame before the player has moved off the snap point.
        private int _edgeCheckGraceFrames = 0;

        public PlayerMovementSystem(EcsWorld world, AudioPlayer audioPlayer, AudioSettings audioSettings,
            GameSettings gameSettings, GameStateService gameState, BeltFlaskService beltFlaskService)
        {
            this.world = world;
            this.audioPlayer = audioPlayer;
            this.audioSettings = audioSettings;
            this.gameSettings = gameSettings;
            this.gameState = gameState;
            this.beltFlaskService = beltFlaskService;

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

            Debug.Log($"[Move] ChangeDirection called — pos={position}");

            // Use a highly forgiving radius (0.6f) when changing direction so slight animation/combat offsets don't softlock tap controls!
            if (!CheckEntityUnder(position, out var blockEntity, 0.6f, verbose: true))
            {
                Debug.LogWarning($"[Move] ChangeDirection BLOCKED — no block found under pos={position}");
                return;
            }

            ref var playerComponent = ref playerPool.Get(ball);

            bool wasStopped = playerComponent.Direction == Vector3.zero;
            Vector3 prevDir = playerComponent.Direction;
            if (playerComponent.Direction == Vector3.zero)
            {
                Vector3 lastDir = playerComponent.LastNonZeroDirection != Vector3.zero
                    ? playerComponent.LastNonZeroDirection
                    : Vector3.forward;
                playerComponent.Direction = lastDir == Vector3.forward ? -Vector3.left : Vector3.forward;
            }
            else
            {
                playerComponent.Direction = playerComponent.Direction == Vector3.forward ? -Vector3.left : Vector3.forward;
            }

            playerComponent.LastNonZeroDirection = playerComponent.Direction;
            Debug.Log($"[Move] Direction: {prevDir} → {playerComponent.Direction}");

            // Give 2 frames of grace so the edge check doesn't fire in the same/next frame
            // before the player has moved off the snap position (frame-ordering issue with Zenject ITickables).
            if (wasStopped)
                _edgeCheckGraceFrames = 2;

            audioPlayer.Play(audioSettings.BallTurnSound);
        }

        public void Tick()
        {
            // Freeze the player while not actively playing (Title / GameOver / GameEnd).
            // ChangeDirection() is still callable externally so GameSystem can kick off the
            // auto-run the instant the game starts.
            if (!gameState.IsPlaying) return;

            foreach (var ball in playerFilter)
                Update(ball);
        }

        private void Update(int ball)
        {
            ref var playerComponent = ref playerPool.Get(ball);
            ref var viewLinkComponent = ref viewLinkPool.Get(ball);
            
            // Active flasks granting AddedMovementSpeed (Quicksilver-style, value = % increase) speed the
            // player up. Applied to the base ECS speed here so it reaches BOTH the real translation and the
            // run-animation factor — the PlayerView.MovementSpeed property is overwritten every frame and
            // never affected movement. Data-driven: any flask with a Buffs entry of AddedMovementSpeed works.
            float moveBonusPct = beltFlaskService != null ? beltFlaskService.SumActiveValue(StatType.AddedMovementSpeed) : 0f;
            float effectiveSpeed = playerComponent.Speed * (1f + moveBonusPct / 100f);

            // Lấy PlayerView từ Transform để điều khiển animation dựa theo di chuyển thực tế
            var ballView = viewLinkComponent.Transform != null ? viewLinkComponent.Transform.GetComponent<PlayerView>() : null;
            if (ballView != null)
            {
                ballView.MovementSpeed = effectiveSpeed;
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

                // Predict if the next step would overshoot and run off the road.
                // Skip this check for a few frames after the player resumes from standstill:
                // ChangeDirection() and this Update() can run in the same Zenject frame, so without
                // grace the check fires before the player has moved off the snap position and
                // immediately resets the direction to zero, making the tap appear to do nothing.
                if (_edgeCheckGraceFrames > 0)
                {
                    _edgeCheckGraceFrames--;
                }
                else if (playerComponent.Direction != Vector3.zero)
                {
                    Vector3 nextPosition = position + playerComponent.Direction * effectiveSpeed * Time.deltaTime;
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
                        Debug.Log($"[Move] Edge reached — snapped to {playerTransform.position}, waiting for tap");
                    }
                }

                // Process collisions and triggers only if the player is actively moving
                if (playerComponent.Direction != Vector3.zero)
                {
                    ProcessPickups(position);

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
                            enemyComp.AttackTimer = 0.5f;
                            // UnityEngine.Debug.Log($"<color=orange>[EnemyAttack] Enemy {enemy} started charging attack!</color>");
                        }
                    }

                    if (CheckEntityUnder(position, out var block) && blockPool.Has(block))
                    {
                        if (block != playerComponent.PreviousHitEntity)
                        {
                            if (playerComponent.PreviousHitEntity != null)
                            {
                                int prev = playerComponent.PreviousHitEntity.Value;
                                // Guard: entity must still be a live block and not already have the event
                                if (blockPool.Has(prev) && !playerPassedPool.Has(prev))
                                    playerPassedPool.Add(prev);
                                else
                                    Debug.LogWarning($"[Move] Skipped PlayerPassedComponent for entity {prev}: blockAlive={blockPool.Has(prev)} alreadyHas={playerPassedPool.Has(prev)}");
                            }
                            playerComponent.PreviousHitEntity = block;
                        }
                    }
                }
            }

            float speed = !fallingPool.Has(ball) ? effectiveSpeed : 1;

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

        private bool CheckEntityUnder(Vector3 position, out int hitEntity, float radius = 0.1f, bool verbose = false)
        {
            var origin = new Vector3(position.x, position.y + 5.0f, position.z);
            var hits = Physics.SphereCastAll(origin, radius, Vector3.down, 10.0f);

            if (verbose)
                Debug.Log($"[CheckEntityUnder] origin={origin} radius={radius} → {hits.Length} physics hit(s)");

            foreach (var hit in hits)
            {
                var linkView = hit.transform.GetComponent<EntityLinkView>();
                int entity = -1;
                bool unpacked = false;
                bool isBlock = false;

                if (linkView != null)
                    unpacked = linkView.Entity.Unpack(world, out entity);

                if (!unpacked && hit.transform.parent != null)
                {
                    var parentLinkView = hit.transform.parent.GetComponent<EntityLinkView>();
                    if (parentLinkView != null)
                        unpacked = parentLinkView.Entity.Unpack(world, out entity);
                }

                if (unpacked)
                    isBlock = blockPool.Has(entity);

                if (verbose)
                    Debug.Log($"  hit='{hit.transform.name}' parent='{(hit.transform.parent != null ? hit.transform.parent.name : "-")}' unpacked={unpacked} entity={entity} isBlock={isBlock}");

                if (unpacked && isBlock)
                {
                    hitEntity = entity;
                    return true;
                }
            }

            hitEntity = -1;
            return false;
        }

        private void ProcessPickups(Vector3 position)
        {
            // Center the sphere at Y = 0.5f and expand radius to 1.2f to cover vertical offset.
            // Use ~0 (All Layers) and QueryTriggerInteraction.Collide unconditionally
            int numHits = Physics.OverlapSphereNonAlloc(position + Vector3.up * 0.5f, 1.2f, hitColliders, ~0, QueryTriggerInteraction.Collide);

            for (int i = 0; i < numHits; i++)
            {
                var hitCollider = hitColliders[i];
                var linkView = hitCollider.transform.GetComponentInParent<EntityLinkView>();

                if (linkView != null)
                {
                    if (linkView.Entity.Unpack(world, out int hitEntity))
                    {
                        if (crystalPool.Has(hitEntity) && !playerHitCrystalPool.Has(hitEntity))
                        {
                            playerHitCrystalPool.Add(hitEntity);
                        }
                        if (shieldPool.Has(hitEntity) && !playerHitShieldPool.Has(hitEntity))
                        {
                            playerHitShieldPool.Add(hitEntity);
                        }
                        if (itemPickupPool.Has(hitEntity))
                        {
                            var pickupView = linkView.GetComponent<ItemPickupView>();
                            string itemName = pickupView != null && pickupView.ItemToGive != null ? pickupView.ItemToGive.ItemName : "Unknown Item";
                            Debug.Log($"<color=cyan>[Player Collision] Touching pickup object: '{hitCollider.gameObject.name}', Entity: {hitEntity}, Item: {itemName}</color>");

                            if (!playerHitItemPool.Has(hitEntity))
                            {
                                playerHitItemPool.Add(hitEntity);
                                Debug.Log($"<color=lime>[Pickup Triggered] Added PlayerHitItemEvent for '{itemName}' (Entity {hitEntity})</color>");
                            }
                        }
                    }
                }
            }
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
