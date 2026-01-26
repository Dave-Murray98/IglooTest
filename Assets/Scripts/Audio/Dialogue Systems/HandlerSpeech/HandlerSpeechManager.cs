using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// CLEANED UP: Central manager for all handler speech in the game.
/// Coordinates playback, tracks state, manages speech-to-speech interruption.
/// NOW USES DialogueSystemsCoordinator for audio log coordination.
/// Scene-based manager that resets state when entering new scenes.
/// </summary>
public class HandlerSpeechManager : MonoBehaviour
{
    public static HandlerSpeechManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private PlayerHandlerSpeechListener playerListener;
    [SerializeField] private bool autoFindPlayerListener = true;

    [Header("Current State")]
    [ShowInInspector][ReadOnly] private HandlerSpeechData currentlyPlayingSpeech;
    [ShowInInspector][ReadOnly] private bool isSpeechPlaying = false;
    [ShowInInspector][ReadOnly] private PlaybackState currentState = PlaybackState.Idle;

    [Header("Queue Management")]
    [ShowInInspector][ReadOnly] private Queue<HandlerSpeechData> speechQueue = new Queue<HandlerSpeechData>();

    [Header("Speech History")]
    [ShowInInspector][ReadOnly] private HashSet<string> playedSpeechIDs = new HashSet<string>();

    [Header("Trigger Tracking")]
    [ShowInInspector][ReadOnly] private List<HandlerSpeechTriggerBase> registeredTriggers = new List<HandlerSpeechTriggerBase>();

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // Events for external systems (UI, etc.)
    public System.Action<HandlerSpeechData> OnSpeechStarted;
    public System.Action<HandlerSpeechData> OnSpeechStopped;
    public System.Action<HandlerSpeechData> OnSpeechCompleted;
    public System.Action<HandlerSpeechData> OnSpeechInterrupted;
    public System.Action<HandlerSpeechData> OnSpeechQueued;

    // Public properties
    public bool IsSpeechPlaying => isSpeechPlaying;
    public HandlerSpeechData CurrentlyPlayingSpeech => currentlyPlayingSpeech;
    public PlaybackState CurrentState => currentState;
    public int QueuedSpeechCount => speechQueue.Count;

    /// <summary>
    /// Playback state machine
    /// </summary>
    public enum PlaybackState
    {
        Idle,           // Nothing playing
        Playing,        // Speech actively playing
        Paused,         // Speech paused (game pause)
        Interrupted     // Speech was interrupted by another speech
    }

    private void Awake()
    {
        // Scene-based singleton (destroyed when scene unloads)
        if (Instance == null)
        {
            Instance = this;
            DebugLog("HandlerSpeechManager initialized for this scene");
        }
        else
        {
            Debug.LogWarning("[HandlerSpeechManager] Duplicate HandlerSpeechManager found - destroying");
            Destroy(gameObject);
            return;
        }

        // Initialize collections
        speechQueue = new Queue<HandlerSpeechData>();
        playedSpeechIDs = new HashSet<string>();

        // Subscribe to game pause events
        GameEvents.OnGamePaused += HandleGamePaused;
        GameEvents.OnGameResumed += HandleGameResumed;
    }

    private void Start()
    {
        // Find PlayerHandlerSpeechListener if not assigned
        if (autoFindPlayerListener && playerListener == null)
        {
            playerListener = FindFirstObjectByType<PlayerHandlerSpeechListener>();

            if (playerListener != null)
            {
                DebugLog($"Auto-found PlayerHandlerSpeechListener on {playerListener.gameObject.name}");
            }
            else
            {
                Debug.LogWarning("[HandlerSpeechManager] No PlayerHandlerSpeechListener found - handler speech won't play!");
            }
        }
    }

    #region Listener Registration

    /// <summary>
    /// Registers the player listener component
    /// </summary>
    public void RegisterListener(PlayerHandlerSpeechListener listener)
    {
        if (listener == null)
        {
            Debug.LogError("[HandlerSpeechManager] Cannot register null listener");
            return;
        }

        playerListener = listener;
        DebugLog($"Registered player listener on {listener.gameObject.name}");
    }

    /// <summary>
    /// Unregisters the player listener component
    /// </summary>
    public void UnregisterListener(PlayerHandlerSpeechListener listener)
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
    /// MODIFIED: Now uses DialogueSystemsCoordinator for audio log coordination.
    /// Attempts to play a handler speech, applying speech-to-speech interruption logic.
    /// This is the main entry point for triggering handler speech.
    /// </summary>
    public void PlaySpeech(HandlerSpeechData speechData, float startTime = 0f)
    {
        if (speechData == null || !speechData.IsValid())
        {
            Debug.LogError("[HandlerSpeechManager] Cannot play - invalid speech data");
            return;
        }

        if (playerListener == null)
        {
            Debug.LogError("[HandlerSpeechManager] Cannot play - no PlayerHandlerSpeechListener!");
            return;
        }

        DebugLog($"=== PLAY SPEECH REQUEST: {speechData.SpeechTitle} ===");
        DebugLog($"CanInterrupt: {speechData.CanInterrupt}, Priority: {speechData.Priority}");

        // NEW: Check with coordinator if playback is allowed (handles audio log interruption)
        if (DialogueSystemsCoordinator.Instance != null)
        {
            if (!DialogueSystemsCoordinator.Instance.RequestHandlerSpeechPlayback(speechData, startTime))
            {
                DebugLog($"Playback request denied by DialogueSystemsCoordinator for: {speechData.SpeechTitle}");
                return;
            }
        }

        // Check if another handler speech is currently playing
        if (isSpeechPlaying && currentlyPlayingSpeech != null)
        {
            HandleSpeechConflict(speechData, startTime);
            return;
        }

        // No conflicts - play immediately
        StartSpeechPlayback(speechData, startTime);
    }

    /// <summary>
    /// Handles conflict when another handler speech is already playing
    /// </summary>
    private void HandleSpeechConflict(HandlerSpeechData newSpeech, float startTime)
    {
        DebugLog($"CONFLICT: '{newSpeech.SpeechTitle}' vs currently playing '{currentlyPlayingSpeech.SpeechTitle}'");

        // Can the current speech be interrupted?
        if (currentlyPlayingSpeech.CanBeInterrupted)
        {
            // Can the new speech interrupt?
            if (newSpeech.CanInterrupt)
            {
                DebugLog($"✓ Current speech CAN be interrupted, new speech CAN interrupt");
                InterruptCurrentSpeech();
                StartSpeechPlayback(newSpeech, startTime);
            }
            else
            {
                DebugLog($"✗ Current speech CAN be interrupted, but new speech CANNOT interrupt");
                HandleCannotInterrupt(newSpeech, startTime);
            }
        }
        else
        {
            DebugLog($"✗ Current speech CANNOT be interrupted");
            HandleCannotInterrupt(newSpeech, startTime);
        }
    }

    /// <summary>
    /// Handles case where new speech cannot interrupt current playback
    /// </summary>
    private void HandleCannotInterrupt(HandlerSpeechData speechData, float startTime)
    {
        if (speechData.MustPlayIfCantInterrupt)
        {
            DebugLog($"Queueing speech: {speechData.SpeechTitle}");
            QueueSpeech(speechData);
        }
        else
        {
            DebugLog($"Speech not queued (MustPlayIfCantInterrupt=false): {speechData.SpeechTitle}");
        }
    }

    /// <summary>
    /// Starts actual playback of a handler speech
    /// </summary>
    private void StartSpeechPlayback(HandlerSpeechData speechData, float startTime)
    {
        DebugLog($"▶ STARTING PLAYBACK: {speechData.SpeechTitle} from {startTime:F2}s");

        currentlyPlayingSpeech = speechData;
        isSpeechPlaying = true;
        currentState = PlaybackState.Playing;

        // Mark as played
        playedSpeechIDs.Add(speechData.SpeechID);

        // Start playback through listener
        playerListener.PlaySpeech(speechData, startTime);

        // Fire event
        OnSpeechStarted?.Invoke(speechData);

        DebugLog($"✅ Playback started: {speechData.SpeechTitle}");
    }

    /// <summary>
    /// Interrupts the currently playing speech
    /// </summary>
    private void InterruptCurrentSpeech()
    {
        if (!isSpeechPlaying || currentlyPlayingSpeech == null)
            return;

        DebugLog($"⏸ INTERRUPTING: {currentlyPlayingSpeech.SpeechTitle}");

        HandlerSpeechData interruptedSpeech = currentlyPlayingSpeech;

        // Stop playback
        if (playerListener != null)
        {
            playerListener.StopSpeech();
        }

        // Update state
        currentState = PlaybackState.Interrupted;
        currentlyPlayingSpeech = null;
        isSpeechPlaying = false;

        // Fire event
        OnSpeechInterrupted?.Invoke(interruptedSpeech);

        DebugLog($"Speech interrupted: {interruptedSpeech.SpeechTitle}");
    }

    /// <summary>
    /// Stops the currently playing handler speech
    /// </summary>
    public void StopCurrentSpeech()
    {
        if (!isSpeechPlaying || currentlyPlayingSpeech == null)
        {
            DebugLog("No speech currently playing");
            return;
        }

        DebugLog($"■ STOPPING: {currentlyPlayingSpeech.SpeechTitle}");

        HandlerSpeechData stoppedSpeech = currentlyPlayingSpeech;

        // Stop playback
        if (playerListener != null)
        {
            playerListener.StopSpeech();
        }

        // Clear state
        currentlyPlayingSpeech = null;
        isSpeechPlaying = false;
        currentState = PlaybackState.Idle;

        // Fire event
        OnSpeechStopped?.Invoke(stoppedSpeech);

        // Check queue
        ProcessQueue();
    }

    /// <summary>
    /// SIMPLIFIED: Called by PlayerHandlerSpeechListener when a speech finishes naturally.
    /// Audio log resumption is now handled automatically by DialogueSystemsCoordinator.
    /// </summary>
    public void OnSpeechFinished(HandlerSpeechData speechData)
    {
        if (speechData == null) return;

        DebugLog($"✓ SPEECH FINISHED: {speechData.SpeechTitle}");

        // Clear state
        currentlyPlayingSpeech = null;
        isSpeechPlaying = false;
        currentState = PlaybackState.Idle;

        // Fire event
        OnSpeechCompleted?.Invoke(speechData);

        // NOTE: Audio log resumption is now handled automatically by DialogueSystemsCoordinator
        // No need to manually check or resume audio logs here

        // Process queue for next speech
        ProcessQueue();
    }

    #endregion

    #region Queue Management

    /// <summary>
    /// Adds a speech to the queue
    /// </summary>
    private void QueueSpeech(HandlerSpeechData speechData)
    {
        if (speechData == null) return;

        speechQueue.Enqueue(speechData);
        DebugLog($"Speech queued: {speechData.SpeechTitle} (Queue size: {speechQueue.Count})");

        // Fire event
        OnSpeechQueued?.Invoke(speechData);
    }

    /// <summary>
    /// Processes the next speech in queue if available
    /// </summary>
    private void ProcessQueue()
    {
        if (speechQueue.Count == 0)
        {
            DebugLog("Queue is empty");
            return;
        }

        DebugLog($"Processing queue ({speechQueue.Count} speeches queued)");

        HandlerSpeechData nextSpeech = speechQueue.Dequeue();
        DebugLog($"Dequeued speech: {nextSpeech.SpeechTitle}");

        // Play the queued speech
        PlaySpeech(nextSpeech, 0f);
    }

    /// <summary>
    /// Clears all queued speeches
    /// </summary>
    public void ClearQueue()
    {
        speechQueue.Clear();
        DebugLog("Speech queue cleared");
    }

    /// <summary>
    /// Gets all queued speech IDs
    /// </summary>
    public List<string> GetQueuedSpeechIDs()
    {
        return speechQueue.Select(s => s.SpeechID).ToList();
    }

    #endregion

    #region Speech History

    /// <summary>
    /// Checks if a speech has been played in this session/save
    /// </summary>
    public bool HasSpeechBeenPlayed(string speechID)
    {
        return playedSpeechIDs.Contains(speechID);
    }

    /// <summary>
    /// Marks a speech as played (useful for save system restoration)
    /// </summary>
    public void MarkSpeechAsPlayed(string speechID)
    {
        if (string.IsNullOrEmpty(speechID)) return;

        playedSpeechIDs.Add(speechID);
        DebugLog($"Marked speech as played: {speechID}");
    }

    /// <summary>
    /// Gets all played speech IDs
    /// </summary>
    public IEnumerable<string> GetPlayedSpeechIDs()
    {
        return playedSpeechIDs;
    }

    /// <summary>
    /// Clears all speech history (for new game)
    /// </summary>
    public void ClearSpeechHistory()
    {
        playedSpeechIDs.Clear();
        DebugLog("Speech history cleared");
    }

    #endregion

    #region Public API

    /// <summary>
    /// Gets current playback time (for save system)
    /// </summary>
    public float GetCurrentPlaybackTime()
    {
        if (playerListener == null || !isSpeechPlaying)
            return 0f;

        return playerListener.CurrentPlaybackTime;
    }

    /// <summary>
    /// Resets manager state when entering new scene
    /// </summary>
    public void ResetForNewScene()
    {
        DebugLog("Resetting handler speech manager for new scene");

        StopCurrentSpeech();
        ClearQueue();
        ClearSpeechHistory();

        // Clear trigger registrations (they'll re-register in their Start methods)
        registeredTriggers.Clear();

        currentState = PlaybackState.Idle;
    }

    #endregion

    #region Game Pause Handling

    /// <summary>
    /// Handles game pause - pauses handler speech
    /// </summary>
    private void HandleGamePaused()
    {
        DebugLog("Game paused - pausing handler speech if playing");

        if (isSpeechPlaying)
        {
            currentState = PlaybackState.Paused;
        }
    }

    /// <summary>
    /// Handles game resume - resumes handler speech
    /// </summary>
    private void HandleGameResumed()
    {
        DebugLog("Game resumed - resuming handler speech if it was playing");

        if (currentState == PlaybackState.Paused && isSpeechPlaying)
        {
            currentState = PlaybackState.Playing;
        }
    }

    #endregion

    #region Trigger Registration

    /// <summary>
    /// Registers a trigger with the manager for tracking
    /// </summary>
    public void RegisterTrigger(HandlerSpeechTriggerBase trigger)
    {
        if (trigger == null)
        {
            Debug.LogError("[HandlerSpeechManager] Cannot register null trigger");
            return;
        }

        if (!registeredTriggers.Contains(trigger))
        {
            registeredTriggers.Add(trigger);
            DebugLog($"Registered trigger: {trigger.TriggerID}");
        }
    }

    /// <summary>
    /// Unregisters a trigger from the manager
    /// </summary>
    public void UnregisterTrigger(HandlerSpeechTriggerBase trigger)
    {
        if (trigger == null) return;

        if (registeredTriggers.Remove(trigger))
        {
            DebugLog($"Unregistered trigger: {trigger.TriggerID}");
        }
    }

    /// <summary>
    /// Gets all registered triggers
    /// </summary>
    public List<HandlerSpeechTriggerBase> GetAllTriggers()
    {
        return new List<HandlerSpeechTriggerBase>(registeredTriggers);
    }

    /// <summary>
    /// Gets a trigger by ID
    /// </summary>
    public HandlerSpeechTriggerBase GetTrigger(string triggerID)
    {
        return registeredTriggers.Find(t => t.TriggerID == triggerID);
    }

    /// <summary>
    /// Gets trigger state for save system
    /// </summary>
    public HandlerSpeechTriggerSaveData GetTriggerSaveData()
    {
        var saveData = new HandlerSpeechTriggerSaveData();

        foreach (var trigger in registeredTriggers)
        {
            if (trigger != null && !string.IsNullOrEmpty(trigger.TriggerID))
            {
                saveData.SetTriggerState(trigger.TriggerID, trigger.HasTriggered);
            }
        }

        DebugLog($"Collected trigger save data: {saveData.TriggerCount} triggers, {saveData.TriggeredCount} triggered");
        return saveData;
    }

    /// <summary>
    /// Restores trigger states from save data
    /// </summary>
    public void RestoreTriggerStates(HandlerSpeechTriggerSaveData saveData)
    {
        if (saveData == null)
        {
            DebugLog("No trigger save data to restore");
            return;
        }

        DebugLog($"Restoring trigger states: {saveData.TriggerCount} triggers");

        int restoredCount = 0;
        foreach (var trigger in registeredTriggers)
        {
            if (trigger == null || string.IsNullOrEmpty(trigger.TriggerID))
                continue;

            if (saveData.HasTriggerState(trigger.TriggerID))
            {
                bool triggeredState = saveData.GetTriggerState(trigger.TriggerID);
                trigger.SetTriggeredState(triggeredState);
                restoredCount++;
                DebugLog($"Restored trigger '{trigger.TriggerID}': triggered={triggeredState}");
            }
        }

        DebugLog($"Trigger restoration complete: {restoredCount}/{registeredTriggers.Count} triggers restored");
    }

    #endregion

    #region Debug Methods

    [Button("Debug: List Current State")]
    private void DebugListState()
    {
        DebugLog("=== HANDLER SPEECH MANAGER STATE ===");
        DebugLog($"Current State: {currentState}");
        DebugLog($"Is Playing: {isSpeechPlaying}");
        DebugLog($"Current Speech: {currentlyPlayingSpeech?.SpeechTitle ?? "None"}");
        DebugLog($"Queue Size: {speechQueue.Count}");
        DebugLog($"Played Speeches: {playedSpeechIDs.Count}");
        DebugLog($"Registered Triggers: {registeredTriggers.Count}");

        if (speechQueue.Count > 0)
        {
            DebugLog("=== QUEUED SPEECHES ===");
            foreach (var speech in speechQueue)
            {
                DebugLog($"  - {speech.SpeechTitle}");
            }
        }

        if (playedSpeechIDs.Count > 0)
        {
            DebugLog("=== PLAYED SPEECHES ===");
            foreach (var id in playedSpeechIDs)
            {
                DebugLog($"  - {id}");
            }
        }

        if (registeredTriggers.Count > 0)
        {
            DebugLog("=== REGISTERED TRIGGERS ===");
            foreach (var trigger in registeredTriggers)
            {
                if (trigger != null)
                {
                    DebugLog($"  - {trigger.TriggerID}: triggered={trigger.HasTriggered}");
                }
            }
        }
    }

    [Button("Debug: Stop Current Speech")]
    private void DebugStopCurrent()
    {
        if (Application.isPlaying)
        {
            StopCurrentSpeech();
        }
        else
        {
            Debug.LogWarning("Debug functions only work in Play mode");
        }
    }

    [Button("Debug: Clear Queue")]
    private void DebugClearQueue()
    {
        if (Application.isPlaying)
        {
            ClearQueue();
        }
        else
        {
            Debug.LogWarning("Debug functions only work in Play mode");
        }
    }

    [Button("Debug: Clear History")]
    private void DebugClearHistory()
    {
        if (Application.isPlaying)
        {
            ClearSpeechHistory();
        }
        else
        {
            Debug.LogWarning("Debug functions only work in Play mode");
        }
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[HandlerSpeechManager] {message}");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        // Unsubscribe from game events
        GameEvents.OnGamePaused -= HandleGamePaused;
        GameEvents.OnGameResumed -= HandleGameResumed;
    }
}