using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Central audio management system with object pooling and category-based volume control.
/// Implements IManager for integration with GameManager's lifecycle management.
/// Persists across scenes as a singleton to maintain audio continuity and settings.
/// 
/// Usage Examples:
/// - AudioManager.Instance.PlaySound(clip, transform.position, AudioCategory.PlayerSFX);
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

    [Header("Audio Settings")]
    [SerializeField] private AudioSettingsData defaultSettings;

    [Header("3D Audio Settings")]
    [Tooltip("Default minimum distance for 3D audio")]
    [SerializeField] private float default3DMinDistance = 1f;

    [Tooltip("Default maximum distance for 3D audio")]
    [SerializeField] private float default3DMaxDistance = 50f;

    [Tooltip("Rolloff mode for 3D audio distance attenuation")]
    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Linear;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showPoolStats = false;

    // Audio pools for each category
    private Dictionary<AudioCategory, AudioPool> audioPools;
    private Transform poolContainer;

    // Current settings
    public AudioSettingsData currentSettings;
    private float masterVolume = 1.0f;

    // Initialization tracking
    private bool isInitialized = false;

    // Events
    public event Action<AudioCategory, float> OnCategoryVolumeChanged;
    public event Action<float> OnMasterVolumeChanged;
    public event Action OnVolumeSettingsChanged; // Event to notify any system that doesn't use audio pooling (ie audio logs) when volume settings change
    public event Action OnAudioManagerInitialized;

    /// <summary>
    /// Whether the manager is properly initialized and ready for use
    /// </summary>
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

    /// <summary>
    /// Initialize the audio manager and create all audio pools
    /// </summary>
    public void Initialize()
    {
        if (isInitialized)
        {
            DebugLog("Already initialized, skipping");
            return;
        }

        DebugLog("Initializing AudioManager");

        // Create pool container
        poolContainer = new GameObject("AudioSourcePools").transform;
        poolContainer.SetParent(transform);

        // Initialize settings
        if (defaultSettings == null)
        {
            defaultSettings = new AudioSettingsData();
        }

        currentSettings = new AudioSettingsData(defaultSettings);
        masterVolume = currentSettings.masterVolume;

        // Create audio pools for each category
        InitializeAudioPools();

        if (MusicManager.Instance != null)
            MusicManager.Instance.SetBaseVolume(masterVolume * currentSettings.GetCategoryVolume(AudioCategory.Music));

        isInitialized = true;
        DebugLog("AudioManager initialization complete");
        OnAudioManagerInitialized?.Invoke();
    }

    /// <summary>
    /// Refresh references after scene changes (IManager implementation)
    /// </summary>
    public void RefreshReferences()
    {
        DebugLog("Refreshing AudioManager references");

        // AudioManager is persistent, but we might need to verify pools still exist
        if (audioPools == null || poolContainer == null)
        {
            Debug.LogWarning("[AudioManager] Pools were destroyed, reinitializing");
            isInitialized = false;
            Initialize();
        }
    }

    /// <summary>
    /// Clean up resources (IManager implementation)
    /// </summary>
    public void Cleanup()
    {
        DebugLog("Cleaning up AudioManager");

        if (audioPools != null)
        {
            foreach (var pool in audioPools.Values)
            {
                pool.Cleanup();
            }
            audioPools.Clear();
        }

        if (poolContainer != null)
        {
            Destroy(poolContainer.gameObject);
        }

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

    /// <summary>
    /// Creates audio pools for all categories with configured sizes
    /// </summary>
    private void InitializeAudioPools()
    {
        audioPools = new Dictionary<AudioCategory, AudioPool>();

        foreach (AudioCategory category in Enum.GetValues(typeof(AudioCategory)))
        {
            int initSize = GetInitialPoolSize(category);
            int maxSize = GetMaxPoolSize(category);

            // Create pool
            var pool = new AudioPool(category, poolContainer, initSize, maxSize);

            // Set initial volume from settings
            pool.Volume = currentSettings.GetCategoryVolume(category) * masterVolume;

            // Add to dictionary
            audioPools[category] = pool;

            DebugLog($"Created {category} pool: Initial={initSize}, Max={maxSize}");
        }
    }

    /// <summary>
    /// Gets the initial pool size for a category (uses custom sizes if enabled)
    /// </summary>
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

    /// <summary>
    /// Gets the maximum pool size for a category (uses custom sizes if enabled)
    /// </summary>
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
    /// Plays a sound at a specific position with full control over playback parameters
    /// </summary>
    /// <param name="clip">Audio clip to play</param>
    /// <param name="position">World position for 3D audio (use Vector3.zero for 2D UI sounds)</param>
    /// <param name="category">Audio category for volume control and pooling</param>
    /// <param name="volume">Additional volume multiplier (0-1), applied on top of category volume</param>
    /// <param name="pitch">Pitch adjustment (default 1.0)</param>
    /// <param name="loop">Whether the audio should loop indefinitely</param>
    /// <param name="spatialBlend">0 = 2D audio, 1 = 3D audio (default 1 for spatial)</param>
    /// <returns>The PooledAudioSource playing the sound (can be used to stop it later)</returns>
    public PooledAudioSource PlaySound(
        AudioClip clip,
        Vector3 position,
        AudioCategory category,
        float volume = 1.0f,
        float pitch = 1.0f,
        bool loop = false,
        float spatialBlend = 1.0f)
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

        // Get audio source from pool
        var pool = audioPools[category];
        var source = pool.GetSource();

        // Calculate final volume (category volume * master volume * additional volume)
        float categoryVolume = currentSettings.GetCategoryVolume(category);
        float finalVolume = categoryVolume * masterVolume * Mathf.Clamp01(volume);

        // Play the audio
        source.Play(clip, position, finalVolume, pitch, loop, spatialBlend);

        DebugLog($"Playing {clip.name} at {position} | Category: {category} | Volume: {finalVolume:F2}");

        return source;
    }

    /// <summary>
    /// Plays a 2D sound (UI sounds, non-spatial audio)
    /// </summary>
    public PooledAudioSource PlaySound2D(AudioClip clip, AudioCategory category, float volume = 1.0f, bool loop = false)
    {
        return PlaySound(clip, Vector3.zero, category, volume, 1.0f, loop, 0f);
    }

    /// <summary>
    /// Plays a one-shot 3D sound (simplified API for common use case)
    /// </summary>
    public PooledAudioSource PlaySoundAtPosition(AudioClip clip, Vector3 position, AudioCategory category, float volume = 1.0f)
    {
        return PlaySound(clip, position, category, volume, 1.0f, false, 1.0f);
    }

    /// <summary>
    /// Plays a 2D sound and returns a playback ID for tracking (useful for looping sounds)
    /// </summary>
    public int PlaySound2DTracked(AudioClip clip, AudioCategory category, float volume = 1.0f, float pitch = 1.0f, bool loop = false, string audioTag = "")
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

        PooledAudioSource source = pool.GetSource();
        float finalVolume = volume * pool.Volume * masterVolume;

        return source.Play(clip, Vector3.zero, finalVolume, pitch, loop, 0f, audioTag);
    }

    public int PlaySoundTracked(AudioClip clip, Vector3 position, AudioCategory category, float volume = 1.0f, float pitch = 1.0f, bool loop = false, float spatialBlend = 1.0f, string audioTag = "")
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

        PooledAudioSource source = pool.GetSource();
        float finalVolume = volume * pool.Volume * masterVolume;

        return source.Play(clip, position, finalVolume, pitch, loop, spatialBlend, audioTag);
    }

    /// <summary>
    /// Stops a specific looping sound by its playback ID
    /// </summary>
    public bool StopLoopingSound(int playbackID, AudioCategory category)
    {
        if (audioPools.TryGetValue(category, out AudioPool pool))
        {
            return pool.StopLoopingSound(playbackID);
        }
        return false;
    }

    /// <summary>
    /// Stops all looping sounds with a specific tag in a category
    /// </summary>
    public int StopLoopingSoundsByTag(string audioTag, AudioCategory category)
    {
        if (audioPools.TryGetValue(category, out AudioPool pool))
        {
            return pool.StopLoopingSoundsByTag(audioTag);
        }
        return 0;
    }

    /// <summary>
    /// Stops all looping sounds across all categories with a specific tag
    /// </summary>
    public int StopLoopingSoundsByTagAllCategories(string audioTag)
    {
        int totalStopped = 0;
        foreach (var pool in audioPools.Values)
        {
            totalStopped += pool.StopLoopingSoundsByTag(audioTag);
        }
        return totalStopped;
    }

    #endregion

    #region Public API - Stop Sounds

    /// <summary>
    /// Stops all sounds in a specific category
    /// </summary>
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

    /// <summary>
    /// Stops all sounds across all categories
    /// </summary>
    public void StopAllSounds()
    {
        if (!isInitialized) return;

        foreach (var pool in audioPools.Values)
        {
            pool.StopAll();
        }

        DebugLog("Stopped all sounds");
    }

    #endregion

    #region Volume Control

    /// <summary>
    /// Sets the volume for a specific audio category
    /// </summary>
    /// <param name="category">Category to adjust</param>
    /// <param name="volume">Volume level (0-1)</param>
    public void SetCategoryVolume(AudioCategory category, float volume)
    {
        volume = Mathf.Clamp01(volume);
        currentSettings.SetCategoryVolume(category, volume);

        // Update pool volume (applies master volume)
        if (audioPools != null && audioPools.ContainsKey(category))
        {
            audioPools[category].Volume = volume * masterVolume;
        }

        OnCategoryVolumeChanged?.Invoke(category, volume);
        OnVolumeSettingsChanged?.Invoke();
        DebugLog($"Set {category} volume to {volume:F2}");
    }

    /// <summary>
    /// Gets the current volume for a specific category
    /// </summary>
    public float GetCategoryVolume(AudioCategory category)
    {
        return currentSettings.GetCategoryVolume(category);
    }

    /// <summary>
    /// Sets the master volume (affects all categories)
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        currentSettings.masterVolume = masterVolume;

        // Update all pool volumes
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

    /// <summary>
    /// Gets the current master volume
    /// </summary>
    public float GetMasterVolume()
    {
        return masterVolume;
    }

    #endregion

    #region Settings Management

    /// <summary>
    /// Applies a complete set of audio settings
    /// </summary>
    public void ApplySettings(AudioSettingsData settings)
    {
        if (settings == null || !settings.IsValid())
        {
            Debug.LogWarning("[AudioManager] Attempted to apply invalid settings");
            return;
        }

        currentSettings = new AudioSettingsData(settings);
        masterVolume = settings.masterVolume;

        // Update all pool volumes
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

    /// <summary>
    /// Gets a copy of the current audio settings
    /// </summary>
    public AudioSettingsData GetCurrentSettings()
    {
        return new AudioSettingsData(currentSettings);
    }

    /// <summary>
    /// Resets all audio settings to defaults
    /// </summary>
    public void ResetToDefaultSettings()
    {
        ApplySettings(defaultSettings);
        DebugLog("Reset to default settings");
    }

    #endregion

    #region Debug & Utility

    /// <summary>
    /// Gets debug information about all audio pools
    /// </summary>
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
        Debug.Log("Pool Status:");

        foreach (var kvp in audioPools)
        {
            Debug.Log($"  {kvp.Value.GetDebugInfo()}");
        }

        Debug.Log("=====================================");
    }

    /// <summary>
    /// Gets the total number of active audio sources across all pools
    /// </summary>
    public int GetTotalActiveSources()
    {
        if (!isInitialized || audioPools == null) return 0;

        int total = 0;
        foreach (var pool in audioPools.Values)
        {
            total += pool.ActiveCount;
        }
        return total;
    }

    /// <summary>
    /// Gets the number of active sources in a specific category
    /// </summary>
    public int GetActiveSources(AudioCategory category)
    {
        if (!isInitialized || !audioPools.ContainsKey(category)) return 0;
        return audioPools[category].ActiveCount;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[AudioManager] {message}");
        }
    }

    #endregion

    #region Unity Editor Helpers

    private void OnValidate()
    {
        // Ensure pool sizes are reasonable
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

        // Draw pool statistics in scene view
        foreach (var kvp in audioPools)
        {
            var pool = kvp.Value;
            Debug.DrawRay(transform.position, Vector3.up * (int)kvp.Key, Color.green);
        }
    }

    #endregion
}