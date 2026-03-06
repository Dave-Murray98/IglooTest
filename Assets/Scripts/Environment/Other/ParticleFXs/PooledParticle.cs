using System.ComponentModel;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Component attached to pooled particle effects.
/// Automatically returns the particle to the pool when it finishes playing.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class PooledParticle : MonoBehaviour
{
    private ParticleFXPool pool;
    private ParticleFXPool.ParticleType particleType;
    private ParticleSystem particle;
    private bool isInitialized = false;

    [Header("Audio")]
    public AudioClip[] audioClips;


    public bool isExteriorAudio;

    private void Awake()
    {
        particle = GetComponent<ParticleSystem>();
        if (particle == null)
        {
            particle = GetComponentInChildren<ParticleSystem>();
        }

        if (particle == null)
        {
            Debug.LogError($"PooledParticle: No ParticleSystem found on {gameObject.name}!");
        }
    }

    /// <summary>
    /// Initialize the pooled particle with its pool reference and type
    /// </summary>
    public void Initialize(ParticleFXPool pool, ParticleFXPool.ParticleType particleType, bool isExternalAudio)
    {
        this.pool = pool;
        this.particleType = particleType;
        this.isExteriorAudio = isExternalAudio;
        isInitialized = true;
    }

    private void OnEnable()
    {
        if (!isInitialized || particle == null) return;

        // Start checking for particle completion
        StartCoroutine(CheckParticleCompletion());

        // Play audio
        PlayAudioClip();
    }

    private void OnDisable()
    {
        // Stop all coroutines when disabled
        StopAllCoroutines();
    }

    /// <summary>
    /// Coroutine that checks if the particle has finished playing and returns it to the pool
    /// </summary>
    private System.Collections.IEnumerator CheckParticleCompletion()
    {
        // Wait for the particle system to start playing
        yield return new WaitForSeconds(0.1f);

        // Wait until the particle system is no longer playing
        while (particle.isPlaying)
        {
            yield return null;
        }

        // Extra safety: wait a bit more to ensure all particles have truly finished
        yield return new WaitForSeconds(0.2f);

        // Return to pool
        if (pool != null && isInitialized)
        {
            pool.ReturnParticle(gameObject, particleType);
        }
    }

    /// <summary>
    /// Manually return this particle to the pool (useful for forced cleanup)
    /// </summary>
    public void ReturnToPool()
    {
        if (pool != null && isInitialized)
        {
            StopAllCoroutines();
            pool.ReturnParticle(gameObject, particleType);
        }
    }

    public void PlayAudioClip()
    {
        if (audioClips.Length <= 0)
        {
            return;
        }

        AudioClip randomClip = audioClips[Random.Range(0, audioClips.Length)];
        if (isExteriorAudio)
            AudioManager.Instance.PlaySound(randomClip, transform.position, AudioCategory.Ambience, layer: AudioLayer.Exterior);
        else
            AudioManager.Instance.PlaySound(randomClip, transform.position, AudioCategory.Ambience, layer: AudioLayer.Interior);
    }

    private void OnDestroy()
    {
        // Clean up if the particle is being destroyed while active
        StopAllCoroutines();
    }
}