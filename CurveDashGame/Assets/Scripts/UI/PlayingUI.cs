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
        [SerializeField] GameObject[] heartIcons; // Kept as optional to prevent serialization warnings, but disabled on Start
        
        private int score;
        private int highScore;
        private int level;

        [Inject] private PlayerStatService playerStatService;

        private void Start()
        {
            // Automatically deactivate old heart icons on start since we are transitioning entirely to Devion Games UI!
            if (heartIcons != null)
            {
                foreach (var icon in heartIcons)
                {
                    if (icon != null) icon.SetActive(false);
                }
            }
        }
        
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
        }
    }
}