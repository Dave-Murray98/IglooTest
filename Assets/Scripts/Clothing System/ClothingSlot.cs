using UnityEngine;

/// <summary>
/// REFACTORED: ClothingSlot that properly stores ItemInstance for complete state preservation.
/// Now maintains the full ItemInstance reference throughout the equipment lifecycle.
/// </summary>
[System.Serializable]
public class ClothingSlot
{
    [Header("Slot Configuration")]
    [Tooltip("Which clothing layer this slot represents")]
    public ClothingLayer layer;

    [Tooltip("Display name for this slot in the UI")]
    public string displayName;

    [Header("Current State")]
    [Tooltip("ID of the currently equipped item (empty if none)")]
    public string equippedItemId = "";

    [Header("Equipped Item Instance")]
    [Tooltip("ItemInstance of the currently equipped item - preserves all state")]
    [SerializeField] private ItemInstance equippedItemInstance;

    /// <summary>
    /// Checks if this slot is currently empty
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(equippedItemId);

    /// <summary>
    /// Checks if this slot has an item equipped
    /// </summary>
    public bool IsOccupied => !IsEmpty;

    /// <summary>
    /// Constructor for creating a clothing slot
    /// </summary>
    public ClothingSlot(ClothingLayer slotLayer)
    {
        layer = slotLayer;
        displayName = GetDefaultDisplayName(slotLayer);
        equippedItemId = "";
        equippedItemInstance = null;
    }

    /// <summary>
    /// Constructor with custom display name
    /// </summary>
    public ClothingSlot(ClothingLayer slotLayer, string customDisplayName)
    {
        layer = slotLayer;
        displayName = customDisplayName;
        equippedItemId = "";
        equippedItemInstance = null;
    }

    /// <summary>
    /// Checks if the specified clothing item can be equipped to this slot
    /// </summary>
    public bool CanEquip(ClothingData clothingData)
    {
        if (clothingData == null)
            return false;

        return System.Array.Exists(clothingData.validLayers, l => l == layer);
    }

    /// <summary>
    /// Checks if the specified inventory item can be equipped to this slot
    /// </summary>
    public bool CanEquip(InventoryItemData inventoryItem)
    {
        if (inventoryItem?.ItemData?.itemType != ItemType.Clothing)
            return false;

        var clothingData = inventoryItem.ItemData.ClothingData;
        return CanEquip(clothingData);
    }

    /// <summary>
    /// REFACTORED: Equips an item to this slot and stores the complete ItemInstance.
    /// This preserves all instance-specific state (condition, etc.)
    /// </summary>
    public void EquipItem(string itemId, ItemInstance itemInstance)
    {
        if (itemInstance?.ItemData == null)
        {
            Debug.LogError($"Cannot equip item {itemId} - ItemInstance or ItemData is null");
            return;
        }

        equippedItemId = itemId;
        equippedItemInstance = itemInstance;

        Debug.Log($"[ClothingSlot] Equipped {itemInstance.ItemData.itemName} to {layer} (Instance: {itemInstance.InstanceID})");
    }

    /// <summary>
    /// REFACTORED: Unequips the current item from this slot and clears references.
    /// Returns the item ID for tracking purposes.
    /// </summary>
    public string UnequipItem()
    {
        string previousItemId = equippedItemId;

        if (!string.IsNullOrEmpty(previousItemId))
        {
            Debug.Log($"[ClothingSlot] Unequipped {previousItemId} from {layer}");
        }

        equippedItemId = "";
        equippedItemInstance = null;

        return previousItemId;
    }

    /// <summary>
    /// REFACTORED: Gets the currently equipped item as InventoryItemData with full ItemInstance.
    /// This allows the item to be returned to inventory with all its state preserved.
    /// </summary>
    public InventoryItemData GetEquippedItem()
    {
        if (IsEmpty || equippedItemInstance == null)
            return null;

        // Create InventoryItemData from the stored ItemInstance
        // Position doesn't matter for equipped items (they'll get a new position in inventory)
        return new InventoryItemData(equippedItemId, equippedItemInstance, Vector2Int.zero);
    }

    /// <summary>
    /// REFACTORED: Gets the ItemInstance of the equipped item (direct access).
    /// </summary>
    public ItemInstance GetEquippedItemInstance()
    {
        return equippedItemInstance;
    }

    /// <summary>
    /// REFACTORED: Gets the ItemData template of the currently equipped item.
    /// </summary>
    public ItemData GetEquippedItemData()
    {
        return equippedItemInstance?.ItemData;
    }

    /// <summary>
    /// REFACTORED: Gets the ClothingData template of the currently equipped item.
    /// </summary>
    public ClothingData GetEquippedClothingData()
    {
        return equippedItemInstance?.ItemData?.ClothingData;
    }

    /// <summary>
    /// REFACTORED: Gets the ClothingInstanceData (mutable state) of the equipped item.
    /// </summary>
    public ClothingInstanceData GetEquippedClothingInstanceData()
    {
        return equippedItemInstance?.ClothingInstanceData;
    }

    /// <summary>
    /// Gets the display name for the equipped item, or "Empty" if none
    /// </summary>
    public string GetEquippedItemDisplayName()
    {
        var itemData = GetEquippedItemData();
        if (itemData != null)
            return itemData.itemName;

        return "Empty";
    }

    /// <summary>
    /// REFACTORED: Gets the condition percentage of the equipped item (0-1).
    /// Now reads from ItemInstance for accurate per-item state.
    /// </summary>
    public float GetEquippedItemCondition()
    {
        var instanceData = GetEquippedClothingInstanceData();
        var templateData = GetEquippedClothingData();

        if (instanceData != null && templateData != null)
            return instanceData.GetConditionPercentage(templateData);

        return 0f;
    }

    /// <summary>
    /// REFACTORED: Checks if the equipped item is damaged (condition below 50%).
    /// Now reads from ItemInstance for accurate per-item state.
    /// </summary>
    public bool IsEquippedItemDamaged()
    {
        var instanceData = GetEquippedClothingInstanceData();
        var templateData = GetEquippedClothingData();

        if (instanceData != null && templateData != null)
            return instanceData.IsDamaged(templateData);

        return false;
    }

    /// <summary>
    /// Gets default display names for each clothing layer
    /// </summary>
    private string GetDefaultDisplayName(ClothingLayer slotLayer)
    {
        return slotLayer switch
        {
            ClothingLayer.HeadUpper => "Head (Upper)",
            ClothingLayer.HeadLower => "Head (Lower)",
            ClothingLayer.TorsoInner => "Torso (Inner)",
            ClothingLayer.TorsoOuter => "Torso (Outer)",
            ClothingLayer.LegsInner => "Legs (Inner)",
            ClothingLayer.LegsOuter => "Legs (Outer)",
            ClothingLayer.Hands => "Hands",
            ClothingLayer.Socks => "Socks",
            ClothingLayer.Shoes => "Shoes",
            _ => slotLayer.ToString()
        };
    }

    /// <summary>
    /// Gets a short display name for UI space constraints
    /// </summary>
    public string GetShortDisplayName()
    {
        return layer switch
        {
            ClothingLayer.HeadUpper => "Hat",
            ClothingLayer.HeadLower => "Scarf",
            ClothingLayer.TorsoInner => "Shirt",
            ClothingLayer.TorsoOuter => "Jacket",
            ClothingLayer.LegsInner => "Underwear",
            ClothingLayer.LegsOuter => "Pants",
            ClothingLayer.Hands => "Gloves",
            ClothingLayer.Socks => "Socks",
            ClothingLayer.Shoes => "Shoes",
            _ => displayName
        };
    }

    /// <summary>
    /// Creates a copy of this clothing slot
    /// </summary>
    public ClothingSlot CreateCopy()
    {
        var copy = new ClothingSlot(layer, displayName);
        copy.equippedItemId = this.equippedItemId;
        copy.equippedItemInstance = this.equippedItemInstance;
        return copy;
    }

    /// <summary>
    /// REFACTORED: Validates that this slot's state is consistent.
    /// Checks ItemInstance validity.
    /// </summary>
    public bool IsValid()
    {
        // If we have an equipped item, verify the data is consistent
        if (IsOccupied)
        {
            if (equippedItemInstance == null)
            {
                Debug.LogWarning($"Clothing slot {layer} has equipped item ID but no ItemInstance");
                return false;
            }

            var itemData = equippedItemInstance.ItemData;
            if (itemData == null)
            {
                Debug.LogWarning($"Clothing slot {layer} has ItemInstance but no ItemData");
                return false;
            }

            // Verify the item can actually be equipped to this slot
            if (itemData.itemType != ItemType.Clothing ||
                itemData.ClothingData == null ||
                !itemData.ClothingData.CanEquipToLayer(layer))
            {
                Debug.LogWarning($"Item {equippedItemId} cannot be equipped to slot {layer}");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// REFACTORED: Gets debug information about this slot including ItemInstance details.
    /// </summary>
    public string GetDebugInfo()
    {
        if (IsEmpty)
            return $"{layer}: Empty";

        var itemData = GetEquippedItemData();
        var instanceData = GetEquippedClothingInstanceData();
        var templateData = GetEquippedClothingData();

        string itemName = itemData?.itemName ?? "UNKNOWN";
        string instanceId = equippedItemInstance?.InstanceID ?? "NO_INSTANCE";

        float condition = 0f;
        if (instanceData != null && templateData != null)
        {
            condition = instanceData.GetConditionPercentage(templateData);
        }

        return $"{layer}: {itemName} ({condition:P0} condition) [Instance: {instanceId}]";
    }
}