using UnityEngine;

/// <summary>
/// Singleton controller for the handler's intercom speaker inside the submarine.
///
/// HOW TO SET UP:
///   1. Add this component to the same GameObject as your intercom AudioSource
///      (or any persistent GameObject in the scene).
///   2. Drag the intercom AudioSource into the "Speech Audio Source" field.
///   3. That's it — EventTrigger will call Play() automatically.
///
/// VOLUME:
///   The controller syncs its volume with AudioManager's Dialogue category every
///   time a clip starts, and also updates live whenever the player changes settings.
///   Because the intercom is a dedicated speaker (not a pooled source), we handle
///   the volume manually here instead of going through the pool system.
/// </summary>
public class HandlerSpeechController : MonoBehaviour
{
    public static HandlerSpeechController Instance { get; private set; }

    [Header("Intercom Speaker")]
    [Tooltip("The AudioSource attached to the submarine's intercom speaker. " +
             "Assign this in the Inspector.")]
    [SerializeField] private AudioSource speechAudioSource;

    [Header("Volume")]
    [Tooltip("An extra multiplier on top of the AudioManager Dialogue volume. " +
             "Leave at 1 unless you want the intercom to be quieter than normal dialogue.")]
    [Range(0f, 1f)]
    [SerializeField] private float localVolumeMultiplier = 1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        // Standard singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (speechAudioSource == null)
            Debug.LogError("[HandlerSpeechController] No AudioSource assigned! " +
                           "Please drag your intercom AudioSource into the Inspector.");
    }

    private void OnEnable()
    {
        // Listen for any volume changes so the intercom stays in sync
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.OnVolumeSettingsChanged += RefreshVolume;
            AudioManager.Instance.OnAudioManagerInitialized += RefreshVolume;
        }
    }

    private void OnDisable()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.OnVolumeSettingsChanged -= RefreshVolume;
            AudioManager.Instance.OnAudioManagerInitialized -= RefreshVolume;
        }
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Play a handler speech clip.
    /// If a speech is already playing it will be cut off immediately and the
    /// new one will start — exactly the interrupt behaviour you asked for.
    /// </summary>
    public void Play(HandlerSpeechData data)
    {
        if (data == null)
        {
            DebugLog("Play called with null HandlerSpeechData, ignoring.");
            return;
        }

        if (data.clip == null)
        {
            Debug.LogWarning($"[HandlerSpeechController] HandlerSpeechData '{data.speechLabel}' " +
                             "has no AudioClip assigned.");
            return;
        }

        if (speechAudioSource == null)
        {
            Debug.LogError("[HandlerSpeechController] No AudioSource — cannot play speech.");
            return;
        }

        // Stop whatever is currently playing (interrupt behaviour)
        if (speechAudioSource.isPlaying)
        {
            DebugLog($"Interrupting current speech to play: {data.speechLabel}");
            speechAudioSource.Stop();
        }

        // Apply the correct volume before playing
        speechAudioSource.volume = CalculateVolume();

        // PlayOneShot would ignore Stop(), so we use clip + Play() instead
        speechAudioSource.clip = data.clip;
        speechAudioSource.Play();

        DebugLog($"Playing handler speech: '{data.speechLabel}'");
    }

    /// <summary>
    /// Immediately stop any handler speech that is currently playing.
    /// </summary>
    public void Stop()
    {
        if (speechAudioSource != null && speechAudioSource.isPlaying)
        {
            speechAudioSource.Stop();
            DebugLog("Handler speech stopped.");
        }
    }

    /// <summary>
    /// Returns true if a handler speech is currently playing.
    /// </summary>
    public bool IsPlaying => speechAudioSource != null && speechAudioSource.isPlaying;

    // -----------------------------------------------------------------------
    // Volume helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Called whenever AudioManager reports a settings change.
    /// Updates the AudioSource volume live, even mid-playback.
    /// </summary>
    private void RefreshVolume()
    {
        if (speechAudioSource != null)
            speechAudioSource.volume = CalculateVolume();
    }

    /// <summary>
    /// Calculates the correct volume:
    ///   masterVolume × dialogueCategoryVolume × localMultiplier
    /// Falls back to localVolumeMultiplier if AudioManager isn't ready yet.
    /// </summary>
    private float CalculateVolume()
    {
        if (AudioManager.Instance == null)
            return localVolumeMultiplier;

        float master = AudioManager.Instance.GetMasterVolume();
        float dialogue = AudioManager.Instance.GetCategoryVolume(AudioCategory.Dialogue);
        return master * dialogue * localVolumeMultiplier;
    }

    // -----------------------------------------------------------------------
    // Internal
    // -----------------------------------------------------------------------

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[HandlerSpeechController] {message}");
    }
}