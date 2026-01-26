using UnityEngine;
using System.Collections;
using Sirenix.OdinInspector;

/// <summary>
/// Centralized coordinator for all dialogue systems (audio logs and handler speech).
/// Manages interruption logic, playback priority, and save/load restoration.
/// This is the single source of truth for what should be playing and when.
/// SCENE-BASED: Does not persist across scenes - each scene has fresh dialogue state.
/// </summary>
public class DialogueSystemsCoordinator : MonoBehaviour
{
    public static DialogueSystemsCoordinator Instance { get; private set; }

    [Header("System References")]
    [SerializeField] private AudioLogManager audioLogManager;
    [SerializeField] private HandlerSpeechManager handlerSpeechManager;
    [SerializeField] private bool autoFindManagers = true;

    [Header("Current Playback State")]
    [ShowInInspector][ReadOnly] private DialogueSystemType currentlyPlaying = DialogueSystemType.None;
    [ShowInInspector][ReadOnly] private bool isHandlerSpeechInterruptingAudioLog = false;

    [Header("Interrupted Audio Log State")]
    [ShowInInspector][ReadOnly] private AudioLog interruptedAudioLog = null;
    [ShowInInspector][ReadOnly] private float interruptedAudioLogTime = 0f;

    [Header("Restoration State")]
    [ShowInInspector][ReadOnly] private bool isPerformingRestoration = false;
    [ShowInInspector][ReadOnly] private RestorationPhase currentRestorationPhase = RestorationPhase.None;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    public enum DialogueSystemType
    {
        None,
        AudioLog,
        HandlerSpeech
    }

    public enum RestorationPhase
    {
        None,
        WaitingForManagers,
        RestoringHandlerSpeech,
        CheckingInterruption,
        RestoringAudioLog,
        Complete
    }

    // Events
    public System.Action<DialogueSystemType> OnDialogueSystemStarted;
    public System.Action<DialogueSystemType> OnDialogueSystemStopped;
    public System.Action<AudioLog, float> OnAudioLogInterrupted;
    public System.Action<AudioLog, float> OnAudioLogResumed;

    // Public properties
    public bool IsHandlerSpeechPlaying => currentlyPlaying == DialogueSystemType.HandlerSpeech;
    public bool IsAudioLogPlaying => currentlyPlaying == DialogueSystemType.AudioLog;
    public bool IsAnythingPlaying => currentlyPlaying != DialogueSystemType.None;
    public bool HasInterruptedAudioLog => isHandlerSpeechInterruptingAudioLog && interruptedAudioLog != null;

    private void Awake()
    {
        // UPDATED: Scene-based singleton (no DontDestroyOnLoad)
        if (Instance == null)
        {
            Instance = this;
            DebugLog("DialogueSystemsCoordinator initialized for this scene");
        }
        else
        {
            Debug.LogWarning("[DialogueSystemsCoordinator] Duplicate found - destroying");
            Destroy(gameObject);
            return;
        }

        // Subscribe to game pause/resume events
        GameEvents.OnGamePaused += HandleGamePaused;
        GameEvents.OnGameResumed += HandleGameResumed;
    }

    private void Start()
    {
        if (autoFindManagers)
        {
            FindManagers();
        }

        SubscribeToEvents();
    }

    #region Manager Discovery and Setup

    private void FindManagers()
    {
        if (audioLogManager == null)
        {
            audioLogManager = AudioLogManager.Instance;
        }

        if (handlerSpeechManager == null)
        {
            handlerSpeechManager = HandlerSpeechManager.Instance;
        }

        DebugLog($"Found managers - AudioLog: {audioLogManager != null}, HandlerSpeech: {handlerSpeechManager != null}");
    }

    private void SubscribeToEvents()
    {
        // Subscribe to manager events to track state
        if (audioLogManager != null)
        {
            audioLogManager.OnAudioLogStarted += HandleAudioLogStarted;
            audioLogManager.OnAudioLogStopped += HandleAudioLogStopped;
            audioLogManager.OnAudioLogCompleted += HandleAudioLogCompleted;
        }

        if (handlerSpeechManager != null)
        {
            handlerSpeechManager.OnSpeechStarted += HandleHandlerSpeechStarted;
            handlerSpeechManager.OnSpeechStopped += HandleHandlerSpeechStopped;
            handlerSpeechManager.OnSpeechCompleted += HandleHandlerSpeechCompleted;
        }

        DebugLog("Subscribed to dialogue system events");
    }

    #endregion

    #region Playback Request Handling

    /// <summary>
    /// Request to play an audio log - coordinator decides if it should play
    /// </summary>
    public bool RequestAudioLogPlayback(AudioLog audioLog, float startTime = 0f)
    {
        if (audioLog == null)
        {
            DebugLog("Cannot play null audio log");
            return false;
        }

        DebugLog($"=== AUDIO LOG PLAYBACK REQUEST: {audioLog.AudioLogData?.LogTitle ?? "Unknown"} ===");

        // Check if handler speech is currently playing
        if (IsHandlerSpeechPlaying)
        {
            DebugLog("Handler speech is playing - cannot start audio log");
            return false;
        }

        // Check if another audio log is playing
        if (IsAudioLogPlaying)
        {
            DebugLog("Another audio log is playing - stopping it first");
            audioLogManager.StopCurrentAudioLog();
        }

        // Clear any interrupted state
        ClearInterruptedAudioLog();

        // Allow playback
        currentlyPlaying = DialogueSystemType.AudioLog;
        DebugLog($"✓ Audio log playback approved: {audioLog.AudioLogData?.LogTitle ?? "Unknown"}");
        return true;
    }

    /// <summary>
    /// Request to play handler speech - coordinator decides if it should interrupt audio log
    /// </summary>
    public bool RequestHandlerSpeechPlayback(HandlerSpeechData speechData, float startTime = 0f)
    {
        if (speechData == null)
        {
            DebugLog("Cannot play null handler speech");
            return false;
        }

        DebugLog($"=== HANDLER SPEECH PLAYBACK REQUEST: {speechData.SpeechTitle} ===");

        // Check if audio log is currently playing
        if (IsAudioLogPlaying)
        {
            DebugLog("Audio log is playing - handler speech will interrupt it");
            InterruptAudioLog();
        }

        // Allow playback
        currentlyPlaying = DialogueSystemType.HandlerSpeech;
        DebugLog($"✓ Handler speech playback approved: {speechData.SpeechTitle}");
        return true;
    }

    #endregion

    #region Audio Log Interruption

    /// <summary>
    /// Interrupts the currently playing audio log to make room for handler speech
    /// </summary>
    private void InterruptAudioLog()
    {
        if (audioLogManager == null || !audioLogManager.IsAudioLogPlaying)
        {
            DebugLog("No audio log to interrupt");
            return;
        }

        // Store the interrupted audio log state
        interruptedAudioLog = audioLogManager.CurrentlyPlayingAudioLog;
        interruptedAudioLogTime = audioLogManager.GetCurrentPlaybackTime();
        isHandlerSpeechInterruptingAudioLog = true;

        DebugLog($"Interrupting audio log: {interruptedAudioLog.AudioLogData?.LogTitle ?? "Unknown"} at {interruptedAudioLogTime:F2}s");

        // Pause the audio log (don't stop it completely)
        var playerListener = FindFirstObjectByType<PlayerAudioLogListener>();
        if (playerListener != null)
        {
            playerListener.PauseAudioLog();
            DebugLog("Audio log paused for handler speech interruption");
        }

        OnAudioLogInterrupted?.Invoke(interruptedAudioLog, interruptedAudioLogTime);
    }

    /// <summary>
    /// Resumes the interrupted audio log after handler speech finishes
    /// </summary>
    private void ResumeInterruptedAudioLog()
    {
        if (!isHandlerSpeechInterruptingAudioLog || interruptedAudioLog == null)
        {
            DebugLog("No interrupted audio log to resume");
            return;
        }

        DebugLog($"Resuming interrupted audio log: {interruptedAudioLog.AudioLogData?.LogTitle ?? "Unknown"} at {interruptedAudioLogTime:F2}s");

        // Check if the audio log still exists and isn't destroyed
        if (interruptedAudioLog.IsDestroyed)
        {
            DebugLog("Interrupted audio log was destroyed - cannot resume");
            ClearInterruptedAudioLog();
            return;
        }

        var playerListener = FindFirstObjectByType<PlayerAudioLogListener>();
        if (playerListener == null)
        {
            DebugLog("PlayerAudioLogListener not found - cannot resume");
            ClearInterruptedAudioLog();
            return;
        }

        // Check if the listener still has the correct audio log loaded
        if (playerListener.CurrentAudioLog == interruptedAudioLog)
        {
            DebugLog("Audio log clip still loaded - resuming from pause");
            playerListener.ResumeAudioLog();
            currentlyPlaying = DialogueSystemType.AudioLog;
        }
        else
        {
            DebugLog("Audio log clip was lost - restarting playback from saved position");
            audioLogManager.PlayAudioLog(interruptedAudioLog, interruptedAudioLogTime);
        }

        OnAudioLogResumed?.Invoke(interruptedAudioLog, interruptedAudioLogTime);

        // Clear interrupted state
        ClearInterruptedAudioLog();

        DebugLog("✓ Audio log resumption complete");
    }

    /// <summary>
    /// Clears the interrupted audio log state
    /// </summary>
    private void ClearInterruptedAudioLog()
    {
        isHandlerSpeechInterruptingAudioLog = false;
        interruptedAudioLog = null;
        interruptedAudioLogTime = 0f;
        DebugLog("Cleared interrupted audio log state");
    }

    #endregion

    #region Event Handlers

    private void HandleAudioLogStarted(AudioLog audioLog)
    {
        currentlyPlaying = DialogueSystemType.AudioLog;
        OnDialogueSystemStarted?.Invoke(DialogueSystemType.AudioLog);
        DebugLog($"Audio log started: {audioLog.AudioLogData?.LogTitle ?? "Unknown"}");
    }

    private void HandleAudioLogStopped(AudioLog audioLog)
    {
        // Only clear if not interrupted by handler speech
        if (!isHandlerSpeechInterruptingAudioLog)
        {
            currentlyPlaying = DialogueSystemType.None;
            OnDialogueSystemStopped?.Invoke(DialogueSystemType.AudioLog);
            DebugLog($"Audio log stopped: {audioLog?.AudioLogData?.LogTitle ?? "Unknown"}");
        }
    }

    private void HandleAudioLogCompleted(AudioLog audioLog)
    {
        currentlyPlaying = DialogueSystemType.None;
        OnDialogueSystemStopped?.Invoke(DialogueSystemType.AudioLog);
        DebugLog($"Audio log completed: {audioLog.AudioLogData?.LogTitle ?? "Unknown"}");
    }

    private void HandleHandlerSpeechStarted(HandlerSpeechData speechData)
    {
        currentlyPlaying = DialogueSystemType.HandlerSpeech;
        OnDialogueSystemStarted?.Invoke(DialogueSystemType.HandlerSpeech);
        DebugLog($"Handler speech started: {speechData.SpeechTitle}");
    }

    private void HandleHandlerSpeechStopped(HandlerSpeechData speechData)
    {
        currentlyPlaying = DialogueSystemType.None;
        OnDialogueSystemStopped?.Invoke(DialogueSystemType.HandlerSpeech);
        DebugLog($"Handler speech stopped: {speechData?.SpeechTitle ?? "Unknown"}");

        // Resume interrupted audio log if there is one
        ResumeInterruptedAudioLog();
    }

    private void HandleHandlerSpeechCompleted(HandlerSpeechData speechData)
    {
        currentlyPlaying = DialogueSystemType.None;
        OnDialogueSystemStopped?.Invoke(DialogueSystemType.HandlerSpeech);
        DebugLog($"Handler speech completed: {speechData.SpeechTitle}");

        // Resume interrupted audio log if there is one
        ResumeInterruptedAudioLog();
    }

    #endregion

    #region Save/Load Coordination

    /// <summary>
    /// Gets the current dialogue state for saving - UPDATED to include coordinator state
    /// </summary>
    public DialogueSaveData GetDialogueSaveData()
    {
        var saveData = new DialogueSaveData();

        // Save handler speech state
        if (handlerSpeechManager != null)
        {
            saveData.handlerSpeechData = handlerSpeechManager.GetComponent<HandlerSpeechManagerSaveComponent>()?.GetDataToSave() as HandlerSpeechSaveData;
        }

        // Save audio log state
        if (audioLogManager != null)
        {
            saveData.audioLogData = audioLogManager.GetComponent<AudioLogManagerSaveComponent>()?.GetDataToSave() as AudioLogManagerSaveData;
        }

        // UPDATED: Save coordinator's own state
        var coordinatorSaveComponent = GetComponent<DialogueSystemsCoordinatorSaveComponent>();
        if (coordinatorSaveComponent != null)
        {
            var coordinatorData = coordinatorSaveComponent.GetDataToSave() as DialogueCoordinatorSaveData;
            if (coordinatorData != null)
            {
                saveData.isHandlerSpeechInterruptingAudioLog = coordinatorData.hasInterruptedAudioLog;
                saveData.interruptedAudioLogID = coordinatorData.interruptedAudioLogID;
                saveData.interruptedAudioLogTime = coordinatorData.interruptedAudioLogTime;
            }
        }

        DebugLog($"Saved dialogue state - Handler playing: {saveData.handlerSpeechData?.wasPlaying ?? false}, " +
                $"Audio log playing: {saveData.audioLogData?.wasPlaying ?? false}, " +
                $"Interrupted: {saveData.isHandlerSpeechInterruptingAudioLog}");

        return saveData;
    }

    /// <summary>
    /// Restores dialogue state from save data with proper coordination
    /// </summary>
    public void RestoreDialogueState(DialogueSaveData saveData, RestoreContext context)
    {
        if (saveData == null)
        {
            DebugLog("No dialogue save data to restore");
            return;
        }

        DebugLog($"=== RESTORING DIALOGUE STATE (Context: {context}) ===");

        switch (context)
        {
            case RestoreContext.NewGame:
                HandleNewGameRestore();
                break;

            case RestoreContext.SaveFileLoad:
                StartCoroutine(HandleSaveFileLoadRestore(saveData));
                break;

            case RestoreContext.DoorwayTransition:
                HandleDoorwayTransitionRestore();
                break;
        }
    }

    /// <summary>
    /// Handles new game restoration - clear everything
    /// </summary>
    private void HandleNewGameRestore()
    {
        DebugLog("New game - clearing all dialogue state");

        ClearInterruptedAudioLog();
        currentlyPlaying = DialogueSystemType.None;

        if (audioLogManager != null)
        {
            audioLogManager.StopCurrentAudioLog();
            audioLogManager.ClearDestroyedAudioLogs();
        }

        if (handlerSpeechManager != null)
        {
            handlerSpeechManager.StopCurrentSpeech();
            handlerSpeechManager.ClearSpeechHistory();
            handlerSpeechManager.ClearQueue();
        }

        DebugLog("New game dialogue restoration complete");
    }

    /// <summary>
    /// Handles doorway transition - stop everything
    /// </summary>
    private void HandleDoorwayTransitionRestore()
    {
        DebugLog("Doorway transition - stopping all dialogue");

        ClearInterruptedAudioLog();
        currentlyPlaying = DialogueSystemType.None;

        if (audioLogManager != null)
        {
            audioLogManager.StopCurrentAudioLog();
        }

        if (handlerSpeechManager != null)
        {
            handlerSpeechManager.StopCurrentSpeech();
        }

        DebugLog("Doorway transition dialogue restoration complete");
    }

    /// <summary>
    /// Handles save file load with coordinated restoration sequence
    /// </summary>
    private IEnumerator HandleSaveFileLoadRestore(DialogueSaveData saveData)
    {
        isPerformingRestoration = true;
        currentRestorationPhase = RestorationPhase.WaitingForManagers;

        DebugLog("Starting coordinated dialogue restoration sequence");

        // PHASE 1: Wait for managers to be ready
        yield return WaitForManagersReady();

        // PHASE 2: Restore handler speech FIRST (takes priority)
        currentRestorationPhase = RestorationPhase.RestoringHandlerSpeech;
        yield return RestoreHandlerSpeechState(saveData.handlerSpeechData);

        // PHASE 3: Check if handler speech interrupted an audio log
        currentRestorationPhase = RestorationPhase.CheckingInterruption;
        bool wasInterrupted = CheckAndRestoreInterruptionState(saveData);

        // PHASE 4: Restore audio log (if not interrupted)
        currentRestorationPhase = RestorationPhase.RestoringAudioLog;
        if (!wasInterrupted)
        {
            yield return RestoreAudioLogState(saveData.audioLogData);
        }
        else
        {
            DebugLog("Audio log was interrupted by handler speech - will resume when speech finishes");
        }

        // PHASE 5: Complete
        currentRestorationPhase = RestorationPhase.Complete;
        isPerformingRestoration = false;

        DebugLog("✅ Coordinated dialogue restoration sequence complete");
    }

    /// <summary>
    /// Waits for dialogue managers to be initialized
    /// </summary>
    private IEnumerator WaitForManagersReady()
    {
        DebugLog("Waiting for dialogue managers to initialize...");

        float waitTime = 0f;
        float maxWaitTime = 2f;

        // Wait for AudioLogManager
        while (AudioLogManager.Instance == null && waitTime < maxWaitTime)
        {
            yield return new WaitForSecondsRealtime(0.1f);
            waitTime += 0.1f;
        }

        if (AudioLogManager.Instance != null)
        {
            audioLogManager = AudioLogManager.Instance;

            while (!audioLogManager.IsInitialized() && waitTime < maxWaitTime)
            {
                yield return new WaitForSecondsRealtime(0.1f);
                waitTime += 0.1f;
            }

            DebugLog(audioLogManager.IsInitialized() ? "✓ AudioLogManager ready" : "⚠ AudioLogManager initialization timeout");
        }

        // Wait for HandlerSpeechManager
        waitTime = 0f;
        while (HandlerSpeechManager.Instance == null && waitTime < maxWaitTime)
        {
            yield return new WaitForSecondsRealtime(0.1f);
            waitTime += 0.1f;
        }

        if (HandlerSpeechManager.Instance != null)
        {
            handlerSpeechManager = HandlerSpeechManager.Instance;
            DebugLog("✓ HandlerSpeechManager ready");
        }

        DebugLog("Manager initialization check complete");
    }

    /// <summary>
    /// Restores handler speech state
    /// </summary>
    private IEnumerator RestoreHandlerSpeechState(HandlerSpeechSaveData speechData)
    {
        if (speechData == null || handlerSpeechManager == null)
        {
            DebugLog("No handler speech data to restore");
            yield break;
        }

        DebugLog("Restoring handler speech state...");

        var speechSaveComponent = handlerSpeechManager.GetComponent<HandlerSpeechManagerSaveComponent>();
        if (speechSaveComponent != null)
        {
            speechSaveComponent.LoadSaveDataWithContext(speechData, RestoreContext.SaveFileLoad);
            yield return new WaitForSecondsRealtime(0.2f);

            if (handlerSpeechManager.IsSpeechPlaying)
            {
                currentlyPlaying = DialogueSystemType.HandlerSpeech;
                DebugLog($"✓ Handler speech restored and playing: {handlerSpeechManager.CurrentlyPlayingSpeech?.SpeechTitle ?? "Unknown"}");
            }
        }
    }

    /// <summary>
    /// Checks and restores interruption state
    /// </summary>
    private bool CheckAndRestoreInterruptionState(DialogueSaveData saveData)
    {
        if (!saveData.isHandlerSpeechInterruptingAudioLog)
        {
            DebugLog("No interruption state to restore");
            return false;
        }

        DebugLog($"Restoring interruption state - Audio log '{saveData.interruptedAudioLogID}' at {saveData.interruptedAudioLogTime:F2}s");

        // Find the interrupted audio log
        if (audioLogManager != null && !string.IsNullOrEmpty(saveData.interruptedAudioLogID))
        {
            interruptedAudioLog = audioLogManager.GetAudioLog(saveData.interruptedAudioLogID);
            interruptedAudioLogTime = saveData.interruptedAudioLogTime;
            isHandlerSpeechInterruptingAudioLog = interruptedAudioLog != null;

            if (isHandlerSpeechInterruptingAudioLog)
            {
                DebugLog($"✓ Interruption state restored - handler speech will block audio log restoration");
                return true;
            }
        }

        DebugLog("Failed to restore interruption state - audio log not found");
        return false;
    }

    /// <summary>
    /// Restores audio log state
    /// </summary>
    private IEnumerator RestoreAudioLogState(AudioLogManagerSaveData audioLogData)
    {
        if (audioLogData == null || audioLogManager == null)
        {
            DebugLog("No audio log data to restore");
            yield break;
        }

        // Check if handler speech is currently playing (would block restoration)
        if (IsHandlerSpeechPlaying)
        {
            DebugLog("Handler speech is playing - audio log restoration blocked");
            yield break;
        }

        DebugLog("Restoring audio log state...");

        var audioLogSaveComponent = audioLogManager.GetComponent<AudioLogManagerSaveComponent>();
        if (audioLogSaveComponent != null)
        {
            audioLogSaveComponent.LoadSaveDataWithContext(audioLogData, RestoreContext.SaveFileLoad);
            yield return new WaitForSecondsRealtime(0.2f);

            if (audioLogManager.IsAudioLogPlaying)
            {
                currentlyPlaying = DialogueSystemType.AudioLog;
                DebugLog($"✓ Audio log restored and playing: {audioLogManager.CurrentlyPlayingAudioLog?.AudioLogData?.LogTitle ?? "Unknown"}");
            }
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Gets the current playing system type
    /// </summary>
    public DialogueSystemType GetCurrentlyPlayingSystem()
    {
        return currentlyPlaying;
    }

    /// <summary>
    /// Gets interruption state information
    /// </summary>
    public (AudioLog audioLog, float time) GetInterruptedAudioLogInfo()
    {
        return (interruptedAudioLog, interruptedAudioLogTime);
    }

    /// <summary>
    /// Checks if restoration is in progress
    /// </summary>
    public bool IsRestorationInProgress()
    {
        return isPerformingRestoration;
    }

    /// <summary>
    /// Forces all dialogue systems to stop
    /// </summary>
    public void StopAllDialogueSystems()
    {
        DebugLog("Force stopping all dialogue systems");

        if (audioLogManager != null)
        {
            audioLogManager.StopCurrentAudioLog();
        }

        if (handlerSpeechManager != null)
        {
            handlerSpeechManager.StopCurrentSpeech();
        }

        ClearInterruptedAudioLog();
        currentlyPlaying = DialogueSystemType.None;
    }

    /// <summary>
    /// Manually restores interruption state (called by save component during restoration)
    /// </summary>
    public void RestoreInterruptionState(AudioLog audioLog, float time)
    {
        if (audioLog == null)
        {
            DebugLog("Cannot restore interruption - null audio log");
            return;
        }

        interruptedAudioLog = audioLog;
        interruptedAudioLogTime = time;
        isHandlerSpeechInterruptingAudioLog = true;

        DebugLog($"Interruption state manually restored - AudioLog: {audioLog.AudioLogData?.LogTitle}, Time: {time:F2}s");
    }

    #endregion

    #region Debug

    [Button("Debug: Print Current State")]
    private void DebugPrintState()
    {
        DebugLog("=== DIALOGUE SYSTEMS COORDINATOR STATE ===");
        DebugLog($"Currently Playing: {currentlyPlaying}");
        DebugLog($"Handler Speech Interrupting: {isHandlerSpeechInterruptingAudioLog}");
        if (isHandlerSpeechInterruptingAudioLog)
        {
            DebugLog($"  Interrupted Audio Log: {interruptedAudioLog?.AudioLogData?.LogTitle ?? "Unknown"}");
            DebugLog($"  Interrupted Time: {interruptedAudioLogTime:F2}s");
        }
        DebugLog($"Restoration In Progress: {isPerformingRestoration}");
        DebugLog($"Restoration Phase: {currentRestorationPhase}");
        DebugLog("==========================================");
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[DialogueCoordinator] {message}");
        }
    }

    private void OnDestroy()
    {
        // UPDATED: Clear instance when scene unloads
        if (Instance == this)
        {
            Instance = null;
            DebugLog("DialogueSystemsCoordinator destroyed with scene");
        }

        // Unsubscribe from events
        if (audioLogManager != null)
        {
            audioLogManager.OnAudioLogStarted -= HandleAudioLogStarted;
            audioLogManager.OnAudioLogStopped -= HandleAudioLogStopped;
            audioLogManager.OnAudioLogCompleted -= HandleAudioLogCompleted;
        }

        if (handlerSpeechManager != null)
        {
            handlerSpeechManager.OnSpeechStarted -= HandleHandlerSpeechStarted;
            handlerSpeechManager.OnSpeechStopped -= HandleHandlerSpeechStopped;
            handlerSpeechManager.OnSpeechCompleted -= HandleHandlerSpeechCompleted;
        }

        // Unsubscribe from game events
        GameEvents.OnGamePaused -= HandleGamePaused;
        GameEvents.OnGameResumed -= HandleGameResumed;
    }

    #region Game Pause/Resume Handling

    /// <summary>
    /// Handles game pause - nothing special needed, audio sources pause automatically
    /// </summary>
    private void HandleGamePaused()
    {
        DebugLog("Game paused - dialogue systems will pause naturally");
    }

    /// <summary>
    /// CRITICAL: Handles game resume - prevents interrupted audio log from resuming
    /// </summary>
    private void HandleGameResumed()
    {
        DebugLog("=== GAME RESUMED ===");
        DebugLog($"Current state: {currentlyPlaying}");
        DebugLog($"Has interrupted audio log: {isHandlerSpeechInterruptingAudioLog}");

        // CRITICAL FIX: If handler speech interrupted an audio log, we need to prevent
        // the audio log from resuming when the game unpauses
        if (isHandlerSpeechInterruptingAudioLog && interruptedAudioLog != null)
        {
            DebugLog("⚠️ Handler speech is still interrupting audio log - re-pausing audio log");

            // Find the player listener and re-pause the audio log
            var listener = FindFirstObjectByType<PlayerAudioLogListener>();
            if (listener != null && listener.CurrentAudioLog == interruptedAudioLog)
            {
                // The audio log will have resumed due to game unpause
                // We need to immediately pause it again since it's interrupted
                listener.PauseAudioLog();
                DebugLog("✓ Re-paused interrupted audio log");
            }
        }
        else
        {
            DebugLog("No interruption state - audio systems will resume normally");
        }
    }

    #endregion
}

/// <summary>
/// Combined save data for both dialogue systems
/// </summary>
[System.Serializable]
public class DialogueSaveData
{
    public HandlerSpeechSaveData handlerSpeechData;
    public AudioLogManagerSaveData audioLogData;

    public bool isHandlerSpeechInterruptingAudioLog = false;
    public string interruptedAudioLogID = "";
    public float interruptedAudioLogTime = 0f;

    public bool IsValid()
    {
        if (handlerSpeechData != null && !handlerSpeechData.IsValid())
            return false;

        if (audioLogData != null && !audioLogData.IsValid())
            return false;

        if (isHandlerSpeechInterruptingAudioLog && string.IsNullOrEmpty(interruptedAudioLogID))
            return false;

        return true;
    }
}