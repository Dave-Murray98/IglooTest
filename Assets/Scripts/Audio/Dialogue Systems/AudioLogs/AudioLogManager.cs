using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Central manager for all audio logs in the scene
/// Coordinates playback, tracks state, and manages audio log registry
/// Enforces "one audio log at a time" rule
/// NOW USES DialogueSystemsCoordinator for all playback decisions
/// </summary>
public class AudioLogManager : MonoBehaviour
{
    public static AudioLogManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private PlayerAudioLogListener playerListener;
    [SerializeField] private bool autoFindPlayerListener = true;

    [Header("Tracking Settings")]
    [SerializeField] private float trackingUpdateInterval = 2f;
    [Tooltip("Time to wait after scene load before initializing audio log tracking")]
    [SerializeField] private float initializationDelay = 0.2f;

    [Header("Audio Log Registry")]
    [ShowInInspector][ReadOnly] private Dictionary<string, AudioLog> audioLogRegistry = new Dictionary<string, AudioLog>();
    [ShowInInspector][ReadOnly] private HashSet<string> destroyedAudioLogs = new HashSet<string>();
    [ShowInInspector, ReadOnly]
    private HashSet<string> allAudioLogIDsInScene = new HashSet<string>();

    [Header("Current State")]
    [ShowInInspector][ReadOnly] private AudioLog currentlyPlayingAudioLog;
    [ShowInInspector][ReadOnly] private bool isAudioLogPlaying = false;

    [Header("Audio Log Quest")]
    [Tooltip("Quest to complete when enough audio logs are destroyed")]
    [SerializeField] private QuestData audioLogDestructionQuest;

    [Tooltip("Number of audio logs required to complete quest")]
    [SerializeField] private int requiredDestructionCount = 6;
    [SerializeField] private QuestData destroyFirstAudioLogQuest;

    // Initialization tracking
    private bool isInitialized = false;
    private Coroutine initializationCoroutine = null;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // Events for external systems (UI, etc.)
    public System.Action<AudioLog> OnAudioLogStarted;
    public System.Action<AudioLog> OnAudioLogStopped;
    public System.Action<AudioLog> OnAudioLogCompleted;
    public System.Action<AudioLog> OnAudioLogDestroyedEvent;

    // Public properties
    public bool IsAudioLogPlaying => isAudioLogPlaying;
    public AudioLog CurrentlyPlayingAudioLog => currentlyPlayingAudioLog;
    public int TotalAudioLogsInScene => audioLogRegistry.Count;
    public int DestroyedAudioLogsCount => destroyedAudioLogs.Count;

    private void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
            DebugLog("AudioLogManager initialized");
        }
        else
        {
            Debug.LogWarning("[AudioLogManager] Duplicate AudioLogManager found - destroying");
            Destroy(gameObject);
            return;
        }

        // Initialize collections
        audioLogRegistry = new Dictionary<string, AudioLog>();
        destroyedAudioLogs = new HashSet<string>();
    }

    private void Start()
    {
        // Find PlayerAudioLogListener if not assigned
        if (autoFindPlayerListener && playerListener == null)
        {
            playerListener = FindFirstObjectByType<PlayerAudioLogListener>();

            if (playerListener != null)
            {
                DebugLog($"Auto-found PlayerAudioLogListener on {playerListener.gameObject.name}");
            }
            else
            {
                Debug.LogWarning("[AudioLogManager] No PlayerAudioLogListener found - audio logs won't play!");
            }
        }

        // Start initialization coroutine
        initializationCoroutine = StartCoroutine(InitializeAudioLogTracking());

        // Start periodic tracking updates
        if (trackingUpdateInterval > 0)
        {
            InvokeRepeating(nameof(UpdateAudioLogTracking), trackingUpdateInterval, trackingUpdateInterval);
        }
    }

    #region Initialization and Discovery

    /// <summary>
    /// Initializes audio log tracking by discovering all components and assigning consistent IDs
    /// </summary>
    private System.Collections.IEnumerator InitializeAudioLogTracking()
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
        DebugLog("Starting audio log discovery and ID assignment...");

        // Find all AudioLog objects in the scene
        var allAudioLogs = FindObjectsByType<AudioLog>(FindObjectsSortMode.None).ToList();

        if (allAudioLogs.Count == 0)
        {
            DebugLog("No AudioLog objects found in scene");
            isInitialized = true;
            return;
        }

        DebugLog($"Found {allAudioLogs.Count} audio logs");

        // Assign consistent IDs to all audio logs
        AssignConsistentAudioLogIDs(allAudioLogs);

        // ============================================
        // NEW: Capture ALL audio log IDs BEFORE any restoration happens
        // ============================================
        allAudioLogIDsInScene.Clear();
        foreach (var audioLog in allAudioLogs)
        {
            if (audioLog.HasValidID)
            {
                allAudioLogIDsInScene.Add(audioLog.AudioLogID);
            }
        }
        DebugLog($"Captured {allAudioLogIDsInScene.Count} total audio log IDs for quest tracking");
        // ============================================

        // Register all audio logs in tracking dictionary
        audioLogRegistry.Clear();
        int destroyedCount = 0;

        foreach (var audioLog in allAudioLogs)
        {
            if (audioLog.HasValidID)
            {
                string id = audioLog.AudioLogID;

                // Check if this audio log was destroyed in a previous session
                if (destroyedAudioLogs.Contains(id))
                {
                    DebugLog($"Audio log '{id}' was previously destroyed - restoring as destroyed");
                    audioLog.RestoreAsDestroyed();
                    destroyedCount++;
                    // Don't add to registry since it's destroyed
                }
                else
                {
                    audioLogRegistry[id] = audioLog;
                    DebugLog($"Registered audio log: {id}");
                }
            }
            else
            {
                Debug.LogWarning($"[AudioLogManager] Audio log on {audioLog.gameObject.name} has no valid ID - skipping");
            }
        }

        isInitialized = true;
        DebugLog($"Audio log tracking initialized: {audioLogRegistry.Count} active, {destroyedCount} destroyed from save data");

        // Check quest completion after initialization
        CheckAndCompleteQuest();
    }

    /// <summary>
    /// Assigns consistent IDs using deterministic ordering based on GetInstanceID().
    /// Same pattern as SceneDestructionStateManager for consistency.
    /// </summary>
    private void AssignConsistentAudioLogIDs(List<AudioLog> audioLogs)
    {
        DebugLog("=== ASSIGNING CONSISTENT AUDIO LOG IDs ===");

        // Group audio logs by cleaned GameObject name
        var audioLogGroups = audioLogs
            .GroupBy(a => a.gameObject.name.Replace(" ", "_"))
            .ToList();

        DebugLog($"Found {audioLogGroups.Count} unique object name groups");

        foreach (var group in audioLogGroups)
        {
            string baseName = group.Key;
            // Sort by Instance ID for deterministic ordering
            var logsInGroup = group.OrderBy(a => a.GetInstanceID()).ToList();

            DebugLog($"Processing group '{baseName}' with {logsInGroup.Count} audio logs");

            for (int i = 0; i < logsInGroup.Count; i++)
            {
                var audioLog = logsInGroup[i];
                string consistentID = $"{baseName}_{(i + 1):D2}";
                audioLog.SetAudioLogID(consistentID);
                DebugLog($"  Assigned ID '{consistentID}' to instance {audioLog.GetInstanceID()}");
            }
        }

        // Validate all IDs are unique
        ValidateUniqueIDs(audioLogs);

        DebugLog("=== ID ASSIGNMENT COMPLETE ===");
    }

    /// <summary>
    /// Validates that all assigned IDs are unique
    /// </summary>
    private void ValidateUniqueIDs(List<AudioLog> audioLogs)
    {
        var idSet = new HashSet<string>();
        var duplicates = new List<string>();

        foreach (var audioLog in audioLogs)
        {
            if (!string.IsNullOrEmpty(audioLog.AudioLogID))
            {
                if (!idSet.Add(audioLog.AudioLogID))
                {
                    duplicates.Add(audioLog.AudioLogID);
                }
            }
        }

        if (duplicates.Count > 0)
        {
            Debug.LogError($"[AudioLogManager] ID COLLISION DETECTED! Duplicate IDs: {string.Join(", ", duplicates)}");
        }
        else
        {
            DebugLog($"✓ ID validation passed - all {idSet.Count} IDs are unique");
        }
    }

    /// <summary>
    /// Periodic update to track audio log state changes
    /// </summary>
    private void UpdateAudioLogTracking()
    {
        // Periodic check for destroyed audio logs
        foreach (var kvp in audioLogRegistry.ToList())
        {
            if (kvp.Value == null)
            {
                DebugLog($"WARNING: Tracked audio log '{kvp.Key}' has been destroyed");
                audioLogRegistry.Remove(kvp.Key);
            }
        }
    }

    #endregion

    #region Audio Log Registration

    /// <summary>
    /// Manually register an audio log (useful for runtime-spawned audio logs)
    /// Note: For scene audio logs, registration happens automatically during initialization
    /// </summary>
    public void RegisterAudioLog(AudioLog audioLog)
    {
        if (audioLog == null)
        {
            Debug.LogError("[AudioLogManager] Cannot register null audio log");
            return;
        }

        if (!audioLog.HasValidID)
        {
            Debug.LogError($"[AudioLogManager] Cannot register audio log - no valid ID assigned");
            return;
        }

        string id = audioLog.AudioLogID;
        if (audioLogRegistry.ContainsKey(id))
        {
            Debug.LogWarning($"[AudioLogManager] Audio log {id} already registered - replacing");
        }

        audioLogRegistry[id] = audioLog;
        DebugLog($"Manually registered audio log: {id} ({audioLog.AudioLogData?.LogTitle ?? "Unknown"})");
    }

    /// <summary>
    /// Unregisters an audio log from the manager
    /// </summary>
    public void UnregisterAudioLog(AudioLog audioLog)
    {
        if (audioLog == null) return;

        string id = audioLog.AudioLogID;
        if (audioLogRegistry.Remove(id))
        {
            DebugLog($"Unregistered audio log: {id}");
        }
    }

    /// <summary>
    /// Registers the player listener component
    /// </summary>
    public void RegisterListener(PlayerAudioLogListener listener)
    {
        if (listener == null)
        {
            Debug.LogError("[AudioLogManager] Cannot register null listener");
            return;
        }

        playerListener = listener;
        DebugLog($"Registered player listener on {listener.gameObject.name}");
    }

    /// <summary>
    /// Unregisters the player listener component
    /// </summary>
    public void UnregisterListener(PlayerAudioLogListener listener)
    {
        if (playerListener == listener)
        {
            playerListener = null;
            DebugLog("Unregistered player listener");
        }
    }

    #endregion

    #region Playback Control

    /// <summary>
    /// MODIFIED: Now uses DialogueSystemsCoordinator for playback decisions
    /// Plays the specified audio log if coordinator approves
    /// </summary>
    public void PlayAudioLog(AudioLog audioLog, float startTime = 0f)
    {
        if (audioLog == null)
        {
            Debug.LogError("[AudioLogManager] Cannot play null audio log");
            return;
        }

        if (audioLog.IsDestroyed)
        {
            DebugLog($"Cannot play destroyed audio log: {audioLog.AudioLogID}");
            return;
        }

        if (playerListener == null)
        {
            Debug.LogError("[AudioLogManager] Cannot play audio log - no PlayerAudioLogListener!");
            return;
        }

        // NEW: Check with coordinator if playback is allowed
        if (DialogueSystemsCoordinator.Instance != null)
        {
            if (!DialogueSystemsCoordinator.Instance.RequestAudioLogPlayback(audioLog, startTime))
            {
                DebugLog($"Playback request denied by DialogueSystemsCoordinator for: {audioLog.AudioLogData?.LogTitle ?? "Unknown"}");
                return;
            }
        }

        // Stop current audio log if one is playing
        if (isAudioLogPlaying && currentlyPlayingAudioLog != null)
        {
            DebugLog($"Stopping current audio log: {currentlyPlayingAudioLog.AudioLogData?.LogTitle ?? "Unknown"}");
            StopCurrentAudioLog();
        }

        // Start new audio log
        string logTitle = audioLog.AudioLogData?.LogTitle ?? "Unknown";
        DebugLog($"Playing audio log: {logTitle} from {startTime:F2}s (ID: {audioLog.AudioLogID})");

        currentlyPlayingAudioLog = audioLog;
        isAudioLogPlaying = true;

        try
        {
            playerListener.PlayAudioLog(audioLog, startTime);
            DebugLog($"✅ Successfully started playback of '{logTitle}'");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AudioLogManager] Failed to play audio log '{logTitle}': {e.Message}");
            currentlyPlayingAudioLog = null;
            isAudioLogPlaying = false;
            return;
        }

        // Fire event
        OnAudioLogStarted?.Invoke(audioLog);
    }

    /// <summary>
    /// Stops the currently playing audio log
    /// </summary>
    public void StopCurrentAudioLog()
    {
        if (!isAudioLogPlaying || currentlyPlayingAudioLog == null)
        {
            DebugLog("No audio log currently playing");
            return;
        }

        DebugLog($"Stopping audio log: {currentlyPlayingAudioLog.AudioLogData?.LogTitle ?? "Unknown"}");

        AudioLog stoppedLog = currentlyPlayingAudioLog;

        // Stop playback
        if (playerListener != null)
        {
            playerListener.StopAudioLog();
        }

        // Clear state
        currentlyPlayingAudioLog = null;
        isAudioLogPlaying = false;

        // Fire event
        OnAudioLogStopped?.Invoke(stoppedLog);
    }

    /// <summary>
    /// Called by PlayerAudioLogListener when an audio log finishes naturally
    /// </summary>
    public void OnAudioLogFinished(AudioLog audioLog)
    {
        if (audioLog == null) return;

        DebugLog($"Audio log finished: {audioLog.AudioLogData?.LogTitle ?? "Unknown"}");

        // Clear state
        currentlyPlayingAudioLog = null;
        isAudioLogPlaying = false;

        // Fire event
        OnAudioLogCompleted?.Invoke(audioLog);
    }

    #endregion

    #region Audio Log State Management

    /// <summary>
    /// Called when an audio log is destroyed (from damage or other means)
    /// </summary>
    public void OnAudioLogDestroyed(AudioLog audioLog)
    {
        if (audioLog == null) return;

        string id = audioLog.AudioLogID;
        DebugLog($"Audio log destroyed: {id}");

        // CRITICAL: Add to destroyed set BEFORE unregistering
        // This ensures the ID is tracked even after the audio log is removed from registry
        if (!string.IsNullOrEmpty(id))
        {
            destroyedAudioLogs.Add(id);
            DebugLog($"Added '{id}' to destroyed tracking (total destroyed: {destroyedAudioLogs.Count})");
        }

        // Stop playback if this was the current log
        if (currentlyPlayingAudioLog == audioLog)
        {
            StopCurrentAudioLog();
        }

        // Unregister from registry (but ID remains in destroyedAudioLogs)
        UnregisterAudioLog(audioLog);

        // Fire event
        OnAudioLogDestroyedEvent?.Invoke(audioLog);

        // Check quest completion
        CheckAndCompleteQuest();
    }

    /// <summary>
    /// Marks an audio log as destroyed (used by save system)
    /// Handles both audio logs in registry and those that might not exist yet
    /// </summary>
    public void MarkAudioLogAsDestroyed(string audioLogID)
    {
        if (string.IsNullOrEmpty(audioLogID)) return;

        // Add to destroyed tracking
        destroyedAudioLogs.Add(audioLogID);
        DebugLog($"Marked audio log as destroyed: {audioLogID} (total destroyed: {destroyedAudioLogs.Count})");

        // Find and destroy the actual audio log if it exists in registry
        if (audioLogRegistry.TryGetValue(audioLogID, out AudioLog audioLog))
        {
            if (audioLog != null && !audioLog.IsDestroyed)
            {
                DebugLog($"Found audio log '{audioLogID}' in registry - calling RestoreAsDestroyed");
                audioLog.RestoreAsDestroyed();
            }
        }
        else
        {
            DebugLog($"Audio log '{audioLogID}' not found in registry (may not be spawned yet or already destroyed)");
        }
    }

    /// <summary>
    /// Checks if an audio log has been destroyed
    /// </summary>
    public bool IsAudioLogDestroyed(string audioLogID)
    {
        return destroyedAudioLogs.Contains(audioLogID);
    }

    /// <summary>
    /// Gets an audio log by ID
    /// </summary>
    public AudioLog GetAudioLog(string audioLogID)
    {
        if (audioLogRegistry.TryGetValue(audioLogID, out AudioLog audioLog))
        {
            return audioLog;
        }
        return null;
    }

    /// <summary>
    /// Gets all audio logs in the scene
    /// </summary>
    public List<AudioLog> GetAllAudioLogs()
    {
        return audioLogRegistry.Values.ToList();
    }

    /// <summary>
    /// Gets all active (non-destroyed) audio logs
    /// </summary>
    public List<AudioLog> GetActiveAudioLogs()
    {
        return audioLogRegistry.Values.Where(log => !log.IsDestroyed).ToList();
    }

    /// <summary>
    /// Gets current playback time (for save system)
    /// </summary>
    public float GetCurrentPlaybackTime()
    {
        if (playerListener == null || !isAudioLogPlaying)
            return 0f;

        return playerListener.CurrentPlaybackTime;
    }

    /// <summary>
    /// Gets all destroyed audio log IDs (for save system)
    /// Returns the HashSet of IDs that have been marked as destroyed
    /// </summary>
    public IEnumerable<string> GetDestroyedAudioLogIDs()
    {
        return destroyedAudioLogs;
    }

    #endregion

    #region Quest Integration

    /// <summary>
    /// Checks and completes quest if enough audio logs destroyed in this scene
    /// </summary>
    private void CheckAndCompleteQuest()
    {
        DebugLog("CheckAndCompleteQuest() called");

        // Skip if no quest assigned or QuestManager not available
        if (audioLogDestructionQuest == null || QuestManager.Instance == null)
        {
            DebugLog("No audio log destruction quest assigned or QuestManager not available");
            return;
        }

        // Skip if quest already complete
        if (QuestManager.Instance.IsQuestComplete(audioLogDestructionQuest.questID))
        {
            DebugLog("Audio log destruction quest already complete");
            return;
        }

        // Count how many of THIS SCENE's audio logs have been destroyed
        int destroyedInThisScene = 0;
        foreach (string sceneID in allAudioLogIDsInScene)
        {
            if (destroyedAudioLogs.Contains(sceneID))
            {
                destroyedInThisScene++;
            }
        }

        int totalInScene = allAudioLogIDsInScene.Count;
        DebugLog($"Quest check: {destroyedInThisScene}/{totalInScene} destroyed (need {requiredDestructionCount})");

        // Complete quest if we've destroyed enough
        if (destroyedInThisScene >= requiredDestructionCount)
        {
            DebugLog($"✅ {destroyedInThisScene}/{totalInScene} audio logs destroyed - completing quest!");
            QuestManager.Instance.CompleteQuest(audioLogDestructionQuest.questID);
        }

        if (destroyFirstAudioLogQuest != null)
        {
            if (destroyedInThisScene > 0)
                if (QuestManager.Instance.IsQuestComplete(destroyFirstAudioLogQuest.questID))
                {
                    DebugLog("First Audio log destruction quest already complete");
                    return;
                }
                else
                {
                    QuestManager.Instance.CompleteQuest(destroyFirstAudioLogQuest.questID);
                }
            else
            {
                DebugLog("First Audio log destruction quest not complete");
            }
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Clears all destroyed audio log tracking (used when starting new game)
    /// </summary>
    public void ClearDestroyedAudioLogs()
    {
        destroyedAudioLogs.Clear();
        DebugLog("Cleared all destroyed audio log tracking");
    }

    /// <summary>
    /// Resets the manager state (used when loading new scenes)
    /// </summary>
    public void ResetManagerState()
    {
        DebugLog("Resetting AudioLogManager state");

        StopCurrentAudioLog();
        audioLogRegistry.Clear();
        destroyedAudioLogs.Clear();
        isInitialized = false;
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
        return $"Initialized: {isInitialized}, Tracked Audio Logs: {audioLogRegistry.Count}";
    }

    #endregion

    #region Debug Methods

    [Button("Debug: List All Audio Logs")]
    private void DebugListAudioLogs()
    {
        DebugLog("=== AUDIO LOG REGISTRY ===");
        DebugLog($"Initialization Status: {GetInitializationStatus()}");
        DebugLog($"Total Active: {audioLogRegistry.Count}");
        DebugLog($"Total Destroyed (tracked): {destroyedAudioLogs.Count}");
        DebugLog($"Currently Playing: {currentlyPlayingAudioLog?.AudioLogData?.LogTitle ?? "None"}");

        if (audioLogRegistry.Count > 0)
        {
            DebugLog("=== ACTIVE AUDIO LOGS ===");
            foreach (var kvp in audioLogRegistry)
            {
                if (kvp.Value != null)
                {
                    string status = kvp.Value.IsDestroyed ? "DESTROYED" : "Active";
                    string playing = kvp.Value.IsCurrentlyPlaying ? "PLAYING" : "";
                    DebugLog($"  {kvp.Key}: {kvp.Value.AudioLogData?.LogTitle ?? "Unknown"} [{status}] {playing}");
                }
                else
                {
                    DebugLog($"  {kvp.Key}: NULL REFERENCE");
                }
            }
        }

        if (destroyedAudioLogs.Count > 0)
        {
            DebugLog("=== DESTROYED AUDIO LOG IDs (Tracked) ===");
            foreach (string id in destroyedAudioLogs)
            {
                DebugLog($"  {id}");
            }
        }
    }

    [Button("Debug: Stop Current Audio Log")]
    private void DebugStopCurrent()
    {
        if (Application.isPlaying)
        {
            StopCurrentAudioLog();
        }
        else
        {
            Debug.LogWarning("Debug functions only work in Play mode");
        }
    }

    [Button("Refresh Tracking")]
    private void RefreshTracking()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Refresh tracking only works in Play mode");
            return;
        }

        if (initializationCoroutine != null)
        {
            StopCoroutine(initializationCoroutine);
        }

        isInitialized = false;
        audioLogRegistry.Clear();
        destroyedAudioLogs.Clear();
        initializationCoroutine = StartCoroutine(InitializeAudioLogTracking());
        DebugLog("Tracking refresh initiated");
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[AudioLogManager] {message}");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        CancelInvoke(nameof(UpdateAudioLogTracking));
    }
}