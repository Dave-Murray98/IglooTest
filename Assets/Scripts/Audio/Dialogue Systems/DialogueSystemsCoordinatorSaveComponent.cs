using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections;

/// <summary>
/// Handles saving and loading of DialogueSystemsCoordinator state.
/// Preserves audio log interruption state within the current scene.
/// SCENE-DEPENDENT: State is tied to specific scenes, not carried between scenes.
/// </summary>
public class DialogueSystemsCoordinatorSaveComponent : SaveComponentBase
{
    [Header("Manager Reference")]
    [SerializeField] private DialogueSystemsCoordinator coordinator;

    [Header("Settings")]
    [SerializeField] private bool autoFindCoordinator = true;

    // UPDATED: Changed to SceneDependent
    public override SaveDataCategory SaveCategory => SaveDataCategory.SceneDependent;

    protected override void Awake()
    {
        // Set fixed save ID
        saveID = "DialogueSystemsCoordinator";
        autoGenerateID = false;

        base.Awake();

        // UPDATED: Try to find on same GameObject first (most reliable)
        if (coordinator == null)
        {
            coordinator = GetComponent<DialogueSystemsCoordinator>();
            if (coordinator != null)
            {
                DebugLog("Found coordinator on same GameObject");
            }
        }

        // Don't error here - we'll find it lazily when needed
    }

    /// <summary>
    /// ADDED: Ensures coordinator reference is valid before use
    /// </summary>
    private bool EnsureCoordinatorReference()
    {
        if (coordinator != null)
            return true;

        DebugLog("Coordinator reference is null - attempting to find...");

        // Try to find on same GameObject first
        coordinator = GetComponent<DialogueSystemsCoordinator>();
        if (coordinator != null)
        {
            DebugLog("Found coordinator on same GameObject");
            return true;
        }

        // Try instance
        if (autoFindCoordinator && DialogueSystemsCoordinator.Instance != null)
        {
            coordinator = DialogueSystemsCoordinator.Instance;
            DebugLog("Found coordinator via Instance");
            return true;
        }

        // Try scene search as last resort
        coordinator = FindFirstObjectByType<DialogueSystemsCoordinator>();
        if (coordinator != null)
        {
            DebugLog("Found coordinator via scene search");
            return true;
        }

        Debug.LogError("[DialogueSystemsCoordinatorSaveComponent] Could not find DialogueSystemsCoordinator!");
        return false;
    }

    #region Save/Load Implementation

    public override object GetDataToSave()
    {
        if (!EnsureCoordinatorReference())
        {
            Debug.LogError("[DialogueSystemsCoordinatorSaveComponent] Cannot save - no coordinator reference!");
            return null;
        }

        var saveData = new DialogueCoordinatorSaveData();

        // Save interruption state
        if (coordinator.HasInterruptedAudioLog)
        {
            var (interruptedLog, interruptedTime) = coordinator.GetInterruptedAudioLogInfo();

            if (interruptedLog != null)
            {
                saveData.hasInterruptedAudioLog = true;
                saveData.interruptedAudioLogID = interruptedLog.AudioLogID;
                saveData.interruptedAudioLogTime = interruptedTime;

                DebugLog($"Saved interruption state - AudioLog: {interruptedLog.AudioLogData?.LogTitle}, Time: {interruptedTime:F2}s");
            }
        }
        else
        {
            DebugLog("No interruption state to save");
        }

        return saveData;
    }

    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (data is not DialogueCoordinatorSaveData saveData)
        {
            DebugLog($"Invalid save data type - expected DialogueCoordinatorSaveData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        // UPDATED: Use EnsureCoordinatorReference with delayed retry
        if (!EnsureCoordinatorReference())
        {
            Debug.LogWarning("[DialogueSystemsCoordinatorSaveComponent] Coordinator not found yet - will retry with delay");
            StartCoroutine(RetryLoadWithDelay(data, context));
            return;
        }

        PerformLoad(saveData, context);
    }

    /// <summary>
    /// ADDED: Retry loading with delay to allow coordinator to initialize
    /// </summary>
    private IEnumerator RetryLoadWithDelay(object data, RestoreContext context)
    {
        DebugLog("Waiting for coordinator to initialize...");

        // Wait a bit for Awake calls to complete
        yield return new WaitForSecondsRealtime(0.1f);

        // Try again
        if (!EnsureCoordinatorReference())
        {
            Debug.LogError("[DialogueSystemsCoordinatorSaveComponent] Coordinator still not found after delay - cannot load!");
            yield break;
        }

        if (data is DialogueCoordinatorSaveData saveData)
        {
            PerformLoad(saveData, context);
        }
    }

    /// <summary>
    /// ADDED: Separated load logic to avoid duplication
    /// </summary>
    private void PerformLoad(DialogueCoordinatorSaveData saveData, RestoreContext context)
    {
        DebugLog($"Loading coordinator data (Context: {context})");

        switch (context)
        {
            case RestoreContext.NewGame:
                // Clear everything for new game
                DebugLog("New game - clearing coordinator state");
                // Nothing to restore, fresh state
                break;

            case RestoreContext.SaveFileLoad:
                // Restore interruption state when loading from save
                if (saveData.hasInterruptedAudioLog)
                {
                    DebugLog($"Restoring interruption state - AudioLog: {saveData.interruptedAudioLogID}, Time: {saveData.interruptedAudioLogTime:F2}s");
                    StartCoroutine(RestoreInterruptionStateDelayed(saveData));
                }
                else
                {
                    DebugLog("No interruption state to restore");
                }
                break;

            case RestoreContext.DoorwayTransition:
                // Restore interruption state when returning to a scene via doorway
                if (saveData.hasInterruptedAudioLog)
                {
                    DebugLog($"Restoring interruption state after doorway - AudioLog: {saveData.interruptedAudioLogID}, Time: {saveData.interruptedAudioLogTime:F2}s");
                    StartCoroutine(RestoreInterruptionStateDelayed(saveData));
                }
                else
                {
                    DebugLog("No interruption state to restore for doorway transition");
                }
                break;
        }
    }

    /// <summary>
    /// Restores interruption state with delay to ensure managers are ready
    /// </summary>
    private System.Collections.IEnumerator RestoreInterruptionStateDelayed(DialogueCoordinatorSaveData saveData)
    {
        DebugLog($"Starting delayed interruption state restoration for: {saveData.interruptedAudioLogID}");

        // Wait for scene to fully load and managers to initialize
        yield return new WaitForSecondsRealtime(0.3f);

        // Ensure we still have coordinator reference
        if (!EnsureCoordinatorReference())
        {
            Debug.LogError("[DialogueSystemsCoordinatorSaveComponent] Lost coordinator reference during restoration!");
            yield break;
        }

        // Find the interrupted audio log by ID
        AudioLog audioLog = null;

        // Wait for AudioLogManager to be ready
        float waitTime = 0f;
        while (AudioLogManager.Instance == null && waitTime < 1f)
        {
            yield return new WaitForSecondsRealtime(0.1f);
            waitTime += 0.1f;
        }

        if (AudioLogManager.Instance != null)
        {
            // Wait for manager to initialize
            while (!AudioLogManager.Instance.IsInitialized() && waitTime < 2f)
            {
                yield return new WaitForSecondsRealtime(0.1f);
                waitTime += 0.1f;
            }

            audioLog = AudioLogManager.Instance.GetAudioLog(saveData.interruptedAudioLogID);
        }

        if (audioLog == null)
        {
            Debug.LogWarning($"[DialogueSystemsCoordinatorSaveComponent] Could not find interrupted audio log: {saveData.interruptedAudioLogID}");
            yield break;
        }

        // Restore the interruption state in the coordinator
        if (coordinator != null)
        {
            coordinator.RestoreInterruptionState(audioLog, saveData.interruptedAudioLogTime);
            DebugLog($"✅ Successfully restored interruption state for: {audioLog.AudioLogData?.LogTitle}");
        }
        else
        {
            Debug.LogError("[DialogueSystemsCoordinatorSaveComponent] Coordinator became null during restoration!");
        }
    }

    public override object ExtractRelevantData(object saveContainer)
    {
        if (saveContainer is DialogueCoordinatorSaveData coordinatorData)
        {
            return coordinatorData;
        }
        else if (saveContainer is SceneSaveData sceneData)
        {
            // Extract from scene data
            return sceneData.GetObjectData<DialogueCoordinatorSaveData>(SaveID);
        }

        return null;
    }

    #endregion

    #region ISaveable Callbacks

    public override void OnBeforeSave()
    {
        DebugLog("Preparing coordinator data for save");

        // Refresh coordinator reference if needed
        EnsureCoordinatorReference();
    }

    public override void OnAfterLoad()
    {
        DebugLog("Coordinator data load completed");
    }

    #endregion

    #region Debug

    [Button("Debug: Print Save Data")]
    private void DebugPrintSaveData()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Debug functions only work in Play mode");
            return;
        }

        if (!EnsureCoordinatorReference())
        {
            Debug.LogError("Cannot print save data - no coordinator reference");
            return;
        }

        var data = GetDataToSave() as DialogueCoordinatorSaveData;
        if (data == null)
        {
            Debug.Log("No save data available");
            return;
        }

        Debug.Log("=== DIALOGUE COORDINATOR SAVE DATA ===");
        Debug.Log($"Has Interrupted Audio Log: {data.hasInterruptedAudioLog}");
        if (data.hasInterruptedAudioLog)
        {
            Debug.Log($"  Audio Log ID: {data.interruptedAudioLogID}");
            Debug.Log($"  Time: {data.interruptedAudioLogTime:F2}s");
        }
    }

    [Button("Debug: Test Reference Finding")]
    private void DebugTestReferenceFinding()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Debug functions only work in Play mode");
            return;
        }

        coordinator = null; // Clear reference
        bool found = EnsureCoordinatorReference();
        Debug.Log($"Reference finding test: {(found ? "SUCCESS" : "FAILED")}");
    }

    #endregion
}



/// <summary>
/// Save data structure for DialogueSystemsCoordinator state
/// </summary>
[System.Serializable]
public class DialogueCoordinatorSaveData
{
    [Header("Interruption State")]
    public bool hasInterruptedAudioLog = false;
    public string interruptedAudioLogID = "";
    public float interruptedAudioLogTime = 0f;

    public bool IsValid()
    {
        if (hasInterruptedAudioLog && string.IsNullOrEmpty(interruptedAudioLogID))
            return false;

        if (hasInterruptedAudioLog && interruptedAudioLogTime < 0f)
            return false;

        return true;
    }

    public string GetDebugInfo()
    {
        return $"DialogueCoordinatorSaveData: HasInterruption={hasInterruptedAudioLog}, " +
               $"AudioLogID={interruptedAudioLogID}, Time={interruptedAudioLogTime:F2}s";
    }
}