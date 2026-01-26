using UnityEngine;

/// <summary>
/// Save data for an individual inventory item.
/// NOW INCLUDES ItemInstance data for proper state preservation.
/// </summary>
[System.Serializable]
public class InventoryItemSaveData
{
    [Header("Item Identity")]
    public string itemID;
    public string itemDataName; // Name of the ItemData ScriptableObject
    public string instanceID; // Unique instance identifier

    [Header("Grid Position")]
    public Vector2Int gridPosition;
    public int currentRotation;

    [Header("Item State")]
    public int stackCount = 1;

    [Header("Instance Data - All Types")]
    public ConsumableInstanceData consumableInstanceData;
    public RangedWeaponInstanceData rangedWeaponInstanceData;
    public AmmoInstanceData ammoInstanceData;
    public ClothingInstanceData clothingInstanceData;
    public MeleeWeaponInstanceData meleeWeaponInstanceData;
    public ToolInstanceData toolInstanceData;
    public ThrowableInstanceData throwableInstanceData;
    public BowInstanceData bowInstanceData;
    public KeyItemInstanceData keyItemInstanceData;
    public OxygenTankInstanceData oxygenTankInstanceData;
    public ToolEnergySourceInstanceData toolEnergySourceInstanceData;

    public InventoryItemSaveData()
    {
        // Default constructor
    }

    /// <summary>
    /// Validate that this item save data is valid
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(itemID) &&
               !string.IsNullOrEmpty(itemDataName) &&
               !string.IsNullOrEmpty(instanceID) &&
               stackCount > 0;
    }

    /// <summary>
    /// Get a debug string representation
    /// </summary>
    public override string ToString()
    {
        return $"Item[{itemID}] Instance[{instanceID}] {itemDataName} at {gridPosition} rot:{currentRotation}";
    }
}