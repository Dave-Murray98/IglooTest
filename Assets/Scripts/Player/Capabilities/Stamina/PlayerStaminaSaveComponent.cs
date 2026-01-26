using UnityEngine;

/// <summary>
/// Handles saving and loading of player stamina data as part of the modular save system.
/// This component works with PlayerStamina to persist stamina data across scene transitions
/// and save/load operations. Implements the enhanced IPlayerDependentSaveable interface
/// for seamless integration with the unified save system.
/// </summary>
public class PlayerStaminaSaveComponent : SaveComponentBase, IPlayerDependentSaveable
{
    [Header("Component References")]
    [SerializeField] private PlayerStamina playerStamina;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindReferences = true;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        saveID = "Player_Stamina";
        autoGenerateID = false;
        base.Awake();

        if (autoFindReferences)
        {
            FindStaminaReference();
        }
    }

    private void Start()
    {
        ValidateReferences();
    }

    /// <summary>
    /// Finds the PlayerStamina component reference
    /// </summary>
    private void FindStaminaReference()
    {
        if (playerStamina == null)
        {
            // First try to find on the same GameObject
            playerStamina = GetComponent<PlayerStamina>();

            // If not found, search in the scene
            if (playerStamina == null)
            {
                playerStamina = FindFirstObjectByType<PlayerStamina>();
            }
        }

        DebugLog($"PlayerStamina reference found: {playerStamina != null}");
    }

    /// <summary>
    /// Validates that all required references are set
    /// </summary>
    private void ValidateReferences()
    {
        if (playerStamina == null)
        {
            Debug.LogError($"[{name}] PlayerStamina reference missing! Stamina data won't be saved/loaded.");
        }
    }

    /// <summary>
    /// Collects current stamina data for saving
    /// </summary>
    public override object GetDataToSave()
    {
        if (playerStamina == null)
        {
            DebugLog("PlayerStamina reference is null - cannot save stamina data");
            return null;
        }

        var staminaData = playerStamina.GetStaminaData();
        DebugLog($"Saving stamina data - Stamina: {staminaData.currentStamina}, Depleted: {staminaData.isDepleted}, " +
                $"UsingStamina: {staminaData.usingStamina}");

        return staminaData;
    }

    /// <summary>
    /// Extracts stamina data from save containers
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog("Extracting stamina data for persistence");

        if (saveContainer is PlayerStaminaData staminaData)
        {
            DebugLog($"Direct stamina data extraction - Stamina: {staminaData.currentStamina}");
            return staminaData;
        }
        else if (saveContainer is PlayerSaveData playerSaveData)
        {
            // Extract from PlayerSaveData if stamina was stored there
            var extractedStamina = playerSaveData.GetCustomData<PlayerStaminaData>(SaveID);
            if (extractedStamina != null)
            {
                DebugLog($"Extracted stamina from PlayerSaveData - Stamina: {extractedStamina.currentStamina}");
                return extractedStamina;
            }

            // Fallback: create from basic stamina field in PlayerSaveData (if it exists)
            // Note: PlayerSaveData doesn't have stamina fields yet, but this provides future compatibility
            var fallbackStamina = new PlayerStaminaData
            {
                currentStamina = 100f, // Default max stamina
                isDepleted = false,
                isInitialized = true,
                usingStamina = false
            };
            DebugLog("Created fallback stamina data from PlayerSaveData - using defaults");
            return fallbackStamina;
        }
        else if (saveContainer is PlayerPersistentData persistentData)
        {
            // Try to get stamina data from persistent data
            var staminaFromPersistent = persistentData.GetComponentData<PlayerStaminaData>(SaveID);
            if (staminaFromPersistent != null)
            {
                DebugLog($"Extracted stamina from persistent data - Stamina: {staminaFromPersistent.currentStamina}");
                return staminaFromPersistent;
            }

            // Fallback: create default stamina data
            var fallbackStamina = new PlayerStaminaData
            {
                currentStamina = 100f, // Default max stamina
                isDepleted = false,
                isInitialized = true,
                usingStamina = false
            };
            DebugLog("Created fallback stamina from persistent data - using defaults");
            return fallbackStamina;
        }

        DebugLog($"Cannot extract stamina data from container type: {saveContainer?.GetType().Name ?? "null"}");
        return null;
    }

    #region IPlayerDependentSaveable Implementation

    /// <summary>
    /// Extracts stamina data from unified save structure
    /// </summary>
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null) return null;

        DebugLog("Using modular extraction from unified save data for stamina");

        // Try to get specific stamina data first
        var staminaData = unifiedData.GetComponentData<PlayerStaminaData>(SaveID);
        if (staminaData != null)
        {
            DebugLog($"Found specific stamina data - Stamina: {staminaData.currentStamina}");
            return staminaData;
        }

        // Fallback: create default stamina data since PlayerPersistentData doesn't have stamina fields yet
        var fallbackData = new PlayerStaminaData
        {
            currentStamina = 100f, // Will be set to max stamina from PlayerData when initialized
            isDepleted = false,
            isInitialized = true,
            usingStamina = false
        };

        DebugLog("Created stamina data from unified basic stats - using defaults");
        return fallbackData;
    }

    /// <summary>
    /// Creates default stamina data for new games
    /// </summary>
    public object CreateDefaultData()
    {
        DebugLog("Creating default stamina data for new game");

        float defaultMaxStamina = 100f;

        // Try to get max stamina from PlayerData if available
        if (GameManager.Instance?.playerData != null)
        {
            defaultMaxStamina = GameManager.Instance.playerData.maxStamina;
        }

        var defaultStamina = new PlayerStaminaData
        {
            currentStamina = defaultMaxStamina,
            isDepleted = false,
            isInitialized = true,
            usingStamina = false
        };

        DebugLog($"Default stamina data created - Stamina: {defaultStamina.currentStamina}");
        return defaultStamina;
    }

    /// <summary>
    /// Contributes stamina data to unified save structure
    /// </summary>
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is PlayerStaminaData staminaData && unifiedData != null)
        {
            DebugLog("Contributing stamina data to unified save structure");

            // Store stamina data in component storage
            // Note: PlayerPersistentData doesn't have basic stamina fields yet,
            // but we store in component data for full preservation
            unifiedData.SetComponentData(SaveID, staminaData);

            DebugLog($"Stamina data contributed - Stamina: {staminaData.currentStamina}, " +
                    $"Depleted: {staminaData.isDepleted}, UsingStamina: {staminaData.usingStamina}");
        }
        else
        {
            DebugLog($"Invalid stamina data for contribution - expected PlayerStaminaData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    /// <summary>
    /// Restores stamina data based on context
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (data is not PlayerStaminaData staminaData)
        {
            DebugLog($"Invalid stamina data type - expected PlayerStaminaData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        DebugLog($"=== STAMINA DATA RESTORATION (Context: {context}) ===");
        DebugLog($"Restoring stamina: {staminaData.currentStamina}, Depleted: {staminaData.isDepleted}, " +
                $"UsingStamina: {staminaData.usingStamina}");

        // Refresh references in case they changed after scene load
        if (autoFindReferences)
        {
            FindStaminaReference();
        }

        if (playerStamina == null)
        {
            Debug.LogError("PlayerStamina reference is null - cannot restore stamina data!");
            return;
        }

        // Restore stamina data based on context
        switch (context)
        {
            case RestoreContext.SaveFileLoad:
                DebugLog("Save file load - restoring complete stamina state");
                RestoreCompleteStaminaState(staminaData);
                break;

            case RestoreContext.DoorwayTransition:
                DebugLog("Doorway transition - preserving stamina state");
                RestoreCompleteStaminaState(staminaData);
                break;

            case RestoreContext.NewGame:
                DebugLog("New game - setting default stamina");
                playerStamina.InitializeWithMaxStamina();
                break;
        }

        DebugLog($"Stamina data restoration complete for context: {context}");
    }

    /// <summary>
    /// Restores complete stamina state including depletion and usage status
    /// </summary>
    private void RestoreCompleteStaminaState(PlayerStaminaData staminaData)
    {
        if (playerStamina == null) return;

        // Restore the complete stamina data
        playerStamina.RestoreStaminaData(staminaData);

        DebugLog($"Complete stamina state restored - Stamina: {staminaData.currentStamina}, " +
                $"Depleted: {staminaData.isDepleted}, UsingStamina: {staminaData.usingStamina}");
    }

    /// <summary>
    /// Called before save operations to ensure data is current
    /// </summary>
    public override void OnBeforeSave()
    {
        DebugLog("Preparing stamina data for save");

        if (autoFindReferences)
        {
            FindStaminaReference();
        }
    }

    /// <summary>
    /// Called after load operations to refresh related systems
    /// </summary>
    public override void OnAfterLoad()
    {
        DebugLog("Stamina data load completed");

        // Trigger any necessary UI updates or system notifications
        if (playerStamina != null)
        {
            // The PlayerStamina component will trigger events for UI updates
            DebugLog("Stamina restoration complete - UI updates should be triggered");
        }
    }
}