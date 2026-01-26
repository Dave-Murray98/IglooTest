using UnityEngine;

/// <summary>
/// Save data for the oxygen tank slot with complete ItemInstance state.
/// Includes InstanceID and OxygenTankInstanceData for full state restoration.
/// Pattern based on ClothingSlotSaveData.cs.
/// </summary>
[System.Serializable]
public class OxygenTankSlotSaveData
{
    [Header("Debug")]
    private bool enableDebugLogs = false;

    [Header("Equipped Tank")]
    public string equippedTankId = "";

    [Header("ItemData Reference")]
    public string equippedTankDataName = ""; // Template name for loading

    [Header("ItemInstance Data")]
    public string instanceID = ""; // ItemInstance.InstanceID
    public OxygenTankInstanceData tankInstanceData; // Instance-specific state (current oxygen)

    /// <summary>
    /// Checks if this slot is empty
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(equippedTankId);

    /// <summary>
    /// Checks if this slot has a tank equipped
    /// </summary>
    public bool IsOccupied => !IsEmpty;


    /// <summary>
    /// Create save data from an OxygenTankSlot with complete ItemInstance data.
    /// </summary>
    public static OxygenTankSlotSaveData FromOxygenTankSlot(OxygenTankSlot tankSlot)
    {
        if (tankSlot == null)
        {
            Debug.LogError("Cannot create OxygenTankSlotSaveData - OxygenTankSlot is null");
            return null;
        }

        var saveData = new OxygenTankSlotSaveData
        {
            equippedTankId = tankSlot.equippedTankId
        };

        // Store complete ItemInstance data if tank is equipped
        if (!tankSlot.IsEmpty)
        {
            var tankInstance = tankSlot.GetEquippedTankInstance();
            if (tankInstance != null)
            {
                // Store ItemData name for template lookup
                if (tankInstance.ItemData != null)
                {
                    saveData.equippedTankDataName = tankInstance.ItemData.name;
                }

                // Store ItemInstance ID
                saveData.instanceID = tankInstance.InstanceID;

                // Store OxygenTankInstanceData (includes current oxygen level)
                saveData.tankInstanceData = tankInstance.OxygenTankInstanceData?.CreateCopy();
            }
            else
            {
                Debug.LogWarning($"Equipped tank {tankSlot.equippedTankId} has no ItemInstance - save may be incomplete");
            }
        }

        return saveData;
    }

    /// <summary>
    /// Apply this save data to an OxygenTankSlot with complete ItemInstance restoration.
    /// </summary>
    public void ApplyToOxygenTankSlot(OxygenTankSlot tankSlot)
    {
        if (tankSlot == null)
        {
            Debug.LogError("Cannot apply save data - OxygenTankSlot is null");
            return;
        }

        if (IsEmpty)
        {
            tankSlot.UnequipTank();
        }
        else
        {
            // Restore complete ItemInstance with saved state
            ItemInstance restoredInstance = RestoreItemInstance();

            if (restoredInstance != null)
            {
                tankSlot.EquipTank(equippedTankId, restoredInstance);
                DebugLog($"[OxygenTankSlotSaveData] Restored {equippedTankId} with Instance {instanceID}");
            }
            else
            {
                Debug.LogError($"Failed to restore ItemInstance for {equippedTankId}");
            }
        }
    }

    /// <summary>
    /// Restore ItemInstance from save data with complete state.
    /// </summary>
    private ItemInstance RestoreItemInstance()
    {
        if (string.IsNullOrEmpty(equippedTankDataName))
        {
            Debug.LogWarning("Cannot restore ItemInstance - no ItemData name");
            return null;
        }

        // Load the ItemData template
        ItemData itemData = LoadItemDataByName(equippedTankDataName);
        if (itemData == null)
        {
            Debug.LogError($"Cannot restore ItemInstance - ItemData '{equippedTankDataName}' not found");
            return null;
        }

        // Create ItemInstance with saved InstanceID
        ItemInstance instance = new ItemInstance(equippedTankDataName, instanceID);

        // Restore OxygenTankInstanceData if available
        if (tankInstanceData != null)
        {
            // Use reflection to set the private instance data field
            var instanceType = typeof(ItemInstance);
            var field = instanceType.GetField("oxygenTankInstanceData",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(instance, tankInstanceData);
                DebugLog($"[OxygenTankSlotSaveData] Restored OxygenTankInstanceData - Oxygen: {tankInstanceData.currentOxygen:F1}");
            }
            else
            {
                Debug.LogWarning("Could not find oxygenTankInstanceData field via reflection");
            }
        }
        else
        {
            Debug.LogWarning($"No OxygenTankInstanceData to restore for {equippedTankId}");
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
        // If equipped tank ID is not empty, it should be a valid format
        if (!string.IsNullOrEmpty(equippedTankId) && equippedTankId.Trim().Length == 0)
            return false;

        // Check ItemInstance data consistency
        if (!string.IsNullOrEmpty(equippedTankId))
        {
            if (string.IsNullOrEmpty(equippedTankDataName))
            {
                Debug.LogWarning($"Equipped tank {equippedTankId} has no ItemData name - may cause restoration issues");
            }

            if (string.IsNullOrEmpty(instanceID))
            {
                Debug.LogWarning($"Equipped tank {equippedTankId} has no InstanceID - may cause restoration issues");
            }

            if (tankInstanceData == null)
            {
                Debug.LogWarning($"Equipped tank {equippedTankId} has no OxygenTankInstanceData - may lose oxygen state");
            }
        }

        return true;
    }

    /// <summary>
    /// Get a debug string representation with complete ItemInstance info.
    /// </summary>
    public override string ToString()
    {
        if (IsEmpty)
            return "OxygenTankSlot = Empty";

        string itemDataInfo = !string.IsNullOrEmpty(equippedTankDataName)
            ? $" ({equippedTankDataName})"
            : "";

        string instanceInfo = !string.IsNullOrEmpty(instanceID)
            ? $" [Instance: {instanceID}]"
            : "";

        string oxygenInfo = tankInstanceData != null
            ? $" Oxygen: {tankInstanceData.currentOxygen:F1}"
            : "";

        return $"OxygenTankSlot = {equippedTankId}{itemDataInfo}{instanceInfo}{oxygenInfo}";
    }

    /// <summary>
    /// Creates a copy of this slot save data with complete ItemInstance data.
    /// </summary>
    public OxygenTankSlotSaveData CreateCopy()
    {
        return new OxygenTankSlotSaveData
        {
            equippedTankId = this.equippedTankId,
            equippedTankDataName = this.equippedTankDataName,
            instanceID = this.instanceID,
            tankInstanceData = this.tankInstanceData?.CreateCopy()
        };
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[OxygenTankSlotSaveData] {message}");
    }
}