using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Zenject;
using Random = UnityEngine.Random;

namespace STG.CurveDash
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerView : MonoBehaviour, IEquippedLoadout
    {
        [Inject] private AssetManager _assetManager;
        [Inject] private DataManager _dataManager;
        [Inject] private ShopService _shopService;
        [Inject] private GameSettings _gameSettings;
        [Inject] private PlayerStatService _playerStatService;
        
        [SerializeField] private Transform _skinContainerTransform;
        [SerializeField] private Transform _auraContainerTransform;
        [SerializeField] private Transform _characterContainerTransform;
        
        private Transform _rightHandSlot;
        private Transform _leftHandSlot;
        
        private int _skinIndex = -1;
        private int _auraIndex = -1;
        private string _characterId = "";

        private CancellationTokenSource _skinCts;
        private CancellationTokenSource _auraCts;
        private CancellationTokenSource _characterCts;

        private readonly Dictionary<int, GameObject> _cachedSkins = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> _cachedAuras = new Dictionary<int, GameObject>();
        private readonly Dictionary<string, GameObject> _cachedCharacters = new Dictionary<string, GameObject>();

        public WeaponInstance GetWeapon() { return CurrentWeaponInstance; }

        public List<AbilityData> GetArmorRuntimeSockets(EquipmentSlot slot) { return _armorRuntimeSockets.ContainsKey(slot) ? _armorRuntimeSockets[slot] : new List<AbilityData>(); }
        public int GetArmorMaxSockets(EquipmentSlot slot)
        {
            if (_dataManager?.UserData?.EquippedItems != null && _assetManager != null)
            {
                if (_dataManager.UserData.EquippedItems.TryGetValue(slot, out string itemId))
                {
                    var item = _assetManager.GetItem(itemId);
                    if (item is ArmorItemData armor)
                        return armor.MaxSockets;
                }
            }
            return 0; // fallback if not equipped or data missing
        }
        public void AddArmorAbility(EquipmentSlot slot, AbilityData ability) { if (!_armorRuntimeSockets.ContainsKey(slot)) _armorRuntimeSockets[slot] = new List<AbilityData>(); _armorRuntimeSockets[slot].Add(ability); }
        public void RemoveArmorAbility(EquipmentSlot slot, AbilityData ability) { if (_armorRuntimeSockets.ContainsKey(slot)) _armorRuntimeSockets[slot].Remove(ability); }

        public string GetEquippedItemName(EquipmentSlot slot) { return _dataManager.UserData.EquippedItems.ContainsKey(slot) ? _dataManager.UserData.EquippedItems[slot] : "None"; }

        // Runtime armor socket storage (slot -> list of abilities)
        private readonly Dictionary<EquipmentSlot, List<AbilityData>> _armorRuntimeSockets = new Dictionary<EquipmentSlot, List<AbilityData>>();

        // Ensure armor runtime socket entry exists
        private void EnsureArmorSlotEntry(EquipmentSlot slot)
        {
            if (!_armorRuntimeSockets.ContainsKey(slot))
                _armorRuntimeSockets[slot] = new List<AbilityData>();
        }

        private bool _isInvincible;
        private float _blinkTimer;
        private bool _isWhite;
        private Material _whiteMaterial;
        private Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();
        
        private float _damageFlashTimer;


        private Animator _characterAnimator;
        private Animator _mountAnimator;
        private bool _isRunning;
        private RuntimeAnimatorController _originalCharacterController;
        
        private EquippableData _leftHandItem;   // Tay Trái: GreatSword, Hammer, Bow, Shield
        private EquippableData _rightHandItem;  // Tay Phải: Sword, Arrow
        private GameObject _rightWeaponObj;
        private GameObject _leftWeaponObj;
        private ModularCharacterView _modularView;
        private RangeCircleVisualizer _rangeVisualizer;

        public EquippableData LeftHandItem => _leftHandItem;
        public EquippableData RightHandItem => _rightHandItem;
        public EquippableData GetLeftHandItem() => _leftHandItem;
        public EquippableData GetRightHandItem() => _rightHandItem;
        public WeaponInstance CurrentWeaponInstance { get; set; }

        /// <summary>
        /// Retrieves all abilities socketed inside equipped armor pieces (Helmet, Chest, Gloves, Boots)
        /// using the DataManager's UserData and AssetManager's catalog.
        /// </summary>
        public List<AbilityData> GetEquippedArmorAbilities()
        {
            var list = new List<AbilityData>();
            if (_dataManager == null || _assetManager == null || _dataManager.UserData == null || _dataManager.UserData.EquippedItems == null)
                return list;

            var slotsToCheck = new List<EquipmentSlot> { EquipmentSlot.Head, EquipmentSlot.Body, EquipmentSlot.Hands, EquipmentSlot.Feet };
            foreach (var slot in slotsToCheck)
            {
                // Static abilities from ScriptableObject armor
                if (_dataManager.UserData.EquippedItems.TryGetValue(slot, out string itemId))
                {
                    var item = _assetManager.GetItem(itemId);
                    if (item is ArmorItemData armor && armor.Abilities != null)
                    {
                        foreach (var ab in armor.Abilities)
                        {
                            if (ab != null && !list.Contains(ab))
                                list.Add(ab);
                        }
                    }
                }
                // Runtime socketed abilities
                if (_armorRuntimeSockets.TryGetValue(slot, out var runtimeList))
                {
                    foreach (var ab in runtimeList)
                    {
                        if (ab != null && !list.Contains(ab))
                            list.Add(ab);
                    }
                }
            }
            return list;
        }

        public float MovementSpeed { get; set; } = 5f;
        public float AttackSpeed { get; set; } = 1f;

        private SmartEquipService _smartEquipService;
        private readonly BeltFlaskService _beltFlaskService = new BeltFlaskService();

        private void EnsureSmartEquipService()
        {
            if (_smartEquipService == null)
            {
                _smartEquipService = new SmartEquipService(new EquipmentResolver(), new PlayerInventoryScanner(_assetManager));
            }
        }

        private void Start()
        {
            EnsureSmartEquipService();
            Init();
            
            // Create simulator/game-view range visualizer for player
            var rangeObj = new GameObject("PlayerRangeVisualizer");
            rangeObj.transform.SetParent(transform, false);
            rangeObj.transform.localPosition = new Vector3(0, 0.05f, 0); // slightly above ground
            _rangeVisualizer = rangeObj.AddComponent<RangeCircleVisualizer>();
            _rangeVisualizer.SetColor(Color.cyan);
            UpdateRangeVisualizer();

            SyncEquipmentContainerListeners();
            StartCoroutine(SyncCharacterInfoStatsDelayed());

#if UNITY_EDITOR
            StartCoroutine(ApplyDebugGemsDelayed());
#endif
        }

        // Waits two frames so Devion's StatsHandler has fully initialized, then rebuilds
        // EquippedItems from the Equipment container (Devion restores saved items silently
        // without firing OnAddItem, so we must scan slots manually on startup).
        private System.Collections.IEnumerator SyncCharacterInfoStatsDelayed()
        {
            yield return null;
            yield return null;
            SyncEquipmentContainerListeners();
            DumpEquipmentSlots();
            RebuildEquippedItemsFromContainer();
            if (_equipmentContainer != null)
                foreach (var s in _equipmentContainer.Slots) s.Repaint();
            SyncCharacterInfoStats();
        }

        private void DumpEquipmentSlots()
        {
            if (_equipmentContainer == null) { Debug.LogWarning("[SLOT-DUMP] _equipmentContainer is null!"); return; }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[SLOT-DUMP] Equipment container has {_equipmentContainer.Slots.Count} slots:");
            foreach (var s in _equipmentContainer.Slots)
            {
                int compCount = s.GetComponents<DevionGames.InventorySystem.Restrictions.EquipmentRegion>().Length;
                int listCount = s.restrictions.Count;
                sb.Append($"  [{s.Index}] GO='{s.gameObject.name}' | GetComponents={compCount} | restrictions.Count={listCount}");
                foreach (var r in s.restrictions)
                {
                    if (r is DevionGames.InventorySystem.Restrictions.EquipmentRegion er)
                        sb.Append($" → region='{er.region?.Name ?? "null"}'");
                }
                sb.AppendLine();
            }
            Debug.Log(sb.ToString());
        }

        // Waits one frame, repaints all equipment slots (icon refresh), then syncs stats.
        // Icon repaint is needed because SyncData() updates adapter.Icon synchronously, but
        // equipment slots that were mirror-set (two-handed) may not have auto-repainted yet.
        // Stats sync is delayed one frame because ReplaceItem sets slot.ObservedItem BEFORE
        // firing NotifyAddItem, so the slot IS populated during the callback — but RepeatSaving
        // events can fire mid-frame and corrupt CurrentWeaponInstance; the 1-frame delay lets
        // all same-frame Devion events settle before we scan slots.
        private System.Collections.IEnumerator SyncEquipmentNextFrame()
        {
            int startFrame = Time.frameCount;
            yield return null;
            Debug.Log($"<color=lime>[DIAG][F{Time.frameCount}] SyncEquipmentNextFrame running (started F{startFrame}) | _left='{_leftHandItem?.name ?? "null"}' _right='{_rightHandItem?.name ?? "null"}'</color>");
            if (_equipmentContainer != null)
                foreach (var s in _equipmentContainer.Slots) s.Repaint();
            SyncCharacterInfoStats();
        }

        private void RebuildEquippedItemsFromContainer()
        {
            if (_equipmentContainer == null)
            {
                Debug.LogWarning("[PlayerView] RebuildEquippedItemsFromContainer: _equipmentContainer is null, skipping.");
                return;
            }

            _dataManager?.UserData?.EquippedItems?.Clear();
            CurrentWeaponInstance = null;

            for (int i = 0; i < _equipmentContainer.Slots.Count; i++)
            {
                var s = _equipmentContainer.Slots[i];
                if (s.IsEmpty || s.ObservedItem == null) continue;

                if (s.ObservedItem is CurveDashEquipmentAdapter adapter && adapter.OriginalEquipmentData != null)
                {
                    if (adapter.OriginalEquipmentData is ArmorItemData armor)
                    {
                        if (_dataManager?.UserData?.EquippedItems != null)
                            _dataManager.UserData.EquippedItems[armor.Slot] = armor.name;
                        EnsureArmorSlotEntry(armor.Slot);
                        Debug.Log($"<color=cyan>[PlayerView] Rebuild: found armor '{armor.name}' defense={armor.Defense} in slot {i}</color>");
                    }
                    else if (adapter.OriginalEquipmentData is BeltItemData belt)
                    {
                        if (_dataManager?.UserData?.EquippedItems != null)
                            _dataManager.UserData.EquippedItems[EquipmentSlot.Belt] = belt.name;
                        _beltFlaskService.SetBelt(belt);
                        Debug.Log($"<color=cyan>[PlayerView] Rebuild: found belt '{belt.name}' in slot {i}</color>");
                    }
                    else if (adapter.OriginalEquipmentData is AmuletItemData amulet)
                    {
                        if (_dataManager?.UserData?.EquippedItems != null)
                            _dataManager.UserData.EquippedItems[EquipmentSlot.Amulet] = amulet.name;
                        Debug.Log($"<color=cyan>[PlayerView] Rebuild: found amulet '{amulet.name}' in slot {i}</color>");
                    }
                    else if (adapter.OriginalEquipmentData is RingItemData ring)
                    {
                        if (_dataManager?.UserData?.EquippedItems != null)
                            _dataManager.UserData.EquippedItems[ring.Slot] = ring.name;
                        Debug.Log($"<color=cyan>[PlayerView] Rebuild: found ring '{ring.name}' slot={ring.Slot} in slot {i}</color>");
                    }
                    else if (adapter.OriginalEquipmentData is WeaponData weaponBase && CurrentWeaponInstance == null)
                    {
                        var instance = new WeaponInstance(weaponBase, weaponBase.Rarity);
                        ItemPickupSystem.AutoLinkTestingAbilities(instance, weaponBase, _assetManager?.MasterItemCatalog);
                        CurrentWeaponInstance = instance;
                        adapter.SyncAffixes(instance.Affixes);
                        Debug.Log($"<color=cyan>[PlayerView] Rebuild: found weapon '{weaponBase.name}' minDmg={instance.FinalMinDamage} maxDmg={instance.FinalMaxDamage} in slot {i}</color>");

                        if (_dataManager?.UserData?.EquippedItems != null)
                            _dataManager.UserData.EquippedItems[EquipmentSlot.MainHand] = weaponBase.name;

                        // FIX: Call Equip to register the weapon in hands and spawn its visuals/animator!
                        Equip(weaponBase);
                    }
                    else if (adapter.OriginalEquipmentData is OffHandData offhand)
                    {
                        if (_dataManager?.UserData?.EquippedItems != null)
                            _dataManager.UserData.EquippedItems[EquipmentSlot.OffHand] = offhand.name;
                        
                        Debug.Log($"<color=cyan>[PlayerView] Rebuild: found offhand '{offhand.name}' in slot {i}</color>");
                        Equip(offhand);
                    }
                    else
                    {
                        Debug.Log($"<color=cyan>[PlayerView] Rebuild: slot {i} has '{adapter.OriginalEquipmentData.GetType().Name}' (not armor/weapon, skipped)</color>");
                    }
                }
            }

            int armorCount = _dataManager?.UserData?.EquippedItems?.Count ?? 0;
            Debug.Log($"<color=cyan>[PlayerView] RebuildEquippedItemsFromContainer done: {armorCount} armor piece(s), weapon={CurrentWeaponInstance?.BaseData?.name ?? "none"}</color>");
        }

#if UNITY_EDITOR
        // Waits one frame for Devion to finish loading saved equipment, then auto-sockets
        // debug gems into any weapon that has empty DynamicAbilities. Editor-only.
        private System.Collections.IEnumerator ApplyDebugGemsDelayed()
        {
            yield return null;
            if (CurrentWeaponInstance != null && CurrentWeaponInstance.DynamicAbilities.Count == 0)
            {
                var catalog = _assetManager?.MasterItemCatalog;
                ItemPickupSystem.AutoLinkTestingAbilities(CurrentWeaponInstance, CurrentWeaponInstance.BaseData, catalog);
                Debug.Log("<color=yellow>[PlayerView] DEBUG: Applied startup gems to pre-equipped weapon.</color>");
            }
        }
#endif

        private DevionGames.InventorySystem.ItemContainer _equipmentContainer;
        private DevionGames.InventorySystem.ItemContainer _inventoryContainerRef;
        private bool _isUpdatingEquipment = false;
        // Items being manually returned to Inventory by ReturnDisplacedToInventory — skip ReturnItemToInventoryIfNeeded for these
        private readonly HashSet<string> _suppressedReturnItems = new HashSet<string>();

        // Tracks a one-handed weapon removed from MainHand in the current frame.
        // If the NEXT event is OnAddItem(another sword), we set up dual wield instead of returning to inventory.
        private EquippableData _pendingDualWieldData = null;
        private DevionGames.InventorySystem.Item _pendingDualWieldDevionItem = null;

        private void SyncEquipmentContainerListeners()
        {
            try
            {
                if (_equipmentContainer == null)
                {
                    _equipmentContainer = DevionGames.UIWidgets.WidgetUtility.Find<DevionGames.InventorySystem.ItemContainer>("Equipment");
                    if (_equipmentContainer == null)
                    {
                        var allContainers = UnityEngine.Object.FindObjectsByType<DevionGames.InventorySystem.ItemContainer>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
                        foreach (var c in allContainers)
                        {
                            if (c != null && c.Name == "Equipment")
                            {
                                _equipmentContainer = c;
                                break;
                            }
                        }
                    }

                    if (_equipmentContainer != null)
                    {
                        _equipmentContainer.OnAddItem += OnDevionEquipmentAdded;
                        _equipmentContainer.OnRemoveItem += OnDevionEquipmentRemoved;
                        Debug.Log("<color=green>[PlayerView] Successfully subscribed to Devion Equipment container events.</color>");
                    }
                }

                if (_inventoryContainerRef == null)
                {
                    _inventoryContainerRef = DevionGames.UIWidgets.WidgetUtility.Find<DevionGames.InventorySystem.ItemContainer>("Inventory");
                    if (_inventoryContainerRef != null)
                    {
                        Debug.Log("<color=green>[PlayerView] Successfully located Devion Inventory container.</color>");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerView] Could not subscribe to Devion container events: {ex.Message}");
            }
        }

        // Waits one frame, then returns the item to Inventory only if Devion didn't already handle it.
        // Devion's ItemSlot.Use() calls Container.AddItem(oldItem) BEFORE firing Equipment.OnRemoveItem,
        // so by the time this coroutine runs (next frame), Devion's swap is already complete.
        // We check the Inventory slots directly to avoid creating a duplicate.
        private System.Collections.IEnumerator ReturnItemToInventoryIfNeeded(DevionGames.InventorySystem.Item item)
        {
            yield return null; // let Devion complete its swap in the same frame

            if (item == null || this == null || !gameObject.activeInHierarchy) yield break;

            try
            {
                // If Devion already returned the item to Inventory (swap path), skip to avoid duplicate.
                if (_inventoryContainerRef != null)
                {
                    for (int i = 0; i < _inventoryContainerRef.Slots.Count; i++)
                    {
                        var s = _inventoryContainerRef.Slots[i];
                        if (!s.IsEmpty && s.ObservedItem != null && s.ObservedItem.name == item.name)
                        {
                            Debug.Log($"[PlayerView] '{item.Name}' already in Inventory (Devion swap) – no duplicate created.");
                            yield break;
                        }
                    }
                }

                // Item not in Inventory — find database template and add it back.
                DevionGames.InventorySystem.Item template = null;
                if (DevionGames.InventorySystem.InventoryManager.Database != null)
                {
                    foreach (var dbItem in DevionGames.InventorySystem.InventoryManager.Database.items)
                    {
                        if (dbItem != null && dbItem.name == item.name)
                        {
                            template = dbItem;
                            break;
                        }
                    }
                }
                if (template == null) template = item;

                var instance = DevionGames.InventorySystem.InventoryManager.CreateInstance(template);
                if (instance != null)
                {
                    bool added = DevionGames.InventorySystem.ItemContainer.AddItem("Inventory", instance);
                    Debug.Log($"<color=cyan>[PlayerView] Returned '{item.Name}' to Inventory (added={added}).</color>");
                }
                else
                {
                    Debug.LogWarning($"[PlayerView] CreateInstance returned null for '{item.Name}' – item may be lost.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerView] Failed to return '{item?.Name}': {ex.Message}");
            }
        }

        private DevionGames.InventorySystem.Slot GetSlotByRegionName(string regionName)
        {
            if (_equipmentContainer == null) return null;

            foreach (var slot in _equipmentContainer.Slots)
            {
                // Method A: same GO (Devion's GetRequiredSlots approach)
                var comps = slot.GetComponents<DevionGames.InventorySystem.Restrictions.EquipmentRegion>();
                foreach (var c in comps)
                {
                    if (c.region != null && c.region.Name.IndexOf(regionName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return slot;
                }

                // Method B: restriction is on a parent GO (common Devion scene structure)
                var parentComps = slot.GetComponentsInParent<DevionGames.InventorySystem.Restrictions.EquipmentRegion>(true);
                foreach (var c in parentComps)
                {
                    if (c.region != null && c.region.Name.IndexOf(regionName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return slot;
                }

                // Method C: container-level restrictions list
                foreach (var r in slot.restrictions)
                {
                    if (r is DevionGames.InventorySystem.Restrictions.EquipmentRegion er
                        && er.region != null
                        && er.region.Name.IndexOf(regionName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return slot;
                }

                // Method D: slot GO name itself contains the regionName keyword (e.g. GO named "Right Hand", "Left Hand")
                if (slot.gameObject.name.IndexOf(regionName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return slot;
            }

            Debug.LogWarning($"[PlayerView] GetSlotByRegionName('{regionName}'): not found via any method. Container has {_equipmentContainer.Slots.Count} slots. Run game and check [SLOT-DUMP] log.");
            return null;
        }

        private void OnDevionEquipmentAdded(DevionGames.InventorySystem.Item item, DevionGames.InventorySystem.Slot slot)
        {
            if (this == null || gameObject == null || !gameObject.activeInHierarchy) return;
            if (_isUpdatingEquipment) return;

            _isUpdatingEquipment = true;
            try
            {
                if (item is CurveDashEquipmentAdapter adapter && adapter.OriginalEquipmentData != null)
                {
                    adapter.SyncData();
                    if (slot != null) slot.Repaint();

                    // Repaint ALL equipment container slots để tránh icon không update khi trang bị vào "Second Set"
                    var allContainers = UnityEngine.Object.FindObjectsByType<DevionGames.InventorySystem.ItemContainer>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
                    foreach (var container in allContainers)
                    {
                        if (container != null && (container.Name == "Equipment" || container.Name == "Second Set"))
                        {
                            foreach (var s in container.Slots)
                            {
                                if (s != null) s.Repaint();
                            }
                        }
                    }

                    var itemData = adapter.OriginalEquipmentData;

                    if (itemData is ArmorItemData armor)
                    {
                        if (_dataManager?.UserData?.EquippedItems != null)
                        {
                            _dataManager.UserData.EquippedItems[armor.Slot] = armor.name;
                        }
                        EnsureArmorSlotEntry(armor.Slot);
                        if (_modularView != null)
                        {
                            _modularView.SetPartByName(armor.Slot, armor.MeshPartName, armor.ModularPartIndex);
                        }
                    }
                    else if (itemData is BeltItemData belt)
                    {
                        if (_dataManager?.UserData?.EquippedItems != null)
                            _dataManager.UserData.EquippedItems[EquipmentSlot.Belt] = belt.name;
                        if (_modularView != null)
                            _modularView.SetPartByName(EquipmentSlot.Belt, belt.MeshPartName, belt.ModularPartIndex);
                        _beltFlaskService.SetBelt(belt);
                        // Check for flask equipment items and populate belt's flask slots
                        UpdateBeltFlasksFromEquipment();
                    }
                    else if (itemData is AmuletItemData amulet)
                    {
                        if (_dataManager?.UserData?.EquippedItems != null)
                            _dataManager.UserData.EquippedItems[EquipmentSlot.Amulet] = amulet.name;
                    }
                    else if (itemData is RingItemData ring)
                    {
                        if (_dataManager?.UserData?.EquippedItems != null)
                            _dataManager.UserData.EquippedItems[ring.Slot] = ring.name;
                    }
                    else if (itemData is EquippableData equippable)
                    {
                        if (equippable is WeaponData weaponBase)
                        {
                            var instance = new WeaponInstance(weaponBase, weaponBase.Rarity);
                            ItemPickupSystem.AutoLinkTestingAbilities(instance, weaponBase, _assetManager?.MasterItemCatalog);
                            this.CurrentWeaponInstance = instance;

                            // Update adapter properties so Devion item UI reflects affix values.
                            // Stats are pushed to StatsHandler later via SyncCharacterInfoStats.
                            adapter.SyncAffixes(instance.Affixes);

                            if (_dataManager?.UserData?.EquippedItems != null)
                            {
                                _dataManager.UserData.EquippedItems[EquipmentSlot.MainHand] = weaponBase.name;
                            }
                        }
                        else if (equippable is OffHandData offhand)
                        {
                            if (_dataManager?.UserData?.EquippedItems != null)
                            {
                                _dataManager.UserData.EquippedItems[EquipmentSlot.OffHand] = offhand.name;
                            }
                        }

                        // Dual wield: kiếm mới thay kiếm cũ ở MainHand → giữ kiếm cũ ở OffHand
                        bool setupDualWield = equippable is OneHandedWeaponData
                            && _pendingDualWieldData != null
                            && _pendingDualWieldData != equippable;

                        Equip(equippable); // sets _rightHandItem = new sword, _leftHandItem per normal rules

                        // If SmartEquip rejected the item (e.g. Arrow without Bow), revert and return to Inventory.
                        bool equipAccepted = (_leftHandItem == equippable || _rightHandItem == equippable);
                        if (!equipAccepted)
                        {
                            if (equippable is WeaponData)
                            {
                                this.CurrentWeaponInstance = null;
                                _dataManager?.UserData?.EquippedItems?.Remove(EquipmentSlot.MainHand);
                            }
                            else if (equippable is OffHandData)
                            {
                                _dataManager?.UserData?.EquippedItems?.Remove(EquipmentSlot.OffHand);
                            }
                            _pendingDualWieldData = null;
                            _pendingDualWieldDevionItem = null;
                            StartCoroutine(ReturnDisplacedToInventory(new List<EquippableData> { equippable }));
                            return;
                        }

                        if (setupDualWield)
                        {
                            _leftHandItem = _pendingDualWieldData;
                            PlaceDualWieldInOffHand(_pendingDualWieldDevionItem);
                            Debug.Log($"<color=magenta>[PlayerView] Dual wield: L='{_leftHandItem?.name}' R='{_rightHandItem?.name}'</color>");
                        }

                        // Pending was consumed (dual wield) or new item is not a sword → clear either way
                        _pendingDualWieldData = null;
                        _pendingDualWieldDevionItem = null;
                    }

                    StartCoroutine(SyncEquipmentNextFrame());
                }
            }
            finally
            {
                _isUpdatingEquipment = false;
            }
        }

        private void OnDevionEquipmentRemoved(DevionGames.InventorySystem.Item item, int amount, DevionGames.InventorySystem.Slot slot)
        {
            if (this == null || gameObject == null || !gameObject.activeInHierarchy) return;
            if (_isUpdatingEquipment) return;
            // ReturnDisplacedToInventory is already handling this item — skip to avoid duplicate return
            if (item != null && _suppressedReturnItems.Contains(item.name)) return;

            _isUpdatingEquipment = true;
            try
            {
                if (item is CurveDashEquipmentAdapter adapter && adapter.OriginalEquipmentData != null)
                {
                    var itemData = adapter.OriginalEquipmentData;

                    if (itemData is ArmorItemData armor)
                    {
                        if (_dataManager?.UserData?.EquippedItems != null)
                        {
                            _dataManager.UserData.EquippedItems.Remove(armor.Slot);
                        }
                        _armorRuntimeSockets.Remove(armor.Slot);
                        if (_modularView != null)
                        {
                            _modularView.SetPart(armor.Slot, -1);
                        }
                    }
                    else if (itemData is BeltItemData)
                    {
                        _dataManager?.UserData?.EquippedItems?.Remove(EquipmentSlot.Belt);
                        if (_modularView != null)
                            _modularView.SetPart(EquipmentSlot.Belt, -1);
                        _beltFlaskService.SetBelt(null);
                    }
                    else if (itemData is AmuletItemData)
                    {
                        _dataManager?.UserData?.EquippedItems?.Remove(EquipmentSlot.Amulet);
                    }
                    else if (itemData is RingItemData ring)
                    {
                        _dataManager?.UserData?.EquippedItems?.Remove(ring.Slot);
                    }
                    else if (itemData is EquippableData equippable)
                    {
                        if (equippable is WeaponData)
                        {
                            this.CurrentWeaponInstance = null;
                            if (_dataManager?.UserData?.EquippedItems != null)
                            {
                                _dataManager.UserData.EquippedItems.Remove(EquipmentSlot.MainHand);
                            }
                        }
                        else if (equippable is OffHandData)
                        {
                            if (_dataManager?.UserData?.EquippedItems != null)
                            {
                                _dataManager.UserData.EquippedItems.Remove(EquipmentSlot.OffHand);
                            }
                        }
                        Unequip(equippable);

                        // Store OneHandedWeapon removals — next OnAddItem may set up dual wield
                        // instead of returning this item to inventory.
                        if (equippable is OneHandedWeaponData)
                        {
                            _pendingDualWieldData = equippable;
                            _pendingDualWieldDevionItem = item;
                            StartCoroutine(ClearPendingDualWieldIfNotHandled(item));
                            StartCoroutine(SyncEquipmentNextFrame());
                            return; // skip ReturnItemToInventoryIfNeeded — coroutine handles it
                        }
                    }

                    StartCoroutine(ReturnItemToInventoryIfNeeded(item));
                    StartCoroutine(SyncEquipmentNextFrame());
                }
            }
            finally
            {
                _isUpdatingEquipment = false;
            }
        }

        private void OnEnable()
        {
            if (_shopService != null)
            {
                _shopService.OnMountSkinEquipped += UpdateSkin;
                _shopService.OnVFXEquipped += UpdateAura;
                _shopService.OnCharacterEquipped += UpdateCharacter;
            }
        }

        private void OnDisable()
        {
            if (_shopService != null)
            {
                _shopService.OnMountSkinEquipped -= UpdateSkin;
                _shopService.OnVFXEquipped -= UpdateAura;
                _shopService.OnCharacterEquipped -= UpdateCharacter;
            }
        }

        private void Update()
        {
            if (_equipmentContainer == null || _inventoryContainerRef == null)
            {
                SyncEquipmentContainerListeners();
            }

            if (_damageFlashTimer > 0)
            {
                _damageFlashTimer -= Time.deltaTime;
                _blinkTimer += Time.deltaTime;
                if (_blinkTimer > 0.05f)
                {
                    _blinkTimer = 0f;
                    _isWhite = !_isWhite;
                    ToggleWhiteMaterials(_isWhite);
                }

                if (_damageFlashTimer <= 0)
                {
                    RestoreOriginalMaterials();
                    _isWhite = false;
                }
            }
            // Logic bất tử cũ (nếu muốn mờ đi thay vì chớp trắng)
            else if (_isInvincible)
            {
                // Có thể thêm hiệu ứng mờ alpha ở đây nếu muốn
            }

            // Sync animator speeds based on active states
            UpdateAnimatorSpeeds();

            // Smoothly align model back to forward road direction when running
            if (_isRunning && _characterContainerTransform != null)
            {
                _characterContainerTransform.localRotation = Quaternion.Slerp(_characterContainerTransform.localRotation, Quaternion.identity, Time.deltaTime * 10f);
            }

            // Toggle range debug visualization on simulator (runs in built games too)
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current[UnityEngine.InputSystem.Key.F3].wasPressedThisFrame)
            {
                RangeCircleVisualizer.IsDebugEnabled = !RangeCircleVisualizer.IsDebugEnabled;
                Debug.Log($"[Debug] Range circle debug visualization toggled: {RangeCircleVisualizer.IsDebugEnabled}");
            }

            // Belt flask auto-use tick (POE-style)
            if (_beltFlaskService.HasActiveBelt && _playerStatService != null)
            {
                ref var playerStats = ref _playerStatService.GetPlayerStat();
                float healing = _beltFlaskService.Tick(Time.deltaTime, playerStats.CurrentLife, playerStats.MaxLife);
                if (healing > 0f)
                {
                    _playerStatService.AddLife(healing);
                }

                // Apply flask modifiers to movement speed and attack speed
                float speedMod = _beltFlaskService.GetSpeedModifier();
                float attackSpeedMod = _beltFlaskService.GetAttackSpeedModifier();
                MovementSpeed *= speedMod;
                AttackSpeed *= attackSpeedMod;
            }
        }



        private void Init()
        {
            UpdateCharacter(_dataManager.UserData.CurrentCharacterId);
        }

        private void OnDestroy()
        {
            if (_equipmentContainer != null)
            {
                _equipmentContainer.OnAddItem -= OnDevionEquipmentAdded;
                _equipmentContainer.OnRemoveItem -= OnDevionEquipmentRemoved;
            }

            _skinCts?.Cancel();
            _skinCts?.Dispose();
            _auraCts?.Cancel();
            _auraCts?.Dispose();
            _characterCts?.Cancel();
            _characterCts?.Dispose();

            if (_modularView != null)
            {
                _modularView.OnAttackHitEvent -= OnAnimationHitTriggered;
            }
        }

        public void UpdateSkin(int index)
        {
            if (_skinIndex == index) return;
            _skinIndex = index;
            LoadOrEnableAsset(index, _skinContainerTransform, _cachedSkins, _assetManager.LoadMountSkin, ref _skinCts, OnVisualLoaded);
        }

        public void UpdateAura(int index)
        {
            if (_auraIndex == index) return;
            _auraIndex = index;
            LoadOrEnableAsset(index, _auraContainerTransform, _cachedAuras, _assetManager.LoadVFX, ref _auraCts, OnVisualLoaded);
        }

        public void UpdateCharacter(string characterId)
        {
            if (string.IsNullOrEmpty(characterId) || _characterId == characterId) return;
            _characterId = characterId;

            foreach (var kvp in _cachedCharacters)
            {
                if (kvp.Value != null) kvp.Value.SetActive(false);
            }

            if (_cachedCharacters.TryGetValue(characterId, out var existingObj) && existingObj != null)
            {
                existingObj.SetActive(true);
                OnVisualLoaded();
                return;
            }

            _characterCts?.Cancel();
            _characterCts?.Dispose();
            _characterCts = new CancellationTokenSource();
            var token = _characterCts.Token;

            _assetManager.LoadCharacter(characterId, prefab => 
            {
                if (this == null || token.IsCancellationRequested) return;
                var obj = Instantiate(prefab, _characterContainerTransform);
                _cachedCharacters[characterId] = obj;
                OnVisualLoaded();
            });
        }

        #region SMART EQUIPMENT SYSTEM (Rule Based)

        public void Equip(EquippableData item)
        {
            if (item == null) return;

            EnsureSmartEquipService();

            var prevLeft  = _leftHandItem;
            var prevRight = _rightHandItem;

            var assignment = _smartEquipService.SmartEquip(_leftHandItem, _rightHandItem, item);
            Debug.Log($"<color=lime>[DIAG][F{Time.frameCount}] SmartEquip('{item.name}'): valid={assignment.IsValid} reason='{assignment.InvalidReason}' → left='{assignment.LeftHand?.name ?? "null"}' right='{assignment.RightHand?.name ?? "null"}'</color>");
            if (!assignment.IsValid)
            {
                UnityEngine.Debug.LogWarning($"<color=red>[DIAG][F{Time.frameCount}] SmartEquip REJECTED for '{item.name}': {assignment.InvalidReason}</color>");
                return;
            }

            _leftHandItem  = assignment.LeftHand;
            _rightHandItem = assignment.RightHand;

            // Collect items removed from hands that are NOT the new item and not still in a hand.
            // These were displaced (e.g. Arrow when 2H equipped, Shield when Bow equipped) and must
            // be returned to inventory because Devion only fires OnRemoveItem for the slot being replaced.
            var displaced = CollectDisplaced(prevLeft, prevRight, _leftHandItem, _rightHandItem, item);
            if (displaced != null)
                StartCoroutine(ReturnDisplacedToInventory(displaced));

            RefreshWeaponVisuals();
            RefreshAnimator();
            UpdateRangeVisualizer();
        }

        public void Unequip(EquippableData item)
        {
            if (item == null) return;

            EnsureSmartEquipService();

            var prevLeft  = _leftHandItem;
            var prevRight = _rightHandItem;

            var assignment = _smartEquipService.SmartUnequip(_leftHandItem, _rightHandItem, item);
            Debug.Log($"<color=orange>[DIAG][F{Time.frameCount}] SmartUnequip('{item.name}'): valid={assignment.IsValid} → left='{assignment.LeftHand?.name ?? "null"}' right='{assignment.RightHand?.name ?? "null"}'</color>");
            if (assignment.IsValid)
            {
                _leftHandItem  = assignment.LeftHand;
                _rightHandItem = assignment.RightHand;

                // Return items displaced by unequip (e.g., Arrow when Bow is removed)
                var displaced = CollectDisplaced(prevLeft, prevRight, _leftHandItem, _rightHandItem, item);
                if (displaced != null)
                    StartCoroutine(ReturnDisplacedToInventory(displaced));
            }

            RefreshWeaponVisuals();
            RefreshAnimator();
            UpdateRangeVisualizer();
        }

        private static List<EquippableData> CollectDisplaced(
            EquippableData prevLeft, EquippableData prevRight,
            EquippableData newLeft,  EquippableData newRight,
            EquippableData triggerItem)
        {
            List<EquippableData> list = null;
            if (prevLeft != null && prevLeft != newLeft && prevLeft != newRight && prevLeft != triggerItem)
                (list = new List<EquippableData>()).Add(prevLeft);
            if (prevRight != null && prevRight != newLeft && prevRight != newRight && prevRight != triggerItem)
                (list ??= new List<EquippableData>()).Add(prevRight);
            return list;
        }

        // Waits one frame for Devion's swap to settle, then removes displaced items that are still
        // sitting in the Equipment container and returns them to the Inventory container.
        private System.Collections.IEnumerator ReturnDisplacedToInventory(List<EquippableData> items)
        {
            yield return null;

            foreach (var displaced in items)
            {
                if (_equipmentContainer == null) break;

                // Find the Devion item still occupying an Equipment slot
                DevionGames.InventorySystem.Item devionItem = null;
                foreach (var slot in _equipmentContainer.Slots)
                {
                    if (slot.IsEmpty || slot.ObservedItem == null) continue;
                    if (slot.ObservedItem is CurveDashEquipmentAdapter adapter
                        && adapter.OriginalEquipmentData == displaced)
                    {
                        devionItem = slot.ObservedItem;
                        break;
                    }
                }

                if (devionItem == null)
                {
                    Debug.Log($"[PlayerView] Displaced '{displaced.name}' already removed from Equipment (Devion handled it).");
                    continue;
                }

                // Suppress OnDevionEquipmentRemoved → ReturnItemToInventoryIfNeeded for this item
                _suppressedReturnItems.Add(devionItem.name);

                _equipmentContainer.RemoveItem(devionItem, 1);

                // Find the database template so CreateInstance gets a clean copy
                DevionGames.InventorySystem.Item template = null;
                if (DevionGames.InventorySystem.InventoryManager.Database != null)
                {
                    foreach (var dbItem in DevionGames.InventorySystem.InventoryManager.Database.items)
                    {
                        if (dbItem != null && dbItem.name == devionItem.name) { template = dbItem; break; }
                    }
                }
                if (template == null) template = devionItem;

                var instance = DevionGames.InventorySystem.InventoryManager.CreateInstance(template);
                bool added = instance != null
                    ? DevionGames.InventorySystem.ItemContainer.AddItem("Inventory", instance)
                    : DevionGames.InventorySystem.ItemContainer.AddItem("Inventory", devionItem);

                Debug.Log($"<color=cyan>[PlayerView] Displaced '{displaced.name}' returned to Inventory (added={added}).</color>");

                _suppressedReturnItems.Remove(devionItem.name);
            }
        }

        // Places the pending dual-wield sword item directly into the OffHand Devion slot,
        // bypassing region restrictions (sword has MainHand region only by design).
        private void PlaceDualWieldInOffHand(DevionGames.InventorySystem.Item devionItem)
        {
            if (devionItem == null || _equipmentContainer == null) return;

            var offHandSlot = GetSlotByRegionName("OffHand");
            if (offHandSlot == null)
            {
                Debug.LogWarning("[PlayerView] PlaceDualWieldInOffHand: OffHand slot not found — dual wield not displayed in UI.");
                return;
            }

            // Setting ObservedItem directly updates the UI without going through ItemContainer.AddItem,
            // so it bypasses the region restriction check and does NOT fire OnAddItem event.
            offHandSlot.ObservedItem = devionItem;
            offHandSlot.Repaint();
        }

        // If no OnAddItem(OneHandedWeapon) follows within the same frame, the pending item was a
        // normal unequip — return it to inventory as usual.
        private System.Collections.IEnumerator ClearPendingDualWieldIfNotHandled(DevionGames.InventorySystem.Item item)
        {
            yield return null; // wait one frame

            if (_pendingDualWieldData != null) // not consumed by dual wield logic
            {
                _pendingDualWieldData = null;
                _pendingDualWieldDevionItem = null;
                StartCoroutine(ReturnItemToInventoryIfNeeded(item));
            }
        }

        private void UpdateRangeVisualizer()
        {
            if (_rangeVisualizer == null) return;
            float range = 2f; // Default
            if (_rightHandItem is WeaponData weapon) range = weapon.BaseAttackRange;
            else if (_leftHandItem is WeaponData weapon2) range = weapon2.BaseAttackRange;
            _rangeVisualizer.SetRadius(range);
        }


        private void RefreshWeaponVisuals()
        {
            if (_rightWeaponObj != null) Destroy(_rightWeaponObj);
            if (_leftWeaponObj != null) Destroy(_leftWeaponObj);

            var rSlot = _rightHandSlot != null ? _rightHandSlot : transform;
            var lSlot = _leftHandSlot != null ? _leftHandSlot : transform;

            // Instantiate tay trái
            if (_leftHandItem != null && _leftHandItem.VisualModel != null)
            {
                _leftWeaponObj = Instantiate(_leftHandItem.VisualModel, lSlot);
                _leftWeaponObj.transform.localPosition = _leftHandItem.PositionOffset;
                _leftWeaponObj.transform.localRotation = Quaternion.Euler(_leftHandItem.RotationOffset);
                _leftWeaponObj.transform.localScale = Vector3.one;
                SetLayerRecursive(_leftWeaponObj, 0);
            }

            // Instantiate tay phải
            if (_rightHandItem != null && _rightHandItem.VisualModel != null)
            {
                _rightWeaponObj = Instantiate(_rightHandItem.VisualModel, rSlot);
                _rightWeaponObj.transform.localPosition = _rightHandItem.PositionOffset;
                _rightWeaponObj.transform.localRotation = Quaternion.Euler(_rightHandItem.RotationOffset);
                _rightWeaponObj.transform.localScale = Vector3.one;
                SetLayerRecursive(_rightWeaponObj, 0);
            }
        }

        private void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
        }

        private void RefreshAnimator()
        {
            if (_characterAnimator == null) return;

            RuntimeAnimatorController targetController = _originalCharacterController;

            // Logic chọn Hoạt ảnh dựa trên Rule
            
            // 1. ƯU TIÊN VŨ KHÍ 2 TAY HOẶC CUNG (Luôn ở tay trái theo Rule)
            if (_leftHandItem is TwoHandedWeaponData || _leftHandItem is BowData)
            {
                if (_leftHandItem.MainAnimator != null) targetController = _leftHandItem.MainAnimator;
            }
            // 2. ƯU TIÊN KIẾM 1 TAY (Vũ khí chính ở tay phải)
            else if (_rightHandItem is OneHandedWeaponData sword)
            {
                if (_leftHandItem == null)
                {
                    if (sword.MainAnimator != null) targetController = sword.MainAnimator;
                }
                else if (_leftHandItem is OneHandedWeaponData)
                {
                    // Song kiếm
                    if (sword.DualWieldController != null) targetController = sword.DualWieldController;
                }
                else if (_leftHandItem is OffHandData offHand && offHand.SubType == OffHandType.Shield)
                {
                    // Kiếm + Khiên
                    if (sword.SwordShieldController != null) targetController = sword.SwordShieldController;
                }
                else
                {
                    // Trường hợp khác (ví dụ cầm kiếm + item lạ)
                    if (sword.MainAnimator != null) targetController = sword.MainAnimator;
                }
            }
            // 3. TRƯỜNG HỢP CHỈ CẦM KHIÊN
            else if (_leftHandItem is OffHandData oh && oh.SubType == OffHandType.Shield)
            {
                if (oh.MainAnimator != null) targetController = oh.MainAnimator;
            }

            if (targetController == null)
            {
                targetController = _originalCharacterController;
            }


            if (_characterAnimator.runtimeAnimatorController != targetController)
            {
                _characterAnimator.runtimeAnimatorController = targetController;
                
                // Sau khi đổi Controller, gán lại các biến
                _characterAnimator.SetBool("IsRunning", _isRunning);

                // Ép nhảy vào State phù hợp nếu nó tồn tại trong Controller mới
                string stateName = _isRunning ? "Run" : "Idle";
                if (_characterAnimator.HasState(0, Animator.StringToHash(stateName)))
                {
                    _characterAnimator.Play(stateName, 0, 0f);
                }
                
                // Cập nhật ngay lập tức
                _characterAnimator.Update(0f);
            }



        }

        #endregion

        // Scans equipment container for Flask equipment items (Flask First, Flask Second, Flask Third regions)
        // and logs them for debugging - the actual flask functionality is in the belt's FlaskSlots
        private void UpdateBeltFlasksFromEquipment()
        {
            if (_equipmentContainer == null) return;

            var flaskCount = 0;
            foreach (var slot in _equipmentContainer.Slots)
            {
                if (slot.IsEmpty || slot.ObservedItem == null) continue;

                if (slot.ObservedItem is CurveDashEquipmentAdapter adapter && adapter.OriginalEquipmentData is FlaskItemData flask)
                {
                    flaskCount++;
                    Debug.Log($"<color=cyan>[PlayerView] Flask equipped via equipment: {flask.ItemName}</color>");
                }
            }

            if (flaskCount > 0)
            {
                Debug.Log($"<color=lime>[PlayerView] Total {flaskCount} flasks equipped in equipment slots</color>");
            }
        }

        private void LoadOrEnableAsset(
            int index, 
            Transform container, 
            Dictionary<int, GameObject> cache, 
            Action<int, Action<GameObject>> loadFunc,
            ref CancellationTokenSource cts,
            Action onLoadedCallback)
        {
            foreach (var kvp in cache)
            {
                if (kvp.Value != null) kvp.Value.SetActive(false);
            }

            if (cache.TryGetValue(index, out var existingObj) && existingObj != null)
            {
                existingObj.SetActive(true);
                onLoadedCallback?.Invoke();
                return;
            }

            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();
            var token = cts.Token;

            loadFunc(index, prefab => 
            {
                if (this == null || token.IsCancellationRequested) return;
                var obj = Instantiate(prefab, container);
                cache[index] = obj;
                onLoadedCallback?.Invoke();
            });
        }

        private void OnVisualLoaded()
        {
            if (_modularView != null)
            {
                _modularView.OnAttackHitEvent -= OnAnimationHitTriggered;
            }

            if (_cachedCharacters.TryGetValue(_characterId, out var characterObj) && characterObj != null)
            {
                _modularView = characterObj.GetComponent<ModularCharacterView>();
                if (_modularView != null)
                {
                    _modularView.OnAttackHitEvent += OnAnimationHitTriggered;
                }
                // Tìm Animator trên chính object nhân vật trước, tránh tìm nhầm vào vũ khí con
                _characterAnimator = characterObj.GetComponent<Animator>();
                if (_characterAnimator == null) 
                    _characterAnimator = characterObj.GetComponentInChildren<Animator>();

                
                if (_originalCharacterController == null && _characterAnimator != null)
                {
                    _originalCharacterController = _characterAnimator.runtimeAnimatorController;
                }

                if (_modularView != null)
                {
                    _rightHandSlot = _modularView.RightHandSlot;
                    _leftHandSlot = _modularView.LeftHandSlot;

                    // Khởi tạo các giáp đang trang bị từ UserData lên mô hình Modular
                    if (_dataManager != null && _dataManager.UserData != null && _dataManager.UserData.EquippedItems != null && _assetManager != null)
                    {
                        foreach (var kvp in _dataManager.UserData.EquippedItems)
                        {
                            var item = _assetManager.GetItem(kvp.Value);
                            if (item is ArmorItemData armor)
                            {
                                _modularView.SetPartByName(armor.Slot, armor.MeshPartName, armor.ModularPartIndex);
                            }
                            else if (item is BeltItemData belt)
                            {
                                _modularView.SetPartByName(EquipmentSlot.Belt, belt.MeshPartName, belt.ModularPartIndex);
                                _beltFlaskService.SetBelt(belt);
                            }
                            else if (item is EquippableData equippable)
                            {
                                Equip(equippable);
                            }
                            // AmuletItemData and RingItemData have no visual — nothing to apply here
                        }
                    }
                }
                
                RefreshWeaponVisuals();
                RefreshAnimator();
            }

            if (_cachedSkins.TryGetValue(_skinIndex, out var mountObj) && mountObj != null)
            {
                _mountAnimator = mountObj.GetComponentInChildren<Animator>();
            }
            else
            {
                _mountAnimator = null;
            }

            SetRunning(_isRunning);

            if (_isInvincible)
            {
                RestoreOriginalMaterials();
                _isInvincible = false;
                SetInvincible(true);
            }
        }

        public void FlashWhite(float duration)
        {
            if (_damageFlashTimer > 0) return; // Đang chớp rồi thì thôi
            
            _damageFlashTimer = duration;
            _blinkTimer = 0;
            _isWhite = true;
            
            PrepareOriginalMaterials();
            ToggleWhiteMaterials(true);
        }

        private void PrepareOriginalMaterials()
        {
            _originalMaterials.Clear();
            var renderers = new List<Renderer>();
            if (_skinContainerTransform != null) renderers.AddRange(_skinContainerTransform.GetComponentsInChildren<Renderer>());
            if (_characterContainerTransform != null) renderers.AddRange(_characterContainerTransform.GetComponentsInChildren<Renderer>());

            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer) continue;
                _originalMaterials[r] = r.sharedMaterials;
            }

            if (_whiteMaterial == null)
            {
                _whiteMaterial = new Material(Shader.Find("Unlit/Color"));
                _whiteMaterial.color = Color.white;
            }
        }

        public void SetInvincible(bool isInvincible)
        {
            _isInvincible = isInvincible;
            if (!isInvincible && _damageFlashTimer <= 0)
            {
                RestoreOriginalMaterials();
            }
        }


        private void ToggleWhiteMaterials(bool useWhite)
        {
            foreach (var kvp in _originalMaterials)
            {
                if (kvp.Key == null) continue;
                if (useWhite)
                {
                    var mats = new Material[kvp.Value.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = _whiteMaterial;
                    kvp.Key.sharedMaterials = mats;
                }
                else kvp.Key.sharedMaterials = kvp.Value;
            }
        }

        private void RestoreOriginalMaterials()
        {
            foreach (var kvp in _originalMaterials)
            {
                if (kvp.Key != null) kvp.Key.sharedMaterials = kvp.Value;
            }
            _originalMaterials.Clear();
        }

        public void SetRunning(bool isRunning)
        {
            _isRunning = isRunning;
            if (_characterAnimator != null) _characterAnimator.SetBool("IsRunning", isRunning);
            if (_mountAnimator != null) _mountAnimator.SetBool("IsRunning", isRunning);
        }

        public void RotateModelTowards(Vector3 targetPosition)
        {
            if (_characterContainerTransform == null) return;
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0; // Lock Y axis
            if (direction != Vector3.zero)
            {
                _characterContainerTransform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }

        private Action _onAttackImpactCallback;
        private Coroutine _attackDelayCoroutine;

        public void TriggerAttack(Action onImpact, float attackSpeed, bool useAnimationEvent, float attackHitDelay, string animationTrigger = "Attack")
        {
            if (_attackDelayCoroutine != null)
            {
                StopCoroutine(_attackDelayCoroutine);
                _attackDelayCoroutine = null;
            }

            _onAttackImpactCallback = onImpact;

            // Trigger the attack animation
            PlayAttack(animationTrigger);

            if (useAnimationEvent)
            {
                // Start a safety timeout (e.g. 1.5s / attackSpeed) to guarantee the attack triggers even if the animation event fails
                float safetyTimeout = (attackHitDelay > 0 ? attackHitDelay * 2.0f : 1.0f) / attackSpeed;
                _attackDelayCoroutine = StartCoroutine(CoSafetyTimeout(safetyTimeout));
            }
            else
            {
                // Start standard delay timer
                float delay = attackHitDelay / attackSpeed;
                _attackDelayCoroutine = StartCoroutine(CoAttackDelay(delay));
            }
        }

        private void TriggerImpact()
        {
            if (_attackDelayCoroutine != null)
            {
                StopCoroutine(_attackDelayCoroutine);
                _attackDelayCoroutine = null;
            }

            if (_onAttackImpactCallback != null)
            {
                var callback = _onAttackImpactCallback;
                _onAttackImpactCallback = null; // Clear to prevent double triggering
                callback.Invoke();
            }
        }

        private void OnAnimationHitTriggered()
        {
            TriggerImpact();
        }

        private System.Collections.IEnumerator CoAttackDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            TriggerImpact();
        }

        private System.Collections.IEnumerator CoSafetyTimeout(float timeout)
        {
            yield return new WaitForSeconds(timeout);
            TriggerImpact();
        }

        public void PlayAttack(string triggerName = "Attack")
        {
            if (_characterAnimator == null) return;

            _characterAnimator.ResetTrigger(triggerName);
            _characterAnimator.SetTrigger(triggerName);
        }

        private void UpdateAnimatorSpeeds()
        {
            if (_characterAnimator == null) return;

            bool isAttacking = false;
            for (int i = 0; i < _characterAnimator.layerCount; i++)
            {
                var stateInfo = _characterAnimator.GetCurrentAnimatorStateInfo(i);
                if (stateInfo.IsTag("Attack"))
                {
                    isAttacking = true;
                    break;
                }
            }

            if (isAttacking)
            {
                // Attack animation speed scales with the attack speed (enforce a healthy minimum of 0.2f to prevent locking)
                float attackAnimSpeed = AttackSpeed;
                if (attackAnimSpeed < 0.2f) attackAnimSpeed = 1.0f;
                _characterAnimator.speed = attackAnimSpeed;
                
                // Mount doesn't attack, run it at normal speed (or idle)
                if (_mountAnimator != null) _mountAnimator.speed = 1f;
            }
            else if (_isRunning)
            {
                // Run animation speed scales with the movement speed
                // Normalize by baseline movement speed (e.g. BallInitialSpeed)
                float baseSpeed = _gameSettings != null ? _gameSettings.BallInitialSpeed : 5f;
                if (baseSpeed <= 0f) baseSpeed = 5f;
                
                float runSpeedFactor = MovementSpeed / baseSpeed;
                _characterAnimator.speed = runSpeedFactor;
                
                if (_mountAnimator != null) _mountAnimator.speed = runSpeedFactor;
            }
            else
            {
                // Idle or other states
                _characterAnimator.speed = 1f;
                if (_mountAnimator != null) _mountAnimator.speed = 1f;
            }
        }

        #region DEBUG GIZMOS

        private void OnDrawGizmos()
        {
            float range = 2f; // Default
            if (_rightHandItem is WeaponData weapon) range = weapon.BaseAttackRange;
            else if (_leftHandItem is WeaponData weapon2) range = weapon2.BaseAttackRange;


            // 1. Vẽ tầm đánh
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, range);

            // 2. Tìm và vẽ quái vật gần đó (Tối ưu: Chỉ tìm trong bán kính rộng hơn tầm đánh chút)
            Gizmos.color = Color.yellow;
            var colliders = Physics.OverlapSphere(transform.position, range * 2f);
            int count = 0;
            foreach (var col in colliders)
            {
                if (count > 5) break; // Giới hạn số lượng line để không bị loạn
                if (col.CompareTag("Enemy") || col.gameObject.name.Contains("Monster"))
                {
                    Gizmos.DrawLine(transform.position, col.transform.position);
                    count++;
                }
            }

        }

        #endregion

        #region CHARACTER INFO STATS SYNC

        /// <summary>
        /// Pushes live game stats into Devion's "Player Stats" handler so Character Info UI reflects
        /// actual gameplay values. Sets the LEAF stats that formula stats (Melee/Ranged Attack) derive from.
        /// Called after any equip/unequip event and on startup.
        /// </summary>
        public void SyncCharacterInfoStats()
        {
            try
            {
                var handler = DevionGames.StatSystem.StatsManager.GetStatsHandler("Player Stats");
                if (handler == null)
                {
                    Debug.LogWarning("[PlayerView] SyncCharacterInfoStats: 'Player Stats' handler is NULL — aborting.");
                    return;
                }

                // Scan Equipment container slots directly — the source of truth.
                // Do NOT rely on _dataManager.UserData.EquippedItems or CurrentWeaponInstance:
                // Devion can fire OnAddItem for previously-saved items at unpredictable times,
                // which corrupts both caches. Reading slots gives the actual equipped state.
                int totalArmor = 0;
                WeaponInstance weaponInSlot = null;

                if (_equipmentContainer != null)
                {
                    for (int i = 0; i < _equipmentContainer.Slots.Count; i++)
                    {
                        var s = _equipmentContainer.Slots[i];
                        if (s.IsEmpty || s.ObservedItem == null) continue;
                        if (s.ObservedItem is CurveDashEquipmentAdapter adapter && adapter.OriginalEquipmentData != null)
                        {
                            if (adapter.OriginalEquipmentData is ArmorItemData armor)
                            {
                                totalArmor += armor.Defense;
                                Debug.Log($"<color=lime>[PlayerView] Sync: slot[{i}] armor '{armor.name}' +{armor.Defense} (total={totalArmor})</color>");
                            }
                            else if (adapter.OriginalEquipmentData is WeaponData weaponBase && weaponInSlot == null)
                            {
                                // Prefer CurrentWeaponInstance when it matches (preserves gem modifiers).
                                weaponInSlot = (CurrentWeaponInstance?.BaseData == weaponBase)
                                    ? CurrentWeaponInstance
                                    : new WeaponInstance(weaponBase, weaponBase.Rarity);
                                Debug.Log($"<color=lime>[PlayerView] Sync: slot[{i}] weapon '{weaponBase.name}' min={weaponInSlot.FinalMinDamage} max={weaponInSlot.FinalMaxDamage}</color>");
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[PlayerView] SyncCharacterInfoStats: _equipmentContainer is null, stats will be zero.");
                }

                Debug.Log($"<color=lime>[PlayerView] SyncCharacterInfoStats → Armor={totalArmor}, Weapon={weaponInSlot?.BaseData?.name ?? "none"}</color>");
                TrySetStatBase(handler, "Armor", totalArmor);

                if (weaponInSlot?.BaseData != null)
                {
                    float minDmg = weaponInSlot.FinalMinDamage;
                    float maxDmg = weaponInSlot.FinalMaxDamage;
                    float avgDmg = (minDmg + maxDmg) / 2f;
                    bool isRanged = weaponInSlot.BaseData is BowData;

                    // Do NOT set Min/Max Damage here — the Devion formula for Melee/Ranged Attack
                    // reads Min Damage + Max Damage and would double-count if those leaf stats are
                    // non-zero. We bypass the formula entirely by setting the display stats directly,
                    // which produces Value = BaseValue + formula(0,0) = BaseValue.
                    TrySetStatBase(handler, "Melee Attack",  isRanged ? 0f : avgDmg);
                    TrySetStatBase(handler, "Ranged Attack", isRanged ? avgDmg : 0f);
                }
                else
                {
                    TrySetStatBase(handler, "Melee Attack", 0f);
                    TrySetStatBase(handler, "Ranged Attack", 0f);
                }

                // Sync affix stats that are not derivable from base weapon data alone.
                float critBonus = 0f;
                if (weaponInSlot?.Affixes != null)
                {
                    foreach (var affix in weaponInSlot.Affixes)
                    {
                        if (affix.Type == StatType.IncreasedCriticalChance)
                            critBonus += affix.Value;
                    }
                }
                TrySetStatBase(handler, "Critical Strike", critBonus);

                handler.onUpdate?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerView] SyncCharacterInfoStats failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void TrySetStatBase(DevionGames.StatSystem.StatsHandler handler, string statName, float value)
        {
            var stat = handler.GetStat(statName);
            if (stat != null)
            {
                float before = stat.BaseValue;
                stat.BaseValue = value;
                Debug.Log($"<color=lime>[PlayerView] TrySetStatBase: '{statName}' {before} → {value} (Value after={stat.Value})</color>");
            }
            else
            {
                Debug.LogWarning($"[PlayerView] TrySetStatBase: stat '{statName}' NOT FOUND in 'Player Stats'. Available: {string.Join(", ", System.Linq.Enumerable.Select(handler.m_Stats, s => s != null ? s.Name : "null"))}");
            }
        }

        #endregion
    }

    public class PlayerViewFactory : PlaceholderFactory<PlayerView> { }
}

