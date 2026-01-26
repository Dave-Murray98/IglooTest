using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

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
    /// <param name="category">The audio category this pool manages</param>
    /// <param name="parent">Parent transform for organizing pool objects in hierarchy</param>
    /// <param name="initialSize">Initial number of audio sources to create</param>
    /// <param name="maxSize">Maximum number of audio sources allowed (prevents memory issues)</param>
    public AudioPool(AudioCategory category, Transform parent, int initialSize = 5, int maxSize = 50)
    {
        this.category = category;
        this.poolParent = parent;
        this.initialSize = initialSize;
        this.maxSize = maxSize;

        // Pre-create initial pool of audio sources
        for (int i = 0; i < initialSize; i++)
        {
            CreateNewSource();
        }
    }

    /// <summary>
    /// Gets an available audio source from the pool, or creates a new one if needed
    /// </summary>
    public PooledAudioSource GetSource()
    {
        PooledAudioSource source;

        // Try to get an available source from the queue
        if (availableSources.Count > 0)
        {
            source = availableSources.Dequeue();
        }
        else
        {
            // No available sources - need to create a new one or reuse oldest active
            if (allSources.Count < maxSize)
            {
                // Create new source (we haven't hit the max limit yet)
                source = CreateNewSource();
            }
            else
            {
                // At max capacity - stop and reuse the oldest active source
                source = activeSources[0];
                source.Stop(); // This will call ReturnToPool, but we immediately reuse it
                Debug.LogWarning($"[AudioPool:{category}] Max pool size reached ({maxSize}). Reusing oldest source.");
            }
        }

        // Activate and track the source
        source.gameObject.SetActive(true);
        activeSources.Add(source);

        return source;
    }

    /// <summary>
    /// Returns a pooled audio source back to the available queue
    /// </summary>
    public void ReturnToPool(PooledAudioSource source)
    {
        if (source == null) return;

        // Remove from active list
        activeSources.Remove(source);

        // Reset state
        source.ResetState();

        // Add back to available queue
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
        // Create a copy of the list since Stop() modifies activeSources
        var sourcesToStop = new List<PooledAudioSource>(activeSources);

        foreach (var source in sourcesToStop)
        {
            source.Stop();
        }
    }

    /// <summary>
    /// Stops a specific looping sound by its playback ID
    /// </summary>
    /// <param name="playbackID">The ID returned when the sound was played</param>
    /// <returns>True if the sound was found and stopped</returns>
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
    /// <param name="audioTag">The tag assigned when playing the sound</param>
    /// <returns>Number of sounds stopped</returns>
    public int StopLoopingSoundsByTag(string audioTag)
    {
        int stoppedCount = 0;

        // Create a copy since Stop() modifies the activeSources list
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
    /// Stops all looping sounds in this pool (existing method, keeping for compatibility)
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


    /// Updates volume for all active audio sources in this pool
    /// </summary>
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

    /// <summary>
    /// Creates a new audio source and adds it to the pool
    /// </summary>
    private PooledAudioSource CreateNewSource()
    {
        // Create GameObject with PooledAudioSource component
        GameObject sourceObj = new GameObject($"AudioSource_{category}_{allSources.Count}");
        sourceObj.transform.SetParent(poolParent);
        sourceObj.SetActive(false);

        // Add and initialize the pooled audio source component
        PooledAudioSource pooledSource = sourceObj.AddComponent<PooledAudioSource>();
        pooledSource.Initialize(this, category);

        // Configure the AudioSource component for 3D audio
        AudioSource audioSource = sourceObj.GetComponent<AudioSource>();
        audioSource.spatialBlend = 1.0f; // Full 3D by default
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 50f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.playOnAwake = false;

        if (category == AudioCategory.UI)
        {
            //ignore the listener effects
            audioSource.bypassListenerEffects = true;
        }

        // Track the new source
        allSources.Add(pooledSource);
        availableSources.Enqueue(pooledSource);

        return pooledSource;
    }

    /// <summary>
    /// Gets debug information about this pool's current state
    /// </summary>
    public string GetDebugInfo()
    {
        return $"{category}: Active={ActiveCount}, Available={AvailableCount}, Total={TotalCount}/{maxSize}, Volume={Volume:F2}";
    }

    /// <summary>
    /// Cleans up all audio sources in this pool
    /// </summary>
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