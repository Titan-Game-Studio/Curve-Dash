using UnityEngine;
using Zenject;
using DevionGames.InventorySystem;

namespace STG.CurveDash
{
    /// <summary>
    /// Listens for Devion inventory "Use" events and forwards gem usage to the GemSocketService.
    /// Attach this component to a persistent GameObject (e.g., a GameManager or the Player GameObject).
    /// </summary>
    public class GemUseHandler : MonoBehaviour
    {
        // Zenject injected dependencies
        [Inject(Id = "Equipment")] private readonly ItemContainer _equipmentContainer;
        [Inject(Id = "Inventory")] private readonly ItemContainer _inventoryContainer;
        [Inject] private readonly IGemSocketService _gemSocketService;
        
        private PlayerView _playerView; // Implements IEquippedLoadout

        private PlayerView GetPlayerView()
        {
            if (_playerView == null)
            {
                _playerView = FindFirstObjectByType<PlayerView>();
            }
            return _playerView;
        }

        private void Awake()
        {
            // Subscribe to OnUseItem events for both containers if they exist.
            if (_equipmentContainer != null)
                _equipmentContainer.OnUseItem += OnUseItem;
            else
                Debug.LogWarning("[GemUseHandler] Equipment container not injected – gem use may not be captured.");

            if (_inventoryContainer != null && _inventoryContainer != _equipmentContainer)
                _inventoryContainer.OnUseItem += OnUseItem;
        }

        private void OnDestroy()
        {
            if (_equipmentContainer != null)
                _equipmentContainer.OnUseItem -= OnUseItem;
            if (_inventoryContainer != null && _inventoryContainer != _equipmentContainer)
                _inventoryContainer.OnUseItem -= OnUseItem;
        }

        private void OnUseItem(Item item, Slot slot)
        {
            if (item == null) return;

            Debug.Log($"[GemUseHandler] OnUseItem invoked for '{item.name}' in container '{slot?.Container?.Name ?? "unknown"}'");

            // Gems use CurveDashItemAdapter (consumable), not CurveDashEquipmentAdapter (weapons/armor)
            if (item is CurveDashItemAdapter itemAdapter && itemAdapter.OriginalItemData is GemItemData gemData)
            {
                Debug.Log($"[GemUseHandler] Detected GemItemData: {gemData.name}, GemType={gemData.GemType}");
                var result = _gemSocketService.TrySocketGem(gemData, GetPlayerView());

                switch (result.ResultType)
                {
                    case GemSocketResultType.Socketed:
                        Debug.Log($"<color=lime>[GemUseHandler] Socketed {gemData.name} into {result.TargetItemName} ({result.TargetSlotLabel})</color>");
                        break;
                    case GemSocketResultType.Replaced:
                        Debug.Log($"<color=orange>[GemUseHandler] Replaced ability in {result.TargetItemName} ({result.TargetSlotLabel}) with {gemData.name}</color>");
                        break;
                    case GemSocketResultType.NoCompatibleSlot:
                        Debug.LogWarning($"<color=red>[GemUseHandler] No compatible socket for {gemData.name}</color>");
                        break;
                }

                // Gem lives in Inventory, not Equipment — remove from the correct container
                if (result.Success && _inventoryContainer != null)
                {
                    Debug.Log($"[GemUseHandler] Removing gem '{gemData.name}' from Inventory after successful socket.");
                    _inventoryContainer.RemoveItem(item);
                }

                // Active skill gems: sync the Actionbar to reflect newly socketed abilities
                if (result.Success && gemData.GemType == GemType.Skill && gemData.EmbeddedAbility != null)
                {
                    if (result.ResultType == GemSocketResultType.Replaced && result.ReplacedAbility != null)
                        RemoveAbilityFromActionbar(result.ReplacedAbility);

                    AddAbilityToActionbar(gemData.EmbeddedAbility);
                }
            }
            else
            {
                Debug.LogWarning($"[GemUseHandler] Item '{item.name}' is not a GemItemData – ignored.");
            }
        }

        private void AddAbilityToActionbar(AbilityData ability)
        {
            if (ability == null) return;
            var db = InventoryManager.Database;
            if (db == null) return;

            foreach (var dbItem in db.items)
            {
                if (dbItem == null) continue;
                if (dbItem.Name.IndexOf(ability.AbilityName, System.StringComparison.OrdinalIgnoreCase) >= 0
                    || dbItem.name.IndexOf(ability.AbilityName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var instance = InventoryManager.CreateInstance(dbItem);
                    if (instance != null)
                    {
                        // Show the skill's own icon, not the gem or database default icon
                        if (ability.Icon != null)
                            instance.Icon = ability.Icon;

                        ItemContainer.AddItem("Actionbar", instance);
                        Debug.Log($"<color=cyan>[GemUseHandler] Added '{ability.AbilityName}' to Actionbar (icon from AbilityData).</color>");
                    }
                    return;
                }
            }
            Debug.LogWarning($"[GemUseHandler] No Devion skill found for ability '{ability.AbilityName}' — Actionbar not updated.");
        }

        private void RemoveAbilityFromActionbar(AbilityData ability)
        {
            if (ability == null) return;
            var actionbar = DevionGames.UIWidgets.WidgetUtility.Find<ItemContainer>("Actionbar");
            if (actionbar == null) return;

            foreach (var s in actionbar.Slots)
            {
                if (s.IsEmpty || s.ObservedItem == null) continue;
                if (s.ObservedItem.Name.IndexOf(ability.AbilityName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    actionbar.RemoveItem(s.ObservedItem, 1);
                    Debug.Log($"<color=cyan>[GemUseHandler] Removed '{ability.AbilityName}' from Actionbar (replaced by new gem).</color>");
                    return;
                }
            }
        }
    }
}
