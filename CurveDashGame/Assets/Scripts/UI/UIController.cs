using System;
using System.Collections.Generic;

using TGS.Ads;
using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class UIController : MonoBehaviour
    {
        [SerializeField] GameObject titleUI;
        [SerializeField] GameObject playingUI;
        [SerializeField] GameObject gameEndUI;
        
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
            uiDictionary = new Dictionary<GameState, GameObject>
            {
                { GameState.Title, titleUI }, { GameState.Playing, playingUI }, { GameState.GameEnd, gameEndUI }
            };
        }

        private void Start()
        {
            if (titleUI != null) container.InjectGameObject(titleUI);
            if (playingUI != null) container.InjectGameObject(playingUI);
            if (gameEndUI != null) container.InjectGameObject(gameEndUI);

            adService.Initialize();
            adService.ShowBanner();
        }

        private void Update()
        {
            if (gameSystem == null || !gameSystem.HasGameState()) return;

            var gameStateComponent = gameSystem.GetGameState();
            if (gameStateComponent.State != gameState)
            {
                if (uiDictionary.TryGetValue(gameState, out var oldUi))
                    oldUi.SetActive(false);
                
                gameState = gameStateComponent.State;
                
                if (uiDictionary.TryGetValue(gameState, out var newUi))
                    newUi.SetActive(true);
            }
        }
    }
}