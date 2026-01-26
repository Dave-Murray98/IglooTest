using UnityEditor;
using UnityEngine;

/// <summary>
/// REFACTORED: Unified data structure for tracking all pickup items with ItemInstance support
/// Now stores complete instance state (ammo, durability, etc.) for proper state preservation
/// </summary>
[System.Serializable]
public class PickupItemData
{
    [Header("Basic Info")]
    public string itemId;
    public string itemDataName;
    public PickupItemType itemType;

    [Header("Collection State")]
    public bool isCollected = false;

    [Header("Transform Data")]
    public Vector3 originalPosition;
    public Vector3 currentPosition;
    public Vector3 originalRotation;
    public Vector3 currentRotation;
    public Vector3 originalScale = Vector3.one;
    public Vector3 currentScale = Vector3.one;

    [Header("Physics State")]
    public bool hasRigidbody = false;
    public bool isKinematic = false;
    public float objectMass = 1f;
    public Vector3 velocity = Vector3.zero;
    public Vector3 angularVelocity = Vector3.zero;

    [Header("Visual Configuration")]
    public string visualPrefabName;
    public Vector3 visualScale = Vector3.one;
    public bool usePhysics = true;

    [Header("State Tracking")]
    public bool hasBeenMoved = false;
    public bool hasBeenRotated = false;
    public bool wasInteractedWith = false;

    [Header("Instance State - REFACTORED")]
    public string instanceID;

    // Type-specific instance data
    public int consumableRemainingUses;
    public int rangedWeaponCurrentAmmo;

    public int ammoCurrentAmmo;

    public float clothingCurrentCondition;

    public int toolCurrentEnergy;
    public int toolEnergySourceCurrentEnergy;

    public float oxygenTankCurrentOxygen;


    // Other types currently have no mutable state, but fields ready for future expansion

    public PickupItemData()
    {
        // Default constructor
    }

    /// <summary>
    /// REFACTORED: Create from an existing ItemPickupInteractable (for original scene items)
    /// Now creates default ItemInstance state
    /// </summary>
    public PickupItemData(string id, ItemPickupInteractable pickup, PickupItemType type)
    {
        itemId = id;
        itemType = type;

        if (pickup != null && pickup.GetItemData() != null)
        {
            var itemData = pickup.GetItemData();
            itemDataName = itemData.name;
            visualPrefabName = itemData.visualPrefab?.name ?? "";
            visualScale = itemData.GetVisualPrefabScale();
            objectMass = itemData.objectMass;
            usePhysics = itemData.usePhysicsOnDrop;

            // Initialize instance state from template defaults
            InitializeInstanceStateFromTemplate(itemData);
        }

        Transform rootTransform = pickup.GetRootTransform() ?? pickup.transform;

        // Store original and current transform data
        originalPosition = rootTransform.position;
        currentPosition = originalPosition;
        originalRotation = rootTransform.eulerAngles;
        currentRotation = originalRotation;
        originalScale = rootTransform.localScale;
        currentScale = originalScale;

        // Store physics data if present
        Rigidbody rb = rootTransform.GetComponent<Rigidbody>();
        if (rb != null)
        {
            hasRigidbody = true;
            isKinematic = rb.isKinematic;
            objectMass = rb.mass;
            velocity = rb.linearVelocity;
            angularVelocity = rb.angularVelocity;
        }

        // Initialize state flags
        isCollected = false;
        hasBeenMoved = false;
        hasBeenRotated = false;
        wasInteractedWith = false;
    }

    /// <summary>
    /// REFACTORED: Create from ItemInstance (for dropped inventory items)
    /// Preserves complete instance state through drop/pickup cycle
    /// </summary>
    public static PickupItemData FromItemInstance(string itemId, ItemInstance itemInstance, Vector3 position, Vector3 rotation = default)
    {
        if (itemInstance?.ItemData == null)
        {
            Debug.LogError("Cannot create pickup item data: ItemInstance or ItemData is null");
            return null;
        }

        var data = new PickupItemData
        {
            itemId = itemId,
            itemType = PickupItemType.DroppedInventoryItem,
            itemDataName = itemInstance.ItemData.name,
            instanceID = itemInstance.InstanceID,

            // Set positions (no "original" for dropped items, they start where dropped)
            originalPosition = position,
            currentPosition = position,
            originalRotation = rotation,
            currentRotation = rotation,
            originalScale = Vector3.one,
            currentScale = Vector3.one,

            // Visual configuration
            visualPrefabName = itemInstance.ItemData.visualPrefab?.name ?? "",
            visualScale = itemInstance.ItemData.GetVisualPrefabScale(),
            objectMass = itemInstance.ItemData.objectMass,
            usePhysics = itemInstance.ItemData.usePhysicsOnDrop,

            // State
            isCollected = false,
            hasBeenMoved = false,
            hasBeenRotated = false,
            wasInteractedWith = true // Dropped items are inherently "interacted with"
        };

        // Copy instance-specific state
        data.CopyInstanceStateFrom(itemInstance);

        return data;
    }

    /// <summary>
    /// NEW: Initialize instance state from ItemData template defaults (for original scene items)
    /// </summary>
    private void InitializeInstanceStateFromTemplate(ItemData itemData)
    {
        instanceID = System.Guid.NewGuid().ToString();

        switch (itemData.itemType)
        {
            case ItemType.Consumable:
                if (itemData.ConsumableData != null)
                {
                    consumableRemainingUses = itemData.ConsumableData.multiUse ?
                        itemData.ConsumableData.maxUses : 1;
                }
                break;

            case ItemType.RangedWeapon:
                if (itemData.RangedWeaponData != null)
                    rangedWeaponCurrentAmmo = itemData.RangedWeaponData.defaultStartingAmmo;
                break;

            case ItemType.Clothing:
                if (itemData.ClothingData != null)
                    clothingCurrentCondition = itemData.ClothingData.maxCondition;
                break;

            case ItemType.Tool:
                if (itemData.ToolData != null)
                    toolCurrentEnergy = itemData.ToolData.maxEnergyCapacity;
                break;

            case ItemType.ToolEnergySource:
                if (itemData.ToolEnergySourceData != null)
                    toolEnergySourceCurrentEnergy = itemData.ToolEnergySourceData.maxEnergyCapacity;
                break;

            case ItemType.OxygenTank:
                if (itemData.OxygenTankData != null)
                    oxygenTankCurrentOxygen = itemData.OxygenTankData.maxCapacity;
                break;

                // Other types have no mutable instance state yet
        }
    }

    /// <summary>
    /// NEW: Copy instance state from ItemInstance (for dropped inventory items)
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
                    oxygenTankCurrentOxygen = itemInstance.OxygenTankInstanceData.currentOxygen;
                break;

                // Other types have no mutable instance state yet
        }
    }

    /// <summary>
    /// NEW: Reconstruct ItemInstance from stored data (for pickup)
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
    /// NEW: Restore instance state to ItemInstance (helper for ToItemInstance)
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
                    itemInstance.OxygenTankInstanceData.currentOxygen = oxygenTankCurrentOxygen;
                break;

                // Other types have no mutable instance state yet
        }
    }

    /// <summary>
    /// Updates current state from a transform and rigidbody
    /// </summary>
    public void UpdateCurrentState(Transform transform, Rigidbody rigidbody = null)
    {
        if (transform == null) return;

        Vector3 newPosition = transform.position;
        Vector3 newRotation = transform.eulerAngles;
        Vector3 newScale = transform.localScale;

        // Check if position has changed significantly
        if (Vector3.Distance(currentPosition, newPosition) > 0.01f)
        {
            hasBeenMoved = true;
            currentPosition = newPosition;
            wasInteractedWith = true;
        }

        // Check if rotation has changed significantly
        if (Vector3.Distance(currentRotation, newRotation) > 1f) // 1 degree threshold
        {
            hasBeenRotated = true;
            currentRotation = newRotation;
            wasInteractedWith = true;
        }

        currentScale = newScale;

        // Update physics state
        if (rigidbody != null && hasRigidbody)
        {
            isKinematic = rigidbody.isKinematic;
            objectMass = rigidbody.mass;
            if (!isKinematic)
            {
                velocity = rigidbody.linearVelocity;
                angularVelocity = rigidbody.angularVelocity;
            }
        }
    }

    /// <summary>
    /// Apply this state to a transform and rigidbody
    /// </summary>
    public void ApplyToTransform(Rigidbody rigidbody)
    {
        if (rigidbody == null)
        {
            Debug.LogWarning($"PickupItemData: No Rigidbody provided to apply state for item {itemId}");
            return;
        }

        rigidbody.Move(currentPosition, Quaternion.Euler(currentRotation));

        if (rigidbody != null && hasRigidbody)
        {
            rigidbody.isKinematic = isKinematic;
            rigidbody.mass = objectMass;

            if (!isKinematic)
            {
                rigidbody.linearVelocity = velocity;
                rigidbody.angularVelocity = angularVelocity;
            }
        }
    }

    /// <summary>
    /// Check if this item has changed from its original state
    /// </summary>
    public bool HasChangedFromOriginal()
    {
        return hasBeenMoved || hasBeenRotated || wasInteractedWith || isCollected;
    }

    /// <summary>
    /// Check if this item needs to exist in the scene
    /// </summary>
    public bool ShouldExistInScene()
    {
        return !isCollected;
    }

    /// <summary>
    /// Mark item as collected
    /// </summary>
    public void MarkAsCollected()
    {
        isCollected = true;
        wasInteractedWith = true;
    }

    /// <summary>
    /// Restore item (uncollect it)
    /// </summary>
    public void RestoreToScene(Vector3? newPosition = null, Vector3? newRotation = null)
    {
        isCollected = false;
        wasInteractedWith = true;

        if (newPosition.HasValue)
        {
            currentPosition = newPosition.Value;
            hasBeenMoved = true;
        }

        if (newRotation.HasValue)
        {
            currentRotation = newRotation.Value;
            hasBeenRotated = true;
        }
    }

    /// <summary>
    /// Check if this item data is valid
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(itemId) &&
               !string.IsNullOrEmpty(itemDataName) &&
               !string.IsNullOrEmpty(visualPrefabName);
    }

    /// <summary>
    /// Get debug information
    /// </summary>
    public string GetDebugInfo()
    {
        return $"PickupItem[{itemId}] Type:{itemType} Collected:{isCollected} " +
               $"Moved:{hasBeenMoved} Rotated:{hasBeenRotated} Pos:{currentPosition} Rot:{currentRotation} " +
               $"InstanceID:{instanceID}";
    }

    /// <summary>
    /// Get a detailed string representation for debugging
    /// </summary>
    public override string ToString()
    {
        return $"PickupItemData[{itemId}] {itemDataName} ({itemType}) " +
               $"at {currentPosition}, rot: {currentRotation} " +
               $"(Collected: {isCollected}, Mass: {objectMass}, Physics: {usePhysics}, InstanceID: {instanceID})";
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

/// <summary>
/// Enum to distinguish between different types of pickup items
/// </summary>
public enum PickupItemType
{
    OriginalSceneItem,      // Items that were in the scene from the start
    DroppedInventoryItem    // Items that were dropped from player inventory
}