using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace STG.CurveDash
{
    public class PlayingUI : MonoBehaviour
    {
        [SerializeField] Text scoreText;
        [SerializeField] Text highScoreText;
        [SerializeField] Text levelText;
        [SerializeField] GameObject[] heartIcons; // Assign 5 heart images here in the Unity Editor
        
        private int score;
        private int highScore;
        private int level;
        private int heartCount = -1;

        [Inject] private PlayerStatService playerStatService;
        
        private void Update()
        {
            ref var playerStatComponent = ref playerStatService.GetPlayerStat();
            
            if (playerStatComponent.Score != score)
            {
                scoreText.text = "Score: " + playerStatComponent.Score;
                score = playerStatComponent.Score;
            }

            if (playerStatComponent.HighScore != highScore)
            {
                highScoreText.text = "High: " + playerStatComponent.HighScore;
                highScore = playerStatComponent.HighScore;
            }

            if (playerStatComponent.Level != level)
            {
                levelText.text = "Level: " + playerStatComponent.Level;
                level = playerStatComponent.Level;
            }

            if (playerStatComponent.Heart != heartCount)
            {
                UpdateHearts(playerStatComponent.Heart);
                heartCount = playerStatComponent.Heart;
            }
        }

        private void UpdateHearts(int currentHearts)
        {
            if (heartIcons == null) return;
            for (int i = 0; i < heartIcons.Length; i++)
            {
                if (heartIcons[i] != null)
                {
                    heartIcons[i].SetActive(i < currentHearts);
                }
            }
        }
    }
}