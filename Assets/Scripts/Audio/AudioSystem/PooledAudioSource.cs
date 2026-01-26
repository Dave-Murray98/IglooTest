using UnityEngine;

/// <summary>
/// Wrapper component for AudioSource that enables object pooling behavior.
/// Automatically returns itself to the pool when audio finishes playing.
/// Handles both one-shot and looping audio playback.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PooledAudioSource : MonoBehaviour
{
    private AudioSource audioSource;
    private AudioPool ownerPool;
    public bool isLooping;
    private float startTime;

    /// <summary>
    /// Whether this audio source is currently playing audio
    /// </summary>
    public bool IsPlaying => audioSource != null && audioSource.isPlaying;

    /// <summary>
    /// The audio category this source belongs to
    /// </summary>
    public AudioCategory Category { get; private set; }

    /// <summary>
    /// Unique ID for this specific audio playback instance
    /// </summary>
    public int PlaybackID { get; private set; }

    /// <summary>
    /// Optional tag to identify what type of sound this is (e.g., "tool_use", "ambience")
    /// </summary>
    public string AudioTag { get; private set; }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    /// <summary>
    /// Initializes this pooled audio source with its owner pool and category
    /// </summary>
    public void Initialize(AudioPool pool, AudioCategory category)
    {
        ownerPool = pool;
        Category = category;
    }

    /// <summary>
    /// Plays an audio clip with the specified settings
    /// Now returns a playback ID for tracking
    /// <param name="clip">The audio clip to play</param>
    /// <param name="position">World position for 3D audio (use Vector3.zero for 2D)</param>
    /// <param name="volume">Volume level (0-1)</param>
    /// <param name="pitch">Pitch adjustment (default 1.0)</param>
    /// <param name="loop">Whether the audio should loop</param>
    /// <param name="spatialBlend">0 = 2D, 1 = 3D (default 1 for spatial audio)</param>
    /// </summary>
    public int Play(AudioClip clip, Vector3 position, float volume, float pitch = 1.0f, bool loop = false, float spatialBlend = 1.0f, string audioTag = "")
    {
        if (clip == null)
        {
            Debug.LogWarning("[PooledAudioSource] Attempted to play null audio clip");
            ReturnToPool();
            return -1;
        }

        // Generate unique ID for this playback
        PlaybackID = UnityEngine.Random.Range(1000000, 9999999);
        AudioTag = audioTag;

        // Set position
        transform.position = position;

        // Configure audio source
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.pitch = pitch;
        audioSource.loop = loop;
        audioSource.spatialBlend = spatialBlend;

        // Store state
        isLooping = loop;
        startTime = Time.time;

        // Play the audio
        audioSource.Play();

        // If not looping, schedule return to pool
        if (!isLooping)
        {
            Invoke(nameof(CheckAndReturnToPool), clip.length / pitch + 0.1f);
        }

        return PlaybackID; // Return ID so caller can track this sound
    }

    /// <summary>
    /// Stops playback and returns this source to the pool
    /// </summary>
    public void Stop()
    {
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        CancelInvoke(nameof(CheckAndReturnToPool));
        ReturnToPool();
    }

    /// <summary>
    /// Updates the volume of this audio source
    /// </summary>
    public void SetVolume(float volume)
    {
        if (audioSource != null)
        {
            audioSource.volume = Mathf.Clamp01(volume);
        }
    }

    /// <summary>
    /// Checks if audio has finished playing, then returns to pool
    /// </summary>
    private void CheckAndReturnToPool()
    {
        if (!audioSource.isPlaying && !isLooping)
        {
            ReturnToPool();
        }
    }

    /// <summary>
    /// Returns this audio source to its owner pool for reuse
    /// </summary>
    private void ReturnToPool()
    {
        if (ownerPool != null)
        {
            ownerPool.ReturnToPool(this);
        }
    }


    /// <summary>
    /// Resets the audio source to default state before returning to pool
    /// </summary>
    public void ResetState()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
            audioSource.volume = 1f;
            audioSource.pitch = 1f;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
        }

        isLooping = false;
        PlaybackID = -1;
        AudioTag = "";
        CancelInvoke();
        gameObject.SetActive(false);
    }


    private void OnDestroy()
    {
        CancelInvoke();
    }
}