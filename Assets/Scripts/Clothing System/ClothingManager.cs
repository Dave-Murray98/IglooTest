using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// REFACTORED: Core clothing system manager handling equipment, stats, and UI coordination.
/// Now properly maintains ItemInstance throughout the equipment lifecycle.
/// Preserves all instance-specific state (condition, etc.) when equipping/unequipping.
/// </summary>
public class ClothingManager : MonoBehaviour
{
    public static ClothingManager Instance { get; private set; }

    [Header("Clothing Slots Configuration")]
    [SerializeField] private ClothingSlot[] clothingSlots;

    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLogs = true;

    // Events for UI synchronization (following InventoryManager pattern)
    public event Action<ClothingSlot, InventoryItemData> OnItemEquipped;
    public event Action<ClothingSlot, string> OnItemUnequipped;
    public event Action<string, float> OnClothingConditionChanged;
    public event Action OnClothingDataChanged;

    // NEW: Events for swap operations
    public event Action<ClothingSlot, string, string> OnItemSwapped; // slot, oldItemId, newItemId

    // Cached stats for performance
    private float cachedTotalDefense = 0f;
    private float cachedTotalWarmth = 0f;
    private float cachedTotalRainResistance = 0f;
    private bool statsCacheValid = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeClothingSlots();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Subscribe to inventory events to handle item removal
        if (PlayerInventoryManager.Instance != null)
        {
            PlayerInventoryManager.Instance.OnItemRemoved += OnInventoryItemRemoved;
        }
    }

    private void OnDestroy()
    {
        if (PlayerInventoryManager.Instance != null)
        {
            PlayerInventoryManager.Instance.OnItemRemoved -= OnInventoryItemRemoved;
        }
    }

    /// <summary>
    /// Initialize the clothing slots array with all available layers
    /// </summary>
    private void InitializeClothingSlots()
    {
        if (clothingSlots == null || clothingSlots.Length == 0)
        {
            DebugLog("Initializing default clothing slots");

            clothingSlots = new ClothingSlot[]
            {
                new ClothingSlot(ClothingLayer.HeadUpper),
                new ClothingSlot(ClothingLayer.HeadLower),
                new ClothingSlot(ClothingLayer.TorsoInner),
                new ClothingSlot(ClothingLayer.TorsoOuter),
                new ClothingSlot(ClothingLayer.LegsInner),
                new ClothingSlot(ClothingLayer.LegsOuter),
                new ClothingSlot(ClothingLayer.Hands),
                new ClothingSlot(ClothingLayer.Socks),
                new ClothingSlot(ClothingLayer.Shoes)
            };
        }

        InvalidateStatsCache();
        DebugLog($"Clothing system initialized with {clothingSlots.Length} slots");
    }

    #region Equipment Methods

    /// <summary>
    /// REFACTORED: Equips an item from inventory to the specified clothing layer.
    /// Now properly maintains ItemInstance throughout the operation.
    /// </summary>
    public bool EquipItemToLayer(string itemId, ClothingLayer targetLayer)
    {
        if (PlayerInventoryManager.Instance == null)
        {
            DebugLog("Cannot equip item - InventoryManager not found");
            return false;
        }

        // Get the item from inventory (includes full ItemInstance)
        var item = PlayerInventoryManager.Instance.InventoryGridData.GetItem(itemId);
        if (item == null)
        {
            DebugLog($"Cannot equip item - item {itemId} not found in inventory");
            return false;
        }

        // Validate item type and compatibility
        if (!ValidateItemForEquipping(item, targetLayer))
        {
            return false;
        }

        var targetSlot = GetSlot(targetLayer);
        if (targetSlot == null)
        {
            DebugLog($"Cannot equip item - no slot found for layer {targetLayer}");
            return false;
        }

        // Handle different scenarios: empty slot vs occupied slot
        if (targetSlot.IsEmpty)
        {
            return EquipToEmptySlot(item, targetSlot);
        }
        else
        {
            return EquipWithSwap(item, targetSlot);
        }
    }

    /// <summary>
    /// Validates that an item can be equipped to the target layer
    /// </summary>
    private bool ValidateItemForEquipping(InventoryItemData item, ClothingLayer targetLayer)
    {
        if (item.ItemData?.itemType != ItemType.Clothing)
        {
            DebugLog($"Cannot equip item {item.ID} - not a clothing item");
            return false;
        }

        var clothingData = item.ItemData.ClothingData;
        if (clothingData == null)
        {
            DebugLog($"Cannot equip item {item.ID} - no clothing data");
            return false;
        }

        if (!clothingData.CanEquipToLayer(targetLayer))
        {
            DebugLog($"Cannot equip {item.ItemData.itemName} to layer {targetLayer} - not compatible");
            return false;
        }

        return true;
    }

    /// <summary>
    /// REFACTORED: Equips item to an empty slot (simple case).
    /// Transfers ItemInstance from inventory to clothing slot.
    /// </summary>
    private bool EquipToEmptySlot(InventoryItemData item, ClothingSlot targetSlot)
    {
        // CRITICAL: Get the ItemInstance BEFORE removing from inventory
        ItemInstance itemInstance = item.ItemInstance;

        if (itemInstance == null)
        {
            DebugLog($"Cannot equip item {item.ID} - ItemInstance is null");
            return false;
        }

        // Remove from inventory first
        if (!PlayerInventoryManager.Instance.RemoveItem(item.ID))
        {
            DebugLog($"Failed to remove item {item.ID} from inventory");
            return false;
        }

        // REFACTORED: Equip to slot with complete ItemInstance
        targetSlot.EquipItem(item.ID, itemInstance);
        InvalidateStatsCache();

        // Fire events
        OnItemEquipped?.Invoke(targetSlot, item);
        OnClothingDataChanged?.Invoke();

        DebugLog($"Equipped {item.ItemData.itemName} to {targetSlot.layer} (empty slot) [Instance: {itemInstance.InstanceID}]");
        return true;
    }

    /// <summary>
    /// REFACTORED: Equips item with swapping logic - maintains ItemInstance for both items.
    /// </summary>
    private bool EquipWithSwap(InventoryItemData newItem, ClothingSlot targetSlot)
    {
        string currentItemId = targetSlot.equippedItemId;

        DebugLog($"Attempting to swap {newItem.ItemData.itemName} with currently equipped item in {targetSlot.layer}");

        // REFACTORED: Get the currently equipped ItemInstance from the slot
        var currentItemInstance = targetSlot.GetEquippedItemInstance();
        if (currentItemInstance == null)
        {
            DebugLog($"Warning: Currently equipped item {currentItemId} has no ItemInstance - treating as empty slot");
            return EquipToEmptySlot(newItem, targetSlot);
        }

        // Pre-validation: Check all conditions before attempting swap
        if (!PreValidateSwapOperation(newItem, currentItemInstance, targetSlot))
        {
            return false;
        }

        // Perform the swap operation
        return ExecuteSwapOperation(newItem, currentItemInstance, targetSlot);
    }

    /// <summary>
    /// REFACTORED: Comprehensive pre-validation for swap operations.
    /// </summary>
    private bool PreValidateSwapOperation(InventoryItemData newItem, ItemInstance currentItemInstance, ClothingSlot targetSlot)
    {
        // DEBUG: Log the complete state before validation
        DebugSwapOperation(newItem.ID, targetSlot.layer);

        // 1. Verify new item exists in inventory
        var inventoryItem = PlayerInventoryManager.Instance.InventoryGridData.GetItem(newItem.ID);
        if (inventoryItem == null)
        {
            DebugLog($"Pre-validation failed: Item {newItem.ID} not found in inventory");
            return false;
        }

        // 2. Verify current item instance is valid
        if (currentItemInstance?.ItemData == null)
        {
            DebugLog($"Pre-validation failed: Current equipped item has no ItemData");
            return false;
        }

        // 3. Check if inventory will have space for displaced item using same-space swap
        if (!ValidateSwapInventorySpace(newItem.ID, currentItemInstance.ItemData))
        {
            DebugLog($"Pre-validation failed: Insufficient inventory space for {currentItemInstance.ItemData.itemName}");
            return false;
        }

        // 4. Verify the new item can actually be equipped to this layer
        if (newItem.ItemData?.ClothingData == null || !newItem.ItemData.ClothingData.CanEquipToLayer(targetSlot.layer))
        {
            DebugLog($"Pre-validation failed: {newItem.ItemData?.itemName} cannot be equipped to {targetSlot.layer}");
            return false;
        }

        DebugLog($"Pre-validation passed for swap: {newItem.ItemData.itemName} <-> {currentItemInstance.ItemData.itemName}");
        return true;
    }

    /// <summary>
    /// REFACTORED: Validates swap by checking if displaced item can fit in the same space.
    /// Now implements "same-space swap" - displaced item goes to the exact position of the new item.
    /// </summary>
    private bool ValidateSwapInventorySpace(string itemToRemoveId, ItemData itemToAdd)
    {
        var inventory = PlayerInventoryManager.Instance;

        // Get the item that we plan to remove from inventory (the one being equipped)
        var itemToRemove = inventory.InventoryGridData.GetItem(itemToRemoveId);
        if (itemToRemove == null)
        {
            DebugLog($"Item {itemToRemoveId} not found in inventory for swap validation");
            return false;
        }

        // Store the original position and rotation of the item being equipped
        Vector2Int originalPosition = itemToRemove.GridPosition;
        int originalRotation = itemToRemove.currentRotation;

        DebugLog($"Validating same-space swap: {itemToAdd.itemName} -> position {originalPosition} (rotation {originalRotation})");

        // Create a temporary ItemInstance for testing
        var tempItemInstance = new ItemInstance(itemToAdd);
        var tempDisplacedItem = new InventoryItemData("temp_displaced", tempItemInstance, originalPosition);

        // Try each possible rotation of the displaced item to see if it fits
        int maxRotations = TetrominoDefinitions.GetRotationCount(tempDisplacedItem.shapeType);

        for (int rotation = 0; rotation < maxRotations; rotation++)
        {
            tempDisplacedItem.SetRotation(rotation);

            // Temporarily remove the original item to test if displaced item fits in its space
            inventory.InventoryGridData.RemoveItem(itemToRemoveId);

            // Test if the displaced item can fit at the original position with this rotation
            bool canFitAtOriginalPosition = inventory.InventoryGridData.IsValidPosition(originalPosition, tempDisplacedItem);

            // Restore the original item
            itemToRemove.SetRotation(originalRotation); // Ensure original rotation is preserved
            inventory.InventoryGridData.PlaceItem(itemToRemove);

            if (canFitAtOriginalPosition)
            {
                DebugLog($"Same-space swap validated: {itemToAdd.itemName} can fit at position {originalPosition} with rotation {rotation}");
                return true;
            }
        }

        DebugLog($"Same-space swap failed: {itemToAdd.itemName} cannot fit at position {originalPosition} with any rotation");
        return false;
    }

    #endregion


    #region Swap Execution (continued from Part 1)

    /// <summary>
    /// REFACTORED: Executes same-space swap operation maintaining ItemInstance for both items.
    /// The displaced item goes exactly where the new item was in inventory.
    /// </summary>
    private bool ExecuteSwapOperation(InventoryItemData newItem, ItemInstance currentItemInstance, ClothingSlot targetSlot)
    {
        string newItemId = newItem.ID;
        string currentItemId = targetSlot.equippedItemId;
        ItemInstance newItemInstance = newItem.ItemInstance;

        if (newItemInstance == null)
        {
            DebugLog($"Cannot execute swap - new item has no ItemInstance");
            return false;
        }

        DebugLog($"Executing same-space swap: {newItem.ItemData.itemName} (from inventory) <-> {currentItemInstance.ItemData.itemName} (from {targetSlot.layer})");

        // Store the original position and rotation where the new item was located
        Vector2Int originalPosition = newItem.GridPosition;
        int originalRotation = newItem.currentRotation;

        DebugLog($"Original inventory position: {originalPosition}, rotation: {originalRotation}");

        // Verify the new item is actually in inventory before trying to remove it
        var inventoryItem = PlayerInventoryManager.Instance.InventoryGridData.GetItem(newItemId);
        if (inventoryItem == null)
        {
            DebugLog($"Same-space swap failed: Item {newItemId} not found in inventory");
            return false;
        }

        // Step 1: Remove new item from inventory
        DebugLog($"Step 1: Removing {newItemId} from inventory position {originalPosition}");
        if (!PlayerInventoryManager.Instance.RemoveItem(newItemId))
        {
            DebugLog($"Same-space swap failed: Could not remove {newItemId} from inventory");
            return false;
        }

        // Step 2: Unequip current item from slot (gets ItemInstance)
        DebugLog($"Step 2: Unequipping {currentItemId} from {targetSlot.layer}");
        string unequippedItemId = targetSlot.UnequipItem();
        if (unequippedItemId != currentItemId)
        {
            DebugLog($"Warning: Unequipped item ID mismatch. Expected: {currentItemId}, Got: {unequippedItemId}");
        }

        // Step 3: Equip new item to slot with its ItemInstance
        DebugLog($"Step 3: Equipping {newItemId} to {targetSlot.layer}");
        targetSlot.EquipItem(newItemId, newItemInstance);

        // Step 4: Place displaced item at the exact same position with ItemInstance
        DebugLog($"Step 4: Placing displaced item {currentItemInstance.ItemData.itemName} at position {originalPosition}");
        if (PlaceDisplacedItemAtOriginalPosition(currentItemInstance, originalPosition))
        {
            // Success! Complete the swap
            InvalidateStatsCache();

            // Fire events
            OnItemSwapped?.Invoke(targetSlot, currentItemId, newItemId);
            OnItemEquipped?.Invoke(targetSlot, newItem);
            OnClothingDataChanged?.Invoke();

            DebugLog($"Same-space swap completed successfully: {newItem.ItemData.itemName} equipped to {targetSlot.layer}, {currentItemInstance.ItemData.itemName} placed at {originalPosition}");
            return true;
        }
        else
        {
            // Rollback: swap failed to place displaced item at original position
            DebugLog($"Same-space swap rollback: Failed to place {currentItemInstance.ItemData.itemName} at original position {originalPosition}");

            // Restore original equipped state
            targetSlot.UnequipItem();
            targetSlot.EquipItem(currentItemId, currentItemInstance);

            // Return new item to inventory at original position with its ItemInstance
            if (!PlayerInventoryManager.Instance.AddItem(newItem, originalPosition))
            {
                // If exact position fails, try any position
                if (!PlayerInventoryManager.Instance.AddItem(newItem))
                {
                    DebugLog($"CRITICAL: Failed to restore {newItem.ItemData.itemName} to inventory during rollback!");
                }
            }
            else
            {
                DebugLog($"Rollback successful: {newItem.ItemData.itemName} returned to original position {originalPosition}");
            }

            return false;
        }
    }

    /// <summary>
    /// REFACTORED: Places the displaced item at the original position with the ItemInstance.
    /// </summary>
    private bool PlaceDisplacedItemAtOriginalPosition(ItemInstance displacedItemInstance, Vector2Int originalPosition)
    {
        var inventory = PlayerInventoryManager.Instance;

        if (displacedItemInstance?.ItemData == null)
        {
            DebugLog("Cannot place displaced item - ItemInstance or ItemData is null");
            return false;
        }

        // Create InventoryItemData with the SAME ItemInstance (not a copy)
        var tempDisplacedItem = new InventoryItemData("temp_swap", displacedItemInstance, originalPosition);

        // Try to add the displaced item back to inventory at the original position
        // The InventoryManager.AddItem method will handle rotation and placement validation
        if (inventory.AddItem(tempDisplacedItem, originalPosition))
        {
            DebugLog($"Successfully placed displaced item {displacedItemInstance.ItemData.itemName} at original position {originalPosition}");
            return true;
        }

        // If that fails, try without specifying position (let InventoryManager find best fit)
        if (inventory.AddItem(tempDisplacedItem))
        {
            DebugLog($"Placed displaced item {displacedItemInstance.ItemData.itemName} at alternative position (original position occupied)");
            return true;
        }

        DebugLog($"Failed to place displaced item {displacedItemInstance.ItemData.itemName} anywhere in inventory");
        return false;
    }

    #endregion

    #region Unequip Methods

    /// <summary>
    /// REFACTORED: Unequips an item from the specified layer and returns it to inventory.
    /// Maintains ItemInstance throughout the operation.
    /// </summary>
    public bool UnequipItemFromLayer(ClothingLayer layer)
    {
        var slot = GetSlot(layer);
        if (slot == null || slot.IsEmpty)
        {
            DebugLog($"Cannot unequip from {layer} - slot empty or not found");
            return false;
        }

        string itemId = slot.equippedItemId;

        // REFACTORED: Get the complete equipped item with ItemInstance
        var equippedItem = slot.GetEquippedItem();

        if (equippedItem?.ItemInstance?.ItemData == null)
        {
            DebugLog($"Cannot unequip {itemId} - item instance data not found");
            return false;
        }

        // Check if inventory has space
        if (!PlayerInventoryManager.Instance.HasSpaceForItem(equippedItem.ItemData))
        {
            DebugLog($"Cannot unequip {equippedItem.ItemData.itemName} - inventory full");
            return false;
        }

        // Unequip from slot
        slot.UnequipItem();
        InvalidateStatsCache();

        // REFACTORED: Add to inventory with the SAME ItemInstance (preserves all state)
        if (PlayerInventoryManager.Instance.AddItem(equippedItem))
        {
            // Success
            OnItemUnequipped?.Invoke(slot, itemId);
            OnClothingDataChanged?.Invoke();

            DebugLog($"Unequipped {equippedItem.ItemData.itemName} from {layer} and returned to inventory [Instance: {equippedItem.ItemInstance.InstanceID}]");
            return true;
        }
        else
        {
            // Failed to add to inventory - restore to slot with ItemInstance
            slot.EquipItem(itemId, equippedItem.ItemInstance);
            InvalidateStatsCache();

            DebugLog($"Failed to return {equippedItem.ItemData.itemName} to inventory - restored to slot");
            return false;
        }
    }

    /// <summary>
    /// Attempts to unequip item and drop it in the world if inventory is full
    /// </summary>
    public bool UnequipAndDropItem(ClothingLayer layer)
    {
        var slot = GetSlot(layer);
        if (slot == null || slot.IsEmpty)
        {
            DebugLog($"Cannot unequip from {layer} - slot empty or not found");
            return false;
        }

        string itemId = slot.equippedItemId;
        var equippedItem = slot.GetEquippedItem();

        if (equippedItem?.ItemInstance?.ItemData == null)
        {
            DebugLog($"Cannot unequip {itemId} - item instance data not found");
            return false;
        }

        // Try to return to inventory first
        if (PlayerInventoryManager.Instance.HasSpaceForItem(equippedItem.ItemData))
        {
            return UnequipItemFromLayer(layer);
        }

        // If inventory is full, drop in world
        if (!equippedItem.ItemData.CanDrop)
        {
            DebugLog($"Cannot drop {equippedItem.ItemData.itemName} - it's a key item");
            return false;
        }

        // Unequip from slot
        slot.UnequipItem();
        InvalidateStatsCache();

        // Drop in world using ItemDropSystem
        if (ItemDropSystem.Instance != null && ItemDropSystem.Instance.DropItem(slot.GetEquippedItemInstance()))
        {
            OnItemUnequipped?.Invoke(slot, itemId);
            OnClothingDataChanged?.Invoke();

            DebugLog($"Unequipped {equippedItem.ItemData.itemName} from {layer} and dropped in world (inventory full)");
            return true;
        }
        else
        {
            // Failed to drop - restore to slot
            slot.EquipItem(itemId, equippedItem.ItemInstance);
            InvalidateStatsCache();

            DebugLog($"Failed to drop {equippedItem.ItemData.itemName} - restored to slot");
            return false;
        }
    }

    #endregion

    #region Accessors

    /// <summary>
    /// Gets the clothing slot for the specified layer
    /// </summary>
    public ClothingSlot GetSlot(ClothingLayer layer)
    {
        return System.Array.Find(clothingSlots, slot => slot.layer == layer);
    }

    /// <summary>
    /// Gets all clothing slots
    /// </summary>
    public ClothingSlot[] GetAllSlots()
    {
        return clothingSlots;
    }

    /// <summary>
    /// Gets all currently equipped items
    /// </summary>
    public List<InventoryItemData> GetEquippedItems()
    {
        var equippedItems = new List<InventoryItemData>();

        foreach (var slot in clothingSlots)
        {
            if (slot.IsOccupied)
            {
                var item = slot.GetEquippedItem();
                if (item != null)
                {
                    equippedItems.Add(item);
                }
            }
        }

        return equippedItems;
    }

    /// <summary>
    /// Checks if an item is currently equipped in any slot
    /// </summary>
    public bool IsItemEquipped(string itemId)
    {
        return System.Array.Exists(clothingSlots, slot => slot.equippedItemId == itemId);
    }

    /// <summary>
    /// Gets the slot where the specified item is equipped, or null if not equipped
    /// </summary>
    public ClothingSlot GetSlotForItem(string itemId)
    {
        return System.Array.Find(clothingSlots, slot => slot.equippedItemId == itemId);
    }

    #endregion

    #region Stats Calculation

    /// <summary>
    /// Gets total defense value from all equipped clothing
    /// </summary>
    public float GetTotalDefense()
    {
        if (!statsCacheValid)
            RecalculateStats();
        return cachedTotalDefense;
    }

    /// <summary>
    /// Gets total warmth value from all equipped clothing
    /// </summary>
    public float GetTotalWarmth()
    {
        if (!statsCacheValid)
            RecalculateStats();
        return cachedTotalWarmth;
    }

    /// <summary>
    /// Gets total rain resistance from all equipped clothing
    /// </summary>
    public float GetTotalRainResistance()
    {
        if (!statsCacheValid)
            RecalculateStats();
        return cachedTotalRainResistance;
    }

    /// <summary>
    /// REFACTORED: Recalculates all clothing stats from equipped items using ItemInstance data.
    /// Now reads from ClothingInstanceData for accurate per-item condition.
    /// </summary>
    private void RecalculateStats()
    {
        cachedTotalDefense = 0f;
        cachedTotalWarmth = 0f;
        cachedTotalRainResistance = 0f;

        foreach (var slot in clothingSlots)
        {
            var templateData = slot.GetEquippedClothingData();
            var instanceData = slot.GetEquippedClothingInstanceData();

            if (templateData != null && instanceData != null)
            {
                // Calculate effective stats using instance condition
                cachedTotalDefense += instanceData.GetEffectiveDefense(templateData);
                cachedTotalWarmth += instanceData.GetEffectiveWarmth(templateData);
                cachedTotalRainResistance += instanceData.GetEffectiveRainResistance(templateData);
            }
        }

        statsCacheValid = true;
        DebugLog($"Recalculated stats - Defense: {cachedTotalDefense:F1}, Warmth: {cachedTotalWarmth:F1}, Rain: {cachedTotalRainResistance:F1}");
    }

    /// <summary>
    /// Invalidates the stats cache, forcing recalculation on next access
    /// </summary>
    private void InvalidateStatsCache()
    {
        statsCacheValid = false;
    }

    #endregion

    #region Damage and Condition

    /// <summary>
    /// REFACTORED: Applies damage to equipped clothing when player takes damage.
    /// Now modifies ClothingInstanceData for each item independently.
    /// </summary>
    public void OnPlayerTakeDamage(float damageAmount)
    {
        // Random chance to damage clothing
        float damageChance = 0.25f; // 25% chance per damage event

        foreach (var slot in clothingSlots)
        {
            if (slot.IsOccupied && UnityEngine.Random.value < damageChance)
            {
                var templateData = slot.GetEquippedClothingData();
                var instanceData = slot.GetEquippedClothingInstanceData();

                if (templateData != null && instanceData != null)
                {
                    float conditionBefore = instanceData.currentCondition;

                    // REFACTORED: Apply damage to instance data
                    instanceData.TakeDamage(templateData);

                    if (instanceData.currentCondition != conditionBefore)
                    {
                        OnClothingConditionChanged?.Invoke(slot.equippedItemId, instanceData.currentCondition);
                        InvalidateStatsCache();

                        DebugLog($"Clothing {slot.equippedItemId} damaged - condition now {instanceData.currentCondition:F1}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// REFACTORED: Repairs a clothing item using instance data.
    /// </summary>
    public bool RepairClothingItem(string itemId, float repairAmount)
    {
        var slot = GetSlotForItem(itemId);
        if (slot == null)
        {
            DebugLog($"Cannot repair {itemId} - item not equipped");
            return false;
        }

        var templateData = slot.GetEquippedClothingData();
        var instanceData = slot.GetEquippedClothingInstanceData();

        if (templateData == null || instanceData == null)
        {
            DebugLog($"Cannot repair {itemId} - no clothing data");
            return false;
        }

        float conditionBefore = instanceData.currentCondition;

        // REFACTORED: Repair using instance data
        bool success = instanceData.Repair(repairAmount, templateData);

        if (success && instanceData.currentCondition != conditionBefore)
        {
            OnClothingConditionChanged?.Invoke(itemId, instanceData.currentCondition);
            InvalidateStatsCache();

            DebugLog($"Repaired {itemId} - condition now {instanceData.currentCondition:F1}");
        }

        return success;
    }

    #endregion

    #region Data Management

    /// <summary>
    /// Sets clothing data directly (used by save system)
    /// </summary>
    public void SetClothingData(ClothingSlot[] newSlots)
    {
        clothingSlots = newSlots ?? new ClothingSlot[0];
        InvalidateStatsCache();
        OnClothingDataChanged?.Invoke();

        DebugLog($"Clothing data set with {clothingSlots.Length} slots");
    }

    /// <summary>
    /// Clears all equipped clothing
    /// </summary>
    public void ClearAllClothing()
    {
        foreach (var slot in clothingSlots)
        {
            if (slot.IsOccupied)
            {
                string itemId = slot.UnequipItem();
                OnItemUnequipped?.Invoke(slot, itemId);
            }
        }

        InvalidateStatsCache();
        OnClothingDataChanged?.Invoke();

        DebugLog("All clothing cleared");
    }

    /// <summary>
    /// Handles inventory item removal - unequip if currently equipped
    /// </summary>
    private void OnInventoryItemRemoved(string itemId)
    {
        var slot = GetSlotForItem(itemId);
        if (slot != null)
        {
            DebugLog($"Item {itemId} removed from inventory - unequipping from {slot.layer}");
            slot.UnequipItem();
            InvalidateStatsCache();
            OnItemUnequipped?.Invoke(slot, itemId);
            OnClothingDataChanged?.Invoke();
        }
    }

    #endregion

    #region Debug Methods


    [Button]
    public void DamageAllClothing(float damageAmount)
    {
        foreach (var slot in clothingSlots)
        {
            if (slot.IsOccupied)
            {
                var templateData = slot.GetEquippedClothingData();
                var instanceData = slot.GetEquippedClothingInstanceData();

                if (templateData != null && instanceData != null)
                {
                    float conditionBefore = instanceData.currentCondition;

                    instanceData.currentCondition = Mathf.Max(0f, instanceData.currentCondition - damageAmount);

                    if (instanceData.currentCondition != conditionBefore)
                    {
                        OnClothingConditionChanged?.Invoke(slot.equippedItemId, instanceData.currentCondition);
                        InvalidateStatsCache();

                        DebugLog($"Clothing {slot.equippedItemId} damaged by debug - condition now {instanceData.currentCondition:F1}");
                    }
                    else
                    {
                        DebugLogWarning($"Clothing {slot.equippedItemId} not damaged by debug - condition unchanged");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Debug method specifically for swap operations
    /// </summary>
    public void DebugSwapOperation(string newItemId, ClothingLayer targetLayer)
    {
        DebugLog($"=== SWAP OPERATION DEBUG for {newItemId} -> {targetLayer} ===");

        // Check inventory item
        var inventoryItem = PlayerInventoryManager.Instance?.InventoryGridData.GetItem(newItemId);
        if (inventoryItem != null)
        {
            DebugLog($"New item found in inventory: {inventoryItem.ItemData?.itemName} at {inventoryItem.GridPosition}");
            DebugLog($"  ItemInstance: {inventoryItem.ItemInstance?.InstanceID}");
        }
        else
        {
            DebugLog($"ERROR: New item {newItemId} NOT found in inventory!");
        }

        // Check target slot
        var targetSlot = GetSlot(targetLayer);
        if (targetSlot != null)
        {
            DebugLog($"Target slot {targetLayer}:");
            DebugLog($"  IsEmpty: {targetSlot.IsEmpty}");
            DebugLog($"  EquippedItemId: {targetSlot.equippedItemId}");

            var currentInstance = targetSlot.GetEquippedItemInstance();
            if (currentInstance != null)
            {
                DebugLog($"  Current ItemInstance: {currentInstance.InstanceID}");
                DebugLog($"  Current ItemData: {currentInstance.ItemData?.itemName}");
            }
            else
            {
                DebugLog($"  Current ItemInstance: NULL");
            }
        }
        else
        {
            DebugLog($"ERROR: Target slot {targetLayer} not found!");
        }

        // Check inventory space
        if (inventoryItem?.ItemInstance != null && targetSlot?.GetEquippedItemInstance() != null)
        {
            var targetInstance = targetSlot.GetEquippedItemInstance();
            bool hasSpace = PlayerInventoryManager.Instance.HasSpaceForItem(targetInstance.ItemData);
            DebugLog($"Inventory has space for displaced item: {hasSpace}");
        }
    }

    [Button("Clear All Clothing")]
    private void DebugClearAllClothing()
    {
        ClearAllClothing();
    }

    [Button("Damage Random Clothing")]
    private void DebugDamageRandomClothing()
    {
        OnPlayerTakeDamage(10f);
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[ClothingManager] {message}");
        }
    }

    private void DebugLogWarning(string message)
    {
        if (showDebugLogs)
        {
            Debug.LogWarning($"[ClothingManager] {message}");
        }
    }

    #endregion
}