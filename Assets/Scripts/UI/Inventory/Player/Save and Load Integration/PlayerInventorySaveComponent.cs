using UnityEngine;

/// <summary>
/// UPDATED: Handles saving and loading of the refactored inventory system including all items, their positions,
/// rotations, and ItemInstance state. Now works with InventoryItemData and ItemInstance instead of GridItem.
/// Integrates with the modular save system and provides context-aware loading.
/// </summary>
public class PlayerInventorySaveComponent : SaveComponentBase, IPlayerDependentSaveable
{
    [Header("Component References")]
    [SerializeField] private PlayerInventoryManager inventoryManager;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindReferences = true;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        base.Awake();
        saveID = "Inventory_Main";
        autoGenerateID = false;

        if (autoFindReferences)
        {
            FindInventoryReferences();
        }
    }

    private void Start()
    {
        ValidateReferences();
    }

    /// <summary>
    /// Automatically locates inventory-related components.
    /// Checks current GameObject, then Instance, then scene search.
    /// </summary>
    private void FindInventoryReferences()
    {
        if (inventoryManager == null)
            inventoryManager = GetComponent<PlayerInventoryManager>() ??
                               PlayerInventoryManager.Instance ??
                               FindFirstObjectByType<PlayerInventoryManager>();

        DebugLog($"Auto-found inventory reference: {inventoryManager != null}");
    }

    /// <summary>
    /// Validates that necessary references are available.
    /// </summary>
    private void ValidateReferences()
    {
        if (inventoryManager == null)
        {
            Debug.LogError($"[{name}] InventoryManager reference missing! Inventory won't be saved/loaded.");
        }
        else
        {
            DebugLog($"InventoryManager reference validated: {inventoryManager.name}");
        }
    }

    /// <summary>
    /// Extracts complete inventory state including grid size, items with their ItemInstances, and next ID counter.
    /// </summary>
    public override object GetDataToSave()
    {
        if (inventoryManager == null)
        {
            DebugLog("Cannot save inventory - InventoryManager not found");
            return new InventorySaveData();
        }

        var saveData = ExtractInventoryDataFromManager();
        DebugLog($"Extracted inventory data: {saveData.ItemCount} items in {saveData.gridWidth}x{saveData.gridHeight} grid");
        return saveData;
    }

    /// <summary>
    /// Extracts inventory data from the manager by directly accessing its state.
    /// Creates InventorySaveData with all items (including ItemInstance state) converted to save format.
    /// </summary>
    private InventorySaveData ExtractInventoryDataFromManager()
    {
        var saveData = new InventorySaveData(inventoryManager.GridWidth, inventoryManager.GridHeight);
        saveData.nextItemId = inventoryManager.NextItemId;

        var allItems = inventoryManager.InventoryGridData.GetAllItems();
        foreach (var item in allItems)
        {
            var itemSaveData = item.ToSaveData();
            if (itemSaveData != null && itemSaveData.IsValid())
            {
                saveData.AddItem(itemSaveData);
                DebugLog($"Added item to save data: {item.ItemData?.itemName} at {item.GridPosition} rotation {item.currentRotation} (Instance: {item.ItemInstance?.InstanceID})");
            }
            else
            {
                Debug.LogWarning($"Invalid item save data for item {item.ID}");
            }
        }

        return saveData;
    }

    /// <summary>
    /// Extracts inventory data from various save container formats.
    /// Handles the transition from unified save structures to inventory-specific data.
    /// UPDATED: Now properly handles ItemInstance data.
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog($"Extracting inventory save data for persistence from: {saveContainer?.GetType().Name ?? "null"}");

        if (saveContainer == null)
        {
            DebugLog("ExtractRelevantData: saveContainer is null");
            return new InventorySaveData();
        }

        // Check PlayerPersistentData first (where rebuilt data is stored)
        if (saveContainer is PlayerPersistentData persistentData)
        {
            DebugLog($"Extracting from PlayerPersistentData with {persistentData.ComponentDataCount} components");

            var inventoryData = persistentData.GetComponentData<InventorySaveData>(SaveID);
            if (inventoryData != null)
            {
                DebugLog($"✓ Extracted inventory from persistent data: {inventoryData.ItemCount} items");
                return inventoryData;
            }
            else
            {
                DebugLog("No inventory data in persistent data - returning empty inventory");
                // List all available component IDs for debugging
                foreach (string componentId in persistentData.GetStoredComponentIDs())
                {
                    var componentData = persistentData.GetComponentData<object>(componentId);
                    DebugLog($"  Available component: {componentId} -> {componentData?.GetType().Name ?? "null"}");
                }
                return new InventorySaveData();
            }
        }
        else if (saveContainer is PlayerSaveData playerSaveData)
        {
            DebugLog($"Extracting from PlayerSaveData with {playerSaveData.CustomDataCount} custom data entries");

            // Method 1: Check for component ID in custom stats (primary method)
            if (playerSaveData.customStats.TryGetValue(SaveID, out object inventoryDataObj))
            {
                DebugLog($"Found data by SaveID '{SaveID}': {inventoryDataObj?.GetType().Name ?? "null"}");

                if (inventoryDataObj is InventorySaveData inventorySaveData)
                {
                    DebugLog($"✓ Extracted inventory from PlayerSaveData by SaveID: {inventorySaveData.ItemCount} items");
                    return inventorySaveData;
                }
                else
                {
                    DebugLog($"Data found by SaveID but wrong type: {inventoryDataObj?.GetType().Name ?? "null"}");
                }
            }

            // Method 2: Check legacy "inventoryData" key
            if (playerSaveData.customStats.TryGetValue("inventoryData", out object invDataObj) &&
                invDataObj is InventorySaveData invData)
            {
                DebugLog($"✓ Extracted inventory from PlayerSaveData legacy key: {invData.ItemCount} items");
                return invData;
            }

            // Debug: List all available keys in customStats
            DebugLog("No inventory data found in PlayerSaveData. Available customStats keys:");
            foreach (string key in playerSaveData.GetCustomDataKeys())
            {
                var data = playerSaveData.GetCustomData<object>(key);
                DebugLog($"  Key: '{key}' -> {data?.GetType().Name ?? "null"}");
            }

            return new InventorySaveData();
        }
        else if (saveContainer is InventorySaveData directInventoryData)
        {
            DebugLog($"✓ Extracted direct InventorySaveData: {directInventoryData.ItemCount} items");
            return directInventoryData;
        }

        DebugLog($"⚠ Unsupported save data type: {saveContainer?.GetType().Name ?? "null"}");
        return new InventorySaveData();
    }

    /// <summary>
    /// Restores inventory data to the manager. Inventory restoration is the same
    /// regardless of context - items are always fully restored to their saved state
    /// including ItemInstance data.
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (!(data is InventorySaveData inventoryData))
        {
            DebugLog($"Invalid save data type for inventory. Expected InventorySaveData, got: {data?.GetType().Name ?? "null"}");
            return;
        }

        DebugLog($"=== RESTORING INVENTORY DATA (Context: {context}) ===");

        // Refresh references after scene load
        if (autoFindReferences)
        {
            FindInventoryReferences();
        }

        if (inventoryManager == null)
        {
            DebugLog("Cannot load inventory - InventoryManager not found");
            return;
        }

        DebugLog($"Loading inventory: {inventoryData.ItemCount} items in {inventoryData.gridWidth}x{inventoryData.gridHeight} grid");

        try
        {
            RestoreInventoryDataToManager(inventoryData);
            DebugLog("Inventory restored successfully to manager");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load inventory: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// Restores complete inventory state by rebuilding the grid and placing all items with their ItemInstances.
    /// UPDATED: Now properly restores ItemInstance state for each item.
    /// </summary>
    private void RestoreInventoryDataToManager(InventorySaveData saveData)
    {
        if (saveData == null || !saveData.IsValid())
        {
            DebugLog("Invalid inventory save data - clearing inventory");
            inventoryManager.ClearInventory();
            return;
        }

        // Create new inventory grid with the saved dimensions
        var newInventoryData = new InventoryGridData(saveData.gridWidth, saveData.gridHeight);

        // Restore each item to the grid (including ItemInstance state)
        int restoredCount = 0;
        int failedCount = 0;

        foreach (var itemSaveData in saveData.items)
        {
            try
            {
                // FromSaveData now properly restores ItemInstance with all instance data
                var item = InventoryItemData.FromSaveData(itemSaveData);
                if (item != null)
                {
                    if (newInventoryData.PlaceItem(item))
                    {
                        restoredCount++;
                        DebugLog($"Restored item {item.ID}: {item.ItemData?.itemName} at {item.GridPosition} rotation {item.currentRotation} (Instance: {item.ItemInstance?.InstanceID})");

                        // Log instance-specific data for verification
                        LogInstanceData(item);
                    }
                    else
                    {
                        failedCount++;
                        Debug.LogWarning($"Failed to place item {item.ID} at position {item.GridPosition}");
                    }
                }
                else
                {
                    failedCount++;
                    Debug.LogWarning($"Failed to create InventoryItemData from save data for item {itemSaveData.itemID}");
                }
            }
            catch (System.Exception e)
            {
                failedCount++;
                Debug.LogError($"Exception restoring item {itemSaveData.itemID}: {e.Message}");
            }
        }

        // Set complete data to manager
        inventoryManager.SetInventoryData(newInventoryData, saveData.nextItemId);

        if (failedCount > 0)
        {
            Debug.LogWarning($"Restored inventory: {restoredCount}/{saveData.ItemCount} items successfully placed, {failedCount} failed");
        }
        else
        {
            DebugLog($"Restored inventory: {restoredCount}/{saveData.ItemCount} items successfully placed");
        }
    }

    /// <summary>
    /// Log instance-specific data for debugging purposes.
    /// </summary>
    private void LogInstanceData(InventoryItemData item)
    {
        if (item?.ItemInstance == null || !enableDebugLogs) return;

        var instance = item.ItemInstance;
        var itemType = item.ItemData?.itemType;

        switch (itemType)
        {
            case ItemType.RangedWeapon:
                if (instance.RangedWeaponInstanceData != null)
                {
                    DebugLog($"  → Weapon ammo: {instance.RangedWeaponInstanceData.currentAmmoInClip}");
                }
                break;

            case ItemType.Clothing:
                if (instance.ClothingInstanceData != null)
                {
                    DebugLog($"  → Clothing condition: {instance.ClothingInstanceData.currentCondition}");
                }
                break;
                // At the moment no other types have mutable instance state
        }
    }

    #region IPlayerDependentSaveable Implementation

    /// <summary>
    /// Extracts inventory data from unified save structure for modular loading.
    /// </summary>
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null) return null;

        DebugLog("Using modular extraction from unified save data");

        var inventoryData = unifiedData.GetComponentData<InventorySaveData>(SaveID);
        if (inventoryData != null)
        {
            DebugLog($"Extracted inventory from dynamic storage: {inventoryData.ItemCount} items");
            return inventoryData;
        }

        DebugLog("No inventory data found in unified save - returning empty inventory");
        return new InventorySaveData();
    }

    /// <summary>
    /// Creates default empty inventory for new games.
    /// </summary>
    public object CreateDefaultData()
    {
        DebugLog("Creating default inventory data for new game");

        int gridWidth = 10;
        int gridHeight = 10;

        if (inventoryManager != null)
        {
            gridWidth = inventoryManager.GridWidth;
            gridHeight = inventoryManager.GridHeight;
        }

        var defaultData = new InventorySaveData(gridWidth, gridHeight);
        defaultData.nextItemId = 1;

        DebugLog($"Default inventory data created: {gridWidth}x{gridHeight} grid, empty");
        return defaultData;
    }

    /// <summary>
    /// Contributes inventory data to unified save structure for save file creation.
    /// </summary>
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is InventorySaveData inventoryData && unifiedData != null)
        {
            DebugLog($"Contributing inventory data to unified save: {inventoryData.ItemCount} items");

            unifiedData.SetComponentData(SaveID, inventoryData);

            DebugLog($"Inventory data contributed: {inventoryData.ItemCount} items stored");
        }
        else
        {
            DebugLog($"Invalid data for contribution - expected InventorySaveData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    /// <summary>
    /// Called before save operations to ensure current references.
    /// </summary>
    public override void OnBeforeSave()
    {
        DebugLog("Preparing inventory for save");

        if (autoFindReferences)
        {
            FindInventoryReferences();
        }
    }

    /// <summary>
    /// Called after load operations. Inventory UI updates automatically via events.
    /// </summary>
    public override void OnAfterLoad()
    {
        DebugLog("Inventory load completed");
    }
}