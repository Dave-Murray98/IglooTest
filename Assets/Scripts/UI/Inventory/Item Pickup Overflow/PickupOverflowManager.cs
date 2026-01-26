using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// REFACTORED: Pickup overflow manager using InventoryItemData architecture.
/// Now properly handles ItemInstance state from world pickups through to player transfer.
/// </summary>
public class PickupOverflowManager : BaseInventoryManager
{
    // The single item this overflow inventory holds (contains ItemInstance)
    private InventoryItemData pickupItem;

    // Events specific to pickup overflow
    public System.Action<InventoryItemData> OnPickupItemSet;
    public System.Action OnPickupItemTransferred;
    public System.Action OnOverflowCleared;

    #region Initialization

    protected override void InitializeInventory()
    {
        // Start with 1x1 grid - will be resized when item is set
        gridWidth = 1;
        gridHeight = 1;
        inventoryGridData = new InventoryGridData(gridWidth, gridHeight);

        DebugLog("Pickup overflow manager initialized with 1x1 grid");
    }

    #endregion

    #region Pickup Item Management

    /// <summary>
    /// REFACTORED: Set the pickup item from ItemData (creates NEW ItemInstance).
    /// Use this when you want to create a fresh pickup item.
    /// </summary>
    public bool SetPickupItem(ItemInstance itemInstance)
    {
        if (itemInstance == null)
        {
            DebugLogError("Cannot set null item data for pickup overflow");
            return false;
        }

        // Clear any existing item first
        ClearPickupItem();

        // Create InventoryItemData with NEW ItemInstance
        string itemId = GenerateItemId();
        pickupItem = new InventoryItemData(itemInstance.InstanceID, itemInstance, Vector2Int.zero);

        return FinalizePickupItemSetup(itemInstance.InstanceID);
    }

    /// <summary>
    /// REFACTORED: Set the pickup item from existing InventoryItemData (preserves ItemInstance).
    /// Use this when transferring an item that already has state (e.g., from world pickup).
    /// </summary>
    public bool SetPickupItem(InventoryItemData existingItem)
    {
        if (existingItem?.ItemData == null)
        {
            DebugLogError("Cannot set null item for pickup overflow");
            return false;
        }

        // Clear any existing item first
        ClearPickupItem();

        // Create NEW InventoryItemData with NEW ItemInstance (copy of existing)
        string itemId = GenerateItemId();
        pickupItem = new InventoryItemData(itemId, existingItem.ItemInstance, Vector2Int.zero);

        return FinalizePickupItemSetup(existingItem.ItemData.itemName);
    }

    /// <summary>
    /// Common finalization logic for setting pickup items.
    /// </summary>
    private bool FinalizePickupItemSetup(string itemName)
    {
        // Calculate required grid size for this item's shape
        ResizeGridForItem(pickupItem);

        // Place the item at (0,0) in the resized grid
        pickupItem.SetGridPosition(Vector2Int.zero);

        if (inventoryGridData.PlaceItem(pickupItem))
        {
            nextItemId++;
            DebugLog($"Set pickup item: {itemName} in {gridWidth}x{gridHeight} grid (Instance: {pickupItem.ItemInstance?.InstanceID})");

            // Notify systems
            OnPickupItemSet?.Invoke(pickupItem);
            TriggerOnItemAdded(pickupItem);
            TriggerOnInventoryDataChanged(inventoryGridData);

            return true;
        }
        else
        {
            DebugLogError($"Failed to place pickup item {itemName} in grid");
            pickupItem = null;
            return false;
        }
    }

    /// <summary>
    /// Clear the pickup item and reset the grid.
    /// </summary>
    public void ClearPickupItem()
    {
        if (pickupItem != null)
        {
            DebugLog($"Clearing pickup item: {pickupItem.ItemData?.itemName} (Instance: {pickupItem.ItemInstance?.InstanceID})");

            // Remove from grid
            inventoryGridData.RemoveItem(pickupItem.ID);

            // Fire events
            TriggerOnItemRemoved(pickupItem.ID);

            pickupItem = null;

            OnOverflowCleared?.Invoke();
        }

        // Reset to minimal grid
        ResizeGrid(1, 1);
    }

    /// <summary>
    /// Get the current pickup item (InventoryItemData with ItemInstance).
    /// </summary>
    public InventoryItemData GetPickupItem()
    {
        return pickupItem;
    }

    /// <summary>
    /// Check if there's currently a pickup item.
    /// </summary>
    public bool HasPickupItem()
    {
        return pickupItem != null;
    }

    #endregion

    #region Grid Resizing

    /// <summary>
    /// Resize the grid to perfectly fit the given item.
    /// </summary>
    private void ResizeGridForItem(InventoryItemData item)
    {
        if (item?.ItemData == null)
        {
            DebugLogError("Cannot resize grid for null item");
            return;
        }

        // Get the item's shape data
        var shapeData = item.CurrentShapeData;
        if (shapeData.cells.Length == 0)
        {
            DebugLogWarning("Item has no shape cells - using 1x1 grid");
            ResizeGrid(1, 1);
            return;
        }

        // Calculate bounding box of the shape
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var cell in shapeData.cells)
        {
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        // Calculate required grid size with padding
        int requiredWidth = maxX - minX + 1;
        int requiredHeight = maxY - minY + 1;
        int paddedWidth = requiredWidth + 1;
        int paddedHeight = requiredHeight + 1;

        ResizeGrid(paddedWidth, paddedHeight);

        DebugLog($"Resized grid to {paddedWidth}x{paddedHeight} for item {item.ItemData.itemName}");
    }

    /// <summary>
    /// Resize the inventory grid to new dimensions.
    /// </summary>
    private void ResizeGrid(int newWidth, int newHeight)
    {
        gridWidth = newWidth;
        gridHeight = newHeight;
        inventoryGridData = new InventoryGridData(gridWidth, gridHeight);

        DebugLog($"Grid resized to {gridWidth}x{gridHeight}");
        TriggerOnInventoryDataChanged(inventoryGridData);
    }

    #endregion

    #region Overridden Methods - Restricted Functionality

    protected override string GenerateItemId()
    {
        return $"pickup_overflow_item_{nextItemId}";
    }

    /// <summary>
    /// Restrict direct item addition - use SetPickupItem() instead.
    /// </summary>
    public override bool AddItem(ItemData itemData, Vector2Int? position = null, int? rotation = null)
    {
        DebugLogWarning("Cannot add items directly to pickup overflow. Use SetPickupItem() instead.");
        return false;
    }

    /// <summary>
    /// Restrict existing item addition - use SetPickupItem(InventoryItemData) instead.
    /// </summary>
    public override bool AddItem(InventoryItemData existingItem, Vector2Int? position = null, int? rotation = null)
    {
        DebugLogWarning("Cannot add items directly to pickup overflow. Use SetPickupItem() instead.");
        return false;
    }

    /// <summary>
    /// Allow removal only for transferring the pickup item.
    /// </summary>
    public override bool RemoveItem(string itemId)
    {
        if (pickupItem != null && pickupItem.ID == itemId)
        {
            DebugLog($"Transferring pickup item: {pickupItem.ItemData?.itemName} (Instance: {pickupItem.ItemInstance?.InstanceID})");

            // Store reference before clearing
            var transferredItem = pickupItem;

            // Clear the pickup item
            pickupItem = null;

            // Remove from grid
            bool success = inventoryGridData.RemoveItem(itemId);

            if (success)
            {
                DebugLog("Pickup item successfully transferred");

                // Fire events
                TriggerOnItemRemoved(itemId);
                OnPickupItemTransferred?.Invoke();

                // Reset grid
                ResizeGrid(1, 1);
                TriggerOnInventoryDataChanged(inventoryGridData);

                return true;
            }
            else
            {
                // Restore pickup item if removal failed
                pickupItem = transferredItem;
                DebugLogError("Failed to remove pickup item from grid");
                return false;
            }
        }

        DebugLogWarning($"Cannot remove item {itemId} - not the current pickup item");
        return false;
    }

    /// <summary>
    /// Allow rotation of the pickup item.
    /// </summary>
    public override bool RotateItem(string itemId)
    {
        if (pickupItem != null && pickupItem.ID == itemId)
        {
            bool success = base.RotateItem(itemId);

            if (success)
            {
                // Resize grid after rotation
                ResizeGridForItem(pickupItem);

                // Ensure item is still at (0,0)
                pickupItem.SetGridPosition(Vector2Int.zero);
                inventoryGridData.RemoveItem(itemId);
                inventoryGridData.PlaceItem(pickupItem);

                TriggerOnInventoryDataChanged(inventoryGridData);
            }

            return success;
        }

        return false;
    }

    /// <summary>
    /// Prevent moving the pickup item.
    /// </summary>
    public override bool MoveItem(string itemId, Vector2Int newPosition)
    {
        DebugLog("Cannot move pickup item - it must remain at origin");
        return false;
    }

    /// <summary>
    /// Prevent clearing the inventory directly.
    /// </summary>
    public override void ClearInventory()
    {
        DebugLogWarning("Cannot clear pickup overflow directly. Use ClearPickupItem() instead.");
    }

    /// <summary>
    /// Override space checking - can only hold one item.
    /// </summary>
    public override bool HasSpaceForItem(ItemData itemData)
    {
        return false;
    }

    #endregion

    #region Public Interface

    /// <summary>
    /// Check if the given item is the current pickup item.
    /// </summary>
    public bool IsPickupItem(string itemId)
    {
        return pickupItem != null && pickupItem.ID == itemId;
    }

    /// <summary>
    /// REFACTORED: Try to transfer the pickup item to the player inventory.
    /// Uses InventoryItemData which contains ItemInstance for proper state preservation.
    /// </summary>
    public bool TryTransferToPlayer()
    {
        if (pickupItem == null)
        {
            DebugLogWarning("No pickup item to transfer");
            return false;
        }

        if (PlayerInventoryManager.Instance == null)
        {
            DebugLogError("PlayerInventoryManager not found - cannot transfer");
            return false;
        }

        DebugLog($"Attempting to transfer pickup item to player (Instance: {pickupItem.ItemInstance?.InstanceID})");

        // Check if player has space
        if (!PlayerInventoryManager.Instance.HasSpaceForItem(pickupItem.ItemData))
        {
            DebugLog("Player inventory has no space for pickup item");
            return false;
        }

        // REFACTORED: Pass InventoryItemData to AddItem
        // BaseInventoryManager.AddItem(InventoryItemData) will create a NEW ItemInstance copy
        if (PlayerInventoryManager.Instance.AddItem(pickupItem))
        {
            DebugLog("Successfully transferred pickup item to player inventory");
            // Remove from overflow (gets InventoryItemData with ItemInstance)
            if (RemoveItem(pickupItem.ID))
                return true;
            else
            {
                DebugLogError("Failed to remove pickup item from overflow");
                return false;
            }
        }
        else
        {
            // Failed to add to player - restore to overflow
            DebugLogError("Failed to add pickup item to player inventory - restoring");
            SetPickupItem(pickupItem);
            return false;
        }
    }


}

#endregion
