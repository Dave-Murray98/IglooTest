using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Hotkey slot assignment with ItemInstance tracking
/// REFACTORED: Each stacked item maintains independent instance
/// </summary>
[System.Serializable]
public class HotkeyBinding
{
    [Header("Slot Info")]
    public int slotNumber;
    public bool isAssigned;

    [Header("Item Reference")]
    public string itemId;
    public string itemDataName;

    [Header("Instance Tracking")]
    public string itemInstanceId;
    [System.NonSerialized]
    private ItemInstance cachedItemInstance;

    [Header("Stack Management")]
    public List<string> stackedItemIds = new List<string>();
    public int currentStackIndex = 0;

    public HotkeyBinding(int slot)
    {
        slotNumber = slot;
        isAssigned = false;
        itemId = "";
        itemDataName = "";
        itemInstanceId = "";
        cachedItemInstance = null;
        stackedItemIds = new List<string>();
        currentStackIndex = 0;
    }

    public HotkeyBinding(HotkeyBinding other)
    {
        slotNumber = other.slotNumber;
        isAssigned = other.isAssigned;
        itemId = other.itemId;
        itemDataName = other.itemDataName;
        itemInstanceId = other.itemInstanceId;
        cachedItemInstance = null; // Rebuilt at runtime
        stackedItemIds = new List<string>(other.stackedItemIds);
        currentStackIndex = other.currentStackIndex;

        if (isAssigned)
        {
            Debug.Log($"[HotkeyBinding] Copy: Slot {slotNumber} = {itemDataName} (ID: {itemId}, InstanceID: {itemInstanceId})");
        }
    }

    /// <summary>
    /// Assign item with instance validation
    /// </summary>
    public bool AssignItem(string newItemId, string newItemDataName)
    {
        ClearSlot();
        RemoveItemFromOtherHotkeys(newItemId);

        itemId = newItemId;
        itemDataName = newItemDataName;

        // CRITICAL: Validate instance exists
        if (!RefreshInstanceCache())
        {
            Debug.LogWarning($"[HotkeyBinding] Cannot assign to slot {slotNumber} - ItemInstance not found for {newItemDataName}");
            ClearSlot();
            return false;
        }

        isAssigned = true;
        stackedItemIds.Add(newItemId);
        currentStackIndex = 0;

        FindAndStackIdenticalConsumables();
        return true;
    }

    /// <summary>
    /// Refresh cached ItemInstance from inventory
    /// </summary>
    public bool RefreshInstanceCache()
    {
        if (!isAssigned || string.IsNullOrEmpty(itemId))
        {
            //Debug.LogWarning($"[HotkeyBinding] Cannot refresh cache - slot {slotNumber} is unassigned");
            cachedItemInstance = null;
            itemInstanceId = "";
            //commented out as this prevents the player from assigning items to an empty hotkey slot
            //return false;
        }

        if (PlayerInventoryManager.Instance == null)
        {
            Debug.LogWarning($"[HotkeyBinding] Cannot refresh cache - PlayerInventoryManager not available");
            cachedItemInstance = null;
            return false;
        }

        var inventoryItem = PlayerInventoryManager.Instance.InventoryGridData.GetItem(itemId);
        if (inventoryItem?.ItemInstance != null)
        {
            cachedItemInstance = inventoryItem.ItemInstance;
            itemInstanceId = inventoryItem.ItemInstance.InstanceID;
            return true;
        }

        Debug.LogWarning($"[HotkeyBinding] Cannot refresh cache - item {itemId} not found or has no ItemInstance");
        cachedItemInstance = null;
        itemInstanceId = "";
        return false;
    }

    /// <summary>
    /// Get current ItemInstance with auto-refresh
    /// PRIMARY accessor for instance data
    /// </summary>
    public ItemInstance GetCurrentItemInstance()
    {
        if (!isAssigned)
            return null;

        // Return cache if valid
        if (cachedItemInstance != null && cachedItemInstance.InstanceID == itemInstanceId)
            return cachedItemInstance;

        // Cache stale - refresh
        if (RefreshInstanceCache())
            return cachedItemInstance;

        return null;
    }

    /// <summary>
    /// Invalidate cache (call when item removed)
    /// </summary>
    public void InvalidateInstanceCache()
    {
        cachedItemInstance = null;
    }

    /// <summary>
    /// Get ItemData template (read-only)
    /// Simplified - goes through instance
    /// </summary>
    public ItemData GetCurrentItemData()
    {
        if (!isAssigned || string.IsNullOrEmpty(itemDataName))
            return null;

        var instance = GetCurrentItemInstance();
        if (instance != null)
            return instance.ItemData;

        // Fallback: Resources load
        return Resources.Load<ItemData>(SaveManager.Instance.itemDataPath + itemDataName);
    }

    /// <summary>
    /// Remove item from other hotkey slots (unique assignment)
    /// </summary>
    private void RemoveItemFromOtherHotkeys(string itemIdToRemove)
    {
        if (EquippedItemManager.Instance == null) return;

        var allBindings = EquippedItemManager.Instance.GetAllHotkeyBindings();
        foreach (var binding in allBindings)
        {
            if (binding != this && binding.isAssigned)
            {
                if (binding.stackedItemIds.Contains(itemIdToRemove))
                {
                    binding.RemoveItem(itemIdToRemove);
                    bool wasCleared = !binding.isAssigned;

                    if (wasCleared)
                        EquippedItemManager.Instance.OnHotkeyCleared?.Invoke(binding.slotNumber);
                    else
                        EquippedItemManager.Instance.OnHotkeyAssigned?.Invoke(binding.slotNumber, binding);
                }
            }
        }
    }

    /// <summary>
    /// Auto-stack identical consumables
    /// Each maintains independent ItemInstance
    /// </summary>
    private void FindAndStackIdenticalConsumables()
    {
        if (PlayerInventoryManager.Instance == null) return;

        var itemData = GetCurrentItemData();
        if (itemData == null || itemData.itemType != ItemType.Consumable) return;

        var inventoryItems = PlayerInventoryManager.Instance.InventoryGridData.GetAllItems();
        foreach (var inventoryItem in inventoryItems)
        {
            if (stackedItemIds.Contains(inventoryItem.ID)) continue;

            if (inventoryItem.ItemData != null && inventoryItem.ItemData.name == itemDataName)
            {
                stackedItemIds.Add(inventoryItem.ID);
            }
        }

        Debug.Log($"Hotkey {slotNumber}: Stacked {stackedItemIds.Count} {itemDataName} (each with unique instance)");
    }

    /// <summary>
    /// Remove item from stack with instance cleanup
    /// </summary>
    public bool RemoveItem(string itemIdToRemove)
    {
        bool removed = stackedItemIds.Remove(itemIdToRemove);

        if (removed)
        {
            if (itemId == itemIdToRemove)
            {
                InvalidateInstanceCache();

                if (stackedItemIds.Count > 0)
                {
                    currentStackIndex = Mathf.Clamp(currentStackIndex, 0, stackedItemIds.Count - 1);
                    itemId = stackedItemIds[currentStackIndex];
                    RefreshInstanceCache();
                    Debug.Log($"Hotkey {slotNumber}: Switched to next {itemDataName} in stack ({stackedItemIds.Count} remaining)");
                }
                else
                {
                    Debug.Log($"Hotkey {slotNumber}: No more {itemDataName} items, clearing slot");
                    ClearSlot();
                }
            }
            else
            {
                currentStackIndex = stackedItemIds.IndexOf(itemId);
            }
        }

        return removed;
    }

    /// <summary>
    /// Clear slot completely
    /// </summary>
    public void ClearSlot()
    {
        itemId = "";
        itemDataName = "";
        itemInstanceId = "";
        cachedItemInstance = null;
        isAssigned = false;
        stackedItemIds.Clear();
        currentStackIndex = 0;
    }

    /// <summary>
    /// Add to stack if matching type
    /// </summary>
    public bool TryAddToStack(string newItemId, string newItemDataName)
    {
        if (!isAssigned || itemDataName != newItemDataName) return false;

        var itemData = GetCurrentItemData();
        if (itemData == null || itemData.itemType != ItemType.Consumable) return false;
        if (stackedItemIds.Contains(newItemId)) return false;

        stackedItemIds.Add(newItemId);
        Debug.Log($"Hotkey {slotNumber}: Added {itemDataName} to stack ({stackedItemIds.Count} total)");
        return true;
    }

    /// <summary>
    /// Check if stack has multiple items
    /// </summary>
    public bool HasMultipleItems => stackedItemIds.Count > 1;

    /// <summary>
    /// Get stack info string
    /// </summary>
    public string GetStackInfo()
    {
        if (!isAssigned || stackedItemIds.Count <= 1) return "";
        return $"{currentStackIndex + 1}/{stackedItemIds.Count}";
    }
}