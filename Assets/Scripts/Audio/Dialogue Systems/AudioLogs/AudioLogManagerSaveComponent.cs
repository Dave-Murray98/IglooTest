using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// CLEANED UP: Handles saving and loading of audio log state.
/// Tracks destroyed audio logs and current playback.
/// Restoration coordination is now handled by DialogueSystemsCoordinator.
/// </summary>
public class AudioLogManagerSaveComponent : SaveComponentBase
{
    [Header("Manager Reference")]
    [SerializeField] private AudioLogManager audioLogManager;

    [Header("Settings")]
    [SerializeField] private bool autoFindManager = true;

    public override SaveDataCategory SaveCategory => SaveDataCategory.SceneDependent;

    protected override void Awake()
    {
        // Set fixed save ID
        saveID = "AudioLogManager";
        autoGenerateID = false;

        base.Awake();

        // Find manager if needed
        if (autoFindManager && audioLogManager == null)
        {
            audioLogManager = AudioLogManager.Instance;
            if (audioLogManager == null)
            {
                audioLogManager = GetComponent<AudioLogManager>();
            }
        }

        if (audioLogManager == null)
        {
            Debug.LogError("[AudioLogManagerSaveComponent] No AudioLogManager found!");
        }
    }

    #region Save/Load Implementation

    public override object GetDataToSave()
    {
        if (audioLogManager == null)
        {
            Debug.LogError("[AudioLogManagerSaveComponent] Cannot save - no AudioLogManager reference!");
            return null;
        }

        var saveData = new AudioLogManagerSaveData();

        // Save destroyed audio log IDs
        saveData.destroyedAudioLogIDs = new List<string>(audioLogManager.GetDestroyedAudioLogIDs());

        DebugLog($"Saved {saveData.destroyedAudioLogIDs.Count} destroyed audio log IDs");

        // Get currently playing audio log info
        if (audioLogManager.IsAudioLogPlaying && audioLogManager.CurrentlyPlayingAudioLog != null)
        {
            saveData.currentlyPlayingAudioLogID = audioLogManager.CurrentlyPlayingAudioLog.AudioLogID;
            saveData.currentPlaybackTime = audioLogManager.GetCurrentPlaybackTime();
            saveData.wasPlaying = true;
        }
        else
        {
            saveData.currentlyPlayingAudioLogID = "";
            saveData.currentPlaybackTime = 0f;
            saveData.wasPlaying = false;
        }

        DebugLog($"Saved audio log data - Destroyed: {saveData.destroyedAudioLogIDs.Count}, " +
                $"Playing: {saveData.currentlyPlayingAudioLogID}, Time: {saveData.currentPlaybackTime:F2}s");

        return saveData;
    }

    /// <summary>
    /// SIMPLIFIED: Restoration is now coordinated by DialogueSystemsCoordinator.
    /// This method handles basic data restoration but delegates playback coordination.
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (data is not AudioLogManagerSaveData saveData)
        {
            DebugLog($"Invalid save data type - expected AudioLogManagerSaveData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        if (audioLogManager == null)
        {
            Debug.LogError("[AudioLogManagerSaveComponent] Cannot load - no AudioLogManager reference!");
            return;
        }

        DebugLog($"Loading audio log data (Context: {context}) - Destroyed: {saveData.destroyedAudioLogIDs?.Count ?? 0}");

        // Handle based on context
        switch (context)
        {
            case RestoreContext.NewGame:
                // Clear all state for new game
                DebugLog("New game - clearing all audio log state");
                audioLogManager.ClearDestroyedAudioLogs();
                audioLogManager.StopCurrentAudioLog();
                break;

            case RestoreContext.SaveFileLoad:
                // Restore destroyed audio logs
                RestoreDestroyedAudioLogs(saveData);

                // NOTE: Playback restoration is handled by DialogueSystemsCoordinator
                // The coordinator will call this method at the right time in the sequence
                // to avoid conflicts with handler speech
                if (DialogueSystemsCoordinator.Instance != null &&
                    DialogueSystemsCoordinator.Instance.IsRestorationInProgress())
                {
                    DebugLog("Coordinator is managing restoration - playback will be handled by coordinator");
                }
                else
                {
                    // Fallback: restore playback if coordinator isn't managing it
                    if (saveData.wasPlaying && !string.IsNullOrEmpty(saveData.currentlyPlayingAudioLogID))
                    {
                        DebugLog("Restoring playback (coordinator not managing)");
                        RestorePlaybackState(saveData, context);
                    }
                }
                break;

            case RestoreContext.DoorwayTransition:
                // Restore destroyed audio logs but not playback
                RestoreDestroyedAudioLogs(saveData);
                // Don't restore playback on doorway transitions
                break;
        }

        DebugLog("Audio log data restoration complete");
    }

    #endregion

    #region Restoration Helpers

    /// <summary>
    /// Restores destroyed audio logs from save data
    /// </summary>
    private void RestoreDestroyedAudioLogs(AudioLogManagerSaveData saveData)
    {
        if (saveData.destroyedAudioLogIDs == null || saveData.destroyedAudioLogIDs.Count == 0)
        {
            DebugLog("No destroyed audio logs to restore");
            return;
        }

        DebugLog($"Restoring {saveData.destroyedAudioLogIDs.Count} destroyed audio logs");

        foreach (string audioLogID in saveData.destroyedAudioLogIDs)
        {
            audioLogManager.MarkAudioLogAsDestroyed(audioLogID);
        }
    }

    /// <summary>
    /// Restores playback state from save data
    /// </summary>
    private void RestorePlaybackState(AudioLogManagerSaveData saveData, RestoreContext context)
    {
        DebugLog($"Restoring playback state - AudioLog: {saveData.currentlyPlayingAudioLogID}, Time: {saveData.currentPlaybackTime:F2}s");

        // Ensure manager is initialized before attempting to restore
        if (audioLogManager != null && !audioLogManager.IsInitialized())
        {
            DebugLog("Manager not initialized - waiting for initialization before restoring playback");
            StartCoroutine(WaitForInitializationThenRestore(saveData));
            return;
        }

        // Manager is initialized, proceed with restoration
        PerformPlaybackRestoration(saveData);
    }

    /// <summary>
    /// Waits for manager initialization before restoring playback
    /// </summary>
    private System.Collections.IEnumerator WaitForInitializationThenRestore(AudioLogManagerSaveData saveData)
    {
        DebugLog("Waiting for AudioLogManager initialization...");

        // Wait up to 2 seconds for initialization
        float waitTime = 0f;
        float maxWaitTime = 2f;

        while (!audioLogManager.IsInitialized() && waitTime < maxWaitTime)
        {
            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
        }

        if (!audioLogManager.IsInitialized())
        {
            Debug.LogWarning("[AudioLogManagerSaveComponent] Manager failed to initialize within timeout - cannot restore playback");
            yield break;
        }

        DebugLog($"Manager initialized after {waitTime:F2}s - proceeding with playback restoration");

        // Now restore playback
        PerformPlaybackRestoration(saveData);
    }

    /// <summary>
    /// Performs the actual playback restoration logic
    /// </summary>
    private void PerformPlaybackRestoration(AudioLogManagerSaveData saveData)
    {
        // Find the audio log
        AudioLog audioLog = audioLogManager.GetAudioLog(saveData.currentlyPlayingAudioLogID);

        if (audioLog == null)
        {
            DebugLog($"Cannot restore playback - audio log '{saveData.currentlyPlayingAudioLogID}' not found");
            return;
        }

        if (audioLog.IsDestroyed)
        {
            DebugLog($"Cannot restore playback - audio log '{saveData.currentlyPlayingAudioLogID}' is destroyed");
            return;
        }

        // Start playback from saved position with additional delay to ensure listener is ready
        StartCoroutine(RestorePlaybackDelayed(audioLog, saveData.currentPlaybackTime));
    }

    /// <summary>
    /// Restores playback with a delay to ensure all systems are ready
    /// </summary>
    private System.Collections.IEnumerator RestorePlaybackDelayed(AudioLog audioLog, float playbackTime)
    {
        DebugLog($"RestorePlaybackDelayed started for '{audioLog.AudioLogData?.LogTitle ?? "Unknown"}' at time {playbackTime:F2}s");

        // Wait for end of frame to ensure scene is fully loaded
        yield return new WaitForEndOfFrame();

        // Additional wait to ensure PlayerAudioLogListener is ready
        yield return new WaitForSeconds(0.3f);

        // Verify audio log still exists and isn't destroyed
        if (audioLog == null || audioLog.IsDestroyed)
        {
            DebugLog("Audio log was destroyed before playback could be restored");
            yield break;
        }

        // Verify manager still exists
        if (audioLogManager == null)
        {
            Debug.LogWarning("[AudioLogManagerSaveComponent] AudioLogManager destroyed before playback restoration");
            yield break;
        }

        DebugLog($"Attempting to play audio log '{audioLog.AudioLogData?.LogTitle ?? "Unknown"}' from {playbackTime:F2}s");

        // Start playback through manager (coordinator will approve if no handler speech playing)
        audioLogManager.PlayAudioLog(audioLog, playbackTime);

        // Verify playback actually started
        yield return new WaitForSeconds(0.1f);

        if (audioLogManager.IsAudioLogPlaying && audioLogManager.CurrentlyPlayingAudioLog == audioLog)
        {
            DebugLog($"✅ Playback restoration successful - now playing at {playbackTime:F2}s");
        }
        else
        {
            Debug.LogWarning($"[AudioLogManagerSaveComponent] Playback restoration may have failed - " +
                           $"IsPlaying: {audioLogManager.IsAudioLogPlaying}, " +
                           $"CurrentLog: {audioLogManager.CurrentlyPlayingAudioLog?.AudioLogID ?? "None"}");
        }
    }

    #endregion

    #region ISaveable Callbacks

    public override void OnBeforeSave()
    {
        DebugLog("Preparing audio log data for save");

        // Refresh manager reference if needed
        if (autoFindManager && audioLogManager == null)
        {
            audioLogManager = AudioLogManager.Instance;
        }
    }

    public override void OnAfterLoad()
    {
        DebugLog("Audio log data load completed");
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

        var data = GetDataToSave() as AudioLogManagerSaveData;
        if (data == null)
        {
            Debug.Log("No save data available");
            return;
        }

        Debug.Log("=== AUDIO LOG SAVE DATA ===");
        Debug.Log($"Destroyed Audio Logs: {data.destroyedAudioLogIDs?.Count ?? 0}");
        if (data.destroyedAudioLogIDs != null)
        {
            foreach (string id in data.destroyedAudioLogIDs)
            {
                Debug.Log($"  - {id}");
            }
        }

        Debug.Log($"Was Playing: {data.wasPlaying}");
        if (data.wasPlaying)
        {
            Debug.Log($"  Audio Log ID: {data.currentlyPlayingAudioLogID}");
            Debug.Log($"  Playback Time: {data.currentPlaybackTime:F2}s");
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
        if (audioLogManager != null)
        {
            audioLogManager.StopCurrentAudioLog();
            Debug.Log("Stopped playback");
        }

        // Restore state
        LoadSaveDataWithContext(saveData, RestoreContext.SaveFileLoad);
        Debug.Log("Restored saved state");
    }

    #endregion
}

/// <summary>
/// Save data structure for audio log manager state
/// </summary>
[System.Serializable]
public class AudioLogManagerSaveData
{
    [Header("Destroyed Audio Logs")]
    public List<string> destroyedAudioLogIDs = new List<string>();

    [Header("Playback State")]
    public bool wasPlaying = false;
    public string currentlyPlayingAudioLogID = "";
    public float currentPlaybackTime = 0f;

    public AudioLogManagerSaveData()
    {
        destroyedAudioLogIDs = new List<string>();
    }

    /// <summary>
    /// Validates the save data
    /// </summary>
    public bool IsValid()
    {
        if (destroyedAudioLogIDs == null)
            return false;

        if (wasPlaying && string.IsNullOrEmpty(currentlyPlayingAudioLogID))
            return false;

        if (wasPlaying && currentPlaybackTime < 0f)
            return false;

        return true;
    }

    /// <summary>
    /// Gets debug info about the save data
    /// </summary>
    public string GetDebugInfo()
    {
        return $"AudioLogSaveData: Destroyed={destroyedAudioLogIDs?.Count ?? 0}, " +
               $"Playing={wasPlaying}, ID={currentlyPlayingAudioLogID}, Time={currentPlaybackTime:F2}s";
    }
}