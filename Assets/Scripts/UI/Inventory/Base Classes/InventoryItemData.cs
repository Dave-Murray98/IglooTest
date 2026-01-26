using UnityEngine;
using System;


/// <summary>
/// Data representation of an inventory item without visual dependencies.
/// Stores position, rotation, and shape information for tetris-style placement.
/// NOW USES ItemInstance for proper instance-based state management.
/// </summary>
[System.Serializable]
public class InventoryItemData
{
    [Header("Basic Properties")]
    public string ID;
    public Vector2Int GridPosition;

    [Header("Item Instance")]
    [SerializeField] private ItemInstance itemInstance;

    [Header("Shape and State")]
    public TetrominoType shapeType;
    public int currentRotation = 0;
    public int stackCount = 1;

    // Cached shape data for performance
    private TetrominoData _currentShapeData;
    private bool _dataCached = false;

    /// <summary>
    /// Create new InventoryItemData from an ItemData template.
    /// Creates a new ItemInstance with unique state.
    /// </summary>
    public InventoryItemData(string id, ItemData itemData, Vector2Int gridPosition)
    {
        if (itemData == null)
        {
            Debug.LogError("Cannot create InventoryItemData - ItemData is null");
            return;
        }

        ID = id;
        itemInstance = new ItemInstance(itemData);
        shapeType = itemData.shapeType;
        GridPosition = gridPosition;
        currentRotation = 0;
        stackCount = 1;
        RefreshShapeData();
    }

    /// <summary>
    /// Create InventoryItemData from an existing ItemInstance.
    /// Useful for transferring items between inventories while preserving state.
    /// </summary>
    public InventoryItemData(string id, ItemInstance instance, Vector2Int gridPosition)
    {
        if (instance?.ItemData == null)
        {
            Debug.LogError("Cannot create InventoryItemData - ItemInstance or ItemData is null");
            return;
        }

        ID = id;
        itemInstance = instance;
        shapeType = instance.ItemData.shapeType;
        GridPosition = gridPosition;
        currentRotation = 0;
        stackCount = 1;
        RefreshShapeData();
    }

    /// <summary>
    /// Constructor for deserialization from save data.
    /// </summary>
    private InventoryItemData()
    {
        // Used by save system
    }

    #region Properties

    /// <summary>
    /// Gets the ItemInstance for this inventory item.
    /// Contains all instance-specific mutable state.
    /// </summary>
    public ItemInstance ItemInstance => itemInstance;

    /// <summary>
    /// Gets the ItemData template (ScriptableObject) for this item.
    /// Convenience property that accesses itemInstance.ItemData.
    /// </summary>
    public ItemData ItemData => itemInstance?.ItemData;

    /// <summary>
    /// Gets the current tetris shape data with rotation applied.
    /// </summary>
    public TetrominoData CurrentShapeData
    {
        get
        {
            if (!_dataCached)
                RefreshShapeData();
            return _currentShapeData;
        }
    }

    /// <summary>
    /// Checks if this item can be rotated based on its ItemData settings and shape definition.
    /// </summary>
    public bool CanRotate
    {
        get
        {
            var itemData = ItemData;
            if (itemData != null)
                return itemData.isRotatable && TetrominoDefinitions.GetRotationCount(shapeType) > 1;
            return TetrominoDefinitions.GetRotationCount(shapeType) > 1;
        }
    }

    #endregion

    #region Grid Position Methods

    /// <summary>
    /// Gets all grid positions occupied by this item at its current location and rotation.
    /// </summary>
    public Vector2Int[] GetOccupiedPositions()
    {
        var shapeData = CurrentShapeData;
        Vector2Int[] positions = new Vector2Int[shapeData.cells.Length];

        for (int i = 0; i < shapeData.cells.Length; i++)
        {
            positions[i] = GridPosition + shapeData.cells[i];
        }

        return positions;
    }

    /// <summary>
    /// Gets all grid positions this item would occupy at a specific location (for placement testing).
    /// </summary>
    public Vector2Int[] GetOccupiedPositionsAt(Vector2Int position)
    {
        var shapeData = CurrentShapeData;
        Vector2Int[] positions = new Vector2Int[shapeData.cells.Length];

        for (int i = 0; i < shapeData.cells.Length; i++)
        {
            positions[i] = position + shapeData.cells[i];
        }

        return positions;
    }

    /// <summary>
    /// Gets all grid positions this item would occupy at a specific location and rotation (for rotation testing).
    /// </summary>
    public Vector2Int[] GetOccupiedPositionsAt(Vector2Int position, int rotation)
    {
        var shapeData = TetrominoDefinitions.GetRotationState(shapeType, rotation);
        Vector2Int[] positions = new Vector2Int[shapeData.cells.Length];

        for (int i = 0; i < shapeData.cells.Length; i++)
        {
            positions[i] = position + shapeData.cells[i];
        }

        return positions;
    }

    #endregion

    #region Rotation Methods

    /// <summary>
    /// Rotates the item clockwise to the next valid rotation state.
    /// </summary>
    public void RotateItem()
    {
        if (!CanRotate) return;

        int maxRotations = TetrominoDefinitions.GetRotationCount(shapeType);
        currentRotation = (currentRotation + 1) % maxRotations;
        RefreshShapeData();
    }

    /// <summary>
    /// Sets the item to a specific rotation state.
    /// </summary>
    public void SetRotation(int rotation)
    {
        int maxRotations = TetrominoDefinitions.GetRotationCount(shapeType);
        currentRotation = Mathf.Clamp(rotation, 0, maxRotations - 1);
        RefreshShapeData();
    }

    /// <summary>
    /// Refreshes the cached shape data when rotation changes.
    /// </summary>
    private void RefreshShapeData()
    {
        _currentShapeData = TetrominoDefinitions.GetRotationState(shapeType, currentRotation);
        _dataCached = true;
    }

    #endregion

    #region Position Methods

    /// <summary>
    /// Updates the item's grid position.
    /// </summary>
    public void SetGridPosition(Vector2Int position)
    {
        GridPosition = position;
    }

    #endregion

    #region Save/Load Methods

    /// <summary>
    /// Converts this item to save data format for persistence.
    /// </summary>
    public InventoryItemSaveData ToSaveData()
    {
        if (itemInstance == null)
        {
            Debug.LogError($"Cannot create save data for item {ID} - ItemInstance is null");
            return null;
        }

        return new InventoryItemSaveData
        {
            itemID = ID,
            itemDataName = itemInstance.ItemDataName,
            instanceID = itemInstance.InstanceID,
            gridPosition = GridPosition,
            currentRotation = currentRotation,
            stackCount = stackCount,

            // Store instance data for each type
            consumableInstanceData = itemInstance.ConsumableInstanceData,
            rangedWeaponInstanceData = itemInstance.RangedWeaponInstanceData,
            ammoInstanceData = itemInstance.AmmoInstanceData,
            clothingInstanceData = itemInstance.ClothingInstanceData,
            meleeWeaponInstanceData = itemInstance.MeleeWeaponInstanceData,
            toolInstanceData = itemInstance.ToolInstanceData,
            throwableInstanceData = itemInstance.ThrowableInstanceData,
            bowInstanceData = itemInstance.BowInstanceData,
            keyItemInstanceData = itemInstance.KeyItemInstanceData,
            oxygenTankInstanceData = itemInstance.OxygenTankInstanceData,
            toolEnergySourceInstanceData = itemInstance.ToolEnergySourceInstanceData
        };
    }

    /// <summary>
    /// Creates an InventoryItemData from save data format.
    /// Reconstructs the ItemInstance with saved state.
    /// </summary>
    public static InventoryItemData FromSaveData(InventoryItemSaveData saveData)
    {
        if (saveData == null)
        {
            Debug.LogError("Cannot create InventoryItemData - save data is null");
            return null;
        }

        // Load the ItemData template
        var itemData = LoadItemDataByName(saveData.itemDataName);
        if (itemData == null)
        {
            Debug.LogError($"Cannot create InventoryItemData - ItemData '{saveData.itemDataName}' not found");
            return null;
        }

        // Create the ItemInstance and restore its state
        var instance = new ItemInstance(saveData.itemDataName, saveData.instanceID);

        // Restore instance data based on item type
        RestoreInstanceData(instance, itemData.itemType, saveData);

        // Create the InventoryItemData
        var item = new InventoryItemData
        {
            ID = saveData.itemID,
            itemInstance = instance,
            shapeType = itemData.shapeType,
            GridPosition = saveData.gridPosition,
            currentRotation = saveData.currentRotation,
            stackCount = saveData.stackCount
        };

        item.RefreshShapeData();
        return item;
    }

    /// <summary>
    /// Restore instance data from save data based on item type.
    /// </summary>
    private static void RestoreInstanceData(ItemInstance instance, ItemType itemType, InventoryItemSaveData saveData)
    {
        // Use reflection to set the private instance data fields
        var instanceType = typeof(ItemInstance);

        switch (itemType)
        {
            case ItemType.Consumable:
                if (saveData.consumableInstanceData != null)
                {
                    var field = instanceType.GetField("consumableInstanceData",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    field?.SetValue(instance, saveData.consumableInstanceData);
                }
                break;

            case ItemType.RangedWeapon:
                if (saveData.rangedWeaponInstanceData != null)
                {
                    var field = instanceType.GetField("rangedWeaponInstanceData",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    field?.SetValue(instance, saveData.rangedWeaponInstanceData);
                }
                break;

            case ItemType.Ammo:
                if (saveData.ammoInstanceData != null)
                {
                    var field = instanceType.GetField("ammoInstanceData",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    field?.SetValue(instance, saveData.ammoInstanceData);
                }
                break;

            case ItemType.Clothing:
                if (saveData.clothingInstanceData != null)
                {
                    var field = instanceType.GetField("clothingInstanceData",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    field?.SetValue(instance, saveData.clothingInstanceData);
                }
                break;

            case ItemType.MeleeWeapon:
                if (saveData.meleeWeaponInstanceData != null)
                {
                    var field = instanceType.GetField("meleeWeaponInstanceData",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    field?.SetValue(instance, saveData.meleeWeaponInstanceData);
                }
                break;

            case ItemType.Tool:
                if (saveData.toolInstanceData != null)
                {
                    var field = instanceType.GetField("toolInstanceData",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    field?.SetValue(instance, saveData.toolInstanceData);
                }
                break;

            case ItemType.Throwable:
                if (saveData.throwableInstanceData != null)
                {
                    var field = instanceType.GetField("throwableInstanceData",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    field?.SetValue(instance, saveData.throwableInstanceData);
                }
                break;

            case ItemType.Bow:
                if (saveData.bowInstanceData != null)
                {
                    var field = instanceType.GetField("bowInstanceData",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    field?.SetValue(instance, saveData.bowInstanceData);
                }
                break;

            case ItemType.KeyItem:
                if (saveData.keyItemInstanceData != null)
                {
                    var field = instanceType.GetField("keyItemInstanceData",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    field?.SetValue(instance, saveData.keyItemInstanceData);
                }
                break;

            case ItemType.OxygenTank:
                if (saveData.oxygenTankInstanceData != null)
                {
                    var field = instanceType.GetField("oxygenTankInstanceData",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    field?.SetValue(instance, saveData.oxygenTankInstanceData);
                }
                break;

            case ItemType.ToolEnergySource:
                if (saveData.toolEnergySourceInstanceData != null)
                {
                    var field = instanceType.GetField("toolEnergySourceInstanceData",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    field?.SetValue(instance, saveData.toolEnergySourceInstanceData);
                }
                break;
        }
    }

    /// <summary>
    /// Load ItemData ScriptableObject by name using Resources system.
    /// </summary>
    private static ItemData LoadItemDataByName(string dataName)
    {
        ItemData itemData = Resources.Load<ItemData>(SaveManager.Instance.itemDataPath + dataName);
        if (itemData != null)
            return itemData;

        ItemData[] allItemData = Resources.FindObjectsOfTypeAll<ItemData>();
        foreach (var data in allItemData)
        {
            if (data.name == dataName)
                return data;
        }

        Debug.LogWarning($"ItemData '{dataName}' not found. Make sure it exists in Resources folder.");
        return null;
    }

    #endregion
}