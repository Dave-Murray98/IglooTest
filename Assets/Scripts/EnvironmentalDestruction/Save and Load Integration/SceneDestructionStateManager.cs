using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Manages persistence of environmental destruction states across scene transitions and save/load operations.
/// Automatically discovers and tracks all RayfireShatterSaveComponent objects in scenes,
/// assigns consistent IDs using deterministic ordering, and coordinates state saving/restoration.
/// 
/// Follows the same pattern as SceneItemStateManager for consistency with the existing save system.
/// </summary>
public class SceneDestructionStateManager : MonoBehaviour, ISaveable
{
    public static SceneDestructionStateManager Instance { get; private set; }

    [Header("Save Settings")]
    [SerializeField] private string saveID = "SceneDestructionStateManager";

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    [Header("Tracking Settings")]
    [SerializeField] private float trackingUpdateInterval = 2f;
    [Tooltip("Time to wait after scene load before initializing destruction tracking")]
    [SerializeField] private float initializationDelay = 0.2f;

    // Tracked destruction components
    [ShowInInspector]
    private Dictionary<string, RayfireShatterSaveComponent> trackedDestructibles = new Dictionary<string, RayfireShatterSaveComponent>();

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
            DebugLog("SceneDestructionStateManager initialized");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        initializationCoroutine = StartCoroutine(InitializeDestructionTracking());

        if (trackingUpdateInterval > 0)
        {
            InvokeRepeating(nameof(UpdateDestructionTracking), trackingUpdateInterval, trackingUpdateInterval);
        }
    }

    #region Initialization and Discovery

    /// <summary>
    /// Initializes destruction tracking by discovering all components and assigning IDs
    /// </summary>
    private System.Collections.IEnumerator InitializeDestructionTracking()
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
        DebugLog("Starting destruction component discovery and ID assignment...");

        // Find all RayfireShatterSaveComponent objects in the scene
        var allComponents = FindObjectsByType<RayfireShatterSaveComponent>(FindObjectsSortMode.None).ToList();

        if (allComponents.Count == 0)
        {
            DebugLog("No RayfireShatterSaveComponent objects found in scene");
            isInitialized = true;
            return;
        }

        DebugLog($"Found {allComponents.Count} destruction components");

        // Assign consistent IDs to all components
        AssignConsistentDestructionIDs(allComponents);

        // Register all components in tracking dictionary
        trackedDestructibles.Clear();
        foreach (var component in allComponents)
        {
            if (component.HasValidID)
            {
                trackedDestructibles[component.DestructionID] = component;
                DebugLog($"Registered destruction component: {component.DestructionID}");
            }
            else
            {
                Debug.LogWarning($"[SceneDestructionStateManager] Component on {component.gameObject.name} has no valid ID - skipping");
            }
        }

        isInitialized = true;
        DebugLog($"Destruction tracking initialized: {trackedDestructibles.Count} components tracked");
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
    /// Adapted from the vehicle ID assignment system for consistency.
    /// </summary>
    private void AssignConsistentDestructionIDs(List<RayfireShatterSaveComponent> components)
    {
        DebugLog("=== ASSIGNING CONSISTENT DESTRUCTION IDs ===");

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
                component.SetDestructionID(consistentID);
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
    private void ValidateUniqueIDs(List<RayfireShatterSaveComponent> components)
    {
        var idSet = new HashSet<string>();
        var duplicates = new List<string>();

        foreach (var component in components)
        {
            if (!string.IsNullOrEmpty(component.DestructionID))
            {
                if (!idSet.Add(component.DestructionID))
                {
                    duplicates.Add(component.DestructionID);
                }
            }
        }

        if (duplicates.Count > 0)
        {
            Debug.LogError($"[SceneDestructionStateManager] ID COLLISION DETECTED! Duplicate IDs: {string.Join(", ", duplicates)}");
        }
        else
        {
            DebugLog($"✓ ID validation passed - all {idSet.Count} IDs are unique");
        }
    }

    /// <summary>
    /// Periodic update to track destruction state changes
    /// </summary>
    private void UpdateDestructionTracking()
    {
        // This runs periodically to keep tracking data fresh
        // Currently just a placeholder - could be extended to detect new activations
        foreach (var kvp in trackedDestructibles)
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
    /// Collects destruction state data from all tracked components for saving
    /// </summary>
    public object GetDataToSave()
    {
        DebugLog("=== COLLECTING DESTRUCTION STATE DATA ===");

        var saveData = new DestructionStateSaveData
        {
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            lastUpdated = System.DateTime.Now
        };

        int savedCount = 0;
        int skippedCount = 0;

        foreach (var kvp in trackedDestructibles)
        {
            string id = kvp.Key;
            RayfireShatterSaveComponent component = kvp.Value;

            if (component == null)
            {
                DebugLog($"Skipping null component: {id}");
                skippedCount++;
                continue;
            }

            try
            {
                // Get current destruction state from component
                List<RayfireShatterFragData> fragmentData = component.SaveData();

                if (fragmentData != null && fragmentData.Count > 0)
                {
                    saveData.SetDestructionState(id, fragmentData);
                    savedCount++;
                    DebugLog($"Saved destruction state: {id} ({fragmentData.Count} fragments)");
                }
                else
                {
                    DebugLog($"No fragment data for: {id}");
                    skippedCount++;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SceneDestructionStateManager] Failed to save {id}: {e.Message}");
                skippedCount++;
            }
        }

        DebugLog($"Destruction state collection complete: {savedCount} saved, {skippedCount} skipped");
        DebugLog(saveData.GetDebugInfo());

        return saveData;
    }

    /// <summary>
    /// Extracts destruction state data from save containers
    /// </summary>
    public object ExtractRelevantData(object saveContainer)
    {
        if (saveContainer is SceneSaveData sceneData)
        {
            var extractedData = sceneData.GetObjectData<DestructionStateSaveData>(SaveID);
            if (extractedData != null)
            {
                DebugLog($"Extracted destruction data: {extractedData.Count} objects");
                return extractedData;
            }
        }
        else if (saveContainer is DestructionStateSaveData destructionData)
        {
            DebugLog($"Direct destruction data: {destructionData.Count} objects");
            return destructionData;
        }

        DebugLog("No destruction data found in save container");
        return null;
    }

    /// <summary>
    /// Restores destruction states with context awareness
    /// CRITICAL: Ensures initialization has happened before attempting restoration
    /// </summary>
    public void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (data is not DestructionStateSaveData saveData)
        {
            DebugLog($"Invalid data type for restoration - expected DestructionStateSaveData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        DebugLog($"=== RESTORING DESTRUCTION STATES (Context: {context}) ===");

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
                DebugLog("New game - clearing all destruction states");
                // Don't restore anything - let objects start fresh
                break;

            case RestoreContext.DoorwayTransition:
            case RestoreContext.SaveFileLoad:
                RestoreDestructionStates(saveData);
                break;
        }
    }

    /// <summary>
    /// Applies saved destruction states to components in the scene
    /// </summary>
    private void RestoreDestructionStates(DestructionStateSaveData saveData)
    {
        if (saveData == null || saveData.Count == 0)
        {
            DebugLog("No destruction states to restore");
            return;
        }

        int restoredCount = 0;
        int notFoundCount = 0;

        foreach (string destructionID in saveData.GetAllDestructionIDs())
        {
            // Find the corresponding component
            if (trackedDestructibles.TryGetValue(destructionID, out RayfireShatterSaveComponent component))
            {
                if (component == null)
                {
                    DebugLog($"Component '{destructionID}' is null - skipping");
                    notFoundCount++;
                    continue;
                }

                // Get saved fragment data
                List<RayfireShatterFragData> fragmentData = saveData.GetDestructionState(destructionID);

                if (fragmentData != null && fragmentData.Count > 0)
                {
                    try
                    {
                        component.RestoreData(fragmentData);
                        restoredCount++;
                        DebugLog($"Restored destruction state: {destructionID}");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[SceneDestructionStateManager] Failed to restore {destructionID}: {e.Message}");
                    }
                }
            }
            else
            {
                DebugLog($"Component not found for saved ID: {destructionID}");
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
        DebugLog("Preparing destruction states for save");
        // Could refresh tracking here if needed
    }

    /// <summary>
    /// Called after load operations
    /// </summary>
    public void OnAfterLoad()
    {
        DebugLog("Destruction state restoration complete");
    }

    #endregion

    #region Public API

    /// <summary>
    /// Manually register a destruction component (useful for runtime-spawned objects)
    /// </summary>
    public void RegisterDestructionComponent(RayfireShatterSaveComponent component)
    {
        if (component == null || !component.HasValidID)
        {
            Debug.LogWarning("[SceneDestructionStateManager] Cannot register component - null or no valid ID");
            return;
        }

        if (trackedDestructibles.ContainsKey(component.DestructionID))
        {
            Debug.LogWarning($"[SceneDestructionStateManager] Component '{component.DestructionID}' already registered");
            return;
        }

        trackedDestructibles[component.DestructionID] = component;
        DebugLog($"Manually registered destruction component: {component.DestructionID}");
    }

    /// <summary>
    /// Unregister a destruction component
    /// </summary>
    public void UnregisterDestructionComponent(string destructionID)
    {
        if (string.IsNullOrEmpty(destructionID))
            return;

        if (trackedDestructibles.Remove(destructionID))
        {
            DebugLog($"Unregistered destruction component: {destructionID}");
        }
    }

    /// <summary>
    /// Gets a tracked component by ID
    /// </summary>
    public RayfireShatterSaveComponent GetSaveComponent(string destructionID)
    {
        if (string.IsNullOrEmpty(destructionID))
            return null;

        if (trackedDestructibles.TryGetValue(destructionID, out var component))
        {
            return component;
        }

        return null;
    }

    /// <summary>
    /// Gets all tracked destruction IDs
    /// </summary>
    public List<string> GetAllTrackedIDs()
    {
        return new List<string>(trackedDestructibles.Keys);
    }

    /// <summary>
    /// Gets count of tracked components
    /// </summary>
    public int GetTrackedCount()
    {
        return trackedDestructibles.Count;
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
        return $"Initialized: {isInitialized}, Tracked Components: {trackedDestructibles.Count}";
    }

    #endregion

    #region Debug Methods

    [Button("Debug All Tracked Destructibles")]
    public void DebugAllTrackedDestructibles()
    {
        DebugLog("=== ALL TRACKED DESTRUCTIBLES DEBUG ===");
        DebugLog($"Initialization Status: {GetInitializationStatus()}");
        DebugLog($"Total Tracked: {trackedDestructibles.Count}");

        foreach (var kvp in trackedDestructibles)
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
        trackedDestructibles.Clear();
        initializationCoroutine = StartCoroutine(InitializeDestructionTracking());
        DebugLog("Tracking refresh initiated");
    }

    #endregion

    #region Utility Methods

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SceneDestructionStateManager] {message}");
        }
    }

    private void OnDestroy()
    {
        CancelInvoke(nameof(UpdateDestructionTracking));
    }

    #endregion
}