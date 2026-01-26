using UnityEngine;

/// <summary>
/// Enhanced oxygen tank save component that properly handles ItemInstance state.
/// Saves complete ItemInstance data including InstanceID and OxygenTankInstanceData.
/// Recreates tanks during load with full state restoration.
/// Pattern based on ClothingSaveComponent.cs.
/// </summary>
public class OxygenTankSaveComponent : SaveComponentBase, IPlayerDependentSaveable
{
    [Header("Component References")]
    [SerializeField] private OxygenTankManager tankManager;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindReferences = true;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        base.Awake();
        saveID = "OxygenTank_Main";
        autoGenerateID = false;

        if (autoFindReferences)
        {
            FindTankReferences();
        }
    }

    private void Start()
    {
        ValidateReferences();
    }

    /// <summary>
    /// Automatically locates tank-related components.
    /// </summary>
    private void FindTankReferences()
    {
        if (tankManager == null)
            tankManager = GetComponent<OxygenTankManager>() ??
                          OxygenTankManager.Instance ??
                          FindFirstObjectByType<OxygenTankManager>();
    }

    /// <summary>
    /// Validates that necessary references are available.
    /// </summary>
    private void ValidateReferences()
    {
        if (tankManager == null)
        {
            Debug.LogError($"[{name}] OxygenTankManager reference missing! Tank won't be saved/loaded.");
        }
        else
        {
            DebugLog($"OxygenTankManager reference validated: {tankManager.name}");
        }
    }

    /// <summary>
    /// Extracts complete tank state including ItemInstance data for equipped tank.
    /// </summary>
    public override object GetDataToSave()
    {
        if (tankManager == null)
        {
            DebugLog("Cannot save tank - OxygenTankManager not found");
            return new OxygenTankSaveData();
        }

        var saveData = ExtractTankDataFromManager();
        DebugLog($"Extracted tank data: {(saveData.HasTankEquipped() ? "Tank equipped" : "No tank")}");
        return saveData;
    }

    /// <summary>
    /// Extracts tank data including complete ItemInstance state for equipped tank.
    /// </summary>
    private OxygenTankSaveData ExtractTankDataFromManager()
    {
        var saveData = new OxygenTankSaveData();
        var slot = tankManager.GetSlot();

        if (slot != null)
        {
            // Use the enhanced FromOxygenTankSlot method that captures ItemInstance data
            var slotSaveData = OxygenTankSlotSaveData.FromOxygenTankSlot(slot);

            if (slotSaveData != null)
            {
                saveData.slot = slotSaveData;

                if (!slot.IsEmpty)
                {
                    DebugLog($"Saving equipped tank: {slot.equippedTankId} " +
                            $"[Instance: {slotSaveData.instanceID}, Oxygen: {slotSaveData.tankInstanceData?.currentOxygen:F1}]");
                }
            }
        }

        return saveData;
    }

    /// <summary>
    /// Extracts tank data from various save container formats.
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog("Extracting tank save data for persistence");

        if (saveContainer == null)
        {
            DebugLog("ExtractRelevantData: saveContainer is null");
            return new OxygenTankSaveData();
        }

        // Check PlayerPersistentData first (where rebuilt data is stored)
        if (saveContainer is PlayerPersistentData persistentData)
        {
            var tankData = persistentData.GetComponentData<OxygenTankSaveData>(SaveID);
            if (tankData != null)
            {
                DebugLog($"Extracted tank from persistent data: {(tankData.HasTankEquipped() ? "Tank equipped" : "No tank")}");
                return tankData;
            }
            else
            {
                DebugLog("No tank data in persistent data - returning empty tank");
                return new OxygenTankSaveData();
            }
        }
        else if (saveContainer is PlayerSaveData playerSaveData)
        {
            // Check custom stats for tank data
            if (playerSaveData.customStats.TryGetValue(SaveID, out object tankDataObj) &&
                tankDataObj is OxygenTankSaveData tankData)
            {
                DebugLog($"Extracted tank from PlayerSaveData by SaveID: {(tankData.HasTankEquipped() ? "Tank equipped" : "No tank")}");
                return tankData;
            }

            DebugLog("No tank data found in PlayerSaveData");
            return new OxygenTankSaveData();
        }
        else if (saveContainer is OxygenTankSaveData directTankData)
        {
            DebugLog($"Extracted direct OxygenTankSaveData: {(directTankData.HasTankEquipped() ? "Tank equipped" : "No tank")}");
            return directTankData;
        }

        DebugLog($"Invalid save data type - got {saveContainer?.GetType().Name ?? "null"}");
        return new OxygenTankSaveData();
    }

    /// <summary>
    /// Restores tank data with complete ItemInstance reconstruction.
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (!(data is OxygenTankSaveData tankData))
        {
            DebugLog($"Invalid save data type for tank. Data type: {data?.GetType()}");
            return;
        }

        DebugLog($"=== RESTORING OXYGEN TANK DATA (Context: {context}) ===");

        // Refresh references after scene load
        if (autoFindReferences)
        {
            FindTankReferences();
        }

        if (tankManager == null)
        {
            DebugLog("Cannot load tank - OxygenTankManager not found");
            return;
        }

        DebugLog($"Loading tank: {(tankData.HasTankEquipped() ? "Tank equipped" : "No tank")}");

        try
        {
            RestoreTankDataToManager(tankData);
            DebugLog("Tank restored successfully to manager");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load tank: {e.Message}");
        }
    }

    /// <summary>
    /// Restores tank state by recreating ItemInstance with saved state.
    /// </summary>
    private void RestoreTankDataToManager(OxygenTankSaveData saveData)
    {
        if (saveData == null || !saveData.IsValid())
        {
            DebugLog("Invalid tank save data - clearing tank");
            tankManager.ClearTank();
            return;
        }

        // Get current slot and clear it
        var currentSlot = tankManager.GetSlot();
        if (currentSlot != null)
        {
            currentSlot.UnequipTank();
        }

        // Restore equipment by recreating ItemInstance with saved state
        if (saveData.slot != null && !saveData.slot.IsEmpty)
        {
            // Create a new tank slot
            var restoredSlot = new OxygenTankSlot();

            // Apply saved data (which restores ItemInstance)
            saveData.slot.ApplyToOxygenTankSlot(restoredSlot);

            // Set the restored slot to the manager
            tankManager.SetTankData(restoredSlot);

            DebugLog($"Restored tank: {saveData.slot.equippedTankId} with oxygen {saveData.slot.tankInstanceData?.currentOxygen:F1}");
        }
        else
        {
            DebugLog("No tank to restore - slot is empty");
        }
    }

    #region IPlayerDependentSaveable Implementation

    /// <summary>
    /// Extracts tank data from unified save structure for modular loading.
    /// </summary>
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null) return null;

        DebugLog("Using modular extraction from unified save data");

        var tankData = unifiedData.GetComponentData<OxygenTankSaveData>(SaveID);
        if (tankData != null)
        {
            DebugLog($"Extracted tank from dynamic storage: {(tankData.HasTankEquipped() ? "Tank equipped" : "No tank")}");
            return tankData;
        }

        DebugLog("No tank data found in unified save - returning empty tank");
        return new OxygenTankSaveData();
    }

    /// <summary>
    /// Creates default empty tank for new games.
    /// </summary>
    public object CreateDefaultData()
    {
        DebugLog("Creating default tank data for new game");

        var defaultData = new OxygenTankSaveData();
        defaultData.slot = new OxygenTankSlotSaveData
        {
            equippedTankId = "",
            equippedTankDataName = "",
            instanceID = "",
            tankInstanceData = null
        };

        DebugLog("Default tank data created: empty slot");
        return defaultData;
    }

    /// <summary>
    /// Contributes tank data to unified save structure for save file creation.
    /// </summary>
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is OxygenTankSaveData tankData && unifiedData != null)
        {
            DebugLog($"Contributing tank data to unified save: {(tankData.HasTankEquipped() ? "Tank equipped" : "No tank")}");

            unifiedData.SetComponentData(SaveID, tankData);

            if (tankData.HasTankEquipped())
            {
                DebugLog($"Tank data contributed: {tankData.slot.equippedTankId} with oxygen {tankData.slot.tankInstanceData?.currentOxygen:F1}");
            }
            else
            {
                DebugLog("Tank data contributed: empty slot");
            }
        }
        else
        {
            DebugLog($"Invalid data for contribution - expected OxygenTankSaveData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    /// <summary>
    /// Called before save operations to ensure current references.
    /// </summary>
    public override void OnBeforeSave()
    {
        DebugLog("Preparing tank for save");

        if (autoFindReferences)
        {
            FindTankReferences();
        }
    }

    /// <summary>
    /// Called after load operations. Tank UI updates automatically via events.
    /// </summary>
    public override void OnAfterLoad()
    {
        DebugLog("Tank load completed");
    }
}