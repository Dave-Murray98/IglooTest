using UnityEngine;

/// <summary>
/// Handles saving and loading of player health data as part of the modular save system.
/// This component works with PlayerHealth to persist health data across scene transitions
/// and save/load operations. Implements the enhanced IPlayerDependentSaveable interface
/// for seamless integration with the unified save system.
/// </summary>
public class PlayerHealthSaveComponent : SaveComponentBase, IPlayerDependentSaveable
{
    [Header("Component References")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindReferences = true;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        saveID = "Player_Health";
        autoGenerateID = false;
        base.Awake();

        if (autoFindReferences)
        {
            FindHealthReference();
        }
    }

    private void Start()
    {
        ValidateReferences();
    }

    /// <summary>
    /// Finds the PlayerHealth component reference
    /// </summary>
    private void FindHealthReference()
    {
        if (playerHealth == null)
        {
            // First try to find on the same GameObject
            playerHealth = GetComponent<PlayerHealth>();

            // If not found, search in the scene
            if (playerHealth == null)
            {
                playerHealth = FindFirstObjectByType<PlayerHealth>();
            }
        }

        DebugLog($"PlayerHealth reference found: {playerHealth != null}");
    }

    /// <summary>
    /// Validates that all required references are set
    /// </summary>
    private void ValidateReferences()
    {
        if (playerHealth == null)
        {
            Debug.LogError($"[{name}] PlayerHealth reference missing! Health data won't be saved/loaded.");
        }
    }

    /// <summary>
    /// Collects current health data for saving
    /// </summary>
    public override object GetDataToSave()
    {
        if (playerHealth == null)
        {
            DebugLog("PlayerHealth reference is null - cannot save health data");
            return null;
        }

        var healthData = playerHealth.GetHealthData();
        DebugLog($"Saving health data - Health: {healthData.currentHealth}, Dead: {healthData.isDead}");

        return healthData;
    }

    /// <summary>
    /// Extracts health data from save containers
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog("Extracting health data for persistence");

        if (saveContainer is PlayerHealthData healthData)
        {
            DebugLog($"Direct health data extraction - Health: {healthData.currentHealth}");
            return healthData;
        }
        else if (saveContainer is PlayerSaveData playerSaveData)
        {
            // Extract from PlayerSaveData if health was stored there
            var extractedHealth = playerSaveData.GetCustomData<PlayerHealthData>(SaveID);
            if (extractedHealth != null)
            {
                DebugLog($"Extracted health from PlayerSaveData - Health: {extractedHealth.currentHealth}");
                return extractedHealth;
            }

            // Fallback: create from basic health field in PlayerSaveData
            var fallbackHealth = new PlayerHealthData
            {
                currentHealth = playerSaveData.currentHealth,
                isDead = playerSaveData.currentHealth <= 0,
                isInitialized = true
            };
            DebugLog($"Created fallback health data from PlayerSaveData - Health: {fallbackHealth.currentHealth}");
            return fallbackHealth;
        }
        else if (saveContainer is PlayerPersistentData persistentData)
        {
            // Try to get health data from persistent data
            var healthFromPersistent = persistentData.GetComponentData<PlayerHealthData>(SaveID);
            if (healthFromPersistent != null)
            {
                DebugLog($"Extracted health from persistent data - Health: {healthFromPersistent.currentHealth}");
                return healthFromPersistent;
            }

            // Fallback: create from basic health in persistent data
            var fallbackHealth = new PlayerHealthData
            {
                currentHealth = persistentData.currentHealth,
                isDead = persistentData.currentHealth <= 0,
                isInitialized = true
            };
            DebugLog($"Created fallback health from persistent data - Health: {fallbackHealth.currentHealth}");
            return fallbackHealth;
        }

        DebugLog($"Cannot extract health data from container type: {saveContainer?.GetType().Name ?? "null"}");
        return null;
    }

    #region IPlayerDependentSaveable Implementation

    /// <summary>
    /// Extracts health data from unified save structure
    /// </summary>
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null) return null;

        DebugLog("Using modular extraction from unified save data for health");

        // Try to get specific health data first
        var healthData = unifiedData.GetComponentData<PlayerHealthData>(SaveID);
        if (healthData != null)
        {
            DebugLog($"Found specific health data - Health: {healthData.currentHealth}");
            return healthData;
        }

        // Fallback: create from basic stats
        var fallbackData = new PlayerHealthData
        {
            currentHealth = unifiedData.currentHealth,
            isDead = unifiedData.currentHealth <= 0,
            isInitialized = true
        };

        DebugLog($"Created health data from unified basic stats - Health: {fallbackData.currentHealth}");
        return fallbackData;
    }

    /// <summary>
    /// Creates default health data for new games
    /// </summary>
    public object CreateDefaultData()
    {
        DebugLog("Creating default health data for new game");

        float defaultMaxHealth = 100f;

        // Try to get max health from PlayerData if available
        if (GameManager.Instance?.playerData != null)
        {
            defaultMaxHealth = GameManager.Instance.playerData.maxHealth;
        }

        var defaultHealth = new PlayerHealthData
        {
            currentHealth = defaultMaxHealth,
            isDead = false,
            isInitialized = true
        };

        DebugLog($"Default health data created - Health: {defaultHealth.currentHealth}");
        return defaultHealth;
    }

    /// <summary>
    /// Contributes health data to unified save structure
    /// </summary>
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is PlayerHealthData healthData && unifiedData != null)
        {
            DebugLog("Contributing health data to unified save structure");

            // Store basic health in main structure for compatibility
            unifiedData.currentHealth = healthData.currentHealth;

            // Store detailed health data in component storage
            unifiedData.SetComponentData(SaveID, healthData);

            DebugLog($"Health data contributed - Health: {healthData.currentHealth}, Dead: {healthData.isDead}");
        }
        else
        {
            DebugLog($"Invalid health data for contribution - expected PlayerHealthData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    /// <summary>
    /// Restores health data based on context
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (data is not PlayerHealthData healthData)
        {
            DebugLog($"Invalid health data type - expected PlayerHealthData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        DebugLog($"=== HEALTH DATA RESTORATION (Context: {context}) ===");
        DebugLog($"Restoring health: {healthData.currentHealth}, Dead: {healthData.isDead}");

        // Refresh references in case they changed after scene load
        if (autoFindReferences)
        {
            FindHealthReference();
        }

        if (playerHealth == null)
        {
            Debug.LogError("PlayerHealth reference is null - cannot restore health data!");
            return;
        }

        // Restore health data based on context
        switch (context)
        {
            case RestoreContext.SaveFileLoad:
                DebugLog("Save file load - restoring complete health state");
                RestoreCompleteHealthState(healthData);
                break;

            case RestoreContext.DoorwayTransition:
                DebugLog("Doorway transition - preserving health state");
                RestoreCompleteHealthState(healthData);
                break;

            case RestoreContext.NewGame:
                DebugLog("New game - setting default health");
                playerHealth.InitializeWithMaxHealth();
                break;
        }

        DebugLog($"Health data restoration complete for context: {context}");
    }

    /// <summary>
    /// Restores complete health state including death status
    /// </summary>
    private void RestoreCompleteHealthState(PlayerHealthData healthData)
    {
        if (playerHealth == null) return;

        // Restore the complete health data
        playerHealth.RestoreHealthData(healthData);

        DebugLog($"Complete health state restored - Health: {healthData.currentHealth}, Dead: {healthData.isDead}");
    }

    /// <summary>
    /// Called before save operations to ensure data is current
    /// </summary>
    public override void OnBeforeSave()
    {
        DebugLog("Preparing health data for save");

        if (autoFindReferences)
        {
            FindHealthReference();
        }
    }

    /// <summary>
    /// Called after load operations to refresh related systems
    /// </summary>
    public override void OnAfterLoad()
    {
        DebugLog("Health data load completed");

        // Trigger any necessary UI updates or system notifications
        if (playerHealth != null)
        {
            // The PlayerHealth component will trigger GameEvents for UI updates
            DebugLog("Health restoration complete - UI updates should be triggered");
        }
    }
}