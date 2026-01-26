// StorageContainerItemDragHandler.cs - REFACTORED
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// REFACTORED: Drag handler for storage container items using InventoryItemData.
/// Now properly preserves ItemInstance state through transfers using existing infrastructure.
/// </summary>
public class StorageContainerItemDragHandler : BaseInventoryDragHandler
{
    [Header("Container Specific")]
    [SerializeField] private bool enableTransferToPlayer = true;
    [SerializeField] private bool enableTransferToOtherContainers = false;

    // Container-specific references
    private StorageContainer containerManager;
    private StorageContainerGridVisual containerGridVisual;

    // Transfer detection
    private bool isDraggedToPlayerInventory = false;
    private BaseInventoryGridVisual playerInventoryGridVisual;

    // Preview state tracking for precise placement
    private Vector2Int lastPreviewPosition;
    private int lastPreviewRotation;
    private bool hasValidPreview = false;

    #region Initialization

    public override void Initialize(InventoryItemData item, BaseInventoryGridVisual visual)
    {
        base.Initialize(item, visual);

        containerGridVisual = visual as StorageContainerGridVisual;

        // Find player inventory visual for transfer detection
        FindPlayerInventoryVisual();
    }

    /// <summary>
    /// Set the container manager reference.
    /// </summary>
    public void SetContainerManager(StorageContainer manager)
    {
        containerManager = manager;
        DebugLog($"Container manager set: {manager?.DisplayName ?? "None"}");
    }

    /// <summary>
    /// Find the player inventory visual for transfer operations.
    /// </summary>
    private void FindPlayerInventoryVisual()
    {
        var playerVisual = FindFirstObjectByType<PlayerInventoryGridVisual>();
        if (playerVisual != null)
        {
            playerInventoryGridVisual = playerVisual;
            DebugLog("Found player inventory visual for transfers");
        }
        else
        {
            DebugLog("Player inventory visual not found - transfers may not work");
        }
    }

    #endregion

    #region Container-Specific Overrides

    /// <summary>
    /// Enhanced drag feedback for container items with transfer detection.
    /// </summary>
    protected override void UpdateDragFeedback(PointerEventData eventData)
    {
        // Check if we're being dragged over the player inventory
        CheckForPlayerInventoryTransfer(eventData);

        if (!isDraggedToPlayerInventory)
        {
            base.UpdateDragFeedback(eventData);
            hasValidPreview = false;
        }
        else
        {
            // Clear container preview since we're over player inventory
            ClearPreview();
            ShowPlayerInventoryTransferPreview();
        }
    }

    /// <summary>
    /// REFACTORED: Enhanced drop handling with transfer support using InventoryItemData.
    /// </summary>
    protected override bool HandleDrop(PointerEventData eventData)
    {
        DebugLog($"HandleDrop() called - isDraggedToPlayerInventory: {isDraggedToPlayerInventory}");

        // Check if we're dropping on player inventory for transfer
        if (isDraggedToPlayerInventory && enableTransferToPlayer)
        {
            return HandleTransferToPlayer();
        }

        // Use default container behavior for other drops
        return false;
    }

    /// <summary>
    /// Container items should generally be draggable.
    /// </summary>
    protected override bool CanBeginDrag(PointerEventData eventData)
    {
        if (!base.CanBeginDrag(eventData))
            return false;

        // Add container-specific validation
        if (containerManager != null && !containerManager.CanPlayerAccess())
        {
            DebugLog("Cannot drag item - player cannot access container");
            return false;
        }

        return true;
    }

    #endregion

    #region Transfer Detection and Handling

    /// <summary>
    /// Check if the item is being dragged over the player inventory.
    /// </summary>
    private void CheckForPlayerInventoryTransfer(PointerEventData eventData)
    {
        if (!enableTransferToPlayer || playerInventoryGridVisual == null)
        {
            isDraggedToPlayerInventory = false;
            hasValidPreview = false;
            return;
        }

        // Check if pointer is over player inventory
        RectTransform playerInventoryRect = playerInventoryGridVisual.GetComponent<RectTransform>();
        if (playerInventoryRect == null)
        {
            isDraggedToPlayerInventory = false;
            hasValidPreview = false;
            return;
        }

        Vector2 localPoint;
        bool isOverPlayerInventory = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            playerInventoryRect,
            eventData.position,
            canvas.worldCamera,
            out localPoint);

        if (isOverPlayerInventory)
        {
            isDraggedToPlayerInventory = playerInventoryRect.rect.Contains(localPoint);
        }
        else
        {
            isDraggedToPlayerInventory = false;
        }

        if (!isDraggedToPlayerInventory)
        {
            hasValidPreview = false;
        }

        // Visual feedback for transfer
        canvasGroup.alpha = isDraggedToPlayerInventory ? 0.9f : 0.8f;
    }

    /// <summary>
    /// Show transfer preview on player inventory and track preview state.
    /// </summary>
    private void ShowPlayerInventoryTransferPreview()
    {
        if (playerInventoryGridVisual == null) return;

        // Get grid position on player inventory
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            playerInventoryGridVisual.GetComponent<RectTransform>(),
            RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rectTransform.position),
            canvas.worldCamera,
            out localPos);

        Vector2Int gridPos = playerInventoryGridVisual.GetGridPosition(localPos);
        int currentRotation = inventoryItemData.currentRotation;

        // Create temporary item for validation
        var tempItem = new InventoryItemData(inventoryItemData.ID + "_transfer_temp", inventoryItemData.ItemData, gridPos);
        tempItem.SetRotation(currentRotation);

        // Check if transfer is valid
        bool isValid = playerInventoryGridVisual.GridData.IsValidPosition(gridPos, tempItem);

        // Show preview on player inventory
        playerInventoryGridVisual.ShowPlacementPreview(gridPos, tempItem, isValid);

        // Store preview state for precise placement
        lastPreviewPosition = gridPos;
        lastPreviewRotation = currentRotation;
        hasValidPreview = isValid;
        wasValidPlacement = isValid;

        DebugLog($"Preview: pos={gridPos}, rot={currentRotation}, valid={isValid}");
    }

    /// <summary>
    /// REFACTORED: Handle transferring item to player inventory using InventoryItemData.
    /// BaseInventoryManager.AddItem(InventoryItemData) automatically creates a NEW ItemInstance copy,
    /// ensuring independence between inventories.
    /// </summary>
    private bool HandleTransferToPlayer()
    {
        if (containerManager == null || PlayerInventoryManager.Instance == null)
        {
            DebugLogError("Cannot transfer - missing references");
            RevertToOriginalState();
            return true;
        }

        DebugLog($"Attempting to transfer {inventoryItemData.ItemData?.itemName} to player inventory");

        // Clear player inventory preview
        if (playerInventoryGridVisual != null)
        {
            playerInventoryGridVisual.ClearPlacementPreview();
        }

        // Check if we have a valid preview position
        if (!hasValidPreview)
        {
            DebugLog("No valid preview position - cannot transfer");
            RevertToOriginalState();
            return true;
        }

        // Restore the item to the container if it was removed during drag
        if (itemRemovedFromGrid)
        {
            DebugLog($"Restoring item {inventoryItemData.ID} to container before transfer");
            inventoryItemData.SetGridPosition(originalGridPosition);
            inventoryItemData.SetRotation(originalRotation);

            if (gridVisual.GridData.PlaceItem(inventoryItemData))
            {
                itemRemovedFromGrid = false;
                DebugLog($"Item {inventoryItemData.ID} restored to container successfully");
            }
            else
            {
                DebugLogError($"Failed to restore item {inventoryItemData.ID} to container!");
                RevertToOriginalState();
                return true;
            }
        }

        // REFACTORED: Use the existing InventoryItemData which contains ItemInstance
        // BaseInventoryManager.AddItem will create a NEW ItemInstance copy automatically
        bool success = PerformPreciseTransfer();

        if (success)
        {
            DebugLog($"Successfully transferred {inventoryItemData.ItemData?.itemName} to player inventory");
            OnItemDeselected?.Invoke();
        }
        else
        {
            DebugLog($"Failed to transfer {inventoryItemData.ItemData?.itemName} - reverting");
            RevertToOriginalState();
        }

        return true;
    }

    /// <summary>
    /// REFACTORED: Perform precise transfer using BaseInventoryManager infrastructure.
    /// This leverages the existing AddItem(InventoryItemData) method which creates
    /// a NEW ItemInstance copy, ensuring proper state isolation.
    /// </summary>
    private bool PerformPreciseTransfer()
    {
        DebugLog($"Performing precise transfer to position {lastPreviewPosition} with rotation {lastPreviewRotation}");

        // Double-check that the target position is still valid
        if (!PlayerInventoryManager.Instance.HasSpaceForItemAt(inventoryItemData.ItemData, lastPreviewPosition, lastPreviewRotation))
        {
            DebugLog("Target position is no longer valid - transfer cancelled");
            return false;
        }

        // Remove from container manager (gets the InventoryItemData with ItemInstance)
        if (containerManager.RemoveItem(inventoryItemData.ID))
        {
            DebugLog($"Successfully removed {inventoryItemData.ID} from container");

            // REFACTORED: Pass the InventoryItemData to AddItem
            // BaseInventoryManager.AddItem(InventoryItemData, position, rotation) will:
            // 1. Create a NEW ItemInstance (copy) from itemData.ItemInstance
            // 2. Place it at the exact position and rotation we specify
            // 3. Ensure complete independence between inventories
            if (PlayerInventoryManager.Instance.AddItem(inventoryItemData, lastPreviewPosition, lastPreviewRotation))
            {
                DebugLog($"Successfully added {inventoryItemData.ItemData?.itemName} to player inventory at precise position");
                return true;
            }
            else
            {
                // Failed to add to player - restore to container
                DebugLogError("Failed to add item to player inventory - restoring to container");

                // Restore using InventoryItemData (which preserves ItemInstance state)
                if (containerManager.AddItem(inventoryItemData, originalGridPosition, originalRotation))
                {
                    DebugLog("Item restored to container after failed player inventory addition");
                }
                else
                {
                    DebugLogError("CRITICAL: Failed to restore item to container!");
                }
                return false;
            }
        }
        else
        {
            DebugLogError("Failed to remove item from container manager");
            return false;
        }
    }

    #endregion

    #region Drop Down Menu Integration

    protected override void TransferItem()
    {
        DebugLog("Transferring item to player inventory via dropdown menu");
        bool success = PerformAutoTransfer();

        if (success)
        {
            DebugLog($"Quick-transferred {inventoryItemData.ItemData?.itemName} to player inventory");
            OnItemDeselected?.Invoke();
        }
        else
        {
            DebugLog("Quick transfer failed");
        }
    }

    /// <summary>
    /// REFACTORED: Perform auto transfer using InventoryItemData.
    /// </summary>
    private bool PerformAutoTransfer()
    {
        // Check if player has space
        if (!PlayerInventoryManager.Instance.HasSpaceForItem(inventoryItemData.ItemData))
        {
            DebugLog("Player inventory has no space for item");
            return false;
        }

        // Remove from container manager
        if (containerManager.RemoveItem(inventoryItemData.ID))
        {
            DebugLog($"Successfully removed {inventoryItemData.ID} from container for auto transfer");

            // REFACTORED: Pass InventoryItemData which contains ItemInstance
            // AddItem will create a NEW copy automatically
            if (PlayerInventoryManager.Instance.AddItem(inventoryItemData))
            {
                DebugLog($"Successfully auto-transferred {inventoryItemData.ItemData?.itemName} to player inventory");
                return true;
            }
            else
            {
                // Failed to add to player - restore to container
                DebugLogError("Failed to add item to player inventory - restoring to container");

                if (containerManager.AddItem(inventoryItemData, originalGridPosition, originalRotation))
                {
                    DebugLog("Item restored to container after failed auto transfer");
                }
                else
                {
                    DebugLogError("CRITICAL: Failed to restore item to container!");
                }
                return false;
            }
        }
        else
        {
            DebugLogError("Failed to remove item from container manager");
            return false;
        }
    }

    protected override void ConsumeItem()
    {
        if (inventoryItemData?.ItemData?.itemType != ItemType.Consumable)
        {
            DebugLogWarning("Cannot consume non-consumable item");
            return;
        }

        DebugLog($"Consuming {inventoryItemData.ItemData.itemName}");

        var consumableData = inventoryItemData.ItemData.ConsumableData;
        if (consumableData != null)
        {
            GameManager.Instance.playerManager.ApplyConsumableEffects(consumableData);
        }

        // Remove item from container
        if (containerManager.RemoveItem(inventoryItemData.ID))
        {
            DebugLog($"Item {inventoryItemData.ItemData.itemName} consumed and removed from container");
            OnItemDeselected?.Invoke();
        }
        else
        {
            DebugLogError("Failed to remove consumed item from container");
        }
    }

    #endregion

    #region Cleanup

    protected override void OnDestroy()
    {
        base.OnDestroy();

        // Clear any player inventory preview if we're being destroyed during a drag
        if (playerInventoryGridVisual != null && isDragging)
        {
            playerInventoryGridVisual.ClearPlacementPreview();
        }
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        // Clear player inventory preview
        if (playerInventoryGridVisual != null)
        {
            playerInventoryGridVisual.ClearPlacementPreview();
        }

        // Call base implementation
        base.OnEndDrag(eventData);

        // Reset transfer state
        isDraggedToPlayerInventory = false;
        hasValidPreview = false;
    }

    #endregion

    #region Public Interface

    public StorageContainer GetContainerManager() => containerManager;
    public bool IsTransferToPlayerEnabled() => enableTransferToPlayer;

    public void SetTransferToPlayerEnabled(bool enabled)
    {
        enableTransferToPlayer = enabled;
        DebugLog($"Transfer to player enabled: {enabled}");
    }

    public Vector2Int GetLastPreviewPosition() => lastPreviewPosition;
    public int GetLastPreviewRotation() => lastPreviewRotation;
    public bool HasValidPreview() => hasValidPreview;

    #endregion
}