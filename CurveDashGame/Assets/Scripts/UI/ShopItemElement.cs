using System;
using UnityEngine;
using UnityEngine.UI;

namespace STG.CurveDash.UI
{
    public class ShopItemElement : MonoBehaviour
    {
        public Image IconImage;
        public Text NameText;
        public Text PriceText;
        public Button ActionButton;
        
        private int _index;
        private Action<int> _onClickAction;
        
        public void Setup(int index, string name, string priceOrStatus, bool isSelected, Action<int> onClick)
        {
            _index = index;
            _onClickAction = onClick;
            
            if (NameText != null) NameText.text = name;
            
            // Visual feedback for selected item
            if (isSelected)
            {
                ActionButton.interactable = false;
                if (PriceText != null) PriceText.text = "Equipped";
            }
            else
            {
                ActionButton.interactable = true;
                if (PriceText != null) PriceText.text = priceOrStatus;
            }
            
            ActionButton.onClick.RemoveAllListeners();
            ActionButton.onClick.AddListener(OnItemClicked);
        }
        
        public void SetIcon(Sprite sprite)
        {
            if (IconImage != null)
            {
                IconImage.sprite = sprite;
                IconImage.gameObject.SetActive(sprite != null); // Hide if null
            }
        }
        
        private void OnItemClicked()
        {
            _onClickAction?.Invoke(_index);
        }
    }
}
