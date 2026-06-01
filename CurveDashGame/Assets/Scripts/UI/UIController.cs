using System.Collections.Generic;

using TGS.Ads;
using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class UIController : MonoBehaviour
    {
        [Inject]
        private GameSystem gameSystem;

        [Inject]
        private IAdService adService;

        [Inject]
        private DiContainer container;

        private GameState gameState;
        private Dictionary<GameState, GameObject> uiDictionary;

        private void Awake()
        {
            var titleUIComp = FindFirstObjectByType<TitleUI>(FindObjectsInactive.Include);
            var playingUIComp = FindFirstObjectByType<PlayingUI>(FindObjectsInactive.Include);
            var gameEndUIComp = FindFirstObjectByType<GameEndUI>(FindObjectsInactive.Include);

            uiDictionary = new Dictionary<GameState, GameObject>
            {
                [GameState.Title] = titleUIComp?.gameObject,
                [GameState.Playing] = playingUIComp?.gameObject,
                [GameState.GameOver] = playingUIComp?.gameObject,
                [GameState.GameEnd] = gameEndUIComp?.gameObject,
            };
        }

        private void Start()
        {
            adService.Initialize();
            adService.ShowBanner();
        }

        private void Update()
        {
            if (gameSystem == null || !gameSystem.HasGameState()) return;

            var gameStateComponent = gameSystem.GetGameState();
            if (gameStateComponent.State != gameState)
            {
                if (uiDictionary.TryGetValue(gameState, out var oldUi) && oldUi != null)
                    oldUi.SetActive(false);

                gameState = gameStateComponent.State;

                if (uiDictionary.TryGetValue(gameState, out var newUi) && newUi != null)
                    newUi.SetActive(true);
            }
        }
    }
}