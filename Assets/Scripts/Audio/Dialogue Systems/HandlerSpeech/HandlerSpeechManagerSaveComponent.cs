using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// HEAVILY CLEANED UP: Handles saving and loading of handler speech state.
/// Tracks played speeches, current playback, playback position, queue, and trigger states.
/// Audio log interruption coordination is now handled by DialogueSystemsCoordinator.
/// </summary>
public class HandlerSpeechManagerSaveComponent : SaveComponentBase
{
    [Header("Manager Reference")]
    [SerializeField] private HandlerSpeechManager handlerSpeechManager;

    [Header("Settings")]
    [SerializeField] private bool autoFindManager = true;

    public override SaveDataCategory SaveCategory => SaveDataCategory.SceneDependent;

    protected override void Awake()
    {
        // Set fixed save ID
        saveID = "HandlerSpeechManager";
        autoGenerateID = false;

        base.Awake();

        // Find manager if needed
        if (autoFindManager && handlerSpeechManager == null)
        {
            handlerSpeechManager = HandlerSpeechManager.Instance;
            if (handlerSpeechManager == null)
            {
                handlerSpeechManager = GetComponent<HandlerSpeechManager>();
            }
        }

        if (handlerSpeechManager == null)
        {
            Debug.LogError("[HandlerSpeechManagerSaveComponent] No HandlerSpeechManager found!");
        }
    }

    #region Save/Load Implementation

    public override object GetDataToSave()
    {
        if (handlerSpeechManager == null)
        {
            Debug.LogError("[HandlerSpeechManagerSaveComponent] Cannot save - no HandlerSpeechManager reference!");
            return null;
        }

        var saveData = new HandlerSpeechSaveData();

        // Save played speech history (only speeches played in THIS scene)
        saveData.playedSpeechIDs = new List<string>(handlerSpeechManager.GetPlayedSpeechIDs());

        // Save currently playing speech info
        if (handlerSpeechManager.IsSpeechPlaying && handlerSpeechManager.CurrentlyPlayingSpeech != null)
        {
            saveData.currentlyPlayingSpeechID = handlerSpeechManager.CurrentlyPlayingSpeech.SpeechID;
            saveData.currentPlaybackTime = handlerSpeechManager.GetCurrentPlaybackTime();
            saveData.wasPlaying = true;
        }
        else
        {
            saveData.currentlyPlayingSpeechID = "";
            saveData.currentPlaybackTime = 0f;
            saveData.wasPlaying = false;
        }

        // Save queued speeches
        saveData.queuedSpeechIDs = handlerSpeechManager.GetQueuedSpeechIDs();

        // Save trigger states
        saveData.triggerStates = handlerSpeechManager.GetTriggerSaveData();

        DebugLog($"Saved handler speech data - Played: {saveData.playedSpeechIDs.Count}, " +
                $"Playing: {saveData.currentlyPlayingSpeechID}, Queued: {saveData.queuedSpeechIDs.Count}, " +
                $"Triggers: {saveData.triggerStates.TriggerCount}");

        return saveData;
    }

    /// <summary>
    /// SIMPLIFIED: Restoration is now coordinated by DialogueSystemsCoordinator.
    /// This method handles basic data restoration but delegates playback coordination.
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (data is not HandlerSpeechSaveData saveData)
        {
            DebugLog($"Invalid save data type - expected HandlerSpeechSaveData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        if (handlerSpeechManager == null)
        {
            Debug.LogError("[HandlerSpeechManagerSaveComponent] Cannot load - no HandlerSpeechManager reference!");
            return;
        }

        DebugLog($"=== LOADING HANDLER SPEECH DATA ===");
        DebugLog($"Context: {context}");
        DebugLog($"Played speeches: {saveData.playedSpeechIDs?.Count ?? 0}");
        DebugLog($"Was playing: {saveData.wasPlaying}");
        DebugLog($"Speech ID: {saveData.currentlyPlayingSpeechID}");
        DebugLog($"Playback time: {saveData.currentPlaybackTime:F2}s");
        DebugLog($"Triggers to restore: {saveData.triggerStates?.TriggerCount ?? 0}");

        // Handle based on context
        switch (context)
        {
            case RestoreContext.NewGame:
                // Clear all state for new game
                DebugLog("New game - clearing all handler speech state");
                handlerSpeechManager.ClearSpeechHistory();
                handlerSpeechManager.ClearQueue();
                handlerSpeechManager.StopCurrentSpeech();
                // Note: Triggers will auto-reset in new scenes
                break;

            case RestoreContext.SaveFileLoad:
                // Restore basic state
                RestorePlayedHistory(saveData);
                RestoreQueuedSpeeches(saveData);

                // Restore trigger states (with small delay to let triggers register)
                StartCoroutine(RestoreTriggerStatesDelayed(saveData));

                // NOTE: Playback restoration is handled by DialogueSystemsCoordinator
                // The coordinator will call this method at the right time in the sequence
                if (DialogueSystemsCoordinator.Instance != null &&
                    DialogueSystemsCoordinator.Instance.IsRestorationInProgress())
                {
                    DebugLog("Coordinator is managing restoration - playback will be handled by coordinator");
                }
                else
                {
                    // Fallback: restore playback if coordinator isn't managing it
                    if (saveData.wasPlaying && !string.IsNullOrEmpty(saveData.currentlyPlayingSpeechID))
                    {
                        DebugLog("Restoring playback (coordinator not managing)");
                        RestorePlaybackState(saveData);
                    }
                }
                break;

            case RestoreContext.DoorwayTransition:
                // Clear everything - fresh state when entering scene via doorway
                DebugLog("Doorway transition - starting fresh in this scene");
                handlerSpeechManager.ClearSpeechHistory();
                handlerSpeechManager.ClearQueue();
                handlerSpeechManager.StopCurrentSpeech();
                // Note: Triggers will auto-reset in new scenes
                break;
        }

        DebugLog("=== HANDLER SPEECH DATA RESTORATION COMPLETE ===");
    }

    #endregion

    #region Restoration Helpers

    /// <summary>
    /// Restores played speech history
    /// </summary>
    private void RestorePlayedHistory(HandlerSpeechSaveData saveData)
    {
        if (saveData.playedSpeechIDs == null || saveData.playedSpeechIDs.Count == 0)
        {
            DebugLog("No played speech history to restore");
            return;
        }

        DebugLog($"Restoring {saveData.playedSpeechIDs.Count} played speeches");

        handlerSpeechManager.ClearSpeechHistory();
        foreach (string speechID in saveData.playedSpeechIDs)
        {
            handlerSpeechManager.MarkSpeechAsPlayed(speechID);
        }
    }

    /// <summary>
    /// Restores queued speeches
    /// </summary>
    private void RestoreQueuedSpeeches(HandlerSpeechSaveData saveData)
    {
        if (saveData.queuedSpeechIDs == null || saveData.queuedSpeechIDs.Count == 0)
        {
            DebugLog("No queued speeches to restore");
            return;
        }

        DebugLog($"Restoring {saveData.queuedSpeechIDs.Count} queued speeches");

        handlerSpeechManager.ClearQueue();

        // Re-queue speeches by finding the HandlerSpeechData assets
        foreach (string speechID in saveData.queuedSpeechIDs)
        {
            HandlerSpeechData speechData = FindHandlerSpeechData(speechID);
            if (speechData != null)
            {
                // Re-trigger the speech (it will queue if needed)
                handlerSpeechManager.PlaySpeech(speechData);
            }
            else
            {
                Debug.LogWarning($"[HandlerSpeechManagerSaveComponent] Could not find HandlerSpeechData for queued speech: {speechID}");
            }
        }
    }

    /// <summary>
    /// Restores trigger states with a small delay to let triggers register
    /// </summary>
    private System.Collections.IEnumerator RestoreTriggerStatesDelayed(HandlerSpeechSaveData saveData)
    {
        if (saveData.triggerStates == null || saveData.triggerStates.TriggerCount == 0)
        {
            DebugLog("No trigger states to restore");
            yield break;
        }

        DebugLog("Waiting for triggers to register before restoring states...");

        // Wait for triggers to register themselves
        yield return new WaitForSecondsRealtime(0.3f);

        DebugLog($"Restoring {saveData.triggerStates.TriggerCount} trigger states");

        if (handlerSpeechManager != null)
        {
            handlerSpeechManager.RestoreTriggerStates(saveData.triggerStates);
            DebugLog("Trigger states restored");
        }
    }

    /// <summary>
    /// Restores playback state from save data
    /// </summary>
    private void RestorePlaybackState(HandlerSpeechSaveData saveData)
    {
        DebugLog($"RestorePlaybackState called - Speech: {saveData.currentlyPlayingSpeechID}, Time: {saveData.currentPlaybackTime:F2}s");

        // Find the HandlerSpeechData asset
        HandlerSpeechData speechData = FindHandlerSpeechData(saveData.currentlyPlayingSpeechID);

        if (speechData == null)
        {
            Debug.LogError($"[HandlerSpeechManagerSaveComponent] ❌ Could not find HandlerSpeechData: {saveData.currentlyPlayingSpeechID}");
            return;
        }

        DebugLog($"✓ Found speech data: {speechData.SpeechTitle}");
        DebugLog($"✓ Speech is valid: {speechData.IsValid()}");

        // Start the restoration coroutine
        StartCoroutine(RestorePlaybackDelayed(speechData, saveData.currentPlaybackTime));
    }

    /// <summary>
    /// Restores playback with a delay to ensure all systems are ready
    /// </summary>
    private System.Collections.IEnumerator RestorePlaybackDelayed(HandlerSpeechData speechData, float playbackTime)
    {
        DebugLog($"=== RESTORE PLAYBACK DELAYED COROUTINE STARTED ===");
        DebugLog($"Speech: '{speechData.SpeechTitle}'");
        DebugLog($"Start time: {playbackTime:F2}s");
        DebugLog($"Speech duration: {speechData.Duration:F2}s");

        // Wait for end of frame to ensure scene is fully loaded
        yield return new WaitForEndOfFrame();
        DebugLog("✓ Waited for end of frame");

        // Additional wait to ensure all systems are ready
        yield return new WaitForSeconds(0.5f);
        DebugLog("✓ Waited additional 0.5s for system initialization");

        // Verify manager still exists
        if (handlerSpeechManager == null)
        {
            Debug.LogError("[HandlerSpeechManagerSaveComponent] ❌ HandlerSpeechManager is null after wait!");
            yield break;
        }
        DebugLog("✓ HandlerSpeechManager exists");

        // Verify the listener exists
        PlayerHandlerSpeechListener listener = FindFirstObjectByType<PlayerHandlerSpeechListener>();
        if (listener == null)
        {
            Debug.LogError("[HandlerSpeechManagerSaveComponent] ❌ PlayerHandlerSpeechListener not found!");
            yield break;
        }
        DebugLog($"✓ PlayerHandlerSpeechListener found on {listener.gameObject.name}");

        // Clamp playback time to valid range
        float clampedTime = Mathf.Clamp(playbackTime, 0f, speechData.Duration - 0.1f);
        if (playbackTime != clampedTime)
        {
            DebugLog($"⚠️ Playback time clamped from {playbackTime:F2}s to {clampedTime:F2}s");
        }

        // Attempt to play the speech through manager (coordinator will handle approval)
        DebugLog($"▶ Calling PlaySpeech on manager...");
        DebugLog($"  - Speech: {speechData.SpeechTitle}");
        DebugLog($"  - Start Time: {clampedTime:F2}s");

        handlerSpeechManager.PlaySpeech(speechData, clampedTime);

        // Wait a frame for the play command to execute
        yield return null;

        // Verify playback started
        DebugLog($"=== VERIFICATION ===");
        DebugLog($"Manager.IsSpeechPlaying: {handlerSpeechManager.IsSpeechPlaying}");
        DebugLog($"Manager.CurrentlyPlayingSpeech: {handlerSpeechManager.CurrentlyPlayingSpeech?.SpeechTitle ?? "NULL"}");
        DebugLog($"Listener.IsPlaying: {listener.IsPlaying}");
        DebugLog($"Listener.CurrentSpeech: {listener.CurrentSpeech?.SpeechTitle ?? "NULL"}");
        DebugLog($"Listener.CurrentPlaybackTime: {listener.CurrentPlaybackTime:F2}s");

        if (handlerSpeechManager.IsSpeechPlaying && handlerSpeechManager.CurrentlyPlayingSpeech == speechData)
        {
            DebugLog($"✅ ✅ ✅ PLAYBACK RESTORATION SUCCESSFUL!");
            DebugLog($"Now playing: {speechData.SpeechTitle} at {listener.CurrentPlaybackTime:F2}s");
        }
        else
        {
            Debug.LogError($"❌ ❌ ❌ PLAYBACK RESTORATION FAILED!");
            Debug.LogError($"Expected: Playing={true}, Speech={speechData.SpeechTitle}");
            Debug.LogError($"Actual: Playing={handlerSpeechManager.IsSpeechPlaying}, Speech={handlerSpeechManager.CurrentlyPlayingSpeech?.SpeechTitle ?? "NULL"}");

            // Additional diagnostics
            if (listener.CurrentSpeech != null && listener.CurrentSpeech != speechData)
            {
                Debug.LogError($"Listener has WRONG speech: {listener.CurrentSpeech.SpeechTitle}");
            }
        }

        DebugLog($"=== RESTORE PLAYBACK DELAYED COROUTINE COMPLETE ===");
    }

    /// <summary>
    /// Finds a HandlerSpeechData asset by its speech ID
    /// </summary>
    private HandlerSpeechData FindHandlerSpeechData(string speechID)
    {
        if (string.IsNullOrEmpty(speechID))
        {
            DebugLog("Cannot find speech data - speechID is null or empty");
            return null;
        }

        DebugLog($"Searching for HandlerSpeechData with ID: {speechID}");

        // Try loading from Resources folder
        HandlerSpeechData[] allSpeeches = Resources.LoadAll<HandlerSpeechData>("Data/HandlerSpeeches");

        DebugLog($"Found {allSpeeches.Length} HandlerSpeechData assets in Resources/Data/HandlerSpeeches");

        foreach (var speech in allSpeeches)
        {
            if (speech.SpeechID == speechID)
            {
                DebugLog($"✓ Match found: {speech.SpeechTitle}");
                return speech;
            }
        }

        // If not found, try alternative search (in case they're in a different folder)
        DebugLog($"Not found in Data/HandlerSpeeches, trying root Resources folder...");
        HandlerSpeechData[] allSpeechesRoot = Resources.LoadAll<HandlerSpeechData>("");

        foreach (var speech in allSpeechesRoot)
        {
            if (speech.SpeechID == speechID)
            {
                DebugLog($"✓ Match found in root: {speech.SpeechTitle}");
                return speech;
            }
        }

        Debug.LogError($"❌ HandlerSpeechData not found with ID: {speechID}");
        return null;
    }

    #endregion

    #region ISaveable Callbacks

    public override void OnBeforeSave()
    {
        DebugLog("Preparing handler speech data for save");

        // Refresh manager reference if needed
        if (autoFindManager && handlerSpeechManager == null)
        {
            handlerSpeechManager = HandlerSpeechManager.Instance;
        }
    }

    public override void OnAfterLoad()
    {
        DebugLog("Handler speech data load completed");
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

        var data = GetDataToSave() as HandlerSpeechSaveData;
        if (data == null)
        {
            Debug.Log("No save data available");
            return;
        }

        Debug.Log("=== HANDLER SPEECH SAVE DATA ===");
        Debug.Log($"Played Speeches: {data.playedSpeechIDs?.Count ?? 0}");
        if (data.playedSpeechIDs != null)
        {
            foreach (string id in data.playedSpeechIDs)
            {
                Debug.Log($"  - {id}");
            }
        }

        Debug.Log($"Was Playing: {data.wasPlaying}");
        if (data.wasPlaying)
        {
            Debug.Log($"  Speech ID: {data.currentlyPlayingSpeechID}");
            Debug.Log($"  Playback Time: {data.currentPlaybackTime:F2}s");
        }

        Debug.Log($"Queued Speeches: {data.queuedSpeechIDs?.Count ?? 0}");
        if (data.queuedSpeechIDs != null)
        {
            foreach (string id in data.queuedSpeechIDs)
            {
                Debug.Log($"  - {id}");
            }
        }

        Debug.Log($"Trigger States: {data.triggerStates?.TriggerCount ?? 0}");
        if (data.triggerStates != null && data.triggerStates.TriggerCount > 0)
        {
            Debug.Log($"  Triggered: {data.triggerStates.TriggeredCount}/{data.triggerStates.TriggerCount}");
            foreach (var entry in data.triggerStates.triggerStates)
            {
                Debug.Log($"  - {entry.triggerID}: {(entry.hasTriggered ? "TRIGGERED" : "Ready")}");
            }
        }
    }

    [Button("Debug: Test Save/Load")]
    private void DebugTestSaveLoad()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Debug functions only work in Play mode");
            return;
        }

        // Save current state
        var saveData = GetDataToSave();
        Debug.Log("Saved current state");

        // Modify state
        if (handlerSpeechManager != null)
        {
            handlerSpeechManager.StopCurrentSpeech();
            Debug.Log("Stopped playback");
        }

        // Restore state
        LoadSaveDataWithContext(saveData, RestoreContext.SaveFileLoad);
        Debug.Log("Restored saved state");
    }

    #endregion
}

/// <summary>
/// CLEANED UP: Save data structure for handler speech manager state.
/// Removed all interrupted audio log fields - now handled by DialogueSystemsCoordinator.
/// </summary>
[System.Serializable]
public class HandlerSpeechSaveData
{
    [Header("Played Speech History")]
    public List<string> playedSpeechIDs = new List<string>();

    [Header("Playback State")]
    public bool wasPlaying = false;
    public string currentlyPlayingSpeechID = "";
    public float currentPlaybackTime = 0f;

    [Header("Queue State")]
    public List<string> queuedSpeechIDs = new List<string>();

    [Header("Trigger States")]
    public HandlerSpeechTriggerSaveData triggerStates = new HandlerSpeechTriggerSaveData();

    public HandlerSpeechSaveData()
    {
        playedSpeechIDs = new List<string>();
        queuedSpeechIDs = new List<string>();
        triggerStates = new HandlerSpeechTriggerSaveData();
    }

    /// <summary>
    /// Validates the save data
    /// </summary>
    public bool IsValid()
    {
        if (playedSpeechIDs == null || queuedSpeechIDs == null || triggerStates == null)
            return false;

        if (wasPlaying && string.IsNullOrEmpty(currentlyPlayingSpeechID))
            return false;

        if (wasPlaying && currentPlaybackTime < 0f)
            return false;

        if (!triggerStates.IsValid())
            return false;

        return true;
    }

    /// <summary>
    /// Gets debug info about the save data
    /// </summary>
    public string GetDebugInfo()
    {
        return $"HandlerSpeechSaveData: Played={playedSpeechIDs?.Count ?? 0}, " +
               $"Playing={wasPlaying}, ID={currentlyPlayingSpeechID}, Time={currentPlaybackTime:F2}s, " +
               $"Queued={queuedSpeechIDs?.Count ?? 0}, " +
               $"Triggers={triggerStates?.TriggerCount ?? 0}";
    }
}