using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// REFACTORED: Player-specific inventory manager with ItemInstance support
/// Now properly passes ItemInstance to drop system to preserve state
/// </summary>
public class PlayerInventoryManager : BaseInventoryManager, IManagerState
{
    public static PlayerInventoryManager Instance { get; private set; }

    [Header("Player Inventory Settings")]
    [SerializeField] private List<ItemData> testItems = new List<ItemData>();
    [SerializeField] private ItemData testItemToAdd;

    [Header("Drop System Integration")]
    [SerializeField] private bool validateDropsBeforeRemoval = true;

    [Header("Debug Controls")]
    [SerializeField] private KeyCode addItemKey = KeyCode.N;
    [SerializeField] private KeyCode clearInventoryKey = KeyCode.P;

    [Header("Audio")]
    [SerializeField] private AudioClip[] dropItemSounds;
    [SerializeField] private AudioClip[] pickUpItemSounds;

    // Enhanced events for drop validation feedback
    public event Action<string, string> OnDropValidationFailed; // itemId, reason
    public event Action<string> OnDropValidationSucceeded; // itemId

    public event Action OnPlayerInventoryLoaded;

    private ManagerOperationalState operationalState = ManagerOperationalState.Gameplay;
    public ManagerOperationalState CurrentOperationalState => operationalState;

    [Header("Inventory Debug Mode")]
    [SerializeField] private bool allowAddingItemsViaDebugInput = false;

    #region Singleton Management

    protected override void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            base.Awake();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #endregion

    #region Update Loop (Debug Controls)

    private void Update()
    {
        if (allowAddingItemsViaDebugInput)
            HandleDebugInput();
    }

    private void HandleDebugInput()
    {
        if (Input.GetKeyDown(addItemKey) && testItems.Count > 0)
        {
            ItemData randomItem = testItems[UnityEngine.Random.Range(0, testItems.Count)];
            AddItem(randomItem, null, null);
        }

        if (Input.GetKeyDown(clearInventoryKey))
        {
            ClearInventory();
        }
    }

    #endregion

    #region Player-Specific Overrides

    protected override string GenerateItemId()
    {
        return $"player_item_{nextItemId}";
    }

    protected override bool CanAddItem(ItemData itemData)
    {
        if (!base.CanAddItem(itemData))
            return false;

        return true;
    }

    protected override bool CanRemoveItem(InventoryItemData item)
    {
        if (!base.CanRemoveItem(item))
            return false;

        return true;
    }

    protected override void OnItemAddedInternal(InventoryItemData item)
    {
        base.OnItemAddedInternal(item);
        DebugLog($"Player inventory: Added {item.ItemData?.itemName} at {item.GridPosition} with rotation {item.currentRotation} (Instance: {item.ItemInstance?.InstanceID})");
    }

    protected override void OnItemRemovedInternal(InventoryItemData item)
    {
        base.OnItemRemovedInternal(item);
        DebugLog($"Player inventory: Removed {item.ItemData?.itemName} (Instance: {item.ItemInstance?.InstanceID})");
    }

    #endregion

    #region Enhanced Player Inventory Methods

    protected override void OnInventoryDataSetInternal()
    {
        base.OnInventoryDataSetInternal();
        DebugLog("Player inventory data set/loaded");
        OnPlayerInventoryLoaded?.Invoke();
    }

    public override bool AddItem(ItemData itemData, Vector2Int? position = null, int? rotation = null)
    {
        string positionStr = position?.ToString() ?? "auto";
        string rotationStr = rotation?.ToString() ?? "auto";
        DebugLog($"AddItem called: {itemData?.itemName}, position={positionStr}, rotation={rotationStr}");

        bool result = base.AddItem(itemData, position, rotation);

        if (result)
        {
            DebugLog($"Successfully added {itemData?.itemName} to player inventory");
        }
        else
        {
            DebugLog($"Failed to add {itemData?.itemName} to player inventory");
        }

        return result;
    }

    public override bool AddItem(InventoryItemData existingItem, Vector2Int? position = null, int? rotation = null)
    {
        string positionStr = position?.ToString() ?? "auto";
        string rotationStr = rotation?.ToString() ?? "auto";
        DebugLog($"AddItem (existing) called: {existingItem?.ItemData?.itemName}, position={positionStr}, rotation={rotationStr}");

        bool result = base.AddItem(existingItem, position, rotation);

        if (result)
        {
            DebugLog($"Successfully added existing item {existingItem?.ItemData?.itemName} to player inventory");

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound2D(pickUpItemSounds[UnityEngine.Random.Range(0, pickUpItemSounds.Length)], AudioCategory.PlayerSFX);
        }
        else
        {
            DebugLog($"Failed to add existing item {existingItem?.ItemData?.itemName} to player inventory");
        }

        return result;
    }

    /// <summary>
    /// NEW: Add ItemInstance directly (for pickup from world with preserved state)
    /// This is the method ItemPickupInteractable should call
    /// </summary>
    public bool AddItem(ItemInstance itemInstance, Vector2Int? position = null, int? rotation = null)
    {
        if (itemInstance?.ItemData == null)
        {
            DebugLogError("Cannot add item - ItemInstance or ItemData is null");
            return false;
        }

        DebugLog($"AddItem (ItemInstance) called: {itemInstance.ItemData.itemName}, InstanceID={itemInstance.InstanceID}");

        // Create InventoryItemData with the EXISTING ItemInstance (not a copy!)
        string itemId = GenerateItemId();
        var inventoryItem = new InventoryItemData(itemId, itemInstance, Vector2Int.zero);

        // Apply rotation if specified
        if (rotation.HasValue)
        {
            inventoryItem.SetRotation(rotation.Value);
        }

        // Find valid position
        Vector2Int? targetPosition = position;
        if (targetPosition == null || !inventoryGridData.IsValidPosition(targetPosition.Value, inventoryItem))
        {
            targetPosition = inventoryGridData.FindValidPositionForItem(inventoryItem);
        }

        if (targetPosition == null)
        {
            DebugLog($"Cannot add item {itemInstance.ItemData.itemName} - no valid position found");
            return false;
        }

        inventoryItem.SetGridPosition(targetPosition.Value);

        if (inventoryGridData.PlaceItem(inventoryItem))
        {
            nextItemId++;
            DebugLog($"Added ItemInstance {itemInstance.ItemData.itemName} at {targetPosition.Value} (InstanceID: {itemInstance.InstanceID})");

            OnItemAddedInternal(inventoryItem);
            TriggerOnItemAdded(inventoryItem);
            TriggerOnInventoryDataChanged(inventoryGridData);

            return true;
        }

        DebugLogError($"Failed to place ItemInstance {itemInstance.ItemData.itemName}");
        return false;
    }

    public override ItemPlacementResult TryAddItemAt(ItemData itemData, Vector2Int position, int rotation = 0)
    {
        DebugLog($"TryAddItemAt called: {itemData?.itemName} at {position} with rotation {rotation}");

        var result = base.TryAddItemAt(itemData, position, rotation);

        DebugLog($"TryAddItemAt result: {result.success} - {result.message}");

        return result;
    }

    #endregion

    #region REFACTORED: Drop System Integration with ItemInstance

    /// <summary>
    /// REFACTORED: Attempts to drop an item with ItemInstance preservation
    /// Now passes the actual ItemInstance to maintain state through drop/pickup cycle
    /// </summary>
    public DropAttemptResult TryDropItem(string itemId, Vector3? customDropPosition = null)
    {
        DebugLog($"=== TryDropItem called for: {itemId} ===");

        // Step 1: Validate item exists
        var item = inventoryGridData.GetItem(itemId);
        if (item?.ItemData == null || item.ItemInstance == null)
        {
            var result = new DropAttemptResult(false, itemId, "Item not found in inventory", DropFailureReason.ItemNotFound);
            DebugLog($"Drop failed: {result.reason}");
            OnDropValidationFailed?.Invoke(itemId, result.reason);
            return result;
        }

        ItemData itemData = item.ItemData;
        ItemInstance itemInstance = item.ItemInstance;
        DebugLog($"Found item {itemData.itemName} in inventory (Instance: {itemInstance.InstanceID})");

        // Step 2: Check if item can be dropped
        if (!itemData.CanDrop)
        {
            var result = new DropAttemptResult(false, itemId, $"Item {itemData.itemName} cannot be dropped (KeyItem)", DropFailureReason.ItemNotDroppable);
            DebugLog($"Drop failed: {result.reason}");
            OnDropValidationFailed?.Invoke(itemId, result.reason);
            return result;
        }

        // Step 3: Check visual prefab
        if (!itemData.HasVisualPrefab)
        {
            var result = new DropAttemptResult(false, itemId, $"Item {itemData.itemName} has no visual prefab", DropFailureReason.NoVisualPrefab);
            DebugLog($"Drop failed: {result.reason}");
            OnDropValidationFailed?.Invoke(itemId, result.reason);
            return result;
        }

        // Step 4: Validate drop position if enabled
        if (validateDropsBeforeRemoval)
        {
            var dropValidation = ValidateWithDropSystem(itemData, customDropPosition);
            if (!dropValidation.isValid)
            {
                var result = new DropAttemptResult(false, itemId, $"Drop position invalid: {dropValidation.reason}", DropFailureReason.InvalidDropPosition);
                DebugLog($"Drop validation failed: {result.reason}");
                OnDropValidationFailed?.Invoke(itemId, result.reason);
                return result;
            }
        }

        // Step 5: All validations passed - proceed with drop
        DebugLog($"All validations passed for {itemData.itemName} - proceeding with drop");

        // CRITICAL: Backup ItemInstance for restoration (not just data!)
        var itemBackup = new InventoryItemBackup(item);

        // Remove from inventory
        if (!RemoveItem(itemId))
        {
            var result = new DropAttemptResult(false, itemId, "Failed to remove item from inventory", DropFailureReason.InventoryRemovalFailed);
            DebugLog($"Drop failed: {result.reason}");
            OnDropValidationFailed?.Invoke(itemId, result.reason);
            return result;
        }

        // REFACTORED: Drop ItemInstance (not ItemData!) to preserve state
        bool dropSuccess = AttemptWorldDrop(itemInstance, customDropPosition);

        if (dropSuccess)
        {
            var result = new DropAttemptResult(true, itemId, $"Successfully dropped {itemData.itemName}", DropFailureReason.None);
            DebugLog($"Drop succeeded: {result.reason}");
            OnDropValidationSucceeded?.Invoke(itemId);

            AudioManager.Instance.PlaySound2D(dropItemSounds[UnityEngine.Random.Range(0, dropItemSounds.Length)], AudioCategory.PlayerSFX);

            return result;
        }
        else
        {
            // Drop failed - restore item with original ItemInstance
            DebugLog($"CRITICAL: World drop failed for {itemData.itemName} after inventory removal - attempting restoration");

            bool restoreSuccess = AttemptItemRestore(itemBackup);

            if (restoreSuccess)
            {
                var result = new DropAttemptResult(false, itemId, $"Drop failed but item restored to inventory", DropFailureReason.DropFailedButRestored);
                DebugLog($"Item restoration successful: {result.reason}");
                OnDropValidationFailed?.Invoke(itemId, result.reason);
                return result;
            }
            else
            {
                var result = new DropAttemptResult(false, itemId, $"CRITICAL: Drop failed and restoration failed - item lost!", DropFailureReason.DropFailedItemLost);
                DebugLogError($"CRITICAL FAILURE: {result.reason}");
                OnDropValidationFailed?.Invoke(itemId, result.reason);
                return result;
            }
        }
    }

    private DropValidationResult ValidateWithDropSystem(ItemData itemData, Vector3? customPosition)
    {
        if (ItemDropSystem.Instance == null)
        {
            DebugLog("ItemDropSystem not found - skipping drop validation");
            return new DropValidationResult(true, Vector3.zero, "No drop system to validate with");
        }

        Vector3 targetPosition = customPosition ?? GetPlayerDropPosition();

        var dropValidator = ItemDropSystem.Instance.GetComponent<ItemDropValidator>();
        if (dropValidator == null)
        {
            DebugLog("ItemDropValidator not found - assuming drop is valid");
            return new DropValidationResult(true, targetPosition, "No validator available");
        }

        return dropValidator.ValidateDropPosition(targetPosition, itemData, true);
    }

    /// <summary>
    /// REFACTORED: Drops ItemInstance (not ItemData!) to preserve state
    /// </summary>
    private bool AttemptWorldDrop(ItemInstance itemInstance, Vector3? customPosition)
    {
        if (ItemDropSystem.Instance == null)
        {
            DebugLog("ItemDropSystem not found - cannot drop item");
            return false;
        }

        // Pass ItemInstance to preserve state!
        return ItemDropSystem.Instance.DropItem(itemInstance, customPosition);
    }

    /// <summary>
    /// REFACTORED: Restores item with original ItemInstance (no copy!)
    /// </summary>
    private bool AttemptItemRestore(InventoryItemBackup backup)
    {
        if (backup == null)
        {
            DebugLogError("Cannot restore item - backup is null");
            return false;
        }

        DebugLog($"Attempting to restore {backup.itemData.itemName} to position {backup.gridPosition} (Instance: {backup.itemInstance.InstanceID})");

        // Try original position with ORIGINAL ItemInstance
        if (TryRestoreToPosition(backup.itemInstance, backup.gridPosition, backup.rotation))
        {
            DebugLog("Item restored to original position with original instance state");
            return true;
        }

        // Find alternative position
        DebugLog("Original position occupied - searching for alternative position");

        string tempId = $"restore_{nextItemId}";
        var tempItem = new InventoryItemData(tempId, backup.itemInstance, Vector2Int.zero);
        tempItem.SetRotation(backup.rotation);

        var validPosition = inventoryGridData.FindValidPositionForItem(tempItem);
        if (validPosition.HasValue)
        {
            if (TryRestoreToPosition(backup.itemInstance, validPosition.Value, backup.rotation))
            {
                DebugLog($"Item restored to alternative position: {validPosition.Value}");
                return true;
            }
        }

        // Try different rotations if rotatable
        if (backup.itemData.isRotatable)
        {
            DebugLog("Trying different rotations for restoration");

            for (int rotation = 0; rotation < 4; rotation++)
            {
                tempItem.SetRotation(rotation);
                validPosition = inventoryGridData.FindValidPositionForItem(tempItem);
                if (validPosition.HasValue)
                {
                    if (TryRestoreToPosition(backup.itemInstance, validPosition.Value, rotation))
                    {
                        DebugLog($"Item restored with rotation {rotation} at position {validPosition.Value}");
                        return true;
                    }
                }
            }
        }

        DebugLogError("All restoration attempts failed");
        return false;
    }

    /// <summary>
    /// REFACTORED: Restores item with ORIGINAL ItemInstance (not a copy!)
    /// This is critical - we must use the same instance to preserve state
    /// </summary>
    private bool TryRestoreToPosition(ItemInstance itemInstance, Vector2Int position, int rotation)
    {
        if (itemInstance?.ItemData == null)
        {
            DebugLogError("Cannot restore - ItemInstance or ItemData is null");
            return false;
        }

        DebugLog($"Trying to restore {itemInstance.ItemData.itemName} to position {position} (Instance: {itemInstance.InstanceID})");

        // Create InventoryItemData with the SAME ItemInstance (not a copy!)
        string newItemId = GenerateItemId();
        var inventoryItem = new InventoryItemData(newItemId, itemInstance, position);
        inventoryItem.SetRotation(rotation);

        if (!inventoryGridData.IsValidPosition(position, inventoryItem))
        {
            DebugLog($"Position {position} is not valid for restoration");
            return false;
        }

        if (inventoryGridData.PlaceItem(inventoryItem))
        {
            nextItemId++;
            DebugLog($"Successfully restored {itemInstance.ItemData.itemName} to position {position} (Instance: {itemInstance.InstanceID})");

            OnItemAddedInternal(inventoryItem);
            TriggerOnItemAdded(inventoryItem);
            TriggerOnInventoryDataChanged(inventoryGridData);

            return true;
        }

        DebugLogError($"Failed to place item in grid at position {position}");
        return false;
    }

    private Vector3 GetPlayerDropPosition()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            return player.transform.position + player.transform.forward * 2f + Vector3.up * 0.5f;
        }

        DebugLog("No player found - using world origin");
        return Vector3.zero;
    }

    #endregion

    #region Configuration Methods

    public void SetDropValidationEnabled(bool enabled)
    {
        validateDropsBeforeRemoval = enabled;
        DebugLog($"Drop validation {(enabled ? "enabled" : "disabled")}");
    }

    public bool IsDropValidationEnabled => validateDropsBeforeRemoval;

    #endregion

    #region Debug Methods

    [Button("Add Test Item")]
    private void AddTestItem()
    {
        if (testItemToAdd != null)
        {
            AddItem(testItemToAdd, null, null);
        }
        else if (testItems.Count > 0)
        {
            ItemData randomItem = testItems[UnityEngine.Random.Range(0, testItems.Count)];
            AddItem(randomItem, null, null);
        }
        else
        {
            Debug.LogWarning("No test items configured");
        }
    }

    [Button("Add Test Item at Specific Position")]
    private void AddTestItemAtPosition()
    {
        if (testItemToAdd != null)
        {
            Vector2Int testPosition = new Vector2Int(2, 2);
            int testRotation = 1;

            var result = TryAddItemAt(testItemToAdd, testPosition, testRotation);
            Debug.Log($"Test placement result: {result.success} - {result.message}");
        }
        else
        {
            Debug.LogWarning("No test item configured");
        }
    }

    [Button("Clear Inventory")]
    private void DebugClearInventory()
    {
        ClearInventory();
    }

    [Button("Test Drop Validation")]
    private void TestDropValidation()
    {
        if (testItems.Count > 0)
        {
            var testItem = testItems[0];
            var result = ValidateWithDropSystem(testItem, null);
            Debug.Log($"Drop validation for {testItem.itemName}: {result.isValid} - {result.reason}");
        }
        else
        {
            Debug.LogWarning("No test items available for drop validation test");
        }
    }

    [Button("Debug Inventory Stats")]
    private void DebugInventoryStats()
    {
        var stats = GetInventoryStats();
        Debug.Log($"=== PLAYER INVENTORY STATS ===");
        Debug.Log($"Grid Size: {GridWidth}x{GridHeight}");
        Debug.Log($"Total Items: {stats.itemCount}");
        Debug.Log($"Occupied Cells: {stats.occupiedCells}/{stats.totalCells}");
        Debug.Log($"Grid Utilization: {(float)stats.occupiedCells / stats.totalCells * 100:F1}%");
    }

    #endregion

    #region IManagerState Implementation

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
        DebugLog("Entering Menu state - inventory preserved but operations disabled");
        // DON'T clear inventory! Just stop operations
    }

    public void OnEnterGameplayState()
    {
        DebugLog("Entering Gameplay state - inventory operations enabled");
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

    #region Save/Load Integration

    public virtual void OnBeforeSave()
    {
        DebugLog("Preparing player inventory for save");
    }

    public virtual void OnAfterLoad()
    {
        DebugLog("Player inventory load completed");
    }

    #endregion

    #region Helper Classes

    /// <summary>
    /// CRITICAL: Stores ItemInstance reference (not a copy!) for restoration
    /// </summary>
    private class InventoryItemBackup
    {
        public ItemData itemData;
        public ItemInstance itemInstance; // Stores REFERENCE, not copy!
        public Vector2Int gridPosition;
        public int rotation;

        public InventoryItemBackup(InventoryItemData item)
        {
            if (item == null)
            {
                Debug.LogError("Cannot create backup - InventoryItemData is null");
                return;
            }

            itemData = item.ItemData;
            itemInstance = item.ItemInstance; // Reference, not copy!
            gridPosition = item.GridPosition;
            rotation = item.currentRotation;
        }
    }

    #endregion
}

/// <summary>
/// Result structure for drop attempts
/// </summary>
[System.Serializable]
public struct DropAttemptResult
{
    public bool success;
    public string itemId;
    public string reason;
    public DropFailureReason failureReason;

    public DropAttemptResult(bool isSuccess, string id, string message, DropFailureReason failure)
    {
        success = isSuccess;
        itemId = id;
        reason = message;
        failureReason = failure;
    }
}

/// <summary>
/// Categorized failure reasons
/// </summary>
public enum DropFailureReason
{
    None,
    ItemNotFound,
    ItemNotDroppable,
    NoVisualPrefab,
    InvalidDropPosition,
    InventoryRemovalFailed,
    DropFailedButRestored,
    DropFailedItemLost
}