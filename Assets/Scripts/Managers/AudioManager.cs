using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Central audio management system with object pooling and category-based volume control.
/// Implements IManager for integration with GameManager lifecycle management.
/// Persists across scenes as a singleton to maintain audio continuity and settings.
///
/// All sounds are routed through one of two mixer groups on the SubmarineMixer:
///   - Interior: sounds originating inside the sub (engine, crew, UI) - played clean
///   - Exterior: sounds originating outside the sub (ambience, creatures, impacts) -
///               processed through the hull low-pass and reverb effects
///
/// Usage Examples:
/// - AudioManager.Instance.PlaySound(clip, transform.position, AudioCategory.PlayerSFX, layer: AudioLayer.Interior);
/// - AudioManager.Instance.PlaySound(clip, transform.position, AudioCategory.EnemySFX, layer: AudioLayer.Exterior);
/// - AudioManager.Instance.SetCategoryVolume(AudioCategory.Music, 0.5f);
/// - AudioManager.Instance.StopAllSounds(AudioCategory.Ambience);
/// </summary>
public class AudioManager : MonoBehaviour, IManager
{
    public static AudioManager Instance { get; private set; }

    [Header("Pool Configuration")]
    [Tooltip("Initial number of audio sources to create for each category")]
    [SerializeField] private int initialPoolSize = 5;

    [Tooltip("Maximum number of audio sources allowed per category (prevents memory issues)")]
    [SerializeField] private int maxPoolSize = 50;

    [Header("Category Pool Sizes (Override defaults)")]
    [SerializeField] private bool useCustomPoolSizes = false;
    [SerializeField][ShowIf("useCustomPoolSizes")] private int ambienceInitialSize = 5;
    [SerializeField][ShowIf("useCustomPoolSizes")] private int ambienceMaxSize = 20;
    [SerializeField][ShowIf("useCustomPoolSizes")] private int playerSFXInitialSize = 10;
    [SerializeField][ShowIf("useCustomPoolSizes")] private int playerSFXMaxSize = 30;
    [SerializeField][ShowIf("useCustomPoolSizes")] private int enemySFXInitialSize = 15;
    [SerializeField][ShowIf("useCustomPoolSizes")] private int enemySFXMaxSize = 50;
    [SerializeField][ShowIf("useCustomPoolSizes")] private int dialogueInitialSize = 3;
    [SerializeField][ShowIf("useCustomPoolSizes")] private int dialogueMaxSize = 10;
    [SerializeField][ShowIf("useCustomPoolSizes")] private int uiInitialSize = 5;
    [SerializeField][ShowIf("useCustomPoolSizes")] private int uiMaxSize = 15;
    [SerializeField][ShowIf("useCustomPoolSizes")] private int musicInitialSize = 2;
    [SerializeField][ShowIf("useCustomPoolSizes")] private int musicMaxSize = 3;

    [Header("Audio Mixer")]
    [Tooltip("Assign the SubmarineMixer asset here")]
    public AudioMixer submarineMixer;
    [Tooltip("The Interior group from SubmarineMixer - for sounds inside the submarine")]
    public AudioMixerGroup interiorMixerGroup;
    [Tooltip("The Exterior group from SubmarineMixer - for sounds heard through the hull")]
    public AudioMixerGroup exteriorMixerGroup;

    [Header("Audio Settings")]
    [SerializeField] private AudioSettingsData defaultSettings;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showPoolStats = false;

    // Audio pools for each category
    private Dictionary<AudioCategory, AudioPool> audioPools;
    private Transform poolContainer;

    public AudioSettingsData currentSettings;
    private float masterVolume = 1.0f;

    private bool isInitialized = false;

    // Events
    public event Action<AudioCategory, float> OnCategoryVolumeChanged;
    public event Action<float> OnMasterVolumeChanged;
    public event Action OnVolumeSettingsChanged;
    public event Action OnAudioManagerInitialized;

    public bool IsProperlyInitialized => isInitialized;

    #region Singleton & Lifecycle

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            DebugLog("AudioManager singleton created");
        }
        else
        {
            Debug.LogWarning("[AudioManager] Duplicate AudioManager detected, destroying");
            Destroy(gameObject);
        }
    }

    public void Initialize()
    {
        if (isInitialized)
        {
            DebugLog("Already initialized, skipping");
            return;
        }

        DebugLog("Initializing AudioManager");

        if (submarineMixer == null)
            Debug.LogWarning("[AudioManager] SubmarineMixer not assigned - sounds will play without mixer routing.");

        if (interiorMixerGroup == null || exteriorMixerGroup == null)
            Debug.LogWarning("[AudioManager] Interior or Exterior mixer group not assigned - sounds will play without hull filtering.");

        poolContainer = new GameObject("AudioSourcePools").transform;
        poolContainer.SetParent(transform);

        if (defaultSettings == null)
            defaultSettings = new AudioSettingsData();

        currentSettings = new AudioSettingsData(defaultSettings);
        masterVolume = currentSettings.masterVolume;

        InitializeAudioPools();

        if (MusicManager.Instance != null)
            MusicManager.Instance.SetBaseVolume(masterVolume * currentSettings.GetCategoryVolume(AudioCategory.Music));

        isInitialized = true;
        DebugLog("AudioManager initialization complete");
        OnAudioManagerInitialized?.Invoke();
    }

    public void RefreshReferences()
    {
        DebugLog("Refreshing AudioManager references");

        if (audioPools == null || poolContainer == null)
        {
            Debug.LogWarning("[AudioManager] Pools were destroyed, reinitializing");
            isInitialized = false;
            Initialize();
        }
    }

    public void Cleanup()
    {
        DebugLog("Cleaning up AudioManager");

        if (audioPools != null)
        {
            foreach (var pool in audioPools.Values)
                pool.Cleanup();
            audioPools.Clear();
        }

        if (poolContainer != null)
            Destroy(poolContainer.gameObject);

        isInitialized = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Cleanup();
            Instance = null;
        }
    }

    #endregion

    #region Pool Initialization

    private void InitializeAudioPools()
    {
        audioPools = new Dictionary<AudioCategory, AudioPool>();

        foreach (AudioCategory category in Enum.GetValues(typeof(AudioCategory)))
        {
            int initSize = GetInitialPoolSize(category);
            int maxSize = GetMaxPoolSize(category);

            var pool = new AudioPool(category, poolContainer, initSize, maxSize);

            // Pass both mixer groups to every pool so it can route on a per-play basis
            pool.SetMixerGroups(interiorMixerGroup, exteriorMixerGroup);

            pool.Volume = currentSettings.GetCategoryVolume(category) * masterVolume;

            audioPools[category] = pool;

            DebugLog($"Created {category} pool: Initial={initSize}, Max={maxSize}");
        }
    }

    private int GetInitialPoolSize(AudioCategory category)
    {
        if (!useCustomPoolSizes) return initialPoolSize;

        return category switch
        {
            AudioCategory.Ambience => ambienceInitialSize,
            AudioCategory.PlayerSFX => playerSFXInitialSize,
            AudioCategory.EnemySFX => enemySFXInitialSize,
            AudioCategory.Dialogue => dialogueInitialSize,
            AudioCategory.UI => uiInitialSize,
            AudioCategory.Music => musicInitialSize,
            _ => initialPoolSize
        };
    }

    private int GetMaxPoolSize(AudioCategory category)
    {
        if (!useCustomPoolSizes) return maxPoolSize;

        return category switch
        {
            AudioCategory.Ambience => ambienceMaxSize,
            AudioCategory.PlayerSFX => playerSFXMaxSize,
            AudioCategory.EnemySFX => enemySFXMaxSize,
            AudioCategory.Dialogue => dialogueMaxSize,
            AudioCategory.UI => uiMaxSize,
            AudioCategory.Music => musicMaxSize,
            _ => maxPoolSize
        };
    }

    #endregion

    #region Public API - Play Sounds

    /// <summary>
    /// Plays a sound at a specific position with full control over playback parameters.
    /// </summary>
    /// <param name="clip">Audio clip to play</param>
    /// <param name="position">World position for 3D audio (use Vector3.zero for 2D sounds)</param>
    /// <param name="category">Audio category for volume control and pooling</param>
    /// <param name="volume">Additional volume multiplier (0-1), applied on top of category volume</param>
    /// <param name="pitch">Pitch adjustment (default 1.0)</param>
    /// <param name="loop">Whether the audio should loop indefinitely</param>
    /// <param name="spatialBlend">0 = 2D audio, 1 = 3D audio</param>
    /// <param name="layer">
    /// Interior = inside the submarine (clean, no hull filtering).
    /// Exterior = outside the submarine (hull low-pass and reverb applied).
    /// Defaults to Interior. Pass AudioLayer.Exterior for any sound originating outside the sub.
    /// </param>
    /// <returns>The PooledAudioSource playing the sound (can be used to stop it later)</returns>
    public PooledAudioSource PlaySound(
        AudioClip clip,
        Vector3 position,
        AudioCategory category,
        float volume = 1.0f,
        float pitch = 1.0f,
        bool loop = false,
        float spatialBlend = 1.0f,
        AudioLayer layer = AudioLayer.Exterior)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("[AudioManager] Not initialized, cannot play sound");
            return null;
        }

        if (clip == null)
        {
            Debug.LogWarning("[AudioManager] Attempted to play null audio clip");
            return null;
        }

        if (!audioPools.ContainsKey(category))
        {
            Debug.LogError($"[AudioManager] No pool found for category: {category}");
            return null;
        }

        var pool = audioPools[category];
        PooledAudioSource source = pool.GetSource(layer); // layer determines mixer group routing

        float categoryVolume = currentSettings.GetCategoryVolume(category);
        float finalVolume = categoryVolume * masterVolume * Mathf.Clamp01(volume);

        source.Play(clip, position, finalVolume, pitch, loop, spatialBlend);

        string mixerGroupName = source.audioSource.outputAudioMixerGroup != null
            ? source.audioSource.outputAudioMixerGroup.name
            : "None (not assigned)";

        DebugLog($"Playing {clip.name} at {position} | Category: {category} | Mixer Group: {mixerGroupName} | Volume: {finalVolume:F2}");

        return source;
    }

    /// <summary>
    /// Plays a 2D sound (non-spatial - UI sounds, music stingers, etc.)
    /// </summary>
    public PooledAudioSource PlaySound2D(
        AudioClip clip,
        AudioCategory category,
        float volume = 1.0f,
        float pitch = 1.0f,
        bool loop = false,
        AudioLayer layer = AudioLayer.Interior)
    {
        return PlaySound(clip, Vector3.zero, category, volume, pitch, loop, 0f, layer);
    }

    /// <summary>
    /// Plays a one-shot 3D sound at a world position (simplified API for common use case)
    /// </summary>
    public PooledAudioSource PlaySoundAtPosition(
        AudioClip clip,
        Vector3 position,
        AudioCategory category,
        float volume = 1.0f,
        AudioLayer layer = AudioLayer.Interior)
    {
        return PlaySound(clip, position, category, volume, 1.0f, false, 1.0f, layer);
    }

    /// <summary>
    /// Plays a 2D sound and returns a playback ID for tracking (useful for looping sounds)
    /// </summary>
    public int PlaySound2DTracked(
        AudioClip clip,
        AudioCategory category,
        float volume = 1.0f,
        float pitch = 1.0f,
        bool loop = false,
        string audioTag = "",
        AudioLayer layer = AudioLayer.Interior)
    {
        if (clip == null)
        {
            Debug.LogWarning("[AudioManager] Cannot play null audio clip");
            return -1;
        }

        if (!audioPools.TryGetValue(category, out AudioPool pool))
        {
            Debug.LogWarning($"[AudioManager] No pool found for category: {category}");
            return -1;
        }

        PooledAudioSource source = pool.GetSource(layer);
        float finalVolume = volume * pool.Volume * masterVolume;

        return source.Play(clip, Vector3.zero, finalVolume, pitch, loop, 0f, audioTag);
    }

    /// <summary>
    /// Plays a 3D sound and returns a playback ID for tracking (useful for looping sounds)
    /// </summary>
    public int PlaySoundTracked(
        AudioClip clip,
        Vector3 position,
        AudioCategory category,
        float volume = 1.0f,
        float pitch = 1.0f,
        bool loop = false,
        float spatialBlend = 1.0f,
        string audioTag = "",
        AudioLayer layer = AudioLayer.Interior)
    {
        if (clip == null)
        {
            Debug.LogWarning("[AudioManager] Cannot play null audio clip");
            return -1;
        }

        if (!audioPools.TryGetValue(category, out AudioPool pool))
        {
            Debug.LogWarning($"[AudioManager] No pool found for category: {category}");
            return -1;
        }

        PooledAudioSource source = pool.GetSource(layer);
        float finalVolume = volume * pool.Volume * masterVolume;

        return source.Play(clip, position, finalVolume, pitch, loop, spatialBlend, audioTag);
    }

    /// <summary>
    /// Stops a specific looping sound by its playback ID
    /// </summary>
    public bool StopLoopingSound(int playbackID, AudioCategory category)
    {
        if (audioPools.TryGetValue(category, out AudioPool pool))
            return pool.StopLoopingSound(playbackID);
        return false;
    }

    /// <summary>
    /// Stops all looping sounds with a specific tag in a category
    /// </summary>
    public int StopLoopingSoundsByTag(string audioTag, AudioCategory category)
    {
        if (audioPools.TryGetValue(category, out AudioPool pool))
            return pool.StopLoopingSoundsByTag(audioTag);
        return 0;
    }

    /// <summary>
    /// Stops all looping sounds across all categories with a specific tag
    /// </summary>
    public int StopLoopingSoundsByTagAllCategories(string audioTag)
    {
        int totalStopped = 0;
        foreach (var pool in audioPools.Values)
            totalStopped += pool.StopLoopingSoundsByTag(audioTag);
        return totalStopped;
    }

    #endregion

    #region Public API - Stop Sounds

    public void StopAllSounds(AudioCategory category)
    {
        if (!isInitialized || !audioPools.ContainsKey(category))
        {
            Debug.LogWarning($"[AudioManager] Cannot stop sounds for category: {category}");
            return;
        }

        audioPools[category].StopAll();
        DebugLog($"Stopped all sounds in category: {category}");
    }

    public void StopAllSounds()
    {
        if (!isInitialized) return;

        foreach (var pool in audioPools.Values)
            pool.StopAll();

        DebugLog("Stopped all sounds");
    }

    #endregion

    #region Volume Control

    public void SetCategoryVolume(AudioCategory category, float volume)
    {
        volume = Mathf.Clamp01(volume);
        currentSettings.SetCategoryVolume(category, volume);

        if (audioPools != null && audioPools.ContainsKey(category))
            audioPools[category].Volume = volume * masterVolume;

        OnCategoryVolumeChanged?.Invoke(category, volume);
        OnVolumeSettingsChanged?.Invoke();
        DebugLog($"Set {category} volume to {volume:F2}");
    }

    public float GetCategoryVolume(AudioCategory category)
    {
        return currentSettings.GetCategoryVolume(category);
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        currentSettings.masterVolume = masterVolume;

        if (audioPools != null)
        {
            foreach (var kvp in audioPools)
            {
                float categoryVolume = currentSettings.GetCategoryVolume(kvp.Key);
                kvp.Value.Volume = categoryVolume * masterVolume;
            }
        }

        OnMasterVolumeChanged?.Invoke(masterVolume);
        OnVolumeSettingsChanged?.Invoke();
        DebugLog($"Set master volume to {masterVolume:F2}");
    }

    public float GetMasterVolume()
    {
        return masterVolume;
    }

    #endregion

    #region Settings Management

    public void ApplySettings(AudioSettingsData settings)
    {
        if (settings == null || !settings.IsValid())
        {
            Debug.LogWarning("[AudioManager] Attempted to apply invalid settings");
            return;
        }

        currentSettings = new AudioSettingsData(settings);
        masterVolume = settings.masterVolume;

        if (audioPools != null)
        {
            foreach (AudioCategory category in Enum.GetValues(typeof(AudioCategory)))
            {
                if (audioPools.ContainsKey(category))
                {
                    float categoryVolume = currentSettings.GetCategoryVolume(category);
                    audioPools[category].Volume = categoryVolume * masterVolume;
                }
            }
        }

        DebugLog($"Applied settings: {settings.GetDebugInfo()}");
    }

    public AudioSettingsData GetCurrentSettings()
    {
        return new AudioSettingsData(currentSettings);
    }

    public void ResetToDefaultSettings()
    {
        ApplySettings(defaultSettings);
        DebugLog("Reset to default settings");
    }

    #endregion

    #region Debug & Utility

    [Button("Show Pool Statistics")]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void ShowPoolStatistics()
    {
        if (!isInitialized)
        {
            Debug.Log("[AudioManager] Not initialized");
            return;
        }

        Debug.Log("=== AUDIO MANAGER POOL STATISTICS ===");
        Debug.Log($"Master Volume: {masterVolume:F2}");
        Debug.Log($"Settings: {currentSettings.GetDebugInfo()}");
        Debug.Log($"Mixer: {(submarineMixer != null ? submarineMixer.name : "NOT ASSIGNED")}");
        Debug.Log($"Interior Group: {(interiorMixerGroup != null ? interiorMixerGroup.name : "NOT ASSIGNED")}");
        Debug.Log($"Exterior Group: {(exteriorMixerGroup != null ? exteriorMixerGroup.name : "NOT ASSIGNED")}");
        Debug.Log("Pool Status:");

        foreach (var kvp in audioPools)
            Debug.Log($"  {kvp.Value.GetDebugInfo()}");

        Debug.Log("=====================================");
    }

    public int GetTotalActiveSources()
    {
        if (!isInitialized || audioPools == null) return 0;

        int total = 0;
        foreach (var pool in audioPools.Values)
            total += pool.ActiveCount;
        return total;
    }

    public int GetActiveSources(AudioCategory category)
    {
        if (!isInitialized || !audioPools.ContainsKey(category)) return 0;
        return audioPools[category].ActiveCount;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[AudioManager] {message}");
    }

    #endregion

    #region Unity Editor Helpers

    private void OnValidate()
    {
        initialPoolSize = Mathf.Max(1, initialPoolSize);
        maxPoolSize = Mathf.Max(initialPoolSize, maxPoolSize);

        if (useCustomPoolSizes)
        {
            ambienceInitialSize = Mathf.Max(1, ambienceInitialSize);
            ambienceMaxSize = Mathf.Max(ambienceInitialSize, ambienceMaxSize);
            playerSFXInitialSize = Mathf.Max(1, playerSFXInitialSize);
            playerSFXMaxSize = Mathf.Max(playerSFXInitialSize, playerSFXMaxSize);
            enemySFXInitialSize = Mathf.Max(1, enemySFXInitialSize);
            enemySFXMaxSize = Mathf.Max(enemySFXInitialSize, enemySFXMaxSize);
            dialogueInitialSize = Mathf.Max(1, dialogueInitialSize);
            dialogueMaxSize = Mathf.Max(dialogueInitialSize, dialogueMaxSize);
            uiInitialSize = Mathf.Max(1, uiInitialSize);
            uiMaxSize = Mathf.Max(uiInitialSize, uiMaxSize);
            musicInitialSize = Mathf.Max(1, musicInitialSize);
            musicMaxSize = Mathf.Max(musicInitialSize, musicMaxSize);
        }
    }

    [Button("Test Play Sound")]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void EditorTestPlaySound()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("[AudioManager] Not initialized, cannot test");
            return;
        }

        Debug.Log("[AudioManager] Test button clicked - attach an AudioClip to test properly");
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (!showPoolStats || !isInitialized || audioPools == null) return;

        foreach (var kvp in audioPools)
            Debug.DrawRay(transform.position, Vector3.up * (int)kvp.Key, Color.green);
    }

    #endregion
}