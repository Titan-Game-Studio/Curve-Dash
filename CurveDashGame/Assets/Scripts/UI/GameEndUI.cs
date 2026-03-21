using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace STG.CurveDash
{
    public class GameEndUI : MonoBehaviour
    {
        [SerializeField] Text scoreText;

        public GameObject ShopUIPanel;

        [Inject] 
        private GameSystem gameSystem;
        
        [Inject]
        private PlayerStatService playerStatService;

        public void OnShopButtonClick()
        {
            if (ShopUIPanel != null)
                ShopUIPanel.SetActive(true);
        }
        
        public void OnTapToStartButtonClick()
        {
            gameSystem.RestartGame();
        }
        
        private void OnEnable()
        {
            ref var playerStatComponent = ref playerStatService.GetPlayerStat();
            
            string text = "Your score: " + playerStatComponent.Score;
            bool newRecord = playerStatComponent.Score == playerStatComponent.HighScore;
            if (newRecord)
                text += "\nNew record !";

            scoreText.text = text;
        }
    }
}