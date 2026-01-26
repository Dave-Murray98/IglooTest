using UnityEngine;

/// <summary>
/// Handles saving and loading of player oxygen data as part of the modular save system.
/// This component works with PlayerOxygen to persist oxygen data across scene transitions
/// and save/load operations. Implements the enhanced IPlayerDependentSaveable interface
/// for seamless integration with the unified save system.
/// </summary>
public class PlayerOxygenSaveComponent : SaveComponentBase, IPlayerDependentSaveable
{
    [Header("Component References")]
    [SerializeField] private PlayerOxygen playerOxygen;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindReferences = true;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        saveID = "Player_Oxygen";
        autoGenerateID = false;
        base.Awake();

        if (autoFindReferences)
        {
            FindOxygenReference();
        }
    }

    private void Start()
    {
        ValidateReferences();
    }

    /// <summary>
    /// Finds the PlayerOxygen component reference
    /// </summary>
    private void FindOxygenReference()
    {
        if (playerOxygen == null)
        {
            // First try to find on the same GameObject
            playerOxygen = GetComponent<PlayerOxygen>();

            // If not found, search in the scene
            if (playerOxygen == null)
            {
                playerOxygen = FindFirstObjectByType<PlayerOxygen>();
            }
        }

        DebugLog($"PlayerOxygen reference found: {playerOxygen != null}");
    }

    /// <summary>
    /// Validates that all required references are set
    /// </summary>
    private void ValidateReferences()
    {
        if (playerOxygen == null)
        {
            Debug.LogError($"[{name}] PlayerOxygen reference missing! Oxygen data won't be saved/loaded.");
        }
    }

    /// <summary>
    /// Collects current oxygen data for saving
    /// </summary>
    public override object GetDataToSave()
    {
        if (playerOxygen == null)
        {
            DebugLog("PlayerOxygen reference is null - cannot save oxygen data");
            return null;
        }

        var oxygenData = playerOxygen.GetOxygenData();

        // Note: currentOxygen value is not meaningful with tank system
        // Actual oxygen comes from equipped tank (saved via OxygenTankSaveComponent)
        // We only save depletion and initialization flags
        DebugLog($"Saving oxygen data - Depleted: {oxygenData.isDepleted}, Initialized: {oxygenData.isInitialized}");
        DebugLog("Note: Oxygen value managed by tank system");

        return oxygenData;
    }

    /// <summary>
    /// Extracts oxygen data from save containers
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {

        // NOTE: With tank system, oxygen value comes from equipped tank (OxygenTankSaveComponent)
        // This component only saves/loads depletion and initialization state

        DebugLog("Extracting oxygen data for persistence");

        if (saveContainer is PlayerOxygenData oxygenData)
        {
            DebugLog($"Direct oxygen data extraction - Oxygen: {oxygenData.currentOxygen}");
            return oxygenData;
        }
        else if (saveContainer is PlayerSaveData playerSaveData)
        {
            // Extract from PlayerSaveData if oxygen was stored there
            var extractedOxygen = playerSaveData.GetCustomData<PlayerOxygenData>(SaveID);
            if (extractedOxygen != null)
            {
                DebugLog($"Extracted oxygen from PlayerSaveData - Oxygen: {extractedOxygen.currentOxygen}");
                return extractedOxygen;
            }

            // Fallback: create from basic oxygen field in PlayerSaveData (if it exists)
            // Note: PlayerSaveData doesn't have oxygen fields yet, but this provides future compatibility
            var fallbackOxygen = new PlayerOxygenData
            {
                currentOxygen = 100f, // Default max oxygen
                isDepleted = false,
                isInitialized = true
            };
            DebugLog("Created fallback oxygen data from PlayerSaveData - using defaults");
            return fallbackOxygen;
        }
        else if (saveContainer is PlayerPersistentData persistentData)
        {
            // Try to get oxygen data from persistent data
            var oxygenFromPersistent = persistentData.GetComponentData<PlayerOxygenData>(SaveID);
            if (oxygenFromPersistent != null)
            {
                DebugLog($"Extracted oxygen from persistent data - Oxygen: {oxygenFromPersistent.currentOxygen}");
                return oxygenFromPersistent;
            }

            // Fallback: create default oxygen data
            var fallbackOxygen = new PlayerOxygenData
            {
                currentOxygen = 100f, // Default max oxygen
                isDepleted = false,
                isInitialized = true
            };
            DebugLog("Created fallback oxygen from persistent data - using defaults");
            return fallbackOxygen;
        }

        DebugLog($"Cannot extract oxygen data from container type: {saveContainer?.GetType().Name ?? "null"}");
        return null;
    }

    #region IPlayerDependentSaveable Implementation

    /// <summary>
    /// Extracts oxygen data from unified save structure
    /// </summary>
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null) return null;

        DebugLog("Using modular extraction from unified save data for oxygen");

        // Try to get specific oxygen data first
        var oxygenData = unifiedData.GetComponentData<PlayerOxygenData>(SaveID);
        if (oxygenData != null)
        {
            DebugLog($"Found specific oxygen data - Oxygen: {oxygenData.currentOxygen}");
            return oxygenData;
        }

        // Fallback: create default oxygen data since PlayerPersistentData doesn't have oxygen fields yet
        var fallbackData = new PlayerOxygenData
        {
            currentOxygen = 100f, // Will be set to max oxygen from PlayerData when initialized
            isDepleted = false,
            isInitialized = true
        };

        DebugLog("Created oxygen data from unified basic stats - using defaults");
        return fallbackData;
    }

    /// <summary>
    /// Creates default oxygen data for new games.
    /// Note: With tank system, oxygen comes from equipped tank.
    /// </summary>
    public object CreateDefaultData()
    {
        DebugLog("Creating default oxygen data for new game");

        // With tank system, we just track initialization and depletion
        // Actual oxygen comes from equipped tank
        var defaultOxygen = new PlayerOxygenData
        {
            currentOxygen = 0f, // Not used with tank system
            isDepleted = true, // Start depleted (no tank equipped)
            isInitialized = true
        };

        DebugLog("Default oxygen data created - starts depleted (no tank equipped)");
        return defaultOxygen;
    }

    /// <summary>
    /// Contributes oxygen data to unified save structure
    /// </summary>
    // In ContributeToUnifiedSave method (around line 165), update the log message:
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is PlayerOxygenData oxygenData && unifiedData != null)
        {
            DebugLog("Contributing oxygen data to unified save structure");

            // Store oxygen data in component storage
            unifiedData.SetComponentData(SaveID, oxygenData);

            DebugLog($"Oxygen data contributed - Depleted: {oxygenData.isDepleted}, " +
                    $"Initialized: {oxygenData.isInitialized}");
            DebugLog("Note: Oxygen value managed by tank system (OxygenTankSaveComponent)");
        }
        else
        {
            DebugLog($"Invalid oxygen data for contribution - expected PlayerOxygenData, got {componentData?.GetType().Name ?? "null"}");
        }
    }
    #endregion

    /// <summary>
    /// Restores oxygen data based on context
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (data is not PlayerOxygenData oxygenData)
        {
            DebugLog($"Invalid oxygen data type - expected PlayerOxygenData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        DebugLog($"=== OXYGEN DATA RESTORATION (Context: {context}) ===");
        DebugLog($"Restoring oxygen: {oxygenData.currentOxygen}, Depleted: {oxygenData.isDepleted}");

        // Refresh references in case they changed after scene load
        if (autoFindReferences)
        {
            FindOxygenReference();
        }

        if (playerOxygen == null)
        {
            Debug.LogError("PlayerOxygen reference is null - cannot restore oxygen data!");
            return;
        }

        // Restore oxygen data based on context
        switch (context)
        {
            case RestoreContext.SaveFileLoad:
                DebugLog("Save file load - restoring complete oxygen state");
                RestoreCompleteOxygenState(oxygenData);
                break;

            case RestoreContext.DoorwayTransition:
                DebugLog("Doorway transition - preserving oxygen state");
                RestoreCompleteOxygenState(oxygenData);
                break;

            case RestoreContext.NewGame:
                DebugLog("New game - setting default oxygen");
                playerOxygen.InitializeWithMaxOxygen();
                break;
        }

        DebugLog($"Oxygen data restoration complete for context: {context}");
    }

    /// <summary>
    /// Restores complete oxygen state including depletion status
    /// </summary>
    private void RestoreCompleteOxygenState(PlayerOxygenData oxygenData)
    {
        if (playerOxygen == null) return;

        // Restore the complete oxygen data
        playerOxygen.RestoreOxygenData(oxygenData);

        DebugLog($"Complete oxygen state restored - Oxygen: {oxygenData.currentOxygen}, " +
                $"Depleted: {oxygenData.isDepleted}");
    }

    /// <summary>
    /// Called before save operations to ensure data is current
    /// </summary>
    public override void OnBeforeSave()
    {
        DebugLog("Preparing oxygen data for save");

        if (autoFindReferences)
        {
            FindOxygenReference();
        }
    }

    /// <summary>
    /// Called after load operations to refresh related systems
    /// </summary>
    public override void OnAfterLoad()
    {
        DebugLog("Oxygen data load completed");

        // Trigger any necessary UI updates or system notifications
        if (playerOxygen != null)
        {
            // The PlayerOxygen component will trigger GameEvents for UI updates
            DebugLog("Oxygen restoration complete - UI updates should be triggered");
        }
    }
}