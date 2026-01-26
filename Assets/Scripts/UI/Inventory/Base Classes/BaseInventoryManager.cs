using System;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Abstract base class for all inventory managers in the game.
/// Now works with ItemInstance for proper per-item state management.
/// Supports precise item placement with specific positions and rotations,
/// enabling proper preview-to-placement functionality for drag and drop operations.
/// </summary>
public abstract class BaseInventoryManager : MonoBehaviour
{
    [Header("Grid Configuration")]
    [SerializeField] protected int gridWidth = 10;
    [SerializeField] protected int gridHeight = 10;

    [Header("Debug Settings")]
    public bool enableDebugLogs = false;

    // Core inventory data
    protected InventoryGridData inventoryGridData;
    protected int nextItemId = 1;

    // Properties
    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;
    public InventoryGridData InventoryGridData => inventoryGridData;
    public int NextItemId { get => nextItemId; set => nextItemId = value; }

    // Events for UI synchronization
    public event Action<InventoryItemData> OnItemAdded;
    public event Action<string> OnItemRemoved;
    public event Action OnInventoryCleared;
    public event Action<InventoryGridData> OnInventoryDataChanged;

    #region Lifecycle

    protected virtual void Awake()
    {
        InitializeInventory();
    }

    /// <summary>
    /// Initialize the inventory system. Can be overridden by derived classes.
    /// </summary>
    protected virtual void InitializeInventory()
    {
        inventoryGridData = new InventoryGridData(gridWidth, gridHeight);
        DebugLog($"Initialized {GetType().Name} with {gridWidth}x{gridHeight} grid");
    }

    #endregion

    #region Core Item Management

    /// <summary>
    /// ENHANCED: Adds an item to the inventory with full control over position and rotation.
    /// Creates a new ItemInstance with unique state for this item.
    /// This is the primary method that supports drag-and-drop preview functionality.
    /// </summary>
    public virtual bool AddItem(ItemData itemData, Vector2Int? position = null, int? rotation = null)
    {
        if (itemData == null)
        {
            DebugLogError("Cannot add item - ItemData is null");
            return false;
        }

        // Allow derived classes to validate before adding
        if (!CanAddItem(itemData))
        {
            DebugLog($"Cannot add item {itemData.itemName} - validation failed");
            return false;
        }

        // Create a new ItemInstance with unique state
        string itemId = GenerateItemId();
        var itemInstance = new ItemInstance(itemData);
        var inventoryItem = new InventoryItemData(itemId, itemInstance, Vector2Int.zero);

        // Apply rotation first if specified
        if (rotation.HasValue)
        {
            inventoryItem.SetRotation(rotation.Value);
            DebugLog($"Applied rotation {rotation.Value} to item {itemData.itemName}");
        }

        // Try to place at specified position, or find a valid position
        Vector2Int? targetPosition = position;
        if (targetPosition == null || !inventoryGridData.IsValidPosition(targetPosition.Value, inventoryItem))
        {
            if (position.HasValue)
            {
                DebugLog($"Specified position {position.Value} invalid for {itemData.itemName}, searching for alternative");
            }
            targetPosition = inventoryGridData.FindValidPositionForItem(inventoryItem);
        }

        if (targetPosition == null)
        {
            DebugLog($"Cannot add item {itemData.itemName} - no valid position found");
            return false;
        }

        inventoryItem.SetGridPosition(targetPosition.Value);

        if (inventoryGridData.PlaceItem(inventoryItem))
        {
            nextItemId++;
            DebugLog($"Added item {itemData.itemName} at position {targetPosition.Value} with rotation {inventoryItem.currentRotation} (Instance: {itemInstance.InstanceID})");

            // Notify derived classes
            OnItemAddedInternal(inventoryItem);

            // Fire events
            OnItemAdded?.Invoke(inventoryItem);
            OnInventoryDataChanged?.Invoke(inventoryGridData);

            return true;
        }

        DebugLogError($"Failed to place item {itemData.itemName} in inventory data");
        return false;
    }

    /// <summary>
    /// ENHANCED: Add an item with a pre-created InventoryItemData.
    /// Useful for transferring items between inventories while preserving all state.
    /// Creates a NEW ItemInstance (copy) to ensure independence between inventories.
    /// </summary>
    public virtual bool AddItem(InventoryItemData existingItem, Vector2Int? position = null, int? rotation = null)
    {
        if (existingItem?.ItemData == null)
        {
            DebugLogError("Cannot add item - InventoryItemData or ItemData is null");
            return false;
        }

        // Allow derived classes to validate before adding
        if (!CanAddItem(existingItem.ItemData))
        {
            DebugLog($"Cannot add item {existingItem.ItemData.itemName} - validation failed");
            return false;
        }

        // Create a NEW ItemInstance (copy) to ensure this inventory has independent state
        string newItemId = GenerateItemId();
        var newItemInstance = existingItem.ItemInstance.CreateCopy();
        var inventoryItem = new InventoryItemData(newItemId, newItemInstance, Vector2Int.zero);

        // Preserve or override rotation
        int targetRotation = rotation ?? existingItem.currentRotation;
        inventoryItem.SetRotation(targetRotation);

        // Try to place at specified position, or find a valid position
        Vector2Int? targetPosition = position;
        if (targetPosition == null || !inventoryGridData.IsValidPosition(targetPosition.Value, inventoryItem))
        {
            if (position.HasValue)
            {
                DebugLog($"Specified position {position.Value} invalid for {existingItem.ItemData.itemName}, searching for alternative");
            }
            targetPosition = inventoryGridData.FindValidPositionForItem(inventoryItem);
        }

        if (targetPosition == null)
        {
            DebugLog($"Cannot add item {existingItem.ItemData.itemName} - no valid position found");
            return false;
        }

        inventoryItem.SetGridPosition(targetPosition.Value);

        if (inventoryGridData.PlaceItem(inventoryItem))
        {
            nextItemId++;
            DebugLog($"Added existing item {existingItem.ItemData.itemName} at position {targetPosition.Value} with rotation {inventoryItem.currentRotation} (New Instance: {newItemInstance.InstanceID})");

            // Notify derived classes
            OnItemAddedInternal(inventoryItem);

            // Fire events
            OnItemAdded?.Invoke(inventoryItem);
            OnInventoryDataChanged?.Invoke(inventoryGridData);

            return true;
        }

        DebugLogError($"Failed to place existing item {existingItem.ItemData.itemName} in inventory data");
        return false;
    }

    /// <summary>
    /// NEW: Try to add an item at a specific position and rotation, returning detailed result.
    /// Creates a new ItemInstance for the item.
    /// This method provides feedback about why placement failed.
    /// </summary>
    public virtual ItemPlacementResult TryAddItemAt(ItemData itemData, Vector2Int position, int rotation = 0)
    {
        if (itemData == null)
        {
            return new ItemPlacementResult(false, "ItemData is null", Vector2Int.zero, 0);
        }

        if (!CanAddItem(itemData))
        {
            return new ItemPlacementResult(false, "Item validation failed", position, rotation);
        }

        string itemId = GenerateItemId();
        var itemInstance = new ItemInstance(itemData);
        var inventoryItem = new InventoryItemData(itemId, itemInstance, position);
        inventoryItem.SetRotation(rotation);

        if (!inventoryGridData.IsValidPosition(position, inventoryItem))
        {
            return new ItemPlacementResult(false, "Position is invalid or occupied", position, rotation);
        }

        if (inventoryGridData.PlaceItem(inventoryItem))
        {
            nextItemId++;
            DebugLog($"Successfully placed item {itemData.itemName} at {position} with rotation {rotation} (Instance: {itemInstance.InstanceID})");

            // Notify derived classes
            OnItemAddedInternal(inventoryItem);

            // Fire events
            OnItemAdded?.Invoke(inventoryItem);
            OnInventoryDataChanged?.Invoke(inventoryGridData);

            return new ItemPlacementResult(true, "Item placed successfully", position, rotation, inventoryItem);
        }

        return new ItemPlacementResult(false, "Failed to place item in grid", position, rotation);
    }

    /// <summary>
    /// Removes an item from the inventory by its unique ID.
    /// </summary>
    public virtual bool RemoveItem(string itemId)
    {
        DebugLog($"RemoveItem called for: {itemId}");

        var item = inventoryGridData.GetItem(itemId);
        if (item == null)
        {
            DebugLogWarning($"RemoveItem failed: Item {itemId} not found in inventory");
            return false;
        }

        // Allow derived classes to validate before removing
        if (!CanRemoveItem(item))
        {
            DebugLog($"Cannot remove item {itemId} - validation failed");
            return false;
        }

        bool success = inventoryGridData.RemoveItem(itemId);

        if (success)
        {
            DebugLog($"Successfully removed {itemId} (Instance: {item.ItemInstance?.InstanceID}) from inventory grid");

            // Notify derived classes
            OnItemRemovedInternal(item);

            // Fire events
            OnItemRemoved?.Invoke(itemId);
            OnInventoryDataChanged?.Invoke(inventoryGridData);

            return true;
        }

        DebugLogError($"Failed to remove {itemId} from inventory grid");
        return false;
    }

    public virtual string GetItemIDByItemData(ItemData itemData)
    {
        foreach (InventoryItemData item in inventoryGridData.GetAllItems())
        {
            if (item.ItemData == itemData)
            {
                return item.ID;
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// Moves an item to a new grid position with collision validation.
    /// </summary>
    public virtual bool MoveItem(string itemId, Vector2Int newPosition)
    {
        var item = inventoryGridData.GetItem(itemId);
        if (item == null)
        {
            DebugLog($"MoveItem failed: Item {itemId} not found");
            return false;
        }

        // Allow derived classes to validate the move
        if (!CanMoveItem(item, newPosition))
        {
            DebugLog($"Cannot move item {itemId} to {newPosition} - validation failed");
            return false;
        }

        var originalPosition = item.GridPosition;

        // Temporarily remove for collision testing
        inventoryGridData.RemoveItem(itemId);
        item.SetGridPosition(newPosition);

        if (inventoryGridData.IsValidPosition(newPosition, item))
        {
            inventoryGridData.PlaceItem(item);
            DebugLog($"Moved item {itemId} from {originalPosition} to {newPosition}");
            OnInventoryDataChanged?.Invoke(inventoryGridData);
            return true;
        }
        else
        {
            // Restore to original position
            item.SetGridPosition(originalPosition);
            inventoryGridData.PlaceItem(item);
            DebugLog($"Move failed - restored item {itemId} to original position");
            return false;
        }
    }

    /// <summary>
    /// Rotates an item clockwise with proper grid state management.
    /// </summary>
    public virtual bool RotateItem(string itemId)
    {
        var item = inventoryGridData.GetItem(itemId);
        if (item == null)
        {
            DebugLog($"RotateItem failed: Item {itemId} not found");
            return false;
        }

        if (!item.CanRotate)
        {
            DebugLog($"Cannot rotate item {itemId} - item is not rotatable");
            return false;
        }

        // Allow derived classes to validate the rotation
        if (!CanRotateItem(item))
        {
            DebugLog($"Cannot rotate item {itemId} - validation failed");
            return false;
        }

        var originalRotation = item.currentRotation;
        var originalPosition = item.GridPosition;

        // Calculate next rotation
        int maxRotations = TetrominoDefinitions.GetRotationCount(item.shapeType);
        int newRotation = (originalRotation + 1) % maxRotations;

        // Remove from grid before testing rotation
        bool wasInGrid = inventoryGridData.GetItem(itemId) != null;
        if (wasInGrid)
        {
            inventoryGridData.RemoveItem(itemId);
        }

        // Apply new rotation and test
        item.SetRotation(newRotation);

        if (inventoryGridData.IsValidPosition(originalPosition, item))
        {
            if (inventoryGridData.PlaceItem(item))
            {
                DebugLog($"Rotated item {itemId} from {originalRotation} to {newRotation}");
                OnInventoryDataChanged?.Invoke(inventoryGridData);
                return true;
            }
            else
            {
                // Failed to place - revert and restore
                item.SetRotation(originalRotation);
                inventoryGridData.PlaceItem(item);
                DebugLogWarning($"Failed to place after rotation - reverted item {itemId}");
                return false;
            }
        }
        else
        {
            // New rotation invalid - revert and restore
            item.SetRotation(originalRotation);
            if (wasInGrid)
            {
                inventoryGridData.PlaceItem(item);
            }
            DebugLog($"New rotation invalid for item {itemId} - reverted");
            return false;
        }
    }

    /// <summary>
    /// Clears all items from the inventory.
    /// </summary>
    public virtual void ClearInventory()
    {
        DebugLog("Clearing inventory");

        inventoryGridData.Clear();
        nextItemId = 1;

        // Notify derived classes
        OnInventoryClearedInternal();

        // Fire events
        OnInventoryCleared?.Invoke();
        OnInventoryDataChanged?.Invoke(inventoryGridData);
    }

    [Button]
    /// <summary>
    /// Checks if the inventory has the specified item.
    /// </summary>
    /// <param name="itemData"></param>
    /// <returns></returns>
    public virtual bool HasItem(ItemData itemData, int amount = 1)
    {
        bool hasItem = inventoryGridData.HasItem(itemData);

        DebugLog($"HasItem({itemData.itemName}): {hasItem}");

        return hasItem;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Checks if the inventory has space for a new item.
    /// </summary>
    public virtual bool HasSpaceForItem(ItemData itemData)
    {
        if (itemData == null) return false;

        var tempInstance = new ItemInstance(itemData);
        var tempItem = new InventoryItemData($"temp_{nextItemId}", tempInstance, Vector2Int.zero);
        return inventoryGridData.FindValidPositionForItem(tempItem) != null;
    }

    /// <summary>
    /// NEW: Checks if the inventory has space for an item at a specific position and rotation.
    /// </summary>
    public virtual bool HasSpaceForItemAt(ItemData itemData, Vector2Int position, int rotation = 0)
    {
        if (itemData == null) return false;

        var tempInstance = new ItemInstance(itemData);
        var tempItem = new InventoryItemData($"temp_{nextItemId}", tempInstance, position);
        tempItem.SetRotation(rotation);
        return inventoryGridData.IsValidPosition(position, tempItem);
    }

    /// <summary>
    /// NEW: Find the best position for an item with a specific rotation.
    /// </summary>
    public virtual Vector2Int? FindValidPositionForItem(ItemData itemData, int rotation = 0)
    {
        if (itemData == null) return null;

        var tempInstance = new ItemInstance(itemData);
        var tempItem = new InventoryItemData($"temp_{nextItemId}", tempInstance, Vector2Int.zero);
        tempItem.SetRotation(rotation);
        return inventoryGridData.FindValidPositionForItem(tempItem);
    }

    /// <summary>
    /// Returns inventory statistics for UI display and debugging.
    /// </summary>
    public virtual (int itemCount, int occupiedCells, int totalCells) GetInventoryStats()
    {
        int occupiedCells = 0;
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (inventoryGridData.IsOccupied(x, y))
                    occupiedCells++;
            }
        }

        return (inventoryGridData.ItemCount, occupiedCells, gridWidth * gridHeight);
    }

    /// <summary>
    /// Sets complete inventory data. Used by save/load systems.
    /// </summary>
    public virtual void SetInventoryData(InventoryGridData newData, int newNextItemId)
    {
        inventoryGridData = newData ?? new InventoryGridData(gridWidth, gridHeight);
        nextItemId = newNextItemId;

        DebugLog($"Inventory data set: {inventoryGridData.ItemCount} items");

        // Notify derived classes
        OnInventoryDataSetInternal();

        // Trigger events for UI updates
        TriggerOnInventoryDataChanged(inventoryGridData);
        var allItems = inventoryGridData.GetAllItems();
        foreach (var item in allItems)
        {
            TriggerOnItemAdded(item);
        }
    }

    protected virtual void TriggerOnInventoryDataChanged(InventoryGridData inventoryGridData)
    {
        OnInventoryDataChanged?.Invoke(inventoryGridData);
    }

    protected virtual void TriggerOnItemAdded(InventoryItemData item)
    {
        OnItemAdded?.Invoke(item);
    }

    /// <summary>
    /// Protected method for derived classes to trigger OnItemRemoved event.
    /// </summary>
    protected virtual void TriggerOnItemRemoved(string itemId)
    {
        OnItemRemoved?.Invoke(itemId);
    }

    #endregion

    #region Abstract and Virtual Methods for Derived Classes

    /// <summary>
    /// Generate a unique item ID. Can be overridden by derived classes.
    /// </summary>
    protected virtual string GenerateItemId()
    {
        return $"item_{nextItemId}";
    }

    /// <summary>
    /// Validate if an item can be added to this inventory. Override for type-specific rules.
    /// </summary>
    protected virtual bool CanAddItem(ItemData itemData)
    {
        return itemData != null;
    }

    /// <summary>
    /// Validate if an item can be removed from this inventory. Override for type-specific rules.
    /// </summary>
    protected virtual bool CanRemoveItem(InventoryItemData item)
    {
        return item != null;
    }

    /// <summary>
    /// Validate if an item can be moved to a new position. Override for type-specific rules.
    /// </summary>
    protected virtual bool CanMoveItem(InventoryItemData item, Vector2Int newPosition)
    {
        return item != null;
    }

    /// <summary>
    /// Validate if an item can be rotated. Override for type-specific rules.
    /// </summary>
    protected virtual bool CanRotateItem(InventoryItemData item)
    {
        return item != null;
    }

    /// <summary>
    /// Called after an item is successfully added. Override for type-specific logic.
    /// </summary>
    protected virtual void OnItemAddedInternal(InventoryItemData item) { }

    /// <summary>
    /// Called after an item is successfully removed. Override for type-specific logic.
    /// </summary>
    protected virtual void OnItemRemovedInternal(InventoryItemData item) { }

    /// <summary>
    /// Called after inventory is cleared. Override for type-specific logic.
    /// </summary>
    protected virtual void OnInventoryClearedInternal() { }

    /// <summary>
    /// Called after inventory data is set. Override for type-specific logic.
    /// </summary>
    protected virtual void OnInventoryDataSetInternal() { }

    #endregion

    #region Debug Helpers

    protected void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[{GetType().Name}] {message}");
        }
    }

    protected void DebugLogWarning(string message)
    {
        if (enableDebugLogs)
        {
            Debug.LogWarning($"[{GetType().Name}] {message}");
        }
    }

    protected void DebugLogError(string message)
    {
        Debug.LogError($"[{GetType().Name}] {message}");
    }

    [Button("Debug Grid State")]
    protected virtual void DebugGridState()
    {
        Debug.Log($"=== {GetType().Name} DEBUG INFO ===");
        for (int y = 0; y < gridHeight; y++)
        {
            string row = $"Row {y}: ";
            for (int x = 0; x < gridWidth; x++)
            {
                var item = inventoryGridData.GetItemAt(x, y);
                row += (item != null ? "X" : ".") + " ";
            }
            Debug.Log(row);
        }

        Debug.Log($"Total items: {inventoryGridData.ItemCount}");
        foreach (var item in inventoryGridData.GetAllItems())
        {
            Debug.Log($"Item {item.ID}: {item.ItemData?.itemName} at {item.GridPosition} rotation {item.currentRotation} (Instance: {item.ItemInstance?.InstanceID})");
        }
    }

    #endregion
}