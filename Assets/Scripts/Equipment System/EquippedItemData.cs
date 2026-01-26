using UnityEngine;

/// <summary>
/// Data for currently equipped item
/// REFACTORED: Full ItemInstance integration with validation
/// UPDATED: Now tracks inventory item ID for proper removal operations
/// </summary>
[System.Serializable]
public class EquippedItemData
{
    [Header("Equipment State")]
    public bool isEquipped;
    public string equippedItemId;
    public string equippedItemDataName;

    [Header("Instance Tracking")]
    public string equippedItemInstanceId;
    [System.NonSerialized]
    private ItemInstance cachedItemInstance;

    [Header("Inventory Reference")]
    public string inventoryItemId; // CRITICAL: Links to InventoryItemData.ID for removal operations

    [Header("Source Info")]
    public bool isEquippedFromHotkey;
    public int sourceHotkeySlot = -1;

    public EquippedItemData()
    {
        Clear();
    }

    public EquippedItemData(EquippedItemData other)
    {
        isEquipped = other.isEquipped;
        equippedItemId = other.equippedItemId;
        equippedItemDataName = other.equippedItemDataName;
        equippedItemInstanceId = other.equippedItemInstanceId;
        cachedItemInstance = null; // Rebuilt at runtime
        inventoryItemId = other.inventoryItemId;
        isEquippedFromHotkey = other.isEquippedFromHotkey;
        sourceHotkeySlot = other.sourceHotkeySlot;

        if (isEquipped)
        {
            Debug.Log($"[EquippedItemData] Copy: {equippedItemDataName} (ID: {equippedItemId}, InstanceID: {equippedItemInstanceId}, InventoryID: {inventoryItemId})");
        }
    }

    /// <summary>
    /// Clear equipped item state
    /// </summary>
    public void Clear()
    {
        isEquipped = false;
        equippedItemId = "";
        equippedItemDataName = "";
        equippedItemInstanceId = "";
        cachedItemInstance = null;
        inventoryItemId = "";
        isEquippedFromHotkey = false;
        sourceHotkeySlot = -1;
    }

    /// <summary>
    /// Equip item from inventory with instance validation
    /// </summary>
    public bool EquipFromInventory(string itemId, ItemData itemData)
    {
        equippedItemId = itemId;
        equippedItemDataName = itemData.name;
        inventoryItemId = itemId; // Store inventory item ID
        isEquippedFromHotkey = false;
        sourceHotkeySlot = -1;

        if (!RefreshInstanceCache())
        {
            Debug.LogWarning($"[EquippedItemData] Cannot equip {itemData.name} - ItemInstance not found");
            Clear();
            return false;
        }

        isEquipped = true;
        return true;
    }

    /// <summary>
    /// Equip item from hotkey with instance validation
    /// UPDATED: Now stores inventory item ID for proper removal
    /// </summary>
    public bool EquipFromHotkey(string itemId, ItemData itemData, int hotkeySlot)
    {
        equippedItemId = itemId;
        equippedItemDataName = itemData.name;
        inventoryItemId = itemId; // CRITICAL: Store inventory item ID for removal operations
        isEquippedFromHotkey = true;
        sourceHotkeySlot = hotkeySlot;

        if (!RefreshInstanceCache())
        {
            Debug.LogWarning($"[EquippedItemData] Cannot equip {itemData.name} from hotkey {hotkeySlot} - ItemInstance not found");
            Clear();
            return false;
        }

        isEquipped = true;
        return true;
    }

    /// <summary>
    /// Refresh cached ItemInstance from inventory
    /// </summary>
    public bool RefreshInstanceCache()
    {
        if (!isEquipped || string.IsNullOrEmpty(equippedItemId))
        {
            cachedItemInstance = null;
            equippedItemInstanceId = "";

            //commented out as this prevents equipping items
            //return false;
        }

        if (PlayerInventoryManager.Instance == null)
        {
            Debug.LogWarning($"[EquippedItemData] Cannot refresh cache - PlayerInventoryManager not available");
            cachedItemInstance = null;
            return false;
        }

        var inventoryItem = PlayerInventoryManager.Instance.InventoryGridData.GetItem(equippedItemId);
        if (inventoryItem?.ItemInstance != null)
        {
            cachedItemInstance = inventoryItem.ItemInstance;
            equippedItemInstanceId = inventoryItem.ItemInstance.InstanceID;
            return true;
        }

        Debug.LogWarning($"[EquippedItemData] Cannot refresh cache - item {equippedItemId} not found or has no ItemInstance");
        cachedItemInstance = null;
        equippedItemInstanceId = "";
        return false;
    }

    /// <summary>
    /// Get ItemInstance with automatic cache refresh if needed
    /// PRIMARY accessor for instance data
    /// </summary>
    public ItemInstance GetItemInstance()
    {
        if (!isEquipped || string.IsNullOrEmpty(equippedItemDataName))
            return null;

        // Return cache if valid
        if (cachedItemInstance != null && cachedItemInstance.InstanceID == equippedItemInstanceId)
            return cachedItemInstance;

        // Cache stale - refresh
        if (RefreshInstanceCache())
            return cachedItemInstance;

        return null;
    }

    /// <summary>
    /// Invalidate cache (call when item removed from inventory)
    /// </summary>
    public void InvalidateInstanceCache()
    {
        cachedItemInstance = null;
    }

    /// <summary>
    /// Get ItemData template (read-only)
    /// Simplified - goes through instance
    /// </summary>
    public ItemData GetItemData()
    {
        if (!isEquipped || string.IsNullOrEmpty(equippedItemDataName))
            return null;

        var instance = GetItemInstance();
        if (instance != null)
            return instance.ItemData;

        // Fallback: Resources load (for edge cases)
        return Resources.Load<ItemData>(SaveManager.Instance.itemDataPath + equippedItemDataName);
    }

    /// <summary>
    /// Check if specific item is equipped
    /// </summary>
    public bool IsEquipped(string itemId)
    {
        return isEquipped && equippedItemId == itemId;
    }

    /// <summary>
    /// Check if instance cache is valid
    /// </summary>
    public bool IsInstanceCacheValid()
    {
        return cachedItemInstance != null && cachedItemInstance.InstanceID == equippedItemInstanceId;
    }
}