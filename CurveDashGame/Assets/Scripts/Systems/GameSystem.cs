using Leopotam.EcsLite;
using UnityEngine;
using Zenject;
using TGS.Ads;

namespace STG.CurveDash
{
    public enum GameMode
    {
        Easy,
        Normal,
        Hard,
        Holes
    }

    public struct PartsCountInBlock
    {
        public int Min;
        public int Max;
    }

    public class GameSystem : IInitializable, ITickable
    {
        private readonly EcsWorld world;
        private readonly GameSettings gameSettings;
        private readonly AudioSettings audioSettings;
        private readonly PlayerMovementSystem ballSystem;
        private readonly PlayerStatService playerStatService;
        private readonly ObjectSpawner spawner;
        private readonly BlockSystem blockSystem;
        private readonly AudioPlayer audioPlayer;
        private readonly DataManager dataManager;

        private readonly EcsPool<PlayerComponent> playerPool;
        private readonly EcsFilter playerFilter;

        private readonly EcsFilter playerPassedFilter;
        private readonly EcsFilter playerHitCrystalFilter;
        private readonly EcsFilter playerHitObstacleFilter;
        private readonly EcsFilter playerHitShieldFilter;
        private readonly EcsFilter playerHitByEnemyFilter;

        private readonly EcsFilter playerFallingFilter;
        private readonly EcsFilter playerLevelUpFilter;

        private readonly EcsFilter gameStateFilter;
        private readonly EcsPool<GameStateComponent> gameStatePool;
        private readonly EcsPool<ViewLinkComponent> viewLinkPool;

        [Inject] private IAdService adService;
        [Inject] private AssetManager assetManager;

        private const float BallSpawnHeight = 0.65f;
        private bool isAudioLoaded;
        private AudioClip currentBgmClip;
        private AudioKey currentBgmKey;

        public GameSystem(EcsWorld world, GameSettings gameSettings, AudioPlayer audioPlayer,
            AudioSettings audioSettings, PlayerMovementSystem ballSystem, PlayerStatService playerStatService,
            ObjectSpawner spawner, BlockSystem blockSystem, DataManager dataManager)
        {
            this.world = world;
            this.gameSettings = gameSettings;
            this.audioPlayer = audioPlayer;
            this.audioSettings = audioSettings;
            this.ballSystem = ballSystem;
            this.playerStatService = playerStatService;
            this.spawner = spawner;
            this.blockSystem = blockSystem;
            this.dataManager = dataManager;

            playerPool = world.GetPool<PlayerComponent>();
            playerFilter = world.Filter<PlayerComponent>().End();

            playerPassedFilter = world.Filter<PlayerPassedComponent>().End();
            playerHitCrystalFilter = world.Filter<PlayerHitCrystalEvent>().End();
            playerHitObstacleFilter = world.Filter<PlayerHitObstacleEvent>().End();
            playerHitByEnemyFilter = world.Filter<PlayerHitByEnemyEvent>().End();

            playerHitShieldFilter = world.Filter<PlayerHitShieldEvent>().End();
            playerFallingFilter = world.Filter<PlayerComponent>().Inc<FallingComponent>().End();

            gameStatePool = world.GetPool<GameStateComponent>();
            gameStateFilter = world.Filter<GameStateComponent>().End();
            viewLinkPool = world.GetPool<ViewLinkComponent>();

            playerLevelUpFilter = world.Filter<PlayerLevelUpComponent>().End();
        }

        public bool HasGameState()
        {
            return gameStateFilter.GetEntitiesCount() > 0;
        }

        public ref GameStateComponent GetGameState()
        {
            var playerStat = gameStateFilter.GetRawEntities()[0];
            return ref gameStatePool.Get(playerStat);
        }

        public void Initialize()
        {
            assetManager.InitializeAddressables();
            ShowTitle(false);
        }

        private void LoadCoreAudio()
        {
            assetManager.LoadAudioAsync(AudioKey.BallHitCrystal, clip => audioSettings.BallHitCrystalSound = clip);
            assetManager.LoadAudioAsync(AudioKey.GameStart, clip => audioSettings.GameStartSound = clip);
            assetManager.LoadAudioAsync(AudioKey.BallFall, clip => audioSettings.BallFallSound = clip);
            assetManager.LoadAudioAsync(AudioKey.BallTurn, clip => audioSettings.BallTurnSound = clip);
            assetManager.LoadAudioAsync(AudioKey.NextLevel, clip => audioSettings.NextLevelSound = clip);

            LoadBgmForMode(gameSettings.GameMode);
        }

        private void LoadBgmForMode(GameMode mode)
        {
            AudioKey targetKey = GetBgmKeyForMode(mode);
            if (currentBgmKey == targetKey && currentBgmClip != null) return;

            currentBgmKey = targetKey;

            assetManager.LoadAudioAsync(targetKey, clip =>
            {
                currentBgmClip = clip;

                if (gameStateFilter.GetEntitiesCount() == 0) return;
                var gameState = gameStateFilter.GetRawEntities()[0];
                ref var gameStateComponent = ref gameStatePool.Get(gameState);
                if (gameStateComponent.State == GameState.Playing)
                {
                    PlayBackgroundMusic();
                }
            });
        }

        private AudioKey GetBgmKeyForMode(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Hard:
                    return AudioKey.BGM_Soulforge;
                case GameMode.Holes:
                    return AudioKey.BGM_Wolfspire;
                case GameMode.Easy:
                    return AudioKey.BGM_Thundershade;
                case GameMode.Normal:
                    return AudioKey.BGM_Thundershade;
                default:
                    return AudioKey.BGM_Thundershade;
            }
        }

        private void PlayBackgroundMusic()
        {
            if (currentBgmClip != null)
                audioPlayer.Play(currentBgmClip);
        }

        private void StopBackgroundMusic()
        {
            audioPlayer.Stop();
        }

        public void Tick()
        {
            if (gameStateFilter.GetEntitiesCount() == 0) return;
            var gameState = gameStateFilter.GetRawEntities()[0];
            ref var gameStateComponent = ref gameStatePool.Get(gameState);

            bool isEscapePressed = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current[UnityEngine.InputSystem.Key.Escape].wasPressedThisFrame;

            bool screenTapOrClick = false;
            if (UnityEngine.InputSystem.Pointer.current != null && UnityEngine.InputSystem.Pointer.current.press.wasPressedThisFrame)
            {
                bool overInteractableUI = false;
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null)
                {
                    var ped = new UnityEngine.EventSystems.PointerEventData(es)
                    {
                        position = UnityEngine.InputSystem.Pointer.current.position.ReadValue()
                    };
                    var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                    es.RaycastAll(ped, results);

                    // DEBUG: log every UI hit so we can see what's blocking input
                    if (results.Count > 0)
                    {
                        var sb = new System.Text.StringBuilder($"[Input] Click at {ped.position} — RaycastAll hits ({results.Count}): ");
                        foreach (var r in results)
                        {
                            bool sel = r.gameObject.GetComponentInParent<UnityEngine.UI.Selectable>() != null;
                            sb.Append($"'{r.gameObject.name}'[sel={sel}] ");
                            if (sel) overInteractableUI = true;
                        }
                        Debug.Log(sb.ToString());
                    }
                    else
                    {
                        Debug.Log($"[Input] Click at {ped.position} — no UI hits, tap accepted (state={gameStateComponent.State})");
                    }
                }
                else
                {
                    Debug.Log("[Input] EventSystem is null — tap accepted unconditionally");
                }

                if (!overInteractableUI)
                    screenTapOrClick = true;
                else
                    Debug.LogWarning("[Input] Tap BLOCKED by interactable UI (see hits above)");
            }

            switch (gameStateComponent.State)
            {
                case GameState.Title:
                    if (Application.platform == RuntimePlatform.Android)
                    {
                        if (isEscapePressed)
                            Application.Quit();
                    }

                    if (screenTapOrClick)
                    {
                        GameStart(GameMode.Easy);
                    }

                    break;
                case GameState.Playing:
                    ref var psc = ref playerStatService.GetPlayerStat();
                    bool isInvincible = psc.InvincibleTimer > 0;
                    if (isInvincible)
                        psc.InvincibleTimer -= Time.deltaTime;

                    if (playerFilter.GetEntitiesCount() == 0) break;
                    var ballEntity = playerFilter.GetRawEntities()[0];
                    ref var viewLink = ref viewLinkPool.Get(ballEntity);
                    var ballView = viewLink.Transform.GetComponent<PlayerView>();
                    if (ballView != null)
                        ballView.SetInvincible(isInvincible);

                    if (isEscapePressed)
                        ShowTitle();

                    if (screenTapOrClick)
                        ballSystem.ChangeDirection(playerFilter.GetRawEntities()[0]);

                    foreach (var _ in playerFallingFilter)
                        GameOver();

                    break;
                case GameState.GameOver:
                    gameStateComponent.GameOverTimer -= Time.deltaTime;
                    if (gameStateComponent.GameOverTimer <= 0)
                        ChangeState(GameState.GameEnd);
                    break;
                case GameState.GameEnd:
                {
                    if (isEscapePressed)
                        ShowTitle();
                    else if (screenTapOrClick)
                        GameStart(GameMode.Easy);
                }
                    break;
                default:
                    break;
            }

            foreach (var _ in playerPassedFilter)
            {
                playerStatService.AddScore(PlayerStatService.ScoreForStep);
            }

            foreach (var _ in playerHitCrystalFilter)
            {
                playerStatService.AddGold(PlayerStatService.ScoreForCrystal);
                dataManager.AddCoin(PlayerStatService.ScoreForCrystal);
                audioPlayer.Play(audioSettings.BallHitCrystalSound, audioSettings.BallHitCrystalVolume);
            }

            foreach (var _ in playerHitShieldFilter)
            {
                playerStatService.AddEnergyShield(20f);
                audioPlayer.Play(audioSettings.BallHitCrystalSound, audioSettings.BallHitCrystalVolume);
            }

            if (playerHitObstacleFilter.GetRawEntities().Length > 0 || playerHitByEnemyFilter.GetRawEntities().Length > 0)
            {
                foreach (var obstacleHit in playerHitObstacleFilter)
                {
                    playerStatService.TakeDamage(10f, GameOver);
                }

                foreach (var enemyHit in playerHitByEnemyFilter)
                {
                    if (playerStatService.TakeDamage(15f, GameOver))
                    {
                        var ball = playerFilter.GetRawEntities()[0];
                        ref var viewLink = ref viewLinkPool.Get(ball);
                        var ballView = viewLink.Transform.GetComponent<PlayerView>();
                        if (ballView != null) ballView.FlashWhite(0.5f);
                    }

                    world.GetPool<PlayerHitByEnemyEvent>().Del(enemyHit);
                }
            }


            foreach (var _ in playerLevelUpFilter)
            {
                audioPlayer.Play(audioSettings.NextLevelSound);

                var ball = playerFilter.GetRawEntities()[0];
                ref var playerComponent = ref playerPool.Get(ball);
                playerComponent.Speed = GetBallSpeedForCurrentLevel();
                playerComponent.Size = GetBallSizeForCurrentLevel();
            }
        }

        private void ClearScene()
        {
            blockSystem.ClearBlocks();
            spawner.Clear();

            playerStatService.Clear();

            // Clear old GameStateComponent entities to avoid duplicates safely
            while (gameStateFilter.GetEntitiesCount() > 0)
            {
                world.DelEntity(gameStateFilter.GetRawEntities()[0]);
            }
        }

        private void InitScene()
        {
            var gameState = world.NewEntity();
            gameStatePool.Add(gameState);

            playerStatService.GameStart(gameSettings.Level);

            blockSystem.CreateStartBlocks(GetPartsCountInBlock());
            spawner.SpawnPlayer(new Vector3(0, BallSpawnHeight, 0), GetBallSpeedForCurrentLevel());
            
            if (playerFilter.GetEntitiesCount() > 0)
            {
                var ballEntity = playerFilter.GetRawEntities()[0];
                ref var viewLink = ref viewLinkPool.Get(ballEntity);
                var ballView = viewLink.Transform.GetComponent<PlayerView>();
                if (ballView != null) ballView.SetRunning(false);
            }
        }

        private void ChangeState(GameState state)
        {
            var gameState = gameStateFilter.GetRawEntities()[0];
            ref var gameStateComponent = ref gameStatePool.Get(gameState);
            gameStateComponent.State = state;
        }

        private float GetBallSpeedForCurrentLevel()
        {
            return gameSettings.BallInitialSpeed;
        }

        private float GetBallSizeForCurrentLevel()
        {
            return gameSettings.BallInitialSize;
        }

        private void ShowTitle(bool clearScene = true)
        {
            if (clearScene)
                ClearScene();
            InitScene();
            ChangeState(GameState.Title);
        }

        public void GameStart(GameMode gameMode)
        {
            gameSettings.GameMode = gameMode;
            LoadBgmForMode(gameMode);
            GameStart(true); // Always recreate scene to guarantee a 100% clean, fresh start state
        }
        
        public void RestartGame()
        {
            GameStart(true);
        }

        private void GameStart(bool recreateScene)
        {
            if (recreateScene)
            {
                ClearScene();
                InitScene();
            }

            audioPlayer.Play(audioSettings.GameStartSound);
            ChangeState(GameState.Playing);
            PlayBackgroundMusic();
            
            if (playerFilter.GetEntitiesCount() > 0)
            {
                var ballEntity = playerFilter.GetRawEntities()[0];
                ref var viewLink = ref viewLinkPool.Get(ballEntity);
                var ballView = viewLink.Transform.GetComponent<PlayerView>();
                // Debug.Log($"[GameStart] Found player entity: {ballEntity}, ballView is {(ballView != null ? "valid" : "null")}");
                if (ballView != null) ballView.SetRunning(true);
            }
            // else Debug.LogWarning("[GameStart] playerFilter is empty!");
        }


        private void GameOver()
        {
            adService.ShowInterstitial();

            playerStatService.GameEnd();

            audioPlayer.Play(audioSettings.BallFallSound);

            var gameState = gameStateFilter.GetRawEntities()[0];
            ref var gameStateComponent = ref gameStatePool.Get(gameState);
            gameStateComponent.GameOverTimer = 1.0f;
            var ball = playerFilter.GetRawEntities()[0];
            ref var playerComponent = ref playerPool.Get(ball);
            playerComponent.Speed = 0;
            
            ref var viewLink = ref viewLinkPool.Get(ball);
            var ballView = viewLink.Transform.GetComponent<PlayerView>();
            if (ballView != null) ballView.SetRunning(false);

            ChangeState(GameState.GameOver);
            StopBackgroundMusic();
        }

        private int GetPartsCountInBlock()
        {
            return 10;
        }
    }
}
