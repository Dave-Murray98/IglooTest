using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Attached to the player to handle handler speech playback.
/// Receives commands from HandlerSpeechManager to play/stop handler speech.
/// Manages a separate AudioSource from audio logs to avoid conflicts.
/// </summary>
public class PlayerHandlerSpeechListener : MonoBehaviour
{
    [Header("Audio Components")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private bool autoCreateAudioSource = true;

    [Header("Current Playback State")]
    [SerializeField][ReadOnly] private HandlerSpeechData currentSpeech;
    [SerializeField][ReadOnly] private bool isPlaying = false;
    [SerializeField][ReadOnly] private float currentPlaybackTime = 0f;

    [Header("Audio Settings")]
    [SerializeField] private bool spatialAudio = false;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip onStartPlayClip;
    [SerializeField] private AudioClip onEndPlayClip;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private float volume = 1f;

    // Public properties
    public bool IsPlaying => isPlaying;
    public float CurrentPlaybackTime => currentPlaybackTime;
    public HandlerSpeechData CurrentSpeech => currentSpeech;

    private void Awake()
    {
        // Find or create AudioSource
        // if (audioSource == null)
        // {
        //     audioSource = GetComponent<AudioSource>();
        // }

        if (audioSource == null && autoCreateAudioSource)
        {
            DebugLog("Creating AudioSource component for handler speech");
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            Debug.LogError("[PlayerHandlerSpeechListener] No AudioSource component found or created!");
            return;
        }

        // Configure AudioSource
        ConfigureAudioSource();

        // Subscribe to game events
        GameEvents.OnGamePaused += HandlePauseGame;
        GameEvents.OnGameResumed += HandleResumeGame;
    }

    private void HandleVolumeSettingsChanged()
    {
        if (audioSource != null && AudioManager.Instance != null)
        {
            volume = AudioManager.Instance.currentSettings.masterVolume *
                     AudioManager.Instance.currentSettings.GetCategoryVolume(AudioCategory.Dialogue);
            SetVolume(volume);
        }
    }

    private void Start()
    {
        // Register with HandlerSpeechManager if it exists
        if (HandlerSpeechManager.Instance != null)
        {
            HandlerSpeechManager.Instance.RegisterListener(this);
            DebugLog("Registered with HandlerSpeechManager");
        }
        else
        {
            Debug.LogWarning("[PlayerHandlerSpeechListener] HandlerSpeechManager not found - handler speech won't play!");
        }

        // Subscribe to volume changes
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.OnVolumeSettingsChanged += HandleVolumeSettingsChanged;
            HandleVolumeSettingsChanged(); // Set initial volume
        }
    }

    private void Update()
    {
        // Track playback time and check if audio finished
        if (isPlaying && audioSource != null)
        {
            currentPlaybackTime = audioSource.time;

            // Check if audio finished playing
            if (!audioSource.isPlaying)
            {
                DebugLog("Handler speech finished playing");
                OnSpeechFinished();
            }
        }
    }

    /// <summary>
    /// Configures the AudioSource with appropriate settings
    /// </summary>
    private void ConfigureAudioSource()
    {
        if (audioSource == null) return;

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.volume = volume;
        audioSource.spatialBlend = spatialAudio ? 1f : 0f; // 0 = 2D, 1 = 3D
        audioSource.priority = 64; // High priority (lower number = higher priority)

        DebugLog($"AudioSource configured - Volume: {audioSource.volume}, Spatial: {spatialAudio}");
    }

    /// <summary>
    /// Plays a handler speech from the specified HandlerSpeechData
    /// </summary>
    public void PlaySpeech(HandlerSpeechData speechData, float startTime = 0f)
    {
        if (audioSource == null)
        {
            Debug.LogError("[PlayerHandlerSpeechListener] Cannot play - no AudioSource!");
            return;
        }

        if (speechData == null || !speechData.IsValid())
        {
            Debug.LogError("[PlayerHandlerSpeechListener] Cannot play - invalid HandlerSpeechData!");
            return;
        }

        AudioClip clip = speechData.AudioClip;
        string speechTitle = speechData.SpeechTitle;

        DebugLog($"Playing handler speech: {speechTitle} from time {startTime:F2}s (clip length: {clip.length:F2}s)");

        // Stop current audio if playing
        if (isPlaying)
        {
            DebugLog($"Stopping previous speech: {currentSpeech?.SpeechTitle ?? "Unknown"}");
            audioSource.Stop();
        }

        // Set new speech
        currentSpeech = speechData;
        audioSource.clip = clip;

        // Clamp start time to valid range
        float clampedTime = Mathf.Clamp(startTime, 0f, clip.length);
        if (startTime != clampedTime)
        {
            DebugLog($"Start time {startTime:F2}s clamped to {clampedTime:F2}s (clip length: {clip.length:F2}s)");
        }

        if (AudioManager.Instance != null && onStartPlayClip != null)
            AudioManager.Instance.PlaySound2D(onStartPlayClip, AudioCategory.UI);

        audioSource.time = clampedTime;
        currentPlaybackTime = audioSource.time;

        // Start playback
        audioSource.Play();
        isPlaying = true;

        // Verify playback started
        if (audioSource.isPlaying)
        {
            DebugLog($"✅ Playback started successfully at {currentPlaybackTime:F2}s");
        }
        else
        {
            Debug.LogWarning($"[PlayerHandlerSpeechListener] AudioSource.Play() called but isPlaying is false!");
        }
    }

    /// <summary>
    /// Stops the currently playing handler speech
    /// </summary>
    public void StopSpeech()
    {
        if (audioSource == null) return;

        if (isPlaying)
        {
            DebugLog($"Stopping handler speech: {currentSpeech?.SpeechTitle ?? "Unknown"}");
        }

        // Stop playback
        audioSource.Stop();
        audioSource.clip = null;
        isPlaying = false;
        currentPlaybackTime = 0f;
        currentSpeech = null;
    }

    /// <summary>
    /// Pauses the currently playing handler speech
    /// </summary>
    public void PauseSpeech()
    {
        if (audioSource == null || !isPlaying) return;

        DebugLog($"Pausing handler speech at {currentPlaybackTime:F2}s");
        audioSource.Pause();
        isPlaying = false;
    }

    /// <summary>
    /// Resumes a paused handler speech
    /// </summary>
    public void ResumeSpeech()
    {
        if (audioSource == null || audioSource.clip == null) return;

        DebugLog($"Resuming handler speech from {currentPlaybackTime:F2}s");
        audioSource.UnPause();
        isPlaying = true;
    }

    /// <summary>
    /// Seeks to a specific time in the current handler speech
    /// </summary>
    public void SeekToTime(float time)
    {
        if (audioSource == null || audioSource.clip == null) return;

        float clampedTime = Mathf.Clamp(time, 0f, audioSource.clip.length);
        audioSource.time = clampedTime;
        currentPlaybackTime = clampedTime;

        DebugLog($"Seeked to {clampedTime:F2}s");
    }

    /// <summary>
    /// Sets the volume for handler speech playback
    /// </summary>
    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        if (audioSource != null)
        {
            audioSource.volume = volume;
        }
    }

    /// <summary>
    /// Called when a handler speech finishes playing naturally
    /// </summary>
    private void OnSpeechFinished()
    {
        // Notify manager that speech finished
        if (HandlerSpeechManager.Instance != null)
        {
            HandlerSpeechManager.Instance.OnSpeechFinished(currentSpeech);
        }

        if (AudioManager.Instance != null && onEndPlayClip != null)
            AudioManager.Instance.PlaySound2D(onEndPlayClip, AudioCategory.UI);

        // Clean up state
        StopSpeech();
    }

    private void HandlePauseGame()
    {
        // When game pauses, we pause the handler speech
        // The audio log (if interrupted) is already paused/stopped, so no action needed there
        PauseSpeech();
    }

    private void HandleResumeGame()
    {
        // When game resumes, we resume the handler speech
        // Note: The audio log listener will check if it's interrupted before resuming
        ResumeSpeech();
    }

    /// <summary>
    /// Gets the remaining time for the current handler speech
    /// </summary>
    public float GetRemainingTime()
    {
        if (audioSource == null || audioSource.clip == null || !isPlaying)
            return 0f;

        return audioSource.clip.length - currentPlaybackTime;
    }

    /// <summary>
    /// Gets the progress percentage (0-1) of the current handler speech
    /// </summary>
    public float GetProgressPercentage()
    {
        if (audioSource == null || audioSource.clip == null || audioSource.clip.length == 0f)
            return 0f;

        return currentPlaybackTime / audioSource.clip.length;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerHandlerSpeechListener] {message}");
        }
    }

    private void OnDestroy()
    {
        // Unregister from manager
        if (HandlerSpeechManager.Instance != null)
        {
            HandlerSpeechManager.Instance.UnregisterListener(this);
        }

        // Unsubscribe from events
        GameEvents.OnGamePaused -= HandlePauseGame;
        GameEvents.OnGameResumed -= HandleResumeGame;

        // Unsubscribe from audio manager events
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.OnVolumeSettingsChanged -= HandleVolumeSettingsChanged;
        }
    }

#if UNITY_EDITOR
    [Button("Test Stop")]
    private void TestStop()
    {
        if (Application.isPlaying)
        {
            StopSpeech();
        }
        else
        {
            Debug.LogWarning("Test functions only work in Play mode");
        }
    }

    [Button("Test Pause/Resume")]
    private void TestPauseResume()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Test functions only work in Play mode");
            return;
        }

        if (isPlaying)
        {
            PauseSpeech();
        }
        else if (audioSource != null && audioSource.clip != null)
        {
            ResumeSpeech();
        }
    }
#endif
}