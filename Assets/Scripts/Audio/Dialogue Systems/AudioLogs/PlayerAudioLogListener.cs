using UnityEngine;
using Sirenix.OdinInspector;
using Infohazard.Core;
using System;
using Unity.VisualScripting;

/// <summary>
/// SIMPLIFIED: Attached to the player to handle audio log playback.
/// Receives commands from AudioLogManager to play/stop audio.
/// Interruption coordination is now handled by DialogueSystemsCoordinator.
/// </summary>
public class PlayerAudioLogListener : MonoBehaviour
{
    [Header("Audio Components")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private bool autoCreateAudioSource = true;

    [Header("Current Playback State")]
    [SerializeField][ReadOnly] private AudioLog currentAudioLog;
    [SerializeField][ReadOnly] private bool isPlaying = false;
    [SerializeField][ReadOnly] private float currentPlaybackTime = 0f;

    [Header("Audio Settings")]
    [SerializeField] private bool spatialAudio = false;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;


    [Header("Audio Clips")]
    [SerializeField] private AudioClip onStartPlayClip;

    private float volume = 0.8f;

    // Public properties
    public bool IsPlaying => isPlaying;
    public float CurrentPlaybackTime => currentPlaybackTime;
    public AudioLog CurrentAudioLog => currentAudioLog;

    private void Awake()
    {
        if (audioSource == null && autoCreateAudioSource)
        {
            DebugLog("Creating AudioSource component");
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            Debug.LogError("[PlayerAudioLogListener] No AudioSource component found or created!");
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
        if (audioSource != null)
        {
            volume = AudioManager.Instance.currentSettings.masterVolume * AudioManager.Instance.currentSettings.GetCategoryVolume(AudioCategory.Dialogue);
            SetVolume(volume);
        }
    }

    private void Start()
    {
        // Register with AudioLogManager if it exists
        if (AudioLogManager.Instance != null)
        {
            AudioLogManager.Instance.RegisterListener(this);
            DebugLog("Registered with AudioLogManager");
        }
        else
        {
            Debug.LogWarning("[PlayerAudioLogListener] AudioLogManager not found - audio logs won't play!");
        }

        AudioManager.Instance.OnVolumeSettingsChanged += HandleVolumeSettingsChanged;
    }

    private void Update()
    {
        // Track playback time and check if audio finished
        if (isPlaying && audioSource != null)
        {
            // SAFETY CHECK: Ensure clip is not null before accessing time
            // This prevents warnings when handler speech interrupts and clears the clip
            if (audioSource.clip == null)
            {
                DebugLog("Audio clip is null but isPlaying was true - cleaning up state");
                // Clean up inconsistent state
                isPlaying = false;
                currentPlaybackTime = 0f;
                currentAudioLog = null;
                return;
            }

            currentPlaybackTime = audioSource.time;

            // Check if audio finished playing
            if (!audioSource.isPlaying)
            {
                DebugLog("Audio log finished playing");
                OnAudioLogFinished();
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
        audioSource.priority = 128; // Medium priority

        DebugLog($"AudioSource configured - Volume: {audioSource.volume}, Spatial: {spatialAudio}");
    }

    /// <summary>
    /// Plays an audio log from the specified AudioLog component
    /// </summary>
    public void PlayAudioLog(AudioLog audioLog, float startTime = 0f)
    {
        if (audioSource == null)
        {
            Debug.LogError("[PlayerAudioLogListener] Cannot play - no AudioSource!");
            return;
        }

        if (audioLog == null || audioLog.AudioLogData == null)
        {
            Debug.LogError("[PlayerAudioLogListener] Cannot play - invalid AudioLog or AudioLogData!");
            return;
        }

        AudioClip clip = audioLog.AudioLogData.AudioClip;
        if (clip == null)
        {
            Debug.LogError($"[PlayerAudioLogListener] Cannot play - no audio clip in {audioLog.AudioLogData.LogTitle}!");
            return;
        }

        string logTitle = audioLog.AudioLogData.LogTitle;
        DebugLog($"Playing audio log: {logTitle} from time {startTime:F2}s (clip length: {clip.length:F2}s)");

        // Stop current audio if playing
        if (isPlaying)
        {
            DebugLog($"Stopping previous audio: {currentAudioLog?.AudioLogData?.LogTitle ?? "Unknown"}");
            audioSource.Stop();
        }

        // Set new audio log
        currentAudioLog = audioLog;
        audioSource.clip = clip;

        // Clamp start time to valid range
        float clampedTime = Mathf.Clamp(startTime, 0f, clip.length);
        if (startTime != clampedTime)
        {
            DebugLog($"Start time {startTime:F2}s clamped to {clampedTime:F2}s (clip length: {clip.length:F2}s)");
        }

        audioSource.time = clampedTime;
        currentPlaybackTime = audioSource.time;

        if (AudioManager.Instance != null && onStartPlayClip != null)
            AudioManager.Instance.PlaySound2D(onStartPlayClip, AudioCategory.UI);

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
            Debug.LogWarning($"[PlayerAudioLogListener] AudioSource.Play() called but isPlaying is false!");
        }

        // Update audio log state
        if (currentAudioLog != null)
        {
            currentAudioLog.IsCurrentlyPlaying = true;
        }
    }

    /// <summary>
    /// Stops the currently playing audio log
    /// </summary>
    public void StopAudioLog()
    {
        if (audioSource == null) return;

        if (isPlaying)
        {
            DebugLog($"Stopping audio log: {currentAudioLog?.AudioLogData?.LogTitle ?? "Unknown"}");
        }

        // Stop playback
        audioSource.Stop();
        audioSource.clip = null;
        isPlaying = false;
        currentPlaybackTime = 0f;

        // Update audio log state
        if (currentAudioLog != null)
        {
            currentAudioLog.IsCurrentlyPlaying = false;
            currentAudioLog = null;
        }
    }

    /// <summary>
    /// Pauses the currently playing audio log
    /// </summary>
    public void PauseAudioLog()
    {
        if (audioSource == null || !isPlaying)
        {
            DebugLog("Cannot pause - not playing or no audio source");
            return;
        }

        DebugLog($"Pausing audio log at {currentPlaybackTime}s");
        audioSource.Pause();
        isPlaying = false;
    }

    /// <summary>
    /// Resumes a paused audio log
    /// </summary>
    public void ResumeAudioLog()
    {
        if (audioSource == null)
        {
            DebugLog("Cannot resume - no audio source");
            return;
        }

        if (audioSource.clip == null)
        {
            DebugLog("Cannot resume - no audio clip loaded");
            return;
        }

        if (currentAudioLog == null)
        {
            DebugLog("Cannot resume - no current audio log reference");
            return;
        }

        DebugLog($"Resuming audio log '{currentAudioLog.AudioLogData?.LogTitle ?? "Unknown"}' from {currentPlaybackTime:F2}s");
        audioSource.UnPause();
        isPlaying = true;

        // Update the audio log's state
        currentAudioLog.IsCurrentlyPlaying = true;
    }

    /// <summary>
    /// Seeks to a specific time in the current audio log
    /// </summary>
    public void SeekToTime(float time)
    {
        if (audioSource == null || audioSource.clip == null) return;

        float clampedTime = Mathf.Clamp(time, 0f, audioSource.clip.length);
        audioSource.time = clampedTime;
        currentPlaybackTime = clampedTime;

        DebugLog($"Seeked to {clampedTime}s");
    }

    /// <summary>
    /// Sets the volume for audio log playback
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
    /// Called when an audio log finishes playing naturally
    /// </summary>
    private void OnAudioLogFinished()
    {
        // Notify manager that audio log finished
        if (AudioLogManager.Instance != null)
        {
            AudioLogManager.Instance.OnAudioLogFinished(currentAudioLog);
        }

        // Clean up state
        StopAudioLog();
    }

    private void HandlePauseGame()
    {
        PauseAudioLog();
    }

    /// <summary>
    /// CRITICAL FIX: Check with coordinator before resuming to prevent interrupted audio from playing
    /// </summary>
    private void HandleResumeGame()
    {
        // Check if this audio log is currently interrupted by handler speech
        if (DialogueSystemsCoordinator.Instance != null &&
            DialogueSystemsCoordinator.Instance.HasInterruptedAudioLog &&
            currentAudioLog != null)
        {
            DebugLog("Audio log is interrupted by handler speech - NOT resuming");
            return;
        }

        // Safe to resume
        ResumeAudioLog();
    }

    /// <summary>
    /// Gets the remaining time for the current audio log
    /// </summary>
    public float GetRemainingTime()
    {
        if (audioSource == null || audioSource.clip == null || !isPlaying)
            return 0f;

        return audioSource.clip.length - currentPlaybackTime;
    }

    /// <summary>
    /// Gets the progress percentage (0-1) of the current audio log
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
            Debug.Log($"[PlayerAudioLogListener] {message}");
        }
    }

    private void OnDestroy()
    {
        // Unregister from manager
        if (AudioLogManager.Instance != null)
        {
            AudioLogManager.Instance.UnregisterListener(this);
        }

        // Unsubscribe from events
        GameEvents.OnGamePaused -= HandlePauseGame;
        GameEvents.OnGameResumed -= HandleResumeGame;

        // Unsubscribe from audio manager events
        if (AudioManager.Instance != null)
            AudioManager.Instance.OnVolumeSettingsChanged -= HandleVolumeSettingsChanged;
    }

#if UNITY_EDITOR
    [Button("Test Stop")]
    private void TestStop()
    {
        if (Application.isPlaying)
        {
            StopAudioLog();
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
            PauseAudioLog();
        }
        else if (audioSource != null && audioSource.clip != null)
        {
            ResumeAudioLog();
        }
    }
#endif
}