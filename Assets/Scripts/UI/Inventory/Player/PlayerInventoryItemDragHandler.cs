using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// REFACTORED: Player inventory item drag handler that extends BaseInventoryDragHandler.
/// Now provides clean separation of concerns while maintaining all existing functionality.
/// Handles player-specific drag behaviors like clothing equipping, item dropping, and context menus.
/// 
/// FIXED: Oxygen tank swap issue - now properly restores item to inventory before equipping,
/// following the same pattern as clothing slots.
/// </summary>
public class PlayerInventoryItemDragHandler : BaseInventoryDragHandler
{
    [Header("Player Inventory Specific")]
    //[SerializeField] private InventoryDropdownMenu dropdownMenu;

    // Player-specific drag state
    private bool isDraggedOutsideInventory = false;
    private ClothingSlotUI lastHoveredClothingSlot = null;


    // Reference to player inventory manager
    private PlayerInventoryManager playerInventoryManager;

    #region  Storage container transfer detection
    private bool isDraggedToStorageContainer = false;
    private StorageContainerGridVisual targetStorageContainer = null;
    private Vector2Int lastStoragePreviewPosition;
    private int lastStoragePreviewRotation;
    private bool hasValidStoragePreview = false;

    #endregion

    #region Initialization

    protected override void Awake()
    {
        // Set inventory type context for player inventory
        inventoryTypeContext = ItemInventoryTypeContext.PlayerInventoryItem;

        base.Awake();
    }

    protected override void Start()
    {
        base.Start();

        // Get reference to player inventory manager
        playerInventoryManager = PlayerInventoryManager.Instance;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        CleanupDropdownEvents();
    }

    #endregion

    #region Player-Specific Overrides

    /// <summary>
    /// ENHANCED: Enhanced drag feedback for player inventory with clothing slot and storage container integration.
    /// </summary>
    protected override void UpdateDragFeedback(PointerEventData eventData)
    {
        // First check if we're outside inventory bounds
        CheckIfOutsideInventoryBounds();

        // Clear previous clothing slot feedback
        if (lastHoveredClothingSlot != null)
        {
            ClothingSlotUI.ClearAllDragFeedback();
            lastHoveredClothingSlot = null;
        }

        // NEW: Check for storage container transfer
        CheckForStorageContainerTransfer(eventData);

        var currentTankSlot = GetOxygenTankSlotUnderPointer(eventData);

        // Check for clothing slot under pointer
        var currentClothingSlot = ClothingDragDropHelper.GetClothingSlotUnderPointer(eventData);

        if (currentTankSlot != null)
        {
            // We're over the oxygen tank slot
            lastHoveredClothingSlot = null; // Clear clothing slot

            // Provide visual feedback to the tank slot
            OxygenTankSlotUI.HandleDragOverTankSlot(eventData, inventoryItemData);

            // Clear inventory, storage, and clothing previews
            ClearPreview();
            ClearStoragePreview();
            ClothingSlotUI.ClearAllDragFeedback();
            return; // Early return since we're handling tank slot
        }
        else if (currentClothingSlot != null)
        {
            // We're over a clothing slot
            lastHoveredClothingSlot = currentClothingSlot;

            // Provide visual feedback to the clothing slot
            ClothingSlotUI.HandleDragOverClothingSlot(eventData, inventoryItemData);

            // Clear inventory and storage previews since we're over clothing
            ClearPreview();
            ClearStoragePreview();
        }
        else if (isDraggedToStorageContainer)
        {
            // NEW: We're over a storage container - show storage preview
            ClearPreview(); // Clear player inventory preview
            ShowStorageContainerPreview();
        }
        else if (!isDraggedOutsideInventory)
        {
            // We're over player inventory - show inventory preview
            ClearStoragePreview(); // Clear storage preview
            ShowInventoryPreview();
        }
        else
        {
            // We're outside both inventory, clothing slots, and storage containers
            ClearPreview();
            ClearStoragePreview();
        }
    }

    /// <summary>
    /// Get oxygen tank slot under pointer
    /// </summary>
    private OxygenTankSlotUI GetOxygenTankSlotUnderPointer(PointerEventData eventData)
    {
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            var tankSlotUI = result.gameObject.GetComponent<OxygenTankSlotUI>();
            if (tankSlotUI != null)
            {
                return tankSlotUI;
            }

            // Also check parent objects
            var parentSlotUI = result.gameObject.GetComponentInParent<OxygenTankSlotUI>();
            if (parentSlotUI != null)
            {
                return parentSlotUI;
            }
        }

        return null;
    }

    /// <summary>
    /// ENHANCED: Enhanced drop handling with clothing slot integration, storage container transfers, and item dropping.
    /// </summary>
    protected override bool HandleDrop(PointerEventData eventData)
    {
        // Clear any clothing slot feedback
        ClothingSlotUI.ClearAllDragFeedback();

        // Clear storage container preview
        ClearStoragePreview();

        // Check if we dropped on a clothing slot
        var droppedOnClothingSlot = ClothingDragDropHelper.GetClothingSlotUnderPointer(eventData);

        if (droppedOnClothingSlot != null)
        {
            return HandleClothingSlotDrop(droppedOnClothingSlot);
        }

        // Check if we dropped on an oxygen tank slot
        var droppedOnTankSlot = GetOxygenTankSlotUnderPointer(eventData);

        if (droppedOnTankSlot != null)
        {
            return HandleOxygenTankSlotDrop(droppedOnTankSlot);
        }

        // NEW: Check if we dropped on a storage container
        if (isDraggedToStorageContainer && targetStorageContainer != null)
        {
            return HandleStorageContainerDrop();
        }

        // Check if we dropped outside inventory (for item dropping)
        if (isDraggedOutsideInventory)
        {
            return HandleDropOutsideInventory();
        }

        // IMPORTANT: Return false to let base class handle normal inventory placement
        // This ensures that normal inventory drag-and-drop still works
        return false;
    }

    /// <summary>
    /// Player-specific validation for drag beginning.
    /// </summary>
    protected override bool CanBeginDrag(PointerEventData eventData)
    {
        if (!base.CanBeginDrag(eventData))
            return false;

        // Add player-specific validation here if needed
        // For example: check if item is currently equipped, in use, etc.

        return true;
    }

    #endregion

    #region Storage Container Transfer Detection

    /// <summary>
    /// NEW: Check if the item is being dragged over a storage container.
    /// </summary>
    private void CheckForStorageContainerTransfer(PointerEventData eventData)
    {
        // Reset storage container state
        isDraggedToStorageContainer = false;
        targetStorageContainer = null;
        hasValidStoragePreview = false;

        // Find all storage container grid visuals in the scene
        var storageContainers = FindObjectsByType<StorageContainerGridVisual>(FindObjectsSortMode.None);

        foreach (var container in storageContainers)
        {
            if (container == null || !container.gameObject.activeInHierarchy) continue;

            RectTransform containerRect = container.GetComponent<RectTransform>();
            if (containerRect == null) continue;

            // Check if pointer is over this storage container
            Vector2 localPoint;
            bool isOverContainer = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                containerRect,
                eventData.position,
                canvas.worldCamera,
                out localPoint);

            if (isOverContainer && containerRect.rect.Contains(localPoint))
            {
                // We're over this storage container
                isDraggedToStorageContainer = true;
                targetStorageContainer = container;
                DebugLog($"Dragging over storage container: {container.GetContainerManager()?.DisplayName ?? "Unknown"}");
                break;
            }
        }

        // Visual feedback for storage container transfer
        if (isDraggedToStorageContainer)
        {
            canvasGroup.alpha = 0.9f; // Slightly more visible when over storage container
        }
        else
        {
            // Reset to normal alpha if not over storage or special areas
            if (!lastHoveredClothingSlot && !isDraggedOutsideInventory)
            {
                canvasGroup.alpha = 0.8f; // Normal drag alpha
            }
        }
    }

    /// <summary>
    /// NEW: Show transfer preview on storage container.
    /// </summary>
    private void ShowStorageContainerPreview()
    {
        if (targetStorageContainer == null) return;

        // Get grid position on storage container
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetStorageContainer.GetComponent<RectTransform>(),
            RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rectTransform.position),
            canvas.worldCamera,
            out localPos);

        Vector2Int gridPos = targetStorageContainer.GetGridPosition(localPos);

        // Use current rotation from the dragged item (which may have been rotated during drag)
        int currentRotation = inventoryItemData.currentRotation;

        // Create temporary item for validation
        var tempItem = new InventoryItemData(inventoryItemData.ID + "_storage_transfer_temp", inventoryItemData.ItemData, gridPos);
        tempItem.SetRotation(currentRotation);

        // Check if transfer is valid
        bool isValid = targetStorageContainer.GridData.IsValidPosition(gridPos, tempItem);

        // Show preview on storage container
        targetStorageContainer.ShowPlacementPreview(gridPos, tempItem, isValid);

        // Store preview state for precise placement
        lastStoragePreviewPosition = gridPos;
        lastStoragePreviewRotation = currentRotation;
        hasValidStoragePreview = isValid;
        wasValidPlacement = isValid;

        DebugLog($"Storage Preview: pos={gridPos}, rot={currentRotation}, valid={isValid}");
    }

    /// <summary>
    /// NEW: Clear storage container preview.
    /// </summary>
    private void ClearStoragePreview()
    {
        if (targetStorageContainer != null)
        {
            targetStorageContainer.ClearPlacementPreview();
        }
    }

    /// <summary>
    /// NEW: Handle dropping on a storage container with precise positioning.
    /// </summary>
    private bool HandleStorageContainerDrop()
    {
        if (targetStorageContainer == null || playerInventoryManager == null)
        {
            DebugLogError("Cannot transfer to storage - missing references");
            RevertToOriginalState();
            return true; // Handled, even though failed
        }

        var containerManager = targetStorageContainer.GetContainerManager();
        if (containerManager == null)
        {
            DebugLogError("Cannot transfer - storage container manager not found");
            RevertToOriginalState();
            return true;
        }

        DebugLog($"Attempting to transfer {inventoryItemData.ItemData?.itemName} to storage container {containerManager.DisplayName}");

        // Check if we have a valid preview position to place at
        if (!hasValidStoragePreview)
        {
            DebugLog("No valid storage preview position - cannot transfer");
            RevertToOriginalState();
            return true;
        }

        // Restore the item to player inventory if it was removed during drag
        if (itemRemovedFromGrid)
        {
            DebugLog($"Restoring item {inventoryItemData.ID} to player inventory before transfer");

            // Restore to original position and rotation
            inventoryItemData.SetGridPosition(originalGridPosition);
            inventoryItemData.SetRotation(originalRotation);

            if (gridVisual.GridData.PlaceItem(inventoryItemData))
            {
                itemRemovedFromGrid = false;
                DebugLog($"Item {inventoryItemData.ID} restored to player inventory successfully");
            }
            else
            {
                DebugLogError($"Failed to restore item {inventoryItemData.ID} to player inventory!");
                RevertToOriginalState();
                return true; // Handled, even though failed
            }
        }

        // Perform the precise transfer to storage container
        bool success = PerformPreciseStorageTransfer(containerManager);

        if (success)
        {
            DebugLog($"Successfully transferred {inventoryItemData.ItemData?.itemName} to storage container at {lastStoragePreviewPosition} with rotation {lastStoragePreviewRotation}");
            OnItemDeselected?.Invoke();
        }
        else
        {
            DebugLog($"Failed to transfer {inventoryItemData.ItemData?.itemName} to storage - reverting");
            RevertToOriginalState();
        }

        return true; // Handled
    }

    /// <summary>
    /// NEW: Perform precise transfer to storage container using exact preview position and rotation.
    /// </summary>
    private bool PerformPreciseStorageTransfer(StorageContainer containerManager)
    {
        DebugLog($"Performing precise storage transfer to position {lastStoragePreviewPosition} with rotation {lastStoragePreviewRotation}");

        // Double-check that the target position is still valid
        if (!containerManager.HasSpaceForItemAt(inventoryItemData.ItemData, lastStoragePreviewPosition, lastStoragePreviewRotation))
        {
            DebugLog("Storage target position is no longer valid - transfer cancelled");
            return false;
        }

        // Remove from player inventory
        if (playerInventoryManager.RemoveItem(inventoryItemData.ID))
        {
            DebugLog($"Successfully removed {inventoryItemData.ID} from player inventory");

            // Try to add to storage container at the exact preview position and rotation
            if (containerManager.AddItem(inventoryItemData, lastStoragePreviewPosition, lastStoragePreviewRotation))
            {
                DebugLog($"Successfully added {inventoryItemData.ItemData?.itemName} to storage container at precise position");
                return true;
            }
            else
            {
                // Failed to add to storage - restore to player inventory
                DebugLogError("Failed to add item to storage container at precise position - attempting to restore to player");

                if (playerInventoryManager.AddItem(inventoryItemData, originalGridPosition, originalRotation))
                {
                    DebugLog("Item restored to player inventory after failed storage transfer");
                }
                else
                {
                    DebugLogError("CRITICAL: Failed to restore item to player inventory after failed storage transfer!");
                }
                return false;
            }
        }
        else
        {
            DebugLogError("Failed to remove item from player inventory for storage transfer");
            return false;
        }
    }

    #endregion


    #region Clothing Slot Integration

    /// <summary>
    /// Handle dropping on a clothing slot with comprehensive validation.
    /// </summary>
    private bool HandleClothingSlotDrop(ClothingSlotUI clothingSlot)
    {
        // Check if this is a valid clothing item for ANY clothing slot
        if (inventoryItemData.ItemData?.itemType == ItemType.Clothing)
        {
            // Check if it can be equipped to THIS specific slot
            if (ClothingDragDropHelper.CanEquipToSlot(inventoryItemData, clothingSlot))
            {
                DebugLog($"Attempting to equip {inventoryItemData.ItemData?.itemName} to clothing slot {clothingSlot.TargetLayer}");

                // Restore item to inventory first if it was removed during drag
                if (itemRemovedFromGrid)
                {
                    if (!RestoreItemToInventoryForEquipment())
                    {
                        RevertToOriginalState();
                        return true; // Handled, even though failed
                    }
                }

                // Attempt equipment
                bool success = ClothingDragDropHelper.HandleClothingSlotDrop(inventoryItemData, clothingSlot);

                if (success)
                {
                    DebugLog($"Successfully equipped {inventoryItemData.ItemData?.itemName} to {clothingSlot.TargetLayer}");
                    OnItemDeselected?.Invoke();
                }
                else
                {
                    DebugLogWarning($"Failed to equip {inventoryItemData.ItemData?.itemName} to {clothingSlot.TargetLayer}");
                    RevertToOriginalState();
                }

                return true; // Handled
            }
            else
            {
                // Wrong slot for this clothing item
                var clothingData = inventoryItemData.ItemData.ClothingData;
                string itemName = inventoryItemData.ItemData.itemName;
                string targetSlotName = ClothingInventoryUtilities.GetFriendlyLayerName(clothingSlot.TargetLayer);

                if (clothingData != null && clothingData.validLayers.Length > 0)
                {
                    string validSlots = GetValidSlotsText(clothingData.validLayers);
                    DebugLogWarning($"Invalid clothing slot: {itemName} cannot be worn on {targetSlotName}. Can be worn on: {validSlots}");
                }
                else
                {
                    DebugLogWarning($"Invalid clothing slot: {itemName} cannot be worn on {targetSlotName}");
                }

                RevertToOriginalStateWithRejectionFeedback();
                return true; // Handled
            }
        }
        else
        {
            // Not a clothing item at all
            string itemTypeName = inventoryItemData.ItemData?.itemType.ToString() ?? "Unknown";
            DebugLogWarning($"Non-clothing item rejected: {inventoryItemData.ItemData?.itemName} is {itemTypeName}, not clothing");

            RevertToOriginalStateWithRejectionFeedback();
            return true; // Handled
        }
    }

    /// <summary>
    /// Get user-friendly text for valid clothing slots.
    /// </summary>
    private string GetValidSlotsText(ClothingLayer[] validLayers)
    {
        if (validLayers == null || validLayers.Length == 0)
            return "nowhere";

        string[] slotNames = new string[validLayers.Length];
        for (int i = 0; i < validLayers.Length; i++)
        {
            slotNames[i] = ClothingInventoryUtilities.GetFriendlyLayerName(validLayers[i]);
        }

        if (slotNames.Length == 1)
            return slotNames[0];
        else if (slotNames.Length == 2)
            return $"{slotNames[0]} or {slotNames[1]}";
        else
            return string.Join(", ", slotNames, 0, slotNames.Length - 1) + $", or {slotNames[slotNames.Length - 1]}";
    }

    /// <summary>
    /// Restore item to inventory specifically for equipment operations.
    /// </summary>
    private bool RestoreItemToInventoryForEquipment()
    {
        DebugLog($"Restoring item {inventoryItemData.ID} to inventory before equipment");

        inventoryItemData.SetGridPosition(originalGridPosition);
        inventoryItemData.SetRotation(originalRotation);

        if (gridVisual.GridData.PlaceItem(inventoryItemData))
        {
            itemRemovedFromGrid = false;
            DebugLog($"Item {inventoryItemData.ID} restored to inventory successfully");
            return true;
        }
        else
        {
            DebugLogError($"Failed to restore item {inventoryItemData.ID} to inventory!");
            gridVisual.GridData.RemoveItem(inventoryItemData.ID);
            if (!gridVisual.GridData.PlaceItem(inventoryItemData))
            {
                DebugLogError($"Could not restore item to inventory - aborting equipment");
                return false;
            }
            itemRemovedFromGrid = false;
            return true;
        }
    }

    #endregion

    #region Oxygen System Integration

    /// <summary>
    /// Handle dropping on an oxygen tank slot with comprehensive validation.
    /// FIXED: Now properly restores item to inventory before equipping by checking actual inventory state.
    /// </summary>
    private bool HandleOxygenTankSlotDrop(OxygenTankSlotUI tankSlot)
    {
        // Clear any feedback
        OxygenTankSlotUI.ClearAllDragFeedback();

        // Check if this is a valid oxygen tank item
        if (inventoryItemData.ItemData?.itemType == ItemType.OxygenTank)
        {
            var tankData = inventoryItemData.ItemData.OxygenTankData;

            if (tankData != null && tankData.IsValid())
            {
                DebugLog($"Attempting to equip {inventoryItemData.ItemData?.itemName} to oxygen tank slot");

                // CRITICAL FIX: Check if item is actually in inventory (don't rely on itemRemovedFromGrid flag)
                // The flag may have been reset by earlier RevertToOriginalState calls
                bool itemIsInInventory = gridVisual.GridData.GetItem(inventoryItemData.ID) != null;
                
                if (!itemIsInInventory)
                {
                    DebugLog($"Item not found in inventory - restoring to inventory before equip");
                    
                    if (!RestoreItemToInventoryForTankEquipment())
                    {
                        DebugLogError("Failed to restore item to inventory - aborting tank equipment");
                        RevertToOriginalState();
                        return true; // Handled, even though failed
                    }
                }
                else
                {
                    DebugLog($"Item already in inventory at position {inventoryItemData.GridPosition}");
                }

                // Attempt equipment - item is now guaranteed to be in inventory
                var tankManager = OxygenTankManager.Instance;
                if (tankManager == null)
                {
                    DebugLogError("OxygenTankManager not found");
                    RevertToOriginalState();
                    return true;
                }

                bool success = tankManager.EquipTank(inventoryItemData.ID);

                if (success)
                {
                    DebugLog($"Successfully equipped {inventoryItemData.ItemData?.itemName} to oxygen tank slot");
                    OnItemDeselected?.Invoke();
                }
                else
                {
                    DebugLogWarning($"Failed to equip {inventoryItemData.ItemData?.itemName} to oxygen tank slot");
                    RevertToOriginalState();
                }

                return true; // Handled
            }
            else
            {
                // Invalid tank data
                DebugLogWarning($"Invalid oxygen tank: {inventoryItemData.ItemData?.itemName} has invalid tank data");
                RevertToOriginalStateWithRejectionFeedback();
                return true;
            }
        }
        else
        {
            // Not an oxygen tank at all
            string itemTypeName = inventoryItemData.ItemData?.itemType.ToString() ?? "Unknown";
            DebugLogWarning($"Non-tank item rejected: {inventoryItemData.ItemData?.itemName} is {itemTypeName}, not an oxygen tank");

            RevertToOriginalStateWithRejectionFeedback();
            return true; // Handled
        }
    }

    /// <summary>
    /// Restore item to inventory specifically for tank equipment operations.
    /// Matches the pattern used in RestoreItemToInventoryForEquipment for clothing.
    /// </summary>
    private bool RestoreItemToInventoryForTankEquipment()
    {
        DebugLog($"Restoring item {inventoryItemData.ID} to inventory at position {originalGridPosition} with rotation {originalRotation}");

        // Restore original position and rotation
        inventoryItemData.SetGridPosition(originalGridPosition);
        inventoryItemData.SetRotation(originalRotation);

        // Try to place the item back in the grid
        if (gridVisual.GridData.PlaceItem(inventoryItemData))
        {
            itemRemovedFromGrid = false;
            DebugLog($"Item {inventoryItemData.ID} restored to inventory successfully");
            return true;
        }
        else
        {
            // First attempt failed - try cleanup and retry
            DebugLogError($"Failed to restore item {inventoryItemData.ID} to inventory on first attempt - trying cleanup");
            
            // Clean up any existing reference
            gridVisual.GridData.RemoveItem(inventoryItemData.ID);
            
            // Retry placement
            if (!gridVisual.GridData.PlaceItem(inventoryItemData))
            {
                DebugLogError($"Could not restore item to inventory even after cleanup - aborting tank equipment");
                return false;
            }
            
            itemRemovedFromGrid = false;
            DebugLog($"Item {inventoryItemData.ID} restored to inventory after cleanup");
            return true;
        }
    }

    #endregion

    #region Item Dropping

    /// <summary>
    /// Handle dropping item outside inventory with proper restoration.
    /// </summary>
    private bool HandleDropOutsideInventory()
    {
        DebugLog($"Item {inventoryItemData.ItemData?.itemName} dropped outside inventory - attempting to drop into scene");

        if (inventoryItemData?.ItemData?.CanDrop != true)
        {
            DebugLogWarning($"Cannot drop {inventoryItemData.ItemData?.itemName} - it's a key item");
            RevertToOriginalState();
            return true; // Handled
        }

        // Restore item to inventory first if it was removed during drag
        if (itemRemovedFromGrid)
        {
            DebugLog($"Restoring item {inventoryItemData.ID} to inventory before dropping");

            inventoryItemData.SetGridPosition(originalGridPosition);
            inventoryItemData.SetRotation(originalRotation);

            if (gridVisual.GridData.PlaceItem(inventoryItemData))
            {
                itemRemovedFromGrid = false;
                DebugLog($"Item {inventoryItemData.ID} restored to inventory successfully");
            }
            else
            {
                DebugLogError($"Failed to restore item {inventoryItemData.ID} to inventory before dropping!");
                gridVisual.GridData.RemoveItem(inventoryItemData.ID);
                if (!gridVisual.GridData.PlaceItem(inventoryItemData))
                {
                    DebugLogError($"Could not restore item to inventory - aborting drop");
                    return true; // Handled, even though failed
                }
                itemRemovedFromGrid = false;
            }
        }

        // Now try to drop the item using player inventory manager
        bool success = false;
        if (playerInventoryManager != null)
        {
            var dropResult = playerInventoryManager.TryDropItem(inventoryItemData.ID);
            success = dropResult.success;
        }
        else
        {
            // Fallback to direct ItemDropSystem
            success = ItemDropSystem.DropItemFromInventory(inventoryItemData.ID);
        }

        if (success)
        {
            DebugLog($"Successfully dropped {inventoryItemData.ItemData?.itemName} into scene");
            OnItemDeselected?.Invoke();
        }
        else
        {
            DebugLogWarning($"Failed to drop {inventoryItemData.ItemData?.itemName} - reverting to original position");
            RevertToOriginalState();
        }

        return true; // Handled
    }

    /// <summary>
    /// Check if the item is being dragged outside inventory bounds.
    /// </summary>
    private void CheckIfOutsideInventoryBounds()
    {
        if (gridVisual == null) return;

        RectTransform gridRect = gridVisual.GetComponent<RectTransform>();
        if (gridRect == null) return;

        Vector2 localPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gridRect,
            RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rectTransform.position),
            canvas.worldCamera,
            out localPosition);

        Rect gridBounds = gridRect.rect;
        gridBounds.xMin -= dropOutsideBuffer;
        gridBounds.xMax += dropOutsideBuffer;
        gridBounds.yMin -= dropOutsideBuffer;
        gridBounds.yMax += dropOutsideBuffer;

        isDraggedOutsideInventory = !gridBounds.Contains(localPosition);

        // Visual feedback for dragging outside
        if (isDraggedOutsideInventory)
        {
            canvasGroup.alpha = 0.6f;
        }
        else
        {
            canvasGroup.alpha = 0.8f;
        }
    }

    #endregion

    #region Enhanced Animation and Feedback

    /// <summary>
    /// Revert to original state with special rejection feedback.
    /// </summary>
    private void RevertToOriginalStateWithRejectionFeedback()
    {
        DebugLog($"Reverting {inventoryItemData.ItemData?.itemName} to original position due to invalid drop");

        // Revert rotation if changed
        if (inventoryItemData.currentRotation != originalRotation)
        {
            inventoryItemData.SetRotation(originalRotation);
            visualRenderer?.RefreshVisual();
        }

        // Restore original position
        inventoryItemData.SetGridPosition(originalGridPosition);

        // Place item back in grid at original position
        if (itemRemovedFromGrid)
        {
            if (gridVisual.GridData.PlaceItem(inventoryItemData))
            {
                itemRemovedFromGrid = false;
            }
            else
            {
                DebugLogError($"Failed to restore item {inventoryItemData.ID} to original position!");
            }
        }

        // Animate back with special rejection animation
        AnimateToOriginalPositionWithRejectionFeedback();
        visualRenderer?.RefreshHotkeyIndicatorVisuals();
    }

    /// <summary>
    /// Animate back to original position with rejection feedback.
    /// </summary>
    private void AnimateToOriginalPositionWithRejectionFeedback()
    {
        // First shake the item to indicate rejection
        var originalPos = originalPosition;

        // Quick shake animation
        rectTransform.DOShakePosition(0.3f, 15f, 10, 90, false, true)
            .OnComplete(() =>
            {
                // Then smoothly animate back to original position
                rectTransform.DOLocalMove(originalPos, snapAnimationDuration * 1.5f)
                    .SetEase(Ease.OutBack);
            });

        // Also add a brief color flash to the visual renderer if possible
        if (visualRenderer != null)
        {
            visualRenderer.SetAlpha(0.5f);
            DOVirtual.Float(0.5f, 1f, 0.5f, (alpha) => visualRenderer.SetAlpha(alpha));
        }
    }

    #endregion

    #region Context Menu Integration

    /// <summary>
    /// Handle dropdown menu action selection.
    /// </summary>
    protected override void OnDropdownActionSelected(InventoryItemData selectedItem, string actionId)
    {
        base.OnDropdownActionSelected(selectedItem, actionId);

        if (inventoryItemData != selectedItem)
        {
            return;
        }

        if (actionId == "equip_tank")
        {
            EquipToTankSlot();
            return;
        }

        // Handle clothing wear actions
        if (actionId.StartsWith("wear_"))
        {
            string layerName = actionId.Substring(5);
            if (System.Enum.TryParse<ClothingLayer>(layerName, out ClothingLayer targetLayer))
            {
                WearInSlot(targetLayer);
            }
            return;
        }

        switch (actionId)
        {
            case "assign_hotkey":
                AssignHotkey();
                break;
            case "unload":
                UnloadWeapon();
                break;
        }
    }

    #endregion

    #region Dropdown Action Handlers

    protected override void TransferItem()
    {

        if (StorageContainerUI.Instance.currentContainer == null)
        {
            DebugLogWarning("Cannot transfer - no active storage container");
            return;
        }

        // CRITICAL: Check if item still exists in player inventory before transfer
        var item = PlayerInventoryManager.Instance.InventoryGridData.GetItem(inventoryItemData.ID);
        if (item == null)
        {
            DebugLogWarning($"Item {inventoryItemData.ID} no longer exists in player inventory - transfer aborted");
            return;
        }

        DebugLog($"Transferring item {inventoryItemData.ItemData?.itemName} from player to storage container (Instance: {inventoryItemData.ItemInstance?.InstanceID})");

        // Check if storage container has space
        if (!StorageContainerUI.Instance.currentContainer.HasSpaceForItem(inventoryItemData.ItemData))
        {
            DebugLog("Storage container has no space for item");
            return;
        }

        // Remove from player inventory first
        if (PlayerInventoryManager.Instance.RemoveItem(inventoryItemData.ID))
        {
            DebugLog($"Successfully removed {inventoryItemData.ID} from player inventory");

            // Try to add to storage container
            if (StorageContainerUI.Instance.currentContainer.AddItem(inventoryItemData))
            {
                DebugLog($"Successfully transferred {inventoryItemData.ItemData?.itemName} to storage container");
                OnItemDeselected?.Invoke();

                // IMPORTANT: Deselect/disable this drag handler since item was transferred
                SetDraggable(false);
            }
            else
            {
                // Failed to add to container - restore to player inventory
                DebugLogWarning("Failed to add item to storage container - restoring to player inventory");

                if (PlayerInventoryManager.Instance.AddItem(inventoryItemData, originalGridPosition, originalRotation))
                {
                    DebugLog("Item restored to player inventory after failed storage transfer");
                }
                else
                {
                    DebugLogError("CRITICAL: Failed to restore item to player inventory after failed storage transfer!");
                }
            }
        }
        else
        {
            DebugLogError("Failed to remove item from player inventory for transfer");
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

        // Get consumable template data for effects
        var consumableTemplate = inventoryItemData.ItemData.ConsumableData;
        if (consumableTemplate != null)
        {
            GameManager.Instance.playerManager.ApplyConsumableEffects(consumableTemplate);
        }

        // Remove the item (single use)
        if (playerInventoryManager != null)
        {
            playerInventoryManager.RemoveItem(inventoryItemData.ID);
            OnItemDeselected?.Invoke();
        }
        else
        {
            DebugLogWarning("PlayerInventoryManager not found - cannot remove consumed item");
        }
    }

    private void AssignHotkey()
    {
        Debug.Log($"AssignHotkey called for item ID: {inventoryItemData?.ID}");
        if (inventoryItemData?.ItemData == null)
        {
            DebugLogWarning("Cannot assign hotkey - no item data");
            return;
        }

        DebugLog($"Assigning hotkey for {inventoryItemData.ItemData.itemName}");
        ShowHotkeySelectionUI();
    }

    private void ShowHotkeySelectionUI()
    {
        if (HotkeySelectionUI.Instance != null)
        {
            HotkeySelectionUI.Instance.ShowSelection(inventoryItemData);
        }
        else
        {
            AutoAssignToAvailableSlot();
        }
    }

    private void AutoAssignToAvailableSlot()
    {
        if (EquippedItemManager.Instance == null) return;

        var bindings = EquippedItemManager.Instance.GetAllHotkeyBindings();

        foreach (var binding in bindings)
        {
            if (binding.isAssigned)
            {
                var assignedItemData = binding.GetCurrentItemData();
                if (assignedItemData != null && assignedItemData.name == inventoryItemData.ItemData.name)
                {
                    bool success = EquippedItemManager.Instance.AssignItemToHotkey(inventoryItemData.ID, binding.slotNumber);
                    if (success)
                    {
                        DebugLog($"Added {inventoryItemData.ItemData.itemName} to existing hotkey {binding.slotNumber} stack");
                    }
                    return;
                }
            }
        }

        foreach (var binding in bindings)
        {
            if (!binding.isAssigned)
            {
                bool success = EquippedItemManager.Instance.AssignItemToHotkey(inventoryItemData.ID, binding.slotNumber);
                if (success)
                {
                    DebugLog($"Assigned {inventoryItemData.ItemData.itemName} to hotkey {binding.slotNumber}");
                }
                return;
            }
        }

        DebugLogWarning("All hotkey slots are occupied - cannot auto-assign");
    }

    private void UnloadWeapon()
    {
        if (inventoryItemData?.ItemData?.itemType != ItemType.RangedWeapon)
        {
            DebugLogWarning("Cannot unload non-weapon item");
            return;
        }

        // Get template data for validation
        var weaponTemplate = inventoryItemData.ItemData.RangedWeaponData;
        if (weaponTemplate == null)
        {
            DebugLogWarning("No weapon data found");
            return;
        }

        // Get instance data for current ammo
        var weaponInstance = inventoryItemData.ItemInstance?.RangedWeaponInstanceData;
        if (weaponInstance == null || weaponInstance.currentAmmoInClip <= 0)
        {
            DebugLogWarning("No ammo to unload");
            return;
        }

        int ammoToUnload = weaponInstance.currentAmmoInClip;
        DebugLog($"Unloading {ammoToUnload} rounds from {inventoryItemData.ItemData.itemName}");

        // Clear the weapon's ammo
        weaponInstance.currentAmmoInClip = 0;

        // Add ammo back to inventory if we have ammo type data
        if (weaponTemplate.requiredAmmoType != null && playerInventoryManager != null)
        {
            // For now, just log - you'd need to implement adding ammo items
            // This would require creating new ammo item instances
            DebugLog($"Would add {ammoToUnload} {weaponTemplate.requiredAmmoType.itemName} to inventory");

            // TODO: Implement actual ammo return to inventory
            // playerInventoryManager.AddItem(weaponTemplate.requiredAmmoType, null, null);
        }
    }

    protected override void DropItem()
    {
        if (inventoryItemData?.ItemData?.CanDrop != true)
        {
            DebugLogWarning($"Cannot drop {inventoryItemData.ItemData.itemName} - it's a key item");
            return;
        }

        if (inventoryItemData?.ID == null)
        {
            DebugLogWarning("Cannot drop item - no item data or ID");
            return;
        }

        bool success = false;
        if (playerInventoryManager != null)
        {
            var dropResult = playerInventoryManager.TryDropItem(inventoryItemData.ID);
            success = dropResult.success;
        }
        else
        {
            success = ItemDropSystem.DropItemFromInventory(inventoryItemData.ID);
        }

        if (success)
        {
            OnItemDeselected?.Invoke();
        }
        else
        {
            DebugLogWarning($"Failed to drop {inventoryItemData.ItemData?.itemName}");
        }
    }

    /// <summary>
    /// Equips the item to the specified clothing layer with improved error handling.
    /// </summary>
    private void WearInSlot(ClothingLayer targetLayer)
    {
        if (inventoryItemData?.ItemData?.itemType != ItemType.Clothing)
        {
            DebugLogWarning("Cannot wear - not a clothing item");
            return;
        }

        if (ClothingManager.Instance == null)
        {
            DebugLogWarning("ClothingManager not found - cannot equip clothing");
            return;
        }

        DebugLog($"Equipping {inventoryItemData.ItemData.itemName} to {targetLayer}");

        var validation = ClothingInventoryUtilities.ValidateClothingEquip(inventoryItemData, targetLayer);
        if (!validation.IsValid)
        {
            DebugLogWarning($"Cannot equip {inventoryItemData.ItemData.itemName} to {targetLayer}: {validation.Message}");
            return;
        }

        var slot = ClothingManager.Instance.GetSlot(targetLayer);
        if (slot != null && !slot.IsEmpty)
        {
            var swapValidation = ClothingInventoryUtilities.ValidateSwapOperation(inventoryItemData, targetLayer);
            if (!swapValidation.IsValid)
            {
                DebugLogWarning($"Cannot swap {inventoryItemData.ItemData.itemName}: {swapValidation.Message}");
                return;
            }
        }

        bool success = ClothingManager.Instance.EquipItemToLayer(inventoryItemData.ID, targetLayer);
        if (success)
        {
            DebugLog($"Successfully equipped {inventoryItemData.ItemData.itemName} to {targetLayer}");
            OnItemDeselected?.Invoke();
        }
        else
        {
            DebugLogWarning($"Failed to equip {inventoryItemData.ItemData.itemName} to {targetLayer}");
        }
    }

    /// <summary>
    /// Equips the oxygen tank to the tank slot.
    /// </summary>
    private void EquipToTankSlot()
    {
        if (inventoryItemData?.ItemData?.itemType != ItemType.OxygenTank)
        {
            DebugLogWarning("Cannot equip to tank slot - not an oxygen tank");
            return;
        }

        var tankManager = OxygenTankManager.Instance;
        if (tankManager == null)
        {
            DebugLogWarning("OxygenTankManager not found - cannot equip tank");
            return;
        }

        DebugLog($"Equipping {inventoryItemData.ItemData.itemName} to oxygen tank slot");

        bool success = tankManager.EquipTank(inventoryItemData.ID);
        if (success)
        {
            DebugLog($"Successfully equipped {inventoryItemData.ItemData.itemName} to tank slot");
            OnItemDeselected?.Invoke();
        }
        else
        {
            DebugLogWarning($"Failed to equip {inventoryItemData.ItemData.itemName} to tank slot");
        }
    }

    #endregion

}