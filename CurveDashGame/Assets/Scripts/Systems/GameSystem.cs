using Leopotam.EcsLite;
using STG.CurveDash.AdsMob;
using UnityEngine;
using Zenject;

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
        private readonly BallSystem ballSystem;
        private readonly PlayerStatService playerStatService;
        private readonly ObjectSpawner spawner;
        private readonly BlockSystem blockSystem;
        private readonly AudioPlayer audioPlayer;

        private readonly EcsPool<BallComponent> ballPool;
        private readonly EcsFilter ballFilter;

        private readonly EcsFilter ballPassedFilter;
        private readonly EcsFilter ballHitCrystalFilter;
        private readonly EcsFilter ballHitObstacleFilter;
        private readonly EcsFilter ballHitShieldFilter;
        private readonly EcsFilter ballFallingFilter;
        private readonly EcsFilter playerLevelUpFilter;

        private readonly EcsFilter gameStateFilter;
        private readonly EcsPool<GameStateComponent> gameStatePool;
        private readonly EcsPool<ViewLinkComponent> viewLinkPool;

        [Inject] private IAdManager adManager;
        [Inject] private AssetManager assetManager;

        private const float BallSpawnHeight = 0.65f;
        private bool isAudioLoaded;
        private AudioClip currentBgmClip;
        private AudioKey currentBgmKey;

        public GameSystem(EcsWorld world, GameSettings gameSettings, AudioPlayer audioPlayer,
            AudioSettings audioSettings, BallSystem ballSystem, PlayerStatService playerStatService,
            ObjectSpawner spawner, BlockSystem blockSystem)
        {
            this.world = world;
            this.gameSettings = gameSettings;
            this.audioPlayer = audioPlayer;
            this.audioSettings = audioSettings;
            this.ballSystem = ballSystem;
            this.playerStatService = playerStatService;
            this.spawner = spawner;
            this.blockSystem = blockSystem;

            ballPool = world.GetPool<BallComponent>();
            ballFilter = world.Filter<BallComponent>().End();

            ballPassedFilter = world.Filter<BallPassedComponent>().End();
            ballHitCrystalFilter = world.Filter<BallHitCrystalEvent>().End();
            ballHitObstacleFilter = world.Filter<BallHitObstacleEvent>().End();
            ballHitShieldFilter = world.Filter<BallHitShieldEvent>().End();
            ballFallingFilter = world.Filter<BallComponent>().Inc<FallingComponent>().End();

            gameStatePool = world.GetPool<GameStateComponent>();
            gameStateFilter = world.Filter<GameStateComponent>().End();
            viewLinkPool = world.GetPool<ViewLinkComponent>();

            playerLevelUpFilter = world.Filter<PlayerLevelUpComponent>().End();
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
            var gameState = gameStateFilter.GetRawEntities()[0];
            ref var gameStateComponent = ref gameStatePool.Get(gameState);

            switch (gameStateComponent.State)
            {
                case GameState.Title:
                    if (Application.platform == RuntimePlatform.Android)
                    {
                        if (Input.GetKeyDown(KeyCode.Escape))
                            Application.Quit();
                    }

                    break;
                case GameState.Playing:
                    ref var psc = ref playerStatService.GetPlayerStat();
                    bool isInvincible = psc.InvincibleTimer > 0;
                    if (isInvincible)
                        psc.InvincibleTimer -= Time.deltaTime;

                    var ballEntity = ballFilter.GetRawEntities()[0];
                    ref var viewLink = ref viewLinkPool.Get(ballEntity);
                    var ballView = viewLink.Transform.GetComponent<BallView>();
                    if (ballView != null)
                        ballView.SetInvincible(isInvincible);

                    if (Input.GetKeyDown(KeyCode.Escape))
                        ShowTitle();

                    if (Input.GetMouseButtonDown(0))
                        ballSystem.ChangeDirection(ballFilter.GetRawEntities()[0]);

                    foreach (var _ in ballFallingFilter)
                        GameOver();

                    break;
                case GameState.GameOver:
                    gameStateComponent.GameOverTimer -= Time.deltaTime;
                    if (gameStateComponent.GameOverTimer <= 0)
                        ChangeState(GameState.GameEnd);
                    break;
                case GameState.GameEnd:
                {
                    if (Input.GetMouseButtonDown(0))
                        GameStart(true);

                    if (Input.GetKeyDown(KeyCode.Escape))
                        ShowTitle();
                }
                    break;
                default:
                    break;
            }

            foreach (var _ in ballPassedFilter)
            {
                playerStatService.AddScore(PlayerStatService.ScoreForStep);
            }

            foreach (var _ in ballHitCrystalFilter)
            {
                playerStatService.AddGold(PlayerStatService.ScoreForCrystal);
                audioPlayer.Play(audioSettings.BallHitCrystalSound, audioSettings.BallHitCrystalVolume);
            }

            foreach (var _ in ballHitShieldFilter)
            {
                playerStatService.AddHeart(1);
                audioPlayer.Play(audioSettings.BallHitCrystalSound, audioSettings.BallHitCrystalVolume);
            }

            foreach (var _ in ballHitObstacleFilter)
            {
                ref var playerStatComponent = ref playerStatService.GetPlayerStat();
                if (playerStatComponent.InvincibleTimer > 0) continue;

#if UNITY_ANDROID || UNITY_IOS
                Handheld.Vibrate();
#endif
                playerStatComponent.InvincibleTimer = 0.1f; // ~1-3 frames

                playerStatService.TakeDamage(PlayerStatService.Damege, () =>
                {
                    Debug.Log("Ball hit obstacle - Game Over!");
                    GameOver();
                });
            }

            foreach (var _ in playerLevelUpFilter)
            {
                audioPlayer.Play(audioSettings.NextLevelSound);

                var ball = ballFilter.GetRawEntities()[0];
                ref var ballComponent = ref ballPool.Get(ball);
                ballComponent.Speed = GetBallSpeedForCurrentLevel();
                ballComponent.Size = GetBallSizeForCurrentLevel();
            }
        }

        private void ClearScene()
        {
            blockSystem.ClearBlocks();
            spawner.Clear();

            playerStatService.Clear();
        }

        private void InitScene()
        {
            var gameState = world.NewEntity();
            gameStatePool.Add(gameState);

            playerStatService.GameStart(gameSettings.Level);

            blockSystem.CreateStartBlocks(GetPartsCountInBlock());
            spawner.SpawnBall(new Vector3(0, BallSpawnHeight, 0), GetBallSpeedForCurrentLevel());
        }

        private void ChangeState(GameState state)
        {
            var gameState = gameStateFilter.GetRawEntities()[0];
            ref var gameStateComponent = ref gameStatePool.Get(gameState);
            gameStateComponent.State = state;
        }

        private float GetBallSpeedForCurrentLevel()
        {
            ref var playerStatComponent = ref playerStatService.GetPlayerStat();
            return gameSettings.BallInitialSpeed + playerStatComponent.Level - 1f;
        }

        private float GetBallSizeForCurrentLevel()
        {
            ref var playerStatComponent = ref playerStatService.GetPlayerStat();
            return gameSettings.BallInitialSize + playerStatComponent.Level - 1f;
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
            bool recreate = gameSettings.GameMode != gameMode;
            gameSettings.GameMode = gameMode;
            LoadBgmForMode(gameMode);
            GameStart(recreate);
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
        }


        private void GameOver()
        {
            adManager.ShowInterstitial(() =>
            {
                playerStatService.GameEnd();

                audioPlayer.Play(audioSettings.BallFallSound);

                var gameState = gameStateFilter.GetRawEntities()[0];
                ref var gameStateComponent = ref gameStatePool.Get(gameState);
                gameStateComponent.GameOverTimer = 1.0f;
                var ball = ballFilter.GetRawEntities()[0];
                ref var ballComponent = ref ballPool.Get(ball);
                ballComponent.Speed = 0;

                ChangeState(GameState.GameOver);
                StopBackgroundMusic();
            });
        }

        private int GetPartsCountInBlock()
        {
            switch (gameSettings.GameMode)
            {
                case GameMode.Easy:
                    return 5;
                case GameMode.Normal:
                    return 4;
                case GameMode.Hard:
                    return 3;
                case GameMode.Holes:
                    return 3;
                default:
                    return 5;
            }
        }
    }
}