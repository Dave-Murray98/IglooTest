using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Wrapper component for AudioSource that enables object pooling behavior.
/// Automatically returns itself to the pool when audio finishes playing.
/// Handles both one-shot and looping audio playback.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PooledAudioSource : MonoBehaviour
{
    public AudioSource audioSource;
    private AudioPool ownerPool;
    public bool isLooping;
    private float startTime;

    public bool IsPlaying => audioSource != null && audioSource.isPlaying;
    public AudioCategory Category { get; private set; }
    public int PlaybackID { get; private set; }
    public string AudioTag { get; private set; }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    public void Initialize(AudioPool pool, AudioCategory category)
    {
        ownerPool = pool;
        Category = category;
    }

    /// <summary>
    /// Routes this audio source through the given mixer group.
    /// Called by AudioPool.GetSource() before playback begins,
    /// based on the AudioLayer (Interior or Exterior) requested by the caller.
    /// </summary>
    public void SetMixerGroup(AudioMixerGroup mixerGroup)
    {
        if (audioSource == null)
        {
            Debug.LogError("[PooledAudioSource] audioSource is null in SetMixerGroup — Awake may not have run yet.");
            return;
        }

        if (mixerGroup == null)
        {
            Debug.LogWarning("[PooledAudioSource] mixerGroup is null — check AudioManager Inspector fields.");
            return;
        }

        audioSource.outputAudioMixerGroup = mixerGroup;
        //Debug.Log("[PooledAudioSource] Mixer group assigned: " + mixerGroup.name);
    }

    /// <summary>
    /// Plays an audio clip with the specified settings.
    /// Returns a playback ID for tracking looping sounds.
    /// </summary>
    public int Play(AudioClip clip, Vector3 position, float volume, float pitch = 1.0f, bool loop = false, float spatialBlend = 1.0f, string audioTag = "")
    {
        if (clip == null)
        {
            Debug.LogWarning("[PooledAudioSource] Attempted to play null audio clip");
            ReturnToPool();
            return -1;
        }

        PlaybackID = UnityEngine.Random.Range(1000000, 9999999);
        AudioTag = audioTag;

        transform.position = position;

        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.pitch = pitch;
        audioSource.loop = loop;
        audioSource.spatialBlend = spatialBlend;

        isLooping = loop;
        startTime = Time.time;

        audioSource.Play();

        if (!isLooping)
        {
            Invoke(nameof(CheckAndReturnToPool), clip.length / pitch + 0.1f);
        }

        return PlaybackID;
    }

    public void Stop()
    {
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        CancelInvoke(nameof(CheckAndReturnToPool));
        ReturnToPool();
    }

    public void SetVolume(float volume)
    {
        if (audioSource != null)
        {
            audioSource.volume = Mathf.Clamp01(volume);
        }
    }

    private void CheckAndReturnToPool()
    {
        if (!audioSource.isPlaying && !isLooping)
        {
            ReturnToPool();
        }
    }

    private void ReturnToPool()
    {
        if (ownerPool != null)
        {
            ownerPool.ReturnToPool(this);
        }
    }

    /// <summary>
    /// Resets the audio source to a clean default state before returning to the pool.
    /// Note: the mixer group assignment is intentionally NOT cleared here — it will be
    /// re-assigned by AudioPool.GetSource() on the next use, so there is no risk of
    /// a source playing through the wrong group.
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