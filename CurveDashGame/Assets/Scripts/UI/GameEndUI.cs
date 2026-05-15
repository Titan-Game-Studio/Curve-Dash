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

        [Inject]
        private DataManager dataManager;

        [Inject]
        private TGS.Ads.IAdService adService;

        public void OnShopButtonClick()
        {
            if (ShopUIPanel != null)
                ShopUIPanel.SetActive(true);
        }
        
        public void OnTapToStartButtonClick()
        {
            gameSystem.GameStart(GameMode.Easy);
        }
        
        public void OnWatchAdButtonClick()
        {
            adService.ShowRewarded(success =>
            {
                if (success)
                {
                    dataManager.AddCoin(100);
                }
            });
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