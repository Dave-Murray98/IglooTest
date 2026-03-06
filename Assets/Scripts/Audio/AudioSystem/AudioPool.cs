using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Manages a pool of audio sources for a specific audio category.
/// Handles dynamic growth, recycling, and volume management for all sources in the pool.
/// This improves performance by reusing AudioSource components instead of constantly creating/destroying them.
/// </summary>
public class AudioPool
{
    private readonly AudioCategory category;
    private readonly Transform poolParent;
    private readonly int initialSize;
    private readonly int maxSize;

    // Mixer groups — set once by AudioManager after creation
    private AudioMixerGroup interiorMixerGroup;
    private AudioMixerGroup exteriorMixerGroup;

    private readonly Queue<PooledAudioSource> availableSources = new Queue<PooledAudioSource>();
    private readonly List<PooledAudioSource> activeSources = new List<PooledAudioSource>();
    private readonly List<PooledAudioSource> allSources = new List<PooledAudioSource>();

    private float currentVolume = 1.0f;

    /// <summary>
    /// Current volume level for this pool's category
    /// </summary>
    public float Volume
    {
        get => currentVolume;
        set
        {
            currentVolume = Mathf.Clamp01(value);
            UpdateAllSourceVolumes();
        }
    }

    /// <summary>
    /// Number of currently active (playing) audio sources
    /// </summary>
    public int ActiveCount => activeSources.Count;

    /// <summary>
    /// Number of available (not playing) audio sources
    /// </summary>
    public int AvailableCount => availableSources.Count;

    /// <summary>
    /// Total number of audio sources in this pool
    /// </summary>
    public int TotalCount => allSources.Count;

    /// <summary>
    /// Creates a new audio pool for the specified category
    /// </summary>
    public AudioPool(AudioCategory category, Transform parent, int initialSize = 5, int maxSize = 50)
    {
        this.category = category;
        this.poolParent = parent;
        this.initialSize = initialSize;
        this.maxSize = maxSize;

        for (int i = 0; i < initialSize; i++)
        {
            CreateNewSource();
        }
    }

    /// <summary>
    /// Assigns the Interior and Exterior mixer groups to this pool.
    /// Called once by AudioManager during initialization.
    /// </summary>
    public void SetMixerGroups(AudioMixerGroup interior, AudioMixerGroup exterior)
    {
        interiorMixerGroup = interior;
        exteriorMixerGroup = exterior;
    }

    /// <summary>
    /// Gets an available audio source from the pool, or creates a new one if needed.
    /// The AudioLayer determines which mixer group the source is routed to.
    /// </summary>
    public PooledAudioSource GetSource(AudioLayer layer = AudioLayer.Interior)
    {
        PooledAudioSource source;

        if (availableSources.Count > 0)
        {
            source = availableSources.Dequeue();
        }
        else
        {
            if (allSources.Count < maxSize)
            {
                source = CreateNewSource();
            }
            else
            {
                source = activeSources[0];
                source.Stop();
                Debug.LogWarning($"[AudioPool:{category}] Max pool size reached ({maxSize}). Reusing oldest source.");
            }
        }

        // Activate FIRST so Awake has definitely run and audioSource is valid
        source.gameObject.SetActive(true);
        activeSources.Add(source);

        // Now safe to assign the mixer group
        AudioMixerGroup targetGroup = layer == AudioLayer.Exterior ? exteriorMixerGroup : interiorMixerGroup;
        source.SetMixerGroup(targetGroup);

        return source;
    }

    /// <summary>
    /// Returns a pooled audio source back to the available queue
    /// </summary>
    public void ReturnToPool(PooledAudioSource source)
    {
        if (source == null) return;

        activeSources.Remove(source);
        source.ResetState();

        if (!availableSources.Contains(source))
        {
            availableSources.Enqueue(source);
        }
    }

    /// <summary>
    /// Stops all currently playing audio sources in this pool
    /// </summary>
    public void StopAll()
    {
        var sourcesToStop = new List<PooledAudioSource>(activeSources);
        foreach (var source in sourcesToStop)
        {
            source.Stop();
        }
    }

    /// <summary>
    /// Stops a specific looping sound by its playback ID
    /// </summary>
    public bool StopLoopingSound(int playbackID)
    {
        foreach (PooledAudioSource source in activeSources)
        {
            if (source.isLooping && source.PlaybackID == playbackID)
            {
                source.Stop();
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Stops all looping sounds with a specific audio tag
    /// </summary>
    public int StopLoopingSoundsByTag(string audioTag)
    {
        int stoppedCount = 0;
        var sourcesToCheck = new List<PooledAudioSource>(activeSources);

        foreach (PooledAudioSource source in sourcesToCheck)
        {
            if (source.isLooping && source.AudioTag == audioTag)
            {
                source.Stop();
                stoppedCount++;
            }
        }

        return stoppedCount;
    }

    /// <summary>
    /// Stops all looping sounds in this pool
    /// </summary>
    public void StopAllLoopingSounds()
    {
        var sourcesToStop = new List<PooledAudioSource>(activeSources);
        foreach (PooledAudioSource source in sourcesToStop)
        {
            if (source.isLooping)
            {
                source.Stop();
            }
        }
    }

    private void UpdateAllSourceVolumes()
    {
        foreach (var source in activeSources)
        {
            if (source != null)
            {
                source.SetVolume(currentVolume);
            }
        }
    }

    private PooledAudioSource CreateNewSource()
    {
        GameObject sourceObj = new GameObject($"AudioSource_{category}_{allSources.Count}");
        sourceObj.transform.SetParent(poolParent);
        sourceObj.SetActive(false);

        PooledAudioSource pooledSource = sourceObj.AddComponent<PooledAudioSource>();
        pooledSource.Initialize(this, category);

        AudioSource audioSource = sourceObj.GetComponent<AudioSource>();
        audioSource.spatialBlend = 1.0f;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 50f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.playOnAwake = false;

        if (category == AudioCategory.UI)
        {
            // UI sounds bypass listener effects (reverb, low-pass) entirely —
            // they always sound clean regardless of the submarine's environment
            audioSource.bypassListenerEffects = true;
        }

        allSources.Add(pooledSource);
        availableSources.Enqueue(pooledSource);

        return pooledSource;
    }

    public string GetDebugInfo()
    {
        return $"{category}: Active={ActiveCount}, Available={AvailableCount}, Total={TotalCount}/{maxSize}, Volume={Volume:F2}";
    }

    public void Cleanup()
    {
        foreach (var source in allSources)
        {
            if (source != null && source.gameObject != null)
            {
                Object.Destroy(source.gameObject);
            }
        }

        availableSources.Clear();
        activeSources.Clear();
        allSources.Clear();
    }
}