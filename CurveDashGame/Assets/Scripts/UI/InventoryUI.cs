using UnityEngine;
using System.Collections.Generic;
using Zenject;

namespace STG.CurveDash
{
    public class InventoryUI : MonoBehaviour
    {
        [Inject] private DataManager _dataManager;
        [Inject] private AssetManager _assetManager;

        [Header("Equipment Slots")]
        [SerializeField] private List<InventorySlotUI> _equipmentSlots;

        private void Start()
        {
            RefreshUI();
            
            foreach (var slot in _equipmentSlots)
            {
                slot.OnSlotClicked += HandleSlotClicked;
            }
        }

        public void RefreshUI()
        {
            foreach (var slotUI in _equipmentSlots)
            {
                if (_dataManager.UserData.EquippedItems.TryGetValue(slotUI.Slot, out string itemId))
                {
                    var item = _assetManager.GetItem(itemId);
                    slotUI.SetItem(item);
                }
                else
                {
                    slotUI.SetItem(null);
                }
            }
        }

        private void HandleSlotClicked(InventorySlotUI slotUI)
        {
            Debug.Log($"[InventoryUI] Clicked slot: {slotUI.Slot}");
            // logic for opening item selection or unequipping
        }

        public void Toggle()
        {
            gameObject.SetActive(!gameObject.activeSelf);
            if (gameObject.activeSelf) RefreshUI();
        }
    }
}
