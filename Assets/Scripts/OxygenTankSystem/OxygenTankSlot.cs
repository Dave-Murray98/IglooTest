using UnityEngine;

/// <summary>
/// Represents the single oxygen tank equipment slot.
/// Stores the equipped tank's ItemInstance for complete state preservation.
/// Pattern based on ClothingSlot.cs but simplified for single-slot usage.
/// </summary>
[System.Serializable]
public class OxygenTankSlot
{
    [Header("Slot Configuration")]
    [Tooltip("Display name for this slot in the UI")]
    public string displayName = "Oxygen Tank";

    [Header("Current State")]
    [Tooltip("ID of the currently equipped tank (empty if none)")]
    public string equippedTankId = "";

    [Header("Equipped Tank Instance")]
    [Tooltip("ItemInstance of the currently equipped tank - preserves all state")]
    [SerializeField] private ItemInstance equippedTankInstance;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    /// <summary>
    /// Checks if this slot is currently empty
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(equippedTankId);

    /// <summary>
    /// Checks if this slot has a tank equipped
    /// </summary>
    public bool IsOccupied => !IsEmpty;

    /// <summary>
    /// Constructor for creating the oxygen tank slot
    /// </summary>
    public OxygenTankSlot()
    {
        displayName = "Oxygen Tank";
        equippedTankId = "";
        equippedTankInstance = null;
    }

    /// <summary>
    /// Checks if the specified inventory item can be equipped to this slot
    /// </summary>
    public bool CanEquip(InventoryItemData inventoryItem)
    {
        if (inventoryItem?.ItemData?.itemType != ItemType.OxygenTank)
            return false;

        var tankData = inventoryItem.ItemData.OxygenTankData;
        return tankData != null && tankData.IsValid();
    }

    /// <summary>
    /// Equips a tank to this slot and stores the complete ItemInstance.
    /// This preserves all instance-specific state (current oxygen level).
    /// </summary>
    public void EquipTank(string tankId, ItemInstance tankInstance)
    {
        if (tankInstance?.ItemData == null)
        {
            Debug.LogError($"Cannot equip tank {tankId} - ItemInstance or ItemData is null");
            return;
        }

        if (tankInstance.ItemData.itemType != ItemType.OxygenTank)
        {
            Debug.LogError($"Cannot equip tank {tankId} - Item is not an oxygen tank");
            return;
        }

        equippedTankId = tankId;
        equippedTankInstance = tankInstance;

        DebugLog($"[OxygenTankSlot] Equipped {tankInstance.ItemData.itemName} (Instance: {tankInstance.InstanceID})");
    }

    /// <summary>
    /// Unequips the current tank from this slot and clears references.
    /// Returns the tank ID for tracking purposes.
    /// </summary>
    public string UnequipTank()
    {
        string previousTankId = equippedTankId;

        if (!string.IsNullOrEmpty(previousTankId))
        {
            DebugLog($"[OxygenTankSlot] Unequipped {previousTankId}");
        }

        equippedTankId = "";
        equippedTankInstance = null;

        return previousTankId;
    }

    /// <summary>
    /// Gets the currently equipped tank as InventoryItemData with full ItemInstance.
    /// This allows the tank to be returned to inventory with all its state preserved.
    /// </summary>
    public InventoryItemData GetEquippedTank()
    {
        if (IsEmpty || equippedTankInstance == null)
            return null;

        // Create InventoryItemData from the stored ItemInstance
        return new InventoryItemData(equippedTankId, equippedTankInstance, Vector2Int.zero);
    }

    /// <summary>
    /// Gets the ItemInstance of the equipped tank (direct access).
    /// </summary>
    public ItemInstance GetEquippedTankInstance()
    {
        return equippedTankInstance;
    }

    /// <summary>
    /// Gets the ItemData template of the currently equipped tank.
    /// </summary>
    public ItemData GetEquippedItemData()
    {
        return equippedTankInstance?.ItemData;
    }

    /// <summary>
    /// Gets the OxygenTankData template of the equipped tank.
    /// </summary>
    public OxygenTankData GetEquippedTankData()
    {
        return equippedTankInstance?.ItemData?.OxygenTankData;
    }

    /// <summary>
    /// Gets the OxygenTankInstanceData (mutable state) of the equipped tank.
    /// </summary>
    public OxygenTankInstanceData GetEquippedTankInstanceData()
    {
        return equippedTankInstance?.OxygenTankInstanceData;
    }

    /// <summary>
    /// Gets the current oxygen percentage (0-1).
    /// </summary>
    public float GetOxygenPercentage()
    {
        var instanceData = GetEquippedTankInstanceData();
        var templateData = GetEquippedTankData();

        if (instanceData != null && templateData != null)
            return instanceData.GetOxygenPercentage(templateData);

        return 0f;
    }

    /// <summary>
    /// Gets the current oxygen amount.
    /// </summary>
    public float GetCurrentOxygen()
    {
        var instanceData = GetEquippedTankInstanceData();
        return instanceData?.currentOxygen ?? 0f;
    }

    /// <summary>
    /// Checks if the equipped tank is empty.
    /// </summary>
    public bool IsTankEmpty()
    {
        var instanceData = GetEquippedTankInstanceData();
        return instanceData?.IsEmpty() ?? true;
    }

    /// <summary>
    /// Validates that this slot's state is consistent.
    /// </summary>
    public bool IsValid()
    {
        // If we have an equipped tank, verify the data is consistent
        if (IsOccupied)
        {
            if (equippedTankInstance == null)
            {
                Debug.LogWarning($"Oxygen tank slot has equipped tank ID but no ItemInstance");
                return false;
            }

            var itemData = equippedTankInstance.ItemData;
            if (itemData == null)
            {
                Debug.LogWarning($"Oxygen tank slot has ItemInstance but no ItemData");
                return false;
            }

            // Verify the item is actually an oxygen tank
            if (itemData.itemType != ItemType.OxygenTank || itemData.OxygenTankData == null)
            {
                Debug.LogWarning($"Item {equippedTankId} is not a valid oxygen tank");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets debug information about this slot including ItemInstance details.
    /// </summary>
    public string GetDebugInfo()
    {
        if (IsEmpty)
            return "Oxygen Tank Slot: Empty";

        var itemData = GetEquippedItemData();
        var instanceData = GetEquippedTankInstanceData();
        var templateData = GetEquippedTankData();

        string itemName = itemData?.itemName ?? "UNKNOWN";
        string instanceId = equippedTankInstance?.InstanceID ?? "NO_INSTANCE";

        float currentOxygen = instanceData?.currentOxygen ?? 0f;
        float maxCapacity = templateData?.maxCapacity ?? 0f;
        float percentage = GetOxygenPercentage();

        return $"Oxygen Tank Slot: {itemName} ({currentOxygen:F1}/{maxCapacity:F1} = {percentage:P0}) [Instance: {instanceId}]";
    }

    /// <summary>
    /// Creates a copy of this tank slot.
    /// </summary>
    public OxygenTankSlot CreateCopy()
    {
        var copy = new OxygenTankSlot();
        copy.equippedTankId = this.equippedTankId;
        copy.equippedTankInstance = this.equippedTankInstance;
        return copy;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[OxygenTankSlot] {message}");
    }
}