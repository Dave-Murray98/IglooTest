using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Manages persistence of egg states across scene transitions and save/load operations.
/// Automatically discovers and tracks all EggSaveComponent objects in scenes,
/// assigns consistent IDs using deterministic ordering, and coordinates state saving/restoration.
/// 
/// Follows the same pattern as SceneDestructionStateManager for consistency with the existing save system.
/// </summary>
public class SceneEggStateManager : MonoBehaviour, ISaveable
{
    public static SceneEggStateManager Instance { get; private set; }

    [Header("Save Settings")]
    [SerializeField] private string saveID = "SceneEggStateManager";

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    [Header("Tracking Settings")]
    [SerializeField] private float trackingUpdateInterval = 2f;
    [Tooltip("Time to wait after scene load before initializing egg tracking")]
    [SerializeField] private float initializationDelay = 0.2f;

    // Tracked egg components
    [ShowInInspector]
    private Dictionary<string, EggSaveComponent> trackedEggs = new Dictionary<string, EggSaveComponent>();

    // Initialization tracking
    private bool isInitialized = false;
    private Coroutine initializationCoroutine = null;

    // ISaveable implementation
    public string SaveID => saveID;
    public SaveDataCategory SaveCategory => SaveDataCategory.SceneDependent;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DebugLog("SceneEggStateManager initialized");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        initializationCoroutine = StartCoroutine(InitializeEggTracking());

        if (trackingUpdateInterval > 0)
        {
            InvokeRepeating(nameof(UpdateEggTracking), trackingUpdateInterval, trackingUpdateInterval);
        }
    }

    #region Initialization and Discovery

    /// <summary>
    /// Initializes egg tracking by discovering all components and assigning IDs
    /// </summary>
    private System.Collections.IEnumerator InitializeEggTracking()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(initializationDelay);

        PerformInitialization();
    }

    /// <summary>
    /// Performs the actual initialization logic (separated for reuse)
    /// </summary>
    private void PerformInitialization()
    {
        DebugLog("Starting egg component discovery and ID assignment...");

        // Find all EggSaveComponent objects in the scene
        var allComponents = FindObjectsByType<EggSaveComponent>(FindObjectsSortMode.None).ToList();

        if (allComponents.Count == 0)
        {
            DebugLog("No EggSaveComponent objects found in scene");
            isInitialized = true;
            return;
        }

        DebugLog($"Found {allComponents.Count} egg components");

        // Assign consistent IDs to all components
        AssignConsistentEggIDs(allComponents);

        // Register all components in tracking dictionary
        trackedEggs.Clear();
        foreach (var component in allComponents)
        {
            if (component.HasValidID)
            {
                trackedEggs[component.EggID] = component;
                DebugLog($"Registered egg component: {component.EggID}");
            }
            else
            {
                Debug.LogWarning($"[SceneEggStateManager] Component on {component.gameObject.name} has no valid ID - skipping");
            }
        }

        isInitialized = true;
        DebugLog($"Egg tracking initialized: {trackedEggs.Count} components tracked");
    }

    /// <summary>
    /// Forces immediate initialization without waiting for coroutine delays.
    /// Used when restoration needs to happen before normal initialization timing.
    /// </summary>
    private void ForceInitializeIfNeeded()
    {
        if (isInitialized)
        {
            DebugLog("Already initialized - skipping force initialization");
            return;
        }

        DebugLog("⚡ Force initialization triggered (restoration called before normal init)");

        // Stop the normal initialization coroutine if it's running
        if (initializationCoroutine != null)
        {
            StopCoroutine(initializationCoroutine);
            initializationCoroutine = null;
        }

        // Perform initialization immediately
        PerformInitialization();
    }

    /// <summary>
    /// Assigns consistent IDs using deterministic ordering based on GetInstanceID().
    /// Follows the same pattern as the destruction system for consistency.
    /// </summary>
    private void AssignConsistentEggIDs(List<EggSaveComponent> components)
    {
        DebugLog("=== ASSIGNING CONSISTENT EGG IDs ===");

        // Group components by cleaned GameObject name
        var componentGroups = components
            .GroupBy(c => c.gameObject.name.Replace(" ", "_"))
            .ToList();

        DebugLog($"Found {componentGroups.Count} unique object name groups");

        foreach (var group in componentGroups)
        {
            string baseName = group.Key;
            // Sort by Instance ID for deterministic ordering
            var componentsInGroup = group.OrderBy(c => c.GetInstanceID()).ToList();

            DebugLog($"Processing group '{baseName}' with {componentsInGroup.Count} components");

            for (int i = 0; i < componentsInGroup.Count; i++)
            {
                var component = componentsInGroup[i];
                string consistentID = $"{baseName}_{(i + 1):D2}";
                component.SetEggID(consistentID);
                DebugLog($"  Assigned ID '{consistentID}' to instance {component.GetInstanceID()}");
            }
        }

        // Validate all IDs are unique
        ValidateUniqueIDs(components);

        DebugLog("=== ID ASSIGNMENT COMPLETE ===");
    }

    /// <summary>
    /// Validates that all assigned IDs are unique
    /// </summary>
    private void ValidateUniqueIDs(List<EggSaveComponent> components)
    {
        var idSet = new HashSet<string>();
        var duplicates = new List<string>();

        foreach (var component in components)
        {
            if (!string.IsNullOrEmpty(component.EggID))
            {
                if (!idSet.Add(component.EggID))
                {
                    duplicates.Add(component.EggID);
                }
            }
        }

        if (duplicates.Count > 0)
        {
            Debug.LogError($"[SceneEggStateManager] ID COLLISION DETECTED! Duplicate IDs: {string.Join(", ", duplicates)}");
        }
        else
        {
            DebugLog($"✓ ID validation passed - all {idSet.Count} IDs are unique");
        }
    }

    /// <summary>
    /// Periodic update to track egg state changes
    /// </summary>
    private void UpdateEggTracking()
    {
        // This runs periodically to keep tracking data fresh
        // Currently just a placeholder - could be extended if needed
        foreach (var kvp in trackedEggs)
        {
            if (kvp.Value == null)
            {
                DebugLog($"WARNING: Tracked component '{kvp.Key}' has been destroyed");
            }
        }
    }

    #endregion

    #region ISaveable Implementation

    /// <summary>
    /// Collects egg state data from all tracked components for saving
    /// </summary>
    public object GetDataToSave()
    {
        DebugLog("=== COLLECTING EGG STATE DATA ===");

        var saveData = new EggStateSaveData
        {
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            lastUpdated = System.DateTime.Now
        };

        int savedCount = 0;
        int skippedCount = 0;

        foreach (var kvp in trackedEggs)
        {
            string id = kvp.Key;
            EggSaveComponent component = kvp.Value;

            if (component == null)
            {
                DebugLog($"Skipping null component: {id}");
                skippedCount++;
                continue;
            }

            try
            {
                // Get current egg state from component
                EggSaveData eggData = component.SaveData();

                if (eggData != null)
                {
                    saveData.SetEggState(id, eggData);
                    savedCount++;
                    DebugLog($"Saved egg state: {id} (isBroken = {eggData.isBroken})");
                }
                else
                {
                    DebugLog($"No data for: {id}");
                    skippedCount++;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SceneEggStateManager] Failed to save {id}: {e.Message}");
                skippedCount++;
            }
        }

        DebugLog($"Egg state collection complete: {savedCount} saved, {skippedCount} skipped");
        DebugLog(saveData.GetDebugInfo());

        return saveData;
    }

    /// <summary>
    /// Extracts egg state data from save containers
    /// </summary>
    public object ExtractRelevantData(object saveContainer)
    {
        if (saveContainer is SceneSaveData sceneData)
        {
            var extractedData = sceneData.GetObjectData<EggStateSaveData>(SaveID);
            if (extractedData != null)
            {
                DebugLog($"Extracted egg data: {extractedData.Count} eggs");
                return extractedData;
            }
        }
        else if (saveContainer is EggStateSaveData eggData)
        {
            DebugLog($"Direct egg data: {eggData.Count} eggs");
            return eggData;
        }

        DebugLog("No egg data found in save container");
        return null;
    }

    /// <summary>
    /// Restores egg states with context awareness
    /// CRITICAL: Ensures initialization has happened before attempting restoration
    /// </summary>
    public void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (data is not EggStateSaveData saveData)
        {
            DebugLog($"Invalid data type for restoration - expected EggStateSaveData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        DebugLog($"=== RESTORING EGG STATES (Context: {context}) ===");

        // CRITICAL FIX: Ensure components are discovered and IDs assigned before restoration
        if (!isInitialized)
        {
            DebugLog("⚠️ Restoration called before initialization - forcing immediate initialization");
            ForceInitializeIfNeeded();
        }

        DebugLog(saveData.GetDebugInfo());

        switch (context)
        {
            case RestoreContext.NewGame:
                DebugLog("New game - clearing all egg states (all eggs will be intact)");
                // Don't restore anything - let eggs start fresh (intact)
                break;

            case RestoreContext.DoorwayTransition:
            case RestoreContext.SaveFileLoad:
                RestoreEggStates(saveData);
                break;
        }
    }

    /// <summary>
    /// Applies saved egg states to components in the scene
    /// </summary>
    private void RestoreEggStates(EggStateSaveData saveData)
    {
        if (saveData == null || saveData.Count == 0)
        {
            DebugLog("No egg states to restore");
            return;
        }

        int restoredCount = 0;
        int notFoundCount = 0;

        foreach (string eggID in saveData.GetAllEggIDs())
        {
            // Find the corresponding component
            if (trackedEggs.TryGetValue(eggID, out EggSaveComponent component))
            {
                if (component == null)
                {
                    DebugLog($"Component '{eggID}' is null - skipping");
                    notFoundCount++;
                    continue;
                }

                // Get saved egg data
                EggSaveData eggData = saveData.GetEggState(eggID);

                if (eggData != null)
                {
                    try
                    {
                        component.RestoreData(eggData);
                        restoredCount++;
                        DebugLog($"Restored egg state: {eggID}");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[SceneEggStateManager] Failed to restore {eggID}: {e.Message}");
                    }
                }
            }
            else
            {
                DebugLog($"Component not found for saved ID: {eggID}");
                notFoundCount++;
            }
        }

        DebugLog($"Restoration complete: {restoredCount} restored, {notFoundCount} not found");
    }

    /// <summary>
    /// Called before save operations
    /// </summary>
    public void OnBeforeSave()
    {
        DebugLog("Preparing egg states for save");
        // Could refresh tracking here if needed
    }

    /// <summary>
    /// Called after load operations
    /// </summary>
    public void OnAfterLoad()
    {
        DebugLog("Egg state restoration complete");
    }

    #endregion

    #region Public API

    /// <summary>
    /// Manually register an egg component (useful for runtime-spawned eggs)
    /// </summary>
    public void RegisterEggComponent(EggSaveComponent component)
    {
        if (component == null || !component.HasValidID)
        {
            Debug.LogWarning("[SceneEggStateManager] Cannot register component - null or no valid ID");
            return;
        }

        if (trackedEggs.ContainsKey(component.EggID))
        {
            Debug.LogWarning($"[SceneEggStateManager] Component '{component.EggID}' already registered");
            return;
        }

        trackedEggs[component.EggID] = component;
        DebugLog($"Manually registered egg component: {component.EggID}");
    }

    /// <summary>
    /// Unregister an egg component
    /// </summary>
    public void UnregisterEggComponent(string eggID)
    {
        if (string.IsNullOrEmpty(eggID))
            return;

        if (trackedEggs.Remove(eggID))
        {
            DebugLog($"Unregistered egg component: {eggID}");
        }
    }

    /// <summary>
    /// Gets a tracked component by ID
    /// </summary>
    public EggSaveComponent GetSaveComponent(string eggID)
    {
        if (string.IsNullOrEmpty(eggID))
            return null;

        if (trackedEggs.TryGetValue(eggID, out var component))
        {
            return component;
        }

        return null;
    }

    /// <summary>
    /// Gets all tracked egg IDs
    /// </summary>
    public List<string> GetAllTrackedIDs()
    {
        return new List<string>(trackedEggs.Keys);
    }

    /// <summary>
    /// Gets count of tracked components
    /// </summary>
    public int GetTrackedCount()
    {
        return trackedEggs.Count;
    }

    /// <summary>
    /// Checks if the manager has completed initialization
    /// </summary>
    public bool IsInitialized()
    {
        return isInitialized;
    }

    /// <summary>
    /// Gets initialization status information for debugging
    /// </summary>
    public string GetInitializationStatus()
    {
        return $"Initialized: {isInitialized}, Tracked Components: {trackedEggs.Count}";
    }

    #endregion

    #region Debug Methods

    [Button("Debug All Tracked Eggs")]
    public void DebugAllTrackedEggs()
    {
        DebugLog("=== ALL TRACKED EGGS DEBUG ===");
        DebugLog($"Initialization Status: {GetInitializationStatus()}");
        DebugLog($"Total Tracked: {trackedEggs.Count}");

        foreach (var kvp in trackedEggs)
        {
            if (kvp.Value != null)
            {
                DebugLog($"  {kvp.Value.GetDebugInfo()}");
            }
            else
            {
                DebugLog($"  {kvp.Key}: NULL COMPONENT");
            }
        }

        DebugLog("======================================");
    }

    [Button("Force Save Current State")]
    public void ForceSaveCurrentState()
    {
        var data = GetDataToSave();
        DebugLog("Force save complete - see console for details");
    }

    [Button("Refresh Tracking")]
    public void RefreshTracking()
    {
        if (initializationCoroutine != null)
        {
            StopCoroutine(initializationCoroutine);
        }

        isInitialized = false;
        trackedEggs.Clear();
        initializationCoroutine = StartCoroutine(InitializeEggTracking());
        DebugLog("Tracking refresh initiated");
    }

    #endregion

    #region Utility Methods

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SceneEggStateManager] {message}");
        }
    }

    private void OnDestroy()
    {
        CancelInvoke(nameof(UpdateEggTracking));
    }

    #endregion
}