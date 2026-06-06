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
        [Inject] private readonly AssetManager _assetManager;
        
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

                // Persist the new socket layout onto the equipped items' adapters (+ save) so gems
                // survive equipment swaps and game restarts.
                if (result.Success)
                    GetPlayerView()?.PersistSocketedGems();

                // Gem lives in Inventory, not Equipment — remove from the correct container
                if (result.Success && _inventoryContainer != null)
                {
                    Debug.Log($"[GemUseHandler] Removing gem '{gemData.name}' from Inventory after successful socket.");
                    _inventoryContainer.RemoveItem(item);
                }

                // Return displaced gem to Inventory when replacing a same-type gem
                if (result.ResultType == GemSocketResultType.Replaced && result.ReplacedAbility != null)
                    ReturnGemToInventory(result.ReplacedAbility);

                // Rebuild the Actionbar from the live sockets so it reflects the new layout (handles
                // socket, replace, and the displaced-gem case in one pass).
                if (result.Success)
                    GetPlayerView()?.SyncActionbarToSockets();
            }
            else
            {
                Debug.LogWarning($"[GemUseHandler] Item '{item.name}' is not a GemItemData – ignored.");
            }
        }

        private void ReturnGemToInventory(AbilityData replacedAbility)
        {
            if (replacedAbility == null || _assetManager?.MasterItemCatalog == null) return;

            GemItemData gem = null;
            foreach (var item in _assetManager.MasterItemCatalog.Items)
            {
                if (item is GemItemData g && g.EmbeddedAbility == replacedAbility)
                {
                    gem = g;
                    break;
                }
            }

            if (gem == null)
            {
                Debug.LogWarning($"[GemUseHandler] Could not find GemItemData for displaced ability '{replacedAbility.name}' — gem may be lost.");
                return;
            }

            var adapter = gem.DevionAdapter;
            if (adapter == null)
            {
                string targetName = gem.name + "_Adapter";
                var db = InventoryManager.Database;
                if (db != null)
                {
                    foreach (var dbItem in db.items)
                    {
                        if (dbItem != null && dbItem.name == targetName)
                        {
                            adapter = dbItem;
                            gem.DevionAdapter = adapter;
                            break;
                        }
                    }
                }
            }

            if (adapter != null)
            {
                var instance = InventoryManager.CreateInstance(adapter);
                if (instance != null)
                {
                    ItemContainer.AddItem("Inventory", instance);
                    Debug.Log($"<color=cyan>[GemUseHandler] Displaced gem '{gem.name}' returned to Inventory.</color>");
                }
            }
            else
            {
                Debug.LogWarning($"[GemUseHandler] No Devion adapter found for gem '{gem.name}' — gem may be lost.");
            }
        }
    }
}
