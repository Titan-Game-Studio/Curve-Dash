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

            // Subscribe to Devion equipment events to automatically refresh the UI!
            var equipmentContainer = DevionGames.UIWidgets.WidgetUtility.Find<DevionGames.InventorySystem.ItemContainer>("Equipment");
            if (equipmentContainer == null)
            {
                var allContainers = UnityEngine.Object.FindObjectsByType<DevionGames.InventorySystem.ItemContainer>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
                foreach (var c in allContainers)
                {
                    if (c != null && c.Name == "Equipment")
                    {
                        equipmentContainer = c;
                        break;
                    }
                }
            }

            if (equipmentContainer != null)
            {
                equipmentContainer.OnAddItem += OnEquipmentChanged;
                equipmentContainer.OnRemoveItem += OnEquipmentChangedAmount;
                Debug.Log("<color=lime>[InventoryUI] Subscribed to Devion Equipment container events for auto-refresh!</color>");
            }
        }

        private void OnEquipmentChanged(DevionGames.InventorySystem.Item item, DevionGames.InventorySystem.Slot slot)
        {
            RefreshUI();
        }

        private void OnEquipmentChangedAmount(DevionGames.InventorySystem.Item item, int amount, DevionGames.InventorySystem.Slot slot)
        {
            RefreshUI();
        }

        private void OnDestroy()
        {
            foreach (var slot in _equipmentSlots)
            {
                if (slot != null)
                {
                    slot.OnSlotClicked -= HandleSlotClicked;
                }
            }

            var equipmentContainer = DevionGames.UIWidgets.WidgetUtility.Find<DevionGames.InventorySystem.ItemContainer>("Equipment");
            if (equipmentContainer != null)
            {
                equipmentContainer.OnAddItem -= OnEquipmentChanged;
                equipmentContainer.OnRemoveItem -= OnEquipmentChangedAmount;
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
