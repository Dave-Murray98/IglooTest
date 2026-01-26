using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Singleton manager that coordinates audio log UI display with playback.
/// Listens to AudioLogManager events and updates the subtitle display accordingly.
/// Handles subtitle timing based on current playback position.
/// </summary>
public class AudioLogUIManager : MonoBehaviour
{
    public static AudioLogUIManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("Reference to the subtitle display component")]
    [SerializeField] private AudioLogSubtitleDisplay subtitleDisplay;

    [Tooltip("Auto-find the subtitle display if not assigned")]
    [SerializeField] private bool autoFindSubtitleDisplay = true;

    [Header("Subtitle Timing")]
    [Tooltip("How often to update subtitles (in seconds)")]
    [SerializeField] private float subtitleUpdateInterval = 0.1f;

    [Header("Current State")]
    [ShowInInspector][ReadOnly] private AudioLog currentAudioLog;
    [ShowInInspector][ReadOnly] private AudioLogData currentAudioLogData;
    [ShowInInspector][ReadOnly] private bool isTrackingSubtitles = false;
    [ShowInInspector][ReadOnly] private SubtitleEntry currentSubtitle;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private PlayerAudioLogListener playerListener;
    private float nextSubtitleUpdateTime = 0f;

    private void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
            DebugLog("AudioLogUIManager initialized");
        }
        else
        {
            Debug.LogWarning("[AudioLogUIManager] Duplicate AudioLogUIManager found - destroying");
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Auto-find subtitle display if needed
        if (autoFindSubtitleDisplay && subtitleDisplay == null)
        {
            subtitleDisplay = FindFirstObjectByType<AudioLogSubtitleDisplay>();

            if (subtitleDisplay != null)
            {
                DebugLog($"Auto-found AudioLogSubtitleDisplay on {subtitleDisplay.gameObject.name}");
            }
            else
            {
                Debug.LogWarning("[AudioLogUIManager] No AudioLogSubtitleDisplay found - UI won't display!");
            }
        }

        // Find player listener
        playerListener = FindFirstObjectByType<PlayerAudioLogListener>();
        if (playerListener == null)
        {
            Debug.LogWarning("[AudioLogUIManager] No PlayerAudioLogListener found - subtitle timing won't work!");
        }

        // Subscribe to AudioLogManager events
        if (AudioLogManager.Instance != null)
        {
            AudioLogManager.Instance.OnAudioLogStarted += HandleAudioLogStarted;
            AudioLogManager.Instance.OnAudioLogStopped += HandleAudioLogStopped;
            AudioLogManager.Instance.OnAudioLogCompleted += HandleAudioLogCompleted;
            DebugLog("Subscribed to AudioLogManager events");
        }
        else
        {
            Debug.LogWarning("[AudioLogUIManager] AudioLogManager not found - UI won't respond to audio logs!");
        }
    }

    private void Update()
    {
        // Update subtitles if we're tracking them
        if (isTrackingSubtitles && Time.time >= nextSubtitleUpdateTime)
        {
            UpdateSubtitles();
            nextSubtitleUpdateTime = Time.time + subtitleUpdateInterval;
        }
    }

    #region Event Handlers

    /// <summary>
    /// Called when an audio log starts playing
    /// </summary>
    private void HandleAudioLogStarted(AudioLog audioLog)
    {
        if (audioLog == null || audioLog.AudioLogData == null)
        {
            DebugLog("Audio log started but data is null");
            return;
        }

        currentAudioLog = audioLog;
        currentAudioLogData = audioLog.AudioLogData;

        DebugLog($"Audio log started: {currentAudioLogData.LogTitle}");

        // Show the UI
        if (subtitleDisplay != null)
        {
            subtitleDisplay.ShowUI(currentAudioLogData);
        }

        // Start tracking subtitles if this audio log has them
        if (currentAudioLogData.HasSubtitles)
        {
            isTrackingSubtitles = true;
            currentSubtitle = null;
            nextSubtitleUpdateTime = Time.time;
            DebugLog($"Started tracking {currentAudioLogData.SubtitleCount} subtitles");
        }
        else
        {
            DebugLog("Audio log has no subtitles");
        }
    }

    /// <summary>
    /// Called when an audio log is manually stopped
    /// </summary>
    private void HandleAudioLogStopped(AudioLog audioLog)
    {
        DebugLog($"Audio log stopped: {audioLog?.AudioLogData?.LogTitle ?? "Unknown"}");
        StopTracking();
    }

    /// <summary>
    /// Called when an audio log finishes playing naturally
    /// </summary>
    private void HandleAudioLogCompleted(AudioLog audioLog)
    {
        DebugLog($"Audio log completed: {audioLog?.AudioLogData?.LogTitle ?? "Unknown"}");
        StopTracking();
    }

    #endregion

    #region Subtitle Management

    /// <summary>
    /// Updates the currently displayed subtitle based on playback time
    /// </summary>
    private void UpdateSubtitles()
    {
        if (currentAudioLogData == null || !currentAudioLogData.HasSubtitles)
        {
            return;
        }

        if (playerListener == null || !playerListener.IsPlaying)
        {
            return;
        }

        // Get current playback time
        float currentTime = playerListener.CurrentPlaybackTime;

        // Get the subtitle that should be displayed at this time
        SubtitleEntry newSubtitle = currentAudioLogData.GetSubtitleAtTime(currentTime);

        // Check if subtitle changed
        if (newSubtitle != currentSubtitle)
        {
            currentSubtitle = newSubtitle;

            // Update the display
            if (subtitleDisplay != null)
            {
                if (currentSubtitle != null)
                {
                    subtitleDisplay.UpdateSubtitle(currentSubtitle.Text);
                    DebugLog($"Subtitle at {currentTime:F2}s: {currentSubtitle.Text}");
                }
                else
                {
                    subtitleDisplay.ClearSubtitle();
                    DebugLog($"No subtitle at {currentTime:F2}s");
                }
            }
        }
    }

    /// <summary>
    /// Stops tracking subtitles and hides the UI
    /// </summary>
    private void StopTracking()
    {
        isTrackingSubtitles = false;
        currentAudioLog = null;
        currentAudioLogData = null;
        currentSubtitle = null;

        // Hide the UI
        if (subtitleDisplay != null)
        {
            subtitleDisplay.HideUI();
        }

        DebugLog("Stopped tracking subtitles");
    }

    #endregion

    #region Public API

    /// <summary>
    /// Manually show UI for an audio log (useful for debugging)
    /// </summary>
    public void ShowUI(AudioLogData audioLogData)
    {
        if (audioLogData == null)
        {
            Debug.LogError("[AudioLogUIManager] Cannot show UI - null AudioLogData");
            return;
        }

        if (subtitleDisplay == null)
        {
            Debug.LogError("[AudioLogUIManager] Cannot show UI - no AudioLogSubtitleDisplay assigned");
            return;
        }

        subtitleDisplay.ShowUI(audioLogData);
    }

    /// <summary>
    /// Manually hide the UI
    /// </summary>
    public void HideUI()
    {
        if (subtitleDisplay != null)
        {
            subtitleDisplay.HideUI();
        }
    }

    /// <summary>
    /// Checks if the UI is currently visible
    /// </summary>
    public bool IsUIVisible()
    {
        return subtitleDisplay != null && subtitleDisplay.IsVisible();
    }

    /// <summary>
    /// Gets the current audio log being displayed
    /// </summary>
    public AudioLog GetCurrentAudioLog()
    {
        return currentAudioLog;
    }

    /// <summary>
    /// Gets the current subtitle being displayed
    /// </summary>
    public SubtitleEntry GetCurrentSubtitle()
    {
        return currentSubtitle;
    }

    #endregion

    #region Debug

    [Button("Debug: Show Current State")]
    private void DebugShowState()
    {
        DebugLog("=== AUDIO LOG UI MANAGER STATE ===");
        DebugLog($"Is Tracking: {isTrackingSubtitles}");
        DebugLog($"Current Audio Log: {currentAudioLogData?.LogTitle ?? "None"}");
        DebugLog($"Has Subtitles: {currentAudioLogData?.HasSubtitles ?? false}");
        DebugLog($"Subtitle Count: {currentAudioLogData?.SubtitleCount ?? 0}");
        DebugLog($"Current Subtitle: {currentSubtitle?.Text ?? "None"}");

        if (playerListener != null)
        {
            DebugLog($"Playback Time: {playerListener.CurrentPlaybackTime:F2}s");
        }
    }

    [Button("Debug: Force Update Subtitles")]
    private void DebugForceUpdate()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Debug functions only work in Play mode");
            return;
        }

        UpdateSubtitles();
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[AudioLogUIManager] {message}");
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (AudioLogManager.Instance != null)
        {
            AudioLogManager.Instance.OnAudioLogStarted -= HandleAudioLogStarted;
            AudioLogManager.Instance.OnAudioLogStopped -= HandleAudioLogStopped;
            AudioLogManager.Instance.OnAudioLogCompleted -= HandleAudioLogCompleted;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}