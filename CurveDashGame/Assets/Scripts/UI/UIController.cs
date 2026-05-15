using System;
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
                if (uiDictionary.TryGetValue(gameState, out var oldUi))
                    oldUi.SetActive(false);
                
                gameState = gameStateComponent.State;
                
                if (uiDictionary.TryGetValue(gameState, out var newUi))
                    newUi.SetActive(true);
            }
        }
    }
}