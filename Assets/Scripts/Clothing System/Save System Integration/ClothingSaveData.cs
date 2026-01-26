using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// REFACTORED: Complete clothing system state for saving/loading with ItemInstance support.
/// Now properly stores ItemInstance data including ClothingInstanceData for accurate restoration.
/// </summary>
[System.Serializable]
public class ClothingSaveData
{
    [Header("Clothing Slots")]
    public List<ClothingSlotSaveData> slots = new List<ClothingSlotSaveData>();

    public ClothingSaveData()
    {
        slots = new List<ClothingSlotSaveData>();
    }

    /// <summary>
    /// Add a clothing slot to the save data
    /// </summary>
    public void AddSlot(ClothingSlotSaveData slotData)
    {
        if (slotData != null)
        {
            slots.Add(slotData);
        }
    }

    /// <summary>
    /// Remove a slot by layer
    /// </summary>
    public bool RemoveSlot(ClothingLayer layer)
    {
        for (int i = slots.Count - 1; i >= 0; i--)
        {
            if (slots[i].layer == layer)
            {
                slots.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Get slot save data by layer
    /// </summary>
    public ClothingSlotSaveData GetSlot(ClothingLayer layer)
    {
        foreach (var slot in slots)
        {
            if (slot.layer == layer)
                return slot;
        }
        return null;
    }

    /// <summary>
    /// Clear all slot data
    /// </summary>
    public void Clear()
    {
        slots.Clear();
    }

    /// <summary>
    /// Get count of equipped items (non-empty slots)
    /// </summary>
    public int GetEquippedCount()
    {
        int count = 0;
        foreach (var slot in slots)
        {
            if (!string.IsNullOrEmpty(slot.equippedItemId))
                count++;
        }
        return count;
    }

    /// <summary>
    /// Get total number of clothing slots
    /// </summary>
    public int SlotCount => slots.Count;

    /// <summary>
    /// Check if an item is equipped in any slot
    /// </summary>
    public bool IsItemEquipped(string itemId)
    {
        foreach (var slot in slots)
        {
            if (slot.equippedItemId == itemId)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Get the layer where an item is equipped, or null if not equipped
    /// </summary>
    public ClothingLayer? GetLayerForItem(string itemId)
    {
        foreach (var slot in slots)
        {
            if (slot.equippedItemId == itemId)
                return slot.layer;
        }
        return null;
    }

    /// <summary>
    /// Get all equipped item IDs
    /// </summary>
    public List<string> GetAllEquippedItemIds()
    {
        var equippedIds = new List<string>();
        foreach (var slot in slots)
        {
            if (!string.IsNullOrEmpty(slot.equippedItemId))
                equippedIds.Add(slot.equippedItemId);
        }
        return equippedIds;
    }

    /// <summary>
    /// REFACTORED: Get all equipped ItemData names for restoration
    /// </summary>
    public List<string> GetAllEquippedItemDataNames()
    {
        var itemDataNames = new List<string>();
        foreach (var slot in slots)
        {
            if (!string.IsNullOrEmpty(slot.equippedItemDataName))
                itemDataNames.Add(slot.equippedItemDataName);
        }
        return itemDataNames;
    }

    /// <summary>
    /// Validate that the save data is consistent
    /// </summary>
    public bool IsValid()
    {
        // Check for duplicate layers
        var seenLayers = new HashSet<ClothingLayer>();
        foreach (var slot in slots)
        {
            if (seenLayers.Contains(slot.layer))
                return false;
            seenLayers.Add(slot.layer);
        }

        // Check for duplicate equipped items
        var seenItems = new HashSet<string>();
        foreach (var slot in slots)
        {
            if (!string.IsNullOrEmpty(slot.equippedItemId))
            {
                if (seenItems.Contains(slot.equippedItemId))
                    return false;
                seenItems.Add(slot.equippedItemId);
            }
        }

        return true;
    }

    /// <summary>
    /// REFACTORED: Gets debug information about the clothing save data including ItemInstance info.
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== Clothing Save Data Debug Info ===");
        info.AppendLine($"Total Slots: {slots.Count}");
        info.AppendLine($"Equipped Items: {GetEquippedCount()}");

        foreach (var slot in slots)
        {
            if (string.IsNullOrEmpty(slot.equippedItemId))
            {
                info.AppendLine($"  {slot.layer}: Empty");
            }
            else
            {
                string itemDataInfo = !string.IsNullOrEmpty(slot.equippedItemDataName)
                    ? $" ({slot.equippedItemDataName})"
                    : " (No ItemData name)";

                string instanceInfo = !string.IsNullOrEmpty(slot.instanceID)
                    ? $" [Instance: {slot.instanceID}]"
                    : " [No InstanceID]";

                string conditionInfo = slot.clothingInstanceData != null
                    ? $" Condition: {slot.clothingInstanceData.currentCondition:F1}"
                    : " (No instance data)";

                info.AppendLine($"  {slot.layer}: {slot.equippedItemId}{itemDataInfo}{instanceInfo}{conditionInfo}");
            }
        }

        return info.ToString();
    }

    /// <summary>
    /// Merge data from another ClothingSaveData instance
    /// </summary>
    public void MergeFrom(ClothingSaveData other, bool overwriteExisting = true)
    {
        if (other == null) return;

        foreach (var otherSlot in other.slots)
        {
            var existingSlot = GetSlot(otherSlot.layer);
            if (existingSlot != null)
            {
                if (overwriteExisting)
                {
                    existingSlot.equippedItemId = otherSlot.equippedItemId;
                    existingSlot.equippedItemDataName = otherSlot.equippedItemDataName;
                    existingSlot.instanceID = otherSlot.instanceID;
                    existingSlot.clothingInstanceData = otherSlot.clothingInstanceData;
                }
            }
            else
            {
                AddSlot(otherSlot.CreateCopy());
            }
        }
    }
}

/// <summary>
/// REFACTORED: Save data for an individual clothing slot with complete ItemInstance state.
/// Now includes InstanceID and ClothingInstanceData for full state restoration.
/// </summary>
[System.Serializable]
public class ClothingSlotSaveData
{
    [Header("Slot Identity")]
    public ClothingLayer layer;

    [Header("Equipped Item")]
    public string equippedItemId = "";

    [Header("ItemData Reference")]
    public string equippedItemDataName = ""; // Template name for loading

    [Header("ItemInstance Data")]
    public string instanceID = ""; // ItemInstance.InstanceID
    public ClothingInstanceData clothingInstanceData; // Instance-specific state

    /// <summary>
    /// Checks if this slot is empty
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(equippedItemId);

    /// <summary>
    /// Checks if this slot has an item equipped
    /// </summary>
    public bool IsOccupied => !IsEmpty;

    /// <summary>
    /// REFACTORED: Create save data from a ClothingSlot with complete ItemInstance data.
    /// </summary>
    public static ClothingSlotSaveData FromClothingSlot(ClothingSlot clothingSlot)
    {
        if (clothingSlot == null)
        {
            Debug.LogError("Cannot create ClothingSlotSaveData - ClothingSlot is null");
            return null;
        }

        var saveData = new ClothingSlotSaveData
        {
            layer = clothingSlot.layer,
            equippedItemId = clothingSlot.equippedItemId
        };

        // REFACTORED: Store complete ItemInstance data if item is equipped
        if (!clothingSlot.IsEmpty)
        {
            var itemInstance = clothingSlot.GetEquippedItemInstance();
            if (itemInstance != null)
            {
                // Store ItemData name for template lookup
                if (itemInstance.ItemData != null)
                {
                    saveData.equippedItemDataName = itemInstance.ItemData.name;
                }

                // Store ItemInstance ID
                saveData.instanceID = itemInstance.InstanceID;

                // Store ClothingInstanceData (includes condition, etc.)
                saveData.clothingInstanceData = itemInstance.ClothingInstanceData?.CreateCopy();

                Debug.Log($"[ClothingSlotSaveData] Saved {clothingSlot.equippedItemId}: " +
                         $"Template={saveData.equippedItemDataName}, Instance={saveData.instanceID}, " +
                         $"Condition={saveData.clothingInstanceData?.currentCondition:F1}");
            }
            else
            {
                Debug.LogWarning($"Equipped item {clothingSlot.equippedItemId} has no ItemInstance - save may be incomplete");
            }
        }

        return saveData;
    }

    /// <summary>
    /// REFACTORED: Apply this save data to a ClothingSlot with complete ItemInstance restoration.
    /// </summary>
    public void ApplyToClothingSlot(ClothingSlot clothingSlot)
    {
        if (clothingSlot == null)
        {
            Debug.LogError("Cannot apply save data - ClothingSlot is null");
            return;
        }

        if (clothingSlot.layer != layer)
        {
            Debug.LogError($"Cannot apply save data - layer mismatch. Expected {layer}, got {clothingSlot.layer}");
            return;
        }

        if (IsEmpty)
        {
            clothingSlot.UnequipItem();
        }
        else
        {
            // REFACTORED: Restore complete ItemInstance with saved state
            ItemInstance restoredInstance = RestoreItemInstance();

            if (restoredInstance != null)
            {
                clothingSlot.EquipItem(equippedItemId, restoredInstance);
                Debug.Log($"[ClothingSlotSaveData] Restored {equippedItemId} to {layer} with Instance {instanceID}");
            }
            else
            {
                Debug.LogError($"Failed to restore ItemInstance for {equippedItemId} in slot {layer}");
            }
        }
    }

    /// <summary>
    /// REFACTORED: Restore ItemInstance from save data with complete state.
    /// </summary>
    private ItemInstance RestoreItemInstance()
    {
        if (string.IsNullOrEmpty(equippedItemDataName))
        {
            Debug.LogWarning("Cannot restore ItemInstance - no ItemData name");
            return null;
        }

        // Load the ItemData template
        ItemData itemData = LoadItemDataByName(equippedItemDataName);
        if (itemData == null)
        {
            Debug.LogError($"Cannot restore ItemInstance - ItemData '{equippedItemDataName}' not found");
            return null;
        }

        // Create ItemInstance with saved InstanceID
        ItemInstance instance = new ItemInstance(equippedItemDataName, instanceID);

        // Restore ClothingInstanceData if available
        if (clothingInstanceData != null)
        {
            // Use reflection to set the private instance data field
            var instanceType = typeof(ItemInstance);
            var field = instanceType.GetField("clothingInstanceData",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(instance, clothingInstanceData);
                Debug.Log($"[ClothingSlotSaveData] Restored ClothingInstanceData - Condition: {clothingInstanceData.currentCondition:F1}");
            }
            else
            {
                Debug.LogWarning("Could not find clothingInstanceData field via reflection");
            }
        }
        else
        {
            Debug.LogWarning($"No ClothingInstanceData to restore for {equippedItemId}");
        }

        return instance;
    }

    /// <summary>
    /// Load ItemData by name for restoration
    /// </summary>
    private ItemData LoadItemDataByName(string itemDataName)
    {
        if (string.IsNullOrEmpty(itemDataName))
            return null;

        // Try to load from Resources
        string resourcePath = $"{SaveManager.Instance?.itemDataPath ?? "Data/Items/"}{itemDataName}";
        ItemData itemData = Resources.Load<ItemData>(resourcePath);

        if (itemData != null)
            return itemData;

        // Fallback: Search all ItemData assets
        ItemData[] allItemData = Resources.FindObjectsOfTypeAll<ItemData>();
        foreach (var data in allItemData)
        {
            if (data.name == itemDataName)
                return data;
        }

        return null;
    }

    /// <summary>
    /// Validate that this slot save data is valid
    /// </summary>
    public bool IsValid()
    {
        // Layer must be a valid enum value
        if (!System.Enum.IsDefined(typeof(ClothingLayer), layer))
            return false;

        // If equipped item ID is not empty, it should be a valid format
        if (!string.IsNullOrEmpty(equippedItemId) && equippedItemId.Trim().Length == 0)
            return false;

        // REFACTORED: Check ItemInstance data consistency
        if (!string.IsNullOrEmpty(equippedItemId))
        {
            if (string.IsNullOrEmpty(equippedItemDataName))
            {
                Debug.LogWarning($"Equipped item {equippedItemId} has no ItemData name - may cause restoration issues");
            }

            if (string.IsNullOrEmpty(instanceID))
            {
                Debug.LogWarning($"Equipped item {equippedItemId} has no InstanceID - may cause restoration issues");
            }

            if (clothingInstanceData == null)
            {
                Debug.LogWarning($"Equipped item {equippedItemId} has no ClothingInstanceData - may lose condition state");
            }
        }

        return true;
    }

    /// <summary>
    /// REFACTORED: Get a debug string representation with complete ItemInstance info.
    /// </summary>
    public override string ToString()
    {
        if (IsEmpty)
            return $"Slot[{layer}] = Empty";

        string itemDataInfo = !string.IsNullOrEmpty(equippedItemDataName)
            ? $" ({equippedItemDataName})"
            : "";

        string instanceInfo = !string.IsNullOrEmpty(instanceID)
            ? $" [Instance: {instanceID}]"
            : "";

        string conditionInfo = clothingInstanceData != null
            ? $" Condition: {clothingInstanceData.currentCondition:F1}"
            : "";

        return $"Slot[{layer}] = {equippedItemId}{itemDataInfo}{instanceInfo}{conditionInfo}";
    }

    /// <summary>
    /// REFACTORED: Creates a copy of this slot save data with complete ItemInstance data.
    /// </summary>
    public ClothingSlotSaveData CreateCopy()
    {
        return new ClothingSlotSaveData
        {
            layer = this.layer,
            equippedItemId = this.equippedItemId,
            equippedItemDataName = this.equippedItemDataName,
            instanceID = this.instanceID,
            clothingInstanceData = this.clothingInstanceData?.CreateCopy()
        };
    }
}