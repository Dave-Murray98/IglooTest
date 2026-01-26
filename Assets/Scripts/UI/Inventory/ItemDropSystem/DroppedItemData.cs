using UnityEngine;

/// <summary>
/// REFACTORED: Enhanced data structure for dropped items with ItemInstance support
/// Now preserves complete instance state (ammo, durability, etc.) through save/load cycles
/// </summary>
[System.Serializable]
public class DroppedItemData
{
    [Header("Basic Item Info")]
    public string id;
    public string itemDataName;
    public Vector3 position;
    public Vector3 rotation;
    public float objectMass = 1f;

    [Header("Visual Configuration")]
    public string visualPrefabName;
    public Vector3 visualScale = Vector3.one;
    public bool usePhysics = true;

    [Header("Physics State")]
    public bool isKinematic = false;
    public Vector3 velocity = Vector3.zero;
    public Vector3 angularVelocity = Vector3.zero;

    [Header("Instance State - REFACTORED")]
    public string instanceID;

    // Type-specific instance data
    public int consumableRemainingUses;
    public int rangedWeaponCurrentAmmo;

    public int ammoCurrentAmmo;

    public float clothingCurrentCondition;

    public int toolCurrentEnergy;
    public int toolEnergySourceCurrentEnergy;

    public float currentOxygenCount;

    public DroppedItemData()
    {
        // Default constructor
    }

    public DroppedItemData(string itemId, string dataName, Vector3 pos, Vector3 rot = default)
    {
        id = itemId;
        itemDataName = dataName;
        position = pos;
        rotation = rot;
        objectMass = 1f;
        visualScale = Vector3.one;
        usePhysics = true;
        isKinematic = false;
    }

    /// <summary>
    /// REFACTORED: Create from ItemInstance with visual prefab information
    /// Preserves instance state through drop/save/load cycle
    /// </summary>
    public static DroppedItemData FromItemInstance(string itemId, ItemInstance itemInstance, Vector3 position, Vector3 rotation = default)
    {
        if (itemInstance?.ItemData == null)
            return null;

        var itemData = itemInstance.ItemData;
        var data = new DroppedItemData(itemId, itemData.name, position, rotation);

        // Store visual prefab configuration
        data.visualPrefabName = itemData.visualPrefab?.name ?? "";
        data.visualScale = itemData.GetVisualPrefabScale();
        data.objectMass = itemData.objectMass;
        data.usePhysics = itemData.usePhysicsOnDrop;

        // Store instance ID
        data.instanceID = itemInstance.InstanceID;

        // Copy instance-specific state
        data.CopyInstanceStateFrom(itemInstance);

        return data;
    }

    /// <summary>
    /// NEW: Copy instance state from ItemInstance
    /// </summary>
    private void CopyInstanceStateFrom(ItemInstance itemInstance)
    {
        var itemData = itemInstance.ItemData;

        switch (itemData.itemType)
        {
            case ItemType.RangedWeapon:
                if (itemInstance.RangedWeaponInstanceData != null)
                    rangedWeaponCurrentAmmo = itemInstance.RangedWeaponInstanceData.currentAmmoInClip;
                break;

            case ItemType.Ammo:
                if (itemInstance.AmmoInstanceData != null)
                    ammoCurrentAmmo = itemInstance.AmmoInstanceData.currentAmmo;
                break;

            case ItemType.Clothing:
                if (itemInstance.ClothingInstanceData != null)
                    clothingCurrentCondition = itemInstance.ClothingInstanceData.currentCondition;
                break;

            case ItemType.Tool:
                if (itemInstance.ToolInstanceData != null)
                    toolCurrentEnergy = itemInstance.ToolInstanceData.equippedEnergySourceAmount;
                break;

            case ItemType.ToolEnergySource:
                if (itemInstance.ToolEnergySourceInstanceData != null)
                    toolEnergySourceCurrentEnergy = itemInstance.ToolEnergySourceInstanceData.currentEnergy;
                break;

            case ItemType.OxygenTank:
                if (itemInstance.OxygenTankInstanceData != null)
                    currentOxygenCount = itemInstance.OxygenTankInstanceData.currentOxygen;
                break;

                // Other types have no mutable instance state yet
        }
    }

    /// <summary>
    /// NEW: Reconstruct ItemInstance from stored data
    /// </summary>
    public ItemInstance ToItemInstance()
    {
        // Load ItemData template
        ItemData itemData = FindItemDataByName(itemDataName);
        if (itemData == null)
        {
            Debug.LogError($"Cannot reconstruct ItemInstance: ItemData '{itemDataName}' not found");
            return null;
        }

        // Create ItemInstance from template
        var itemInstance = new ItemInstance(itemData);

        // Restore instance-specific state
        RestoreInstanceStateTo(itemInstance);

        return itemInstance;
    }

    /// <summary>
    /// NEW: Restore instance state to ItemInstance
    /// </summary>
    private void RestoreInstanceStateTo(ItemInstance itemInstance)
    {
        var itemData = itemInstance.ItemData;

        switch (itemData.itemType)
        {
            case ItemType.RangedWeapon:
                if (itemInstance.RangedWeaponInstanceData != null)
                    itemInstance.RangedWeaponInstanceData.currentAmmoInClip = rangedWeaponCurrentAmmo;
                break;

            case ItemType.Ammo:
                if (itemInstance.AmmoInstanceData != null)
                    itemInstance.AmmoInstanceData.currentAmmo = ammoCurrentAmmo;
                break;

            case ItemType.Clothing:
                if (itemInstance.ClothingInstanceData != null)
                    itemInstance.ClothingInstanceData.currentCondition = clothingCurrentCondition;
                break;

            case ItemType.Tool:
                if (itemInstance.ToolInstanceData != null)
                    itemInstance.ToolInstanceData.equippedEnergySourceAmount = toolCurrentEnergy;
                break;

            case ItemType.ToolEnergySource:
                if (itemInstance.ToolEnergySourceInstanceData != null)
                    itemInstance.ToolEnergySourceInstanceData.currentEnergy = toolEnergySourceCurrentEnergy;
                break;

            case ItemType.OxygenTank:
                if (itemInstance.OxygenTankInstanceData != null)
                    itemInstance.OxygenTankInstanceData.currentOxygen = currentOxygenCount;
                break;

                // Other types have no mutable instance state yet
        }
    }

    /// <summary>
    /// Update physics state from a rigidbody
    /// </summary>
    public void UpdatePhysicsState(Rigidbody rb)
    {
        if (rb != null)
        {
            isKinematic = rb.isKinematic;
            velocity = rb.linearVelocity;
            angularVelocity = rb.angularVelocity;
        }
    }

    /// <summary>
    /// Apply physics state to a rigidbody
    /// </summary>
    public void ApplyPhysicsState(Rigidbody rb)
    {
        if (rb != null)
        {
            rb.isKinematic = isKinematic;
            if (!isKinematic)
            {
                rb.linearVelocity = velocity;
                rb.angularVelocity = angularVelocity;
            }
        }
    }

    /// <summary>
    /// Check if this dropped item data is valid
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(id) &&
               !string.IsNullOrEmpty(itemDataName) &&
               !string.IsNullOrEmpty(visualPrefabName);
    }

    /// <summary>
    /// Get debug string representation
    /// </summary>
    public override string ToString()
    {
        return $"DroppedItem[{id}] {itemDataName} ({visualPrefabName}) at {position}, rot: {rotation} " +
               $"(Mass: {objectMass}, Physics: {usePhysics}, Kinematic: {isKinematic}, InstanceID: {instanceID})";
    }

    /// <summary>
    /// Helper: Find ItemData ScriptableObject by name
    /// </summary>
    private ItemData FindItemDataByName(string dataName)
    {
        ItemData itemData = Resources.Load<ItemData>(SaveManager.Instance.itemDataPath + dataName);
        if (itemData != null) return itemData;

        ItemData[] allItemData = Resources.FindObjectsOfTypeAll<ItemData>();
        return System.Array.Find(allItemData, data => data.name == dataName);
    }
}