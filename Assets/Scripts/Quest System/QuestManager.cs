using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// Central manager for tracking quest completion across all scenes.
/// Integrates with your save system to persist quest states.
/// FIXED: Now properly extracts quest data from all save container types
/// </summary>
public class QuestManager : MonoBehaviour, IPlayerDependentSaveable, IManagerState
{
    public static QuestManager Instance { get; private set; }

    [Header("Quest Tracking")]
    [ShowInInspector, ReadOnly]
    private HashSet<string> completedQuests = new HashSet<string>();

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Events for other systems to react to quest completion
    public System.Action<string> OnQuestCompleted;
    public System.Action<string> OnQuestReset;
    public System.Action<string> OnQuestManagerFinishedLoading;

    // ISaveable implementation
    public string SaveID => "QuestManager";
    public SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    private ManagerOperationalState operationalState = ManagerOperationalState.Gameplay;
    public ManagerOperationalState CurrentOperationalState => operationalState;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            DebugLog("QuestManager initialized");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #region Quest Status

    /// <summary>
    /// Check if a quest has been completed
    /// </summary>
    public bool IsQuestComplete(string questID)
    {
        if (string.IsNullOrEmpty(questID))
            return false;

        return completedQuests.Contains(questID);
    }

    /// <summary>
    /// Mark a quest as complete
    /// </summary>
    public void CompleteQuest(string questID)
    {
        if (string.IsNullOrEmpty(questID))
        {
            Debug.LogWarning("[QuestManager] Attempted to complete quest with empty ID");
            return;
        }

        if (completedQuests.Contains(questID))
        {
            DebugLog($"Quest already complete: {questID}");
            return;
        }

        completedQuests.Add(questID);
        DebugLog($"Quest completed: {questID}");

        // Fire event for other systems to react
        OnQuestCompleted?.Invoke(questID);
    }

    /// <summary>
    /// Reset a quest (useful for repeatable quests or debugging)
    /// </summary>
    public void ResetQuest(string questID)
    {
        if (string.IsNullOrEmpty(questID))
            return;

        if (completedQuests.Remove(questID))
        {
            DebugLog($"Quest reset: {questID}");
            OnQuestReset?.Invoke(questID);
        }
    }

    /// <summary>
    /// Check if multiple quests are all complete
    /// </summary>
    public bool AreAllQuestsComplete(string[] questIDs)
    {
        if (questIDs == null || questIDs.Length == 0)
            return true;

        foreach (string questID in questIDs)
        {
            if (!IsQuestComplete(questID))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Get list of all completed quest IDs
    /// </summary>
    public List<string> GetCompletedQuests()
    {
        return new List<string>(completedQuests);
    }

    /// <summary>
    /// Get count of completed quests
    /// </summary>
    public int GetCompletedQuestCount()
    {
        return completedQuests.Count;
    }

    /// <summary>
    /// Clear all quest completion (useful for new game)
    /// </summary>
    public void ClearAllQuests()
    {
        completedQuests.Clear();
        DebugLog("All quests cleared");
    }

    #endregion

    #region ISaveable Implementation

    public object GetDataToSave()
    {
        var questData = new QuestManagerSaveData
        {
            completedQuestIDs = new List<string>(completedQuests)
        };

        DebugLog($"Saving quest data - {questData.completedQuestIDs.Count} completed quests");
        return questData;
    }

    /// <summary>
    /// FIXED: Now properly extracts quest data from all container types
    /// </summary>
    public object ExtractRelevantData(object saveContainer)
    {
        DebugLog("Extracting quest data for persistence");

        // Direct quest data
        if (saveContainer is QuestManagerSaveData questData)
        {
            DebugLog($"Direct quest data extraction - {questData.completedQuestIDs.Count} quests");
            return questData;
        }
        // From PlayerPersistentData (unified save)
        else if (saveContainer is PlayerPersistentData persistentData)
        {
            var extractedQuests = persistentData.GetComponentData<QuestManagerSaveData>(SaveID);
            if (extractedQuests != null)
            {
                DebugLog($"Extracted quest data from persistent data - {extractedQuests.completedQuestIDs.Count} quests");
                return extractedQuests;
            }
        }
        // CRITICAL FIX: From PlayerSaveData (might have it in customStats)
        else if (saveContainer is PlayerSaveData playerSaveData)
        {
            var extractedQuests = playerSaveData.GetCustomData<QuestManagerSaveData>(SaveID);
            if (extractedQuests != null)
            {
                DebugLog($"Extracted quest data from PlayerSaveData customStats - {extractedQuests.completedQuestIDs.Count} quests");
                return extractedQuests;
            }
            else
            {
                DebugLog("No quest data found in PlayerSaveData customStats - returning empty quest data");
                // Return empty data instead of null to prevent errors
                return new QuestManagerSaveData { completedQuestIDs = new List<string>() };
            }
        }

        DebugLog($"Cannot extract quest data from container type: {saveContainer?.GetType().Name ?? "null"} - returning empty");
        // Return empty data instead of null
        return new QuestManagerSaveData { completedQuestIDs = new List<string>() };
    }

    public void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (data is not QuestManagerSaveData saveData)
        {
            DebugLog($"Invalid quest data type - expected QuestManagerSaveData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        DebugLog($"=== QUEST DATA RESTORATION (Context: {context}) ===");
        DebugLog($"Loading {saveData.completedQuestIDs.Count} completed quests");

        completedQuests.Clear();

        switch (context)
        {
            case RestoreContext.SaveFileLoad:
                DebugLog("Save file load - restoring all quest completion");
                foreach (string questID in saveData.completedQuestIDs)
                {
                    completedQuests.Add(questID);
                    DebugLog($"  Restored quest: {questID}");
                }
                break;

            case RestoreContext.DoorwayTransition:
                DebugLog("Doorway transition - preserving quest completion");
                foreach (string questID in saveData.completedQuestIDs)
                {
                    completedQuests.Add(questID);
                    DebugLog($"  Preserved quest: {questID}");
                }
                break;

            case RestoreContext.NewGame:
                DebugLog("New game - clearing all quests");
                // Don't restore any quests for new games
                completedQuests.Clear();
                break;
        }

        DebugLog($"Quest data restoration complete - {completedQuests.Count} quests now active");

        // Debug: Print all completed quests
        if (completedQuests.Count > 0)
        {
            DebugLog("Currently completed quests:");
            foreach (string questID in completedQuests)
            {
                DebugLog($"  - {questID}");
            }
        }
    }

    public void OnBeforeSave()
    {
        DebugLog($"Preparing to save {completedQuests.Count} completed quests");
    }

    public void OnAfterLoad()
    {
        DebugLog($"Quest data loaded - {completedQuests.Count} quests complete");

        RefreshAllExits();
        OnQuestManagerFinishedLoading?.Invoke("FinishedLoading");
    }

    #endregion

    #region Level Exit Controller Integration

    /// <summary>
    /// Refreshes all LevelExitControllers in the current scene
    /// </summary>
    private void RefreshAllExits()
    {
        DebugLog("Refreshing all LevelExitControllers in scene");

        // Find all LevelExitControllers
        var exitControllers = FindObjectsByType<QuestLockedDoor>(FindObjectsSortMode.None);

        if (exitControllers.Length == 0)
        {
            DebugLog("No LevelExitControllers found in scene");
            return;
        }

        DebugLog($"Found {exitControllers.Length} LevelExitControllers - refreshing...");

        // Refresh each one
        foreach (var exitController in exitControllers)
        {
            if (exitController != null)
            {
                exitController.RefreshFromQuestManager();
                DebugLog($"Refreshed: {exitController.gameObject.name}");
            }
        }

        DebugLog("Exit refresh complete");
    }

    #endregion

    #region IManagerState Implementation

    public void SetOperationalState(ManagerOperationalState newState)
    {
        if (newState == operationalState) return;

        DebugLog($"Transitioning from {operationalState} to {newState}");
        operationalState = newState;

        switch (newState)
        {
            case ManagerOperationalState.Menu:
                OnEnterMenuState();
                break;
            case ManagerOperationalState.Gameplay:
                OnEnterGameplayState();
                break;
            case ManagerOperationalState.Transition:
                OnEnterTransitionState();
                break;
        }
    }

    public void OnEnterMenuState()
    {
        DebugLog("Entering Menu state - quest data preserved");
        // Quest data persists, no operations needed
    }

    public void OnEnterGameplayState()
    {
        DebugLog("Entering Gameplay state");
        // Resume normal operations
    }

    public void OnEnterTransitionState()
    {
        DebugLog("Entering Transition state");
    }

    public bool CanOperateInCurrentState()
    {
        // Quest manager can operate in all states (save/load works in menu)
        return true;
    }

    #endregion

    #region IPlayerDependentSaveable Implementation

    /// <summary>
    /// Extracts quest data from unified save structure
    /// </summary>
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null)
        {
            DebugLog("No unified data provided");
            return new QuestManagerSaveData { completedQuestIDs = new List<string>() };
        }

        DebugLog("Using modular extraction from unified save data for quests");

        // Try to get quest data from component storage
        var questData = unifiedData.GetComponentData<QuestManagerSaveData>(SaveID);
        if (questData != null)
        {
            DebugLog($"Found quest data in unified save - {questData.completedQuestIDs.Count} completed quests");
            return questData;
        }

        DebugLog("No quest data found in unified save - returning empty state");
        return new QuestManagerSaveData { completedQuestIDs = new List<string>() };
    }

    /// <summary>
    /// Creates default quest data for new games
    /// </summary>
    public object CreateDefaultData()
    {
        DebugLog("Creating default quest data for new game - no quests completed");

        return new QuestManagerSaveData
        {
            completedQuestIDs = new List<string>()
        };
    }

    /// <summary>
    /// Contributes quest data to unified save structure
    /// </summary>
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is QuestManagerSaveData questData && unifiedData != null)
        {
            DebugLog($"Contributing quest data to unified save - {questData.completedQuestIDs.Count} quests");

            // Store quest data in component storage
            unifiedData.SetComponentData(SaveID, questData);

            DebugLog("Quest data contribution complete");
        }
        else
        {
            DebugLog($"Invalid quest data for contribution - expected QuestManagerSaveData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    #region Debug Helpers

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[QuestManager] {message}");
        }
    }

    #endregion
}

/// <summary>
/// Save data structure for quest completion
/// </summary>
[System.Serializable]
public class QuestManagerSaveData
{
    public List<string> completedQuestIDs = new List<string>();
}