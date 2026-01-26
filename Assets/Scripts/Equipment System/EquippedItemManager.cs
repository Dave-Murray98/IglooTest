using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// Equipment manager with ItemInstance validation
/// REFACTORED: All equipment operations validate instances exist
/// UPDATED: Added GetEquippedInventoryItemId() for handler removal operations
/// </summary>
public class EquippedItemManager : MonoBehaviour, IManagerState
{
    public static EquippedItemManager Instance { get; private set; }

    private ManagerOperationalState operationalState = ManagerOperationalState.Gameplay;
    public ManagerOperationalState CurrentOperationalState => operationalState;


    [Header("Equipment Configuration")]
    [SerializeField] private bool enableStateRestrictions = true;
    [SerializeField] private bool showRestrictionFeedback = true;
    [SerializeField] private float scrollCooldown = 0.1f;

    [Header("Audio Feedback")]
    [SerializeField] private AudioClip equipSound;
    [SerializeField] private AudioClip hotkeySound;
    [SerializeField] private AudioClip restrictedSound;

    [Header("Debug Settings")]
    public bool enableDebugLogs = false;

    [SerializeField, ReadOnly] private EquipmentSaveData equipmentData;
    [SerializeField, ReadOnly] private int currentActiveSlot = 1;
    [SerializeField, ReadOnly] private bool isCurrentSlotUsable = false;
    [SerializeField, ReadOnly] private bool hasEquippedItem = false;

    private PlayerInventoryManager inventoryManager;
    private PlayerStateManager playerStateManager;
    [Header("Visual System")]
    [HideInInspector] public EquippedItemVisualManager visualManager;
    [SerializeField] private bool autoFindVisualManager = true;

    private float lastScrollTime = 0f;

    #region Events
    public System.Action<EquippedItemData> OnItemEquipped;
    public System.Action OnItemUnequipped;
    public System.Action OnUnarmedActivated;
    public System.Action<int, HotkeyBinding, bool> OnSlotSelected;
    public System.Action<int, HotkeyBinding> OnHotkeyAssigned;
    public System.Action<int> OnHotkeyCleared;
    public System.Action<string, string> OnEquipmentRestricted;
    public System.Action<string> OnStateRestrictionMessage;
    public System.Action<ItemType, bool> OnItemActionPerformed;
    #endregion

    #region Public Properties
    public EquippedItemData CurrentEquippedItem => equipmentData.equippedItem;
    public bool HasEquippedItem => hasEquippedItem;
    public int GetCurrentActiveSlot() => currentActiveSlot;
    public bool IsUnarmed => !hasEquippedItem;
    public ItemData GetEquippedItemData() => hasEquippedItem ? equipmentData.equippedItem.GetItemData() : null;

    /// <summary>
    /// Get equipped ItemInstance for handlers
    /// </summary>
    public ItemInstance GetEquippedItemInstance() => hasEquippedItem ? equipmentData.equippedItem.GetItemInstance() : null;

    /// <summary>
    /// CRITICAL: Get the inventory item ID for the currently equipped item
    /// Handlers should use this when removing items from inventory
    /// </summary>
    public string GetEquippedInventoryItemId()
    {
        if (!hasEquippedItem || equipmentData?.equippedItem == null)
        {
            DebugLog("GetEquippedInventoryItemId: No item equipped");
            return null;
        }

        string inventoryId = equipmentData.equippedItem.inventoryItemId;

        if (string.IsNullOrEmpty(inventoryId))
        {
            DebugLog($"WARNING: Equipped item has no inventory ID! (ItemData: {equipmentData.equippedItem.equippedItemDataName})");
        }
        else
        {
            DebugLog($"GetEquippedInventoryItemId: {inventoryId} (ItemData: {equipmentData.equippedItem.equippedItemDataName}, Instance: {equipmentData.equippedItem.equippedItemInstanceId})");
        }

        return inventoryId;
    }
    #endregion

    #region Initialization
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeEquipmentSystem();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        FindSystemReferences();
        SetupEventSubscriptions();
        SetupInputHandling();
        DebugLog("EquippedItemManager initialized");
    }

    private void InitializeEquipmentSystem()
    {
        equipmentData = new EquipmentSaveData();
        currentActiveSlot = 1;
        isCurrentSlotUsable = false;
        hasEquippedItem = false;
        DebugLog("Equipment system initialized");
    }

    private void FindSystemReferences()
    {
        inventoryManager = PlayerInventoryManager.Instance;
        playerStateManager = PlayerStateManager.Instance ?? FindFirstObjectByType<PlayerStateManager>();

        if (inventoryManager == null)
            Debug.LogError("[EquippedItemManager] InventoryManager not found!");

        if (playerStateManager == null)
        {
            Debug.LogError("[EquippedItemManager] PlayerStateManager not found! State restrictions disabled.");
            enableStateRestrictions = false;
        }

        if (visualManager == null && autoFindVisualManager)
            visualManager = FindFirstObjectByType<EquippedItemVisualManager>();

        DebugLog($"References - Inventory: {inventoryManager != null}, StateManager: {playerStateManager != null}, VisualManager: {visualManager != null}");
    }

    private void SetupEventSubscriptions()
    {
        if (GameManager.Instance != null)
            GameManager.OnManagersRefreshed += FindSystemReferences;

        if (InputManager.Instance != null)
            InputManager.OnInputManagerReady += OnInputManagerReady;

        if (inventoryManager != null)
        {
            inventoryManager.OnItemRemoved += OnInventoryItemRemoved;
            inventoryManager.OnItemAdded += OnInventoryItemAdded;
        }

        if (playerStateManager != null)
            playerStateManager.OnStateChanged += OnPlayerStateChanged;
    }

    private void SetupInputHandling()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnScrollWheelInput += HandleScrollInput;
            InputManager.Instance.OnHotkeyPressed += OnHotkeyPressed;
        }
    }

    private void OnInputManagerReady(InputManager inputManager) => SetupInputHandling();
    #endregion

    #region Input Handling

    private void Update()
    {
        // Add this check first!
        if (!CanOperateInCurrentState())
            return;

        // Your existing Update code
        HandleItemActions();
        if (InputManager.Instance == null) HandleFallbackHotkeyInput();
    }

    private void HandleScrollInput(Vector2 scrollDelta)
    {
        if (Time.time - lastScrollTime < scrollCooldown) return;
        if (GameManager.Instance?.uiManager?.isInventoryOpen == true) return;
        if (Mathf.Abs(scrollDelta.y) <= 0.1f) return;

        lastScrollTime = Time.time;
        CycleToNextSlot(scrollDelta.y > 0);
    }

    private void OnHotkeyPressed(int slotNumber) => SelectSlot(slotNumber);

    private void HandleFallbackHotkeyInput()
    {
        for (int i = 1; i <= 10; i++)
        {
            KeyCode key = i == 10 ? KeyCode.Alpha0 : (KeyCode)((int)KeyCode.Alpha1 + (i - 1));
            if (Input.GetKeyDown(key)) SelectSlot(i);
        }
    }

    private void HandleItemActions()
    {
        if (GameManager.Instance?.uiManager?.isInventoryOpen == true) return;
        if (Input.GetMouseButtonDown(0)) PerformItemAction(true);
        if (Input.GetMouseButtonDown(1)) PerformItemAction(false);
    }
    #endregion


    #region Core Slot Management
    /// <summary>
    /// Select slot with instance validation
    /// UPDATED: Enhanced debug logging for inventory ID tracking
    /// </summary>
    public void SelectSlot(int slotNumber)
    {
        if (!IsValidSlotNumber(slotNumber)) return;

        DebugLog($"Selecting slot {slotNumber}");
        currentActiveSlot = slotNumber;

        var binding = equipmentData.GetHotkeyBinding(slotNumber);
        if (binding == null) return;

        bool slotHasItem = false;
        bool slotIsUsable = false;
        ItemData itemData = null;
        ItemInstance itemInstance = null;

        if (binding.isAssigned)
        {
            if (inventoryManager != null)
            {
                var inventoryItem = inventoryManager.InventoryGridData.GetItem(binding.itemId);
                if (inventoryItem?.ItemInstance != null)
                {
                    itemInstance = inventoryItem.ItemInstance;
                    itemData = itemInstance.ItemData;
                    slotHasItem = true;
                    slotIsUsable = !enableStateRestrictions || CanEquipItemInCurrentState(itemData);
                }
                else
                {
                    DebugLog($"Item in slot {slotNumber} missing instance - clearing binding");
                    binding.ClearSlot();
                    OnHotkeyCleared?.Invoke(slotNumber);
                }
            }
        }

        isCurrentSlotUsable = slotIsUsable;
        hasEquippedItem = slotIsUsable && itemData != null;
        equipmentData.UpdateCurrentState(currentActiveSlot, hasEquippedItem);

        if (slotHasItem && slotIsUsable && itemData != null)
        {
            bool equipped = equipmentData.equippedItem.EquipFromHotkey(binding.itemId, itemData, slotNumber);

            if (equipped)
            {
                if (visualManager != null)
                    visualManager.EquipHotbarSlot(slotNumber, itemData);

                DebugLog($"Equipped {itemData.itemName} from slot {slotNumber} (InventoryID: {binding.itemId}, Instance: {itemInstance.InstanceID})");
                OnItemEquipped?.Invoke(equipmentData.equippedItem);
            }
            else
            {
                DebugLog($"Failed to equip {itemData.itemName} - instance validation failed");
                hasEquippedItem = false;
                equipmentData.UpdateCurrentState(currentActiveSlot, false);
                OnItemUnequipped?.Invoke();
                OnUnarmedActivated?.Invoke();
            }
        }
        else
        {
            equipmentData.equippedItem.Clear();

            if (visualManager != null)
                visualManager.UnequipCurrentItem();

            if (slotHasItem && !slotIsUsable && itemData != null)
            {
                DebugLog($"Slot {slotNumber} restricted - {itemData.itemName}");
                ShowRestrictionFeedback(itemData, "Cannot use in current state");
            }
            else
            {
                DebugLog($"Slot {slotNumber} unarmed (empty)");
            }

            OnItemUnequipped?.Invoke();
            OnUnarmedActivated?.Invoke();
        }

        OnSlotSelected?.Invoke(slotNumber, binding, slotIsUsable);

        if (hasEquippedItem) PlayHotkeySound();
        else if (slotHasItem && !slotIsUsable) PlayRestrictedSound();
        else PlayHotkeySound();
    }

    public void CycleToNextSlot(bool forward)
    {
        int nextSlot = forward ? currentActiveSlot + 1 : currentActiveSlot - 1;
        if (nextSlot > 10) nextSlot = 1;
        if (nextSlot < 1) nextSlot = 10;
        DebugLog($"Cycling from {currentActiveSlot} to {nextSlot}");
        SelectSlot(nextSlot);
    }
    #endregion

    #region Equipment Management
    /// <summary>
    /// Assign item with instance validation
    /// </summary>
    public bool AssignItemToHotkey(string itemId, int slotNumber)
    {
        DebugLog($"Trying to assigning item {itemId} to slot {slotNumber}");

        if (!IsValidSlotNumber(slotNumber)) return false;
        if (!ValidateInventoryManager()) return false;

        var inventoryItem = inventoryManager.InventoryGridData.GetItem(itemId);
        if (inventoryItem?.ItemInstance == null)
        {
            DebugLog($"Cannot assign - item {itemId} has no ItemInstance");
            return false;
        }

        var binding = equipmentData.GetHotkeyBinding(slotNumber);
        if (binding == null) return false;

        bool assigned = binding.AssignItem(itemId, inventoryItem.ItemData.name);
        if (!assigned)
        {
            DebugLog($"Failed to assign {itemId} to slot {slotNumber} - instance validation failed");
            return false;
        }

        OnHotkeyAssigned?.Invoke(slotNumber, binding);

        if (visualManager != null)
            visualManager.AddHotbarSlotObject(slotNumber, inventoryItem.ItemData);

        if (slotNumber == currentActiveSlot)
            SelectSlot(slotNumber);

        DebugLog($"Assigned {inventoryItem.ItemData.itemName} to slot {slotNumber} (Instance: {inventoryItem.ItemInstance.InstanceID})");
        return true;
    }

    public void UnequipCurrentItem()
    {
        if (!hasEquippedItem) return;

        string itemName = equipmentData.equippedItem.GetItemData()?.itemName ?? "Unknown";
        equipmentData.equippedItem.Clear();
        hasEquippedItem = false;
        equipmentData.UpdateCurrentState(currentActiveSlot, false);

        OnItemUnequipped?.Invoke();
        OnUnarmedActivated?.Invoke();

        var binding = equipmentData.GetHotkeyBinding(currentActiveSlot);
        OnSlotSelected?.Invoke(currentActiveSlot, binding, false);

        DebugLog($"Unequipped {itemName}");
    }

    public void RefreshAllEquippedItemPrefabs()
    {
        if (visualManager == null)
        {
            visualManager = FindFirstObjectByType<EquippedItemVisualManager>();
            if (visualManager == null) return;
        }

        DebugLog("Refreshing all hotbar visuals");

        Dictionary<int, ItemData> hotbarItems = new Dictionary<int, ItemData>();

        foreach (HotkeyBinding binding in equipmentData.hotkeyBindings)
        {
            if (binding.isAssigned)
            {
                ItemData itemData = binding.GetCurrentItemData();
                if (itemData != null && itemData.equippedItemPrefab != null)
                {
                    hotbarItems[binding.slotNumber] = itemData;
                    DebugLog($"Added slot {binding.slotNumber}: {itemData.itemName}");
                }
            }
        }

        if (visualManager != null)
        {
            visualManager.PopulateHotbarEquippedItemPrefabs(hotbarItems);
            DebugLog($"Populated {hotbarItems.Count} hotbar visuals");
        }
    }
    #endregion

    #region Item Actions
    public void PerformItemAction(bool isLeftClick)
    {
        if (!hasEquippedItem)
        {
            if (isLeftClick) PerformPunch();
            return;
        }

        var itemData = GetEquippedItemData();
        if (itemData == null) return;

        if (!CanEquipItemInCurrentState(itemData))
        {
            DebugLog($"Action blocked - {itemData.itemName} not valid");
            ShowRestrictionFeedback(itemData, "Cannot use in current state");
            if (isLeftClick) PerformPunch();
            return;
        }

        switch (itemData.itemType)
        {
            case ItemType.RangedWeapon:
                if (isLeftClick) PerformWeaponAttack(itemData);
                else PerformWeaponAim(itemData);
                break;
            case ItemType.Consumable:
                if (isLeftClick) PerformPunch();
                else PerformConsumeItem(itemData);
                break;
            case ItemType.Tool:
                if (isLeftClick) PerformPunch();
                else PerformUseEquipment(itemData);
                break;
            case ItemType.KeyItem:
                if (isLeftClick) PerformPunch();
                else PerformUseKeyItem(itemData);
                break;
            case ItemType.Clothing:
                if (isLeftClick) PerformPunch();
                else PerformUseClothing(itemData);
                break;
            case ItemType.Ammo:
                if (isLeftClick) PerformPunch();
                break;
        }

        OnItemActionPerformed?.Invoke(itemData.itemType, isLeftClick);
    }

    private void PerformPunch() => DebugLog("Performing unarmed attack");
    private void PerformWeaponAttack(ItemData weapon) => DebugLog($"Attacking with {weapon.itemName}");
    private void PerformWeaponAim(ItemData weapon) => DebugLog($"Aiming {weapon.itemName}");
    private void PerformConsumeItem(ItemData consumable) => DebugLog($"Consuming {consumable.itemName}");
    private void PerformUseEquipment(ItemData equipment) => DebugLog($"Using equipment {equipment.itemName}");
    private void PerformUseKeyItem(ItemData keyItem) => DebugLog($"Using key item {keyItem.itemName}");
    private void PerformUseClothing(ItemData clothing) => DebugLog($"Using clothing {clothing.itemName}");
    #endregion

    #region State Management
    private bool CanEquipItemInCurrentState(ItemData itemData)
    {
        if (!enableStateRestrictions || playerStateManager == null || itemData == null)
            return true;
        return playerStateManager.CanEquipItem(itemData);
    }

    private void ShowRestrictionFeedback(ItemData itemData, string context)
    {
        if (!showRestrictionFeedback || itemData == null) return;

        string reason = GetRestrictionReason(itemData);
        string currentState = playerStateManager?.GetCurrentStateDisplayName() ?? "current state";

        OnEquipmentRestricted?.Invoke(itemData.itemName, reason);
        OnStateRestrictionMessage?.Invoke($"{context} in {currentState}");
        PlayRestrictedSound();
    }

    private string GetRestrictionReason(ItemData itemData)
    {
        if (itemData == null) return "Unknown restriction";

        var usableStates = itemData.GetUsableStates();
        if (usableStates.Length == 0) return "This item cannot be used anywhere";

        var stateNames = new List<string>();
        foreach (var state in usableStates)
        {
            stateNames.Add(state switch
            {
                PlayerStateType.Ground => "on land",
                PlayerStateType.Water => "in water",
                PlayerStateType.Vehicle => "in vehicles",
                _ => state.ToString().ToLower()
            });
        }

        return $"Only usable {string.Join(" or ", stateNames)}";
    }

    private void OnPlayerStateChanged(PlayerStateType previousState, PlayerStateType newState)
    {
        DebugLog($"Player state changed: {previousState} -> {newState}");
        SelectSlot(currentActiveSlot);
    }
    #endregion

    #region Event Handlers
    private void OnInventoryItemRemoved(string itemId)
    {
        bool slotStillHasItems = false;

        foreach (var binding in equipmentData.hotkeyBindings)
        {
            if (binding.RemoveItem(itemId))
            {
                slotStillHasItems = binding.isAssigned;

                if (!slotStillHasItems && visualManager != null)
                {
                    DebugLog($"Slot {binding.slotNumber} empty - removing visual");
                    visualManager.RemoveHotbarSlotObject(binding.slotNumber);
                    OnHotkeyCleared?.Invoke(binding.slotNumber);
                }
                else if (slotStillHasItems)
                {
                    DebugLog($"Slot {binding.slotNumber} still has items");
                    OnHotkeyAssigned?.Invoke(binding.slotNumber, binding);
                }

                if (binding.slotNumber == currentActiveSlot)
                    SelectSlot(currentActiveSlot);
            }
        }

        if (equipmentData.equippedItem.IsEquipped(itemId))
        {
            DebugLog($"Equipped item {itemId} removed - switching to unarmed");
            hasEquippedItem = false;
            equipmentData.equippedItem.Clear();
            equipmentData.UpdateCurrentState(currentActiveSlot, false);

            if (visualManager != null && !slotStillHasItems)
                visualManager.UnequipCurrentItem();

            OnItemUnequipped?.Invoke();
            OnUnarmedActivated?.Invoke();
        }
    }

    private void OnInventoryItemAdded(InventoryItemData newItem)
    {
        if (newItem?.ItemData?.itemType != ItemType.Consumable) return;

        foreach (var binding in equipmentData.hotkeyBindings)
        {
            if (binding.TryAddToStack(newItem.ID, newItem.ItemData.name))
            {
                OnHotkeyAssigned?.Invoke(binding.slotNumber, binding);

                if (visualManager != null)
                    visualManager.UpdateHotbarSlotObject(binding.slotNumber, newItem.ItemData);

                if (binding.slotNumber == currentActiveSlot)
                    SelectSlot(currentActiveSlot);

                DebugLog($"Added {newItem.ItemData.itemName} to slot {binding.slotNumber} stack");
                break;
            }
        }
    }
    #endregion

    #region Public API
    public HotkeyBinding GetHotkeyBinding(int slotNumber) => equipmentData.GetHotkeyBinding(slotNumber);
    public List<HotkeyBinding> GetAllHotkeyBindings() => equipmentData.hotkeyBindings;

    public List<(HotkeyBinding binding, bool isUsable)> GetAllHotkeyBindingsWithValidity()
    {
        var result = new List<(HotkeyBinding, bool)>();

        foreach (var binding in equipmentData.hotkeyBindings)
        {
            bool isUsable = true;

            if (binding.isAssigned && inventoryManager != null)
            {
                var item = inventoryManager.InventoryGridData.GetItem(binding.itemId);
                if (item?.ItemData != null)
                    isUsable = CanEquipItemInCurrentState(item.ItemData);
            }

            result.Add((binding, isUsable));
        }

        return result;
    }

    public (int slotNumber, bool hasItem, bool isUsable) GetCurrentSlotInfo()
    {
        var binding = equipmentData.GetHotkeyBinding(currentActiveSlot);
        return (currentActiveSlot, binding?.isAssigned ?? false, isCurrentSlotUsable);
    }

    public bool IsCurrentEquipmentValid() => hasEquippedItem;
    public void ValidateEquipmentForCurrentState() => SelectSlot(currentActiveSlot);
    #endregion

    #region IManagerState Implementation

    // ADD THESE METHODS
    public void SetOperationalState(ManagerOperationalState newState)
    {
        if (newState == operationalState) return;

        DebugLog($"Transitioning from {operationalState} to {newState}");
        operationalState = newState;

        switch (newState)
        {
            case ManagerOperationalState.Menu:
                OnEnterMenuState();
                break;
            case ManagerOperationalState.Gameplay:
                OnEnterGameplayState();
                break;
            case ManagerOperationalState.Transition:
                OnEnterTransitionState();
                break;
        }
    }

    public void OnEnterMenuState()
    {
        DebugLog("Entering Menu state - equipment preserved but actions disabled");
        // Equipment data should persist, just disable actions
    }

    public void OnEnterGameplayState()
    {
        DebugLog("Entering Gameplay state - equipment actions enabled");
        // Resume normal operations
    }

    public void OnEnterTransitionState()
    {
        DebugLog("Entering Transition state");
    }

    public bool CanOperateInCurrentState()
    {
        return operationalState == ManagerOperationalState.Gameplay;
    }

    #endregion

    #region Save System Integration
    public void SetEquipmentData(EquipmentSaveData newData)
    {
        if (newData == null || !newData.IsValid())
        {
            DebugLog("Invalid equipment data - clearing");
            ClearEquipmentState();
            return;
        }

        DebugLog("=== RESTORING EQUIPMENT ===");
        equipmentData = newData;
        currentActiveSlot = newData.currentActiveSlot;
        hasEquippedItem = newData.hasEquippedItem;

        var assignedCount = equipmentData.hotkeyBindings.FindAll(h => h.isAssigned).Count;
        DebugLog($"Restoring: {assignedCount} assignments, slot: {currentActiveSlot}, equipped: {hasEquippedItem}");

        RefreshAllEquippedItemPrefabs();
        RestoreSlotSelection();

        DebugLog("Equipment restoration complete");
    }

    private void RestoreSlotSelection()
    {
        DebugLog($"Restoring slot {currentActiveSlot}");

        var activeBinding = equipmentData.GetHotkeyBinding(currentActiveSlot);
        if (activeBinding == null)
        {
            DebugLog($"No binding for slot {currentActiveSlot} - defaulting to 1");
            currentActiveSlot = 1;
            activeBinding = equipmentData.GetHotkeyBinding(1);
        }

        bool shouldHaveEquippedItem = false;
        ItemData itemData = null;
        ItemInstance itemInstance = null;

        if (activeBinding.isAssigned && inventoryManager != null)
        {
            var inventoryItem = inventoryManager.InventoryGridData.GetItem(activeBinding.itemId);
            if (inventoryItem?.ItemInstance != null)
            {
                itemInstance = inventoryItem.ItemInstance;
                itemData = itemInstance.ItemData;
                shouldHaveEquippedItem = !enableStateRestrictions || CanEquipItemInCurrentState(itemData);
            }
        }

        isCurrentSlotUsable = shouldHaveEquippedItem;
        hasEquippedItem = shouldHaveEquippedItem;
        equipmentData.UpdateCurrentState(currentActiveSlot, hasEquippedItem);

        if (shouldHaveEquippedItem && itemData != null)
        {
            bool equipped = equipmentData.equippedItem.EquipFromHotkey(activeBinding.itemId, itemData, currentActiveSlot);

            if (equipped && visualManager != null)
            {
                DebugLog($"Activating visual: {itemData.itemName} slot {currentActiveSlot} (Instance: {itemInstance.InstanceID})");
                visualManager.EquipHotbarSlot(currentActiveSlot, itemData);
            }

            DebugLog($"Restored equipped: {itemData.itemName}");
            OnItemEquipped?.Invoke(equipmentData.equippedItem);
        }
        else
        {
            equipmentData.equippedItem.Clear();

            if (visualManager != null)
                visualManager.UnequipCurrentItem();

            DebugLog($"Restored unarmed - slot {currentActiveSlot}");
            OnItemUnequipped?.Invoke();
            OnUnarmedActivated?.Invoke();
        }

        OnSlotSelected?.Invoke(currentActiveSlot, activeBinding, isCurrentSlotUsable);
        DebugLog($"Slot restoration complete - Active: {currentActiveSlot}, Equipped: {hasEquippedItem}");
    }

    public void ClearEquipmentState()
    {
        equipmentData.equippedItem.Clear();
        hasEquippedItem = false;
        currentActiveSlot = 1;
        isCurrentSlotUsable = false;
        equipmentData.UpdateCurrentState(1, false);

        if (visualManager != null)
            visualManager.ForceCleanup();

        foreach (var binding in equipmentData.hotkeyBindings)
        {
            if (binding.isAssigned)
            {
                int slotNumber = binding.slotNumber;
                binding.ClearSlot();
                OnHotkeyCleared?.Invoke(slotNumber);
            }
        }

        OnItemUnequipped?.Invoke();
        OnUnarmedActivated?.Invoke();
        DebugLog("Equipment cleared");
    }

    public EquipmentSaveData GetEquipmentDataDirect()
    {
        if (equipmentData != null)
        {
            equipmentData.UpdateCurrentState(currentActiveSlot, hasEquippedItem);
            DebugLog($"Updated save data - slot: {currentActiveSlot}, equipped: {hasEquippedItem}");
        }

        return new EquipmentSaveData(equipmentData);
    }
    #endregion

    #region Audio
    private void PlayEquipSound()
    {
        if (equipSound != null)
            AudioSource.PlayClipAtPoint(equipSound, Vector3.zero);
    }

    private void PlayHotkeySound()
    {
        if (hotkeySound != null)
            AudioSource.PlayClipAtPoint(hotkeySound, Vector3.zero);
    }

    private void PlayRestrictedSound()
    {
        if (restrictedSound != null)
            AudioSource.PlayClipAtPoint(restrictedSound, Vector3.zero);
    }
    #endregion

    #region Utility
    private bool ValidateInventoryManager()
    {
        if (inventoryManager == null)
        {
            DebugLog("InventoryManager not available");
            return false;
        }
        return true;
    }

    private bool IsValidSlotNumber(int slotNumber) => slotNumber >= 1 && slotNumber <= 10;

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[EquippedItemManager] {message}");
    }
    #endregion

    #region Cleanup
    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.OnManagersRefreshed -= FindSystemReferences;

        if (InputManager.Instance != null)
        {
            InputManager.OnInputManagerReady -= OnInputManagerReady;
            InputManager.Instance.OnScrollWheelInput -= HandleScrollInput;
            InputManager.Instance.OnHotkeyPressed -= OnHotkeyPressed;
        }

        if (inventoryManager != null)
        {
            inventoryManager.OnItemRemoved -= OnInventoryItemRemoved;
            inventoryManager.OnItemAdded -= OnInventoryItemAdded;
        }

        if (playerStateManager != null)
            playerStateManager.OnStateChanged -= OnPlayerStateChanged;
    }
    #endregion
}