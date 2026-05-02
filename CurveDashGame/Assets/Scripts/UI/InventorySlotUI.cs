using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace STG.CurveDash
{
    public class InventorySlotUI : MonoBehaviour
    {
        public EquipmentSlot Slot;
        public Image IconImage;
        public Image SelectionHighlight;
        
        public event Action<InventorySlotUI> OnSlotClicked;
        
        private ItemData _currentItem;

        public void SetItem(ItemData item)
        {
            _currentItem = item;
            if (item != null)
            {
                IconImage.sprite = item.Icon;
                IconImage.enabled = true;
            }
            else
            {
                IconImage.enabled = false;
            }
        }

        public void OnClick()
        {
            OnSlotClicked?.Invoke(this);
        }
        
        public void SetSelected(bool selected)
        {
            if (SelectionHighlight != null)
                SelectionHighlight.enabled = selected;
        }
    }
}
