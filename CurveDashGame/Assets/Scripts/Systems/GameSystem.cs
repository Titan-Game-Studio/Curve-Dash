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
        private readonly EcsFilter ballFallingFilter;
        private readonly EcsFilter playerLevelUpFilter;

        private readonly EcsFilter gameStateFilter;
        private readonly EcsPool<GameStateComponent> gameStatePool;

        [Inject] private IAdManager adManager;

        private const float BallSpawnHeight = 0.65f;

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
            ballFallingFilter = world.Filter<BallComponent>().Inc<FallingComponent>().End();

            gameStatePool = world.GetPool<GameStateComponent>();
            gameStateFilter = world.Filter<GameStateComponent>().End();

            playerLevelUpFilter = world.Filter<PlayerLevelUpComponent>().End();
        }

        public ref GameStateComponent GetGameState()
        {
            var playerStat = gameStateFilter.GetRawEntities()[0];
            return ref gameStatePool.Get(playerStat);
        }

        public void Initialize()
        {
            ShowTitle(false);
        }

        private int currentMusicIndex = 0;
        private float musicTimer = 0f;

        private void PlayBackgroundMusic()
        {
            if (audioSettings.BackgroundSounds == null || audioSettings.BackgroundSounds.Count == 0)
            {
                Debug.LogWarning("No background sounds available in audioSettings.");
                return;
            }

            // Start playing the first clip
            PlayNextMusic();
        }

        private void PlayNextMusic()
        {
            if (audioSettings.BackgroundSounds == null || audioSettings.BackgroundSounds.Count == 0) return;

            // Play the current AudioClip
            var clip = audioSettings.BackgroundSounds[currentMusicIndex];
            
            Debug.Log($"Playing music: {clip.name}");
            audioPlayer.Play(clip);

            // Set timer based on the length of the clip
            musicTimer = clip.length;

            // Move to the next clip, loop back to the start if necessary
            currentMusicIndex = (currentMusicIndex + 1) % audioSettings.BackgroundSounds.Count;
        }

        private void StopBackgroundMusic()
        {
            // Stop the currently playing clip
            audioPlayer.Stop();
            
            // Reset the timer and index for safety
            musicTimer = 0f;
            currentMusicIndex = 0;
        }

        public void Tick()
        {
            // Reduce the timer by delta time
            if (musicTimer > 0)
            {
                musicTimer -= Time.deltaTime;

                // If timer hits zero, start the next music
                if (musicTimer <= 0f)
                {
                    PlayNextMusic();
                }
            }

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

            foreach (var _ in ballHitObstacleFilter)
            {
                playerStatService.TakeDamage(PlayerStatService.Damege, () =>
                {
                    Debug.Log("Ball hit obstacle");
                    // GameOver();
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