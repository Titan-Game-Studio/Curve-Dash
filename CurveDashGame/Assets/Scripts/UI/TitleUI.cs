using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class TitleUI : MonoBehaviour
    {
        [Inject] 
        private GameSystem gameSystem;

        public GameObject ShopUIPanel;

        public void OnShopButtonClick()
        {
            if (ShopUIPanel != null)
                ShopUIPanel.SetActive(true);
        }

        public void OnEasyButtonClick()
        {
            gameSystem.GameStart(GameMode.Easy);
        }

        public void OnNormalButtonClick()
        {
            gameSystem.GameStart(GameMode.Normal);
        }

        public void OnHardButtonClick()
        {
            gameSystem.GameStart(GameMode.Hard);
        }

        public void OnHolesButtonClick()
        {
            gameSystem.GameStart(GameMode.Holes);
        }
    }
}