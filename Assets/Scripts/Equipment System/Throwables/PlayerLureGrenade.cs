using System.Collections;
using UnityEngine;

/// <summary>
/// Lure grenade that makes noises at intervals after an activation delay.
/// The grenade will wait for the activation delay before making its first noise,
/// then continues making noises until it despawns.
/// </summary>
public class PlayerLureGrenade : PlayerThrowable
{
    [Header("Lure Grenade Settings")]
    [SerializeField] private float noiseActiveDuration = 15f;
    [SerializeField] private float noiseInterval = 5f; // Time between each noise after activation

    /// <summary>
    /// (this is the damage of the throwable)
    /// </summary>
    [SerializeField] private float noiseVolume = 100f;

    private float makeNoiseTimer = 0f;
    private bool hasSpawnedParticle = false;

    /// <summary>
    /// Initialize throwable when spawned from pool.
    /// Called by PlayerThrowablePool AFTER position/rotation are set.
    /// </summary>
    public override void Initialize(float throwableDamage, ItemData throwableType, Vector3 position, Quaternion rotation)
    {
        base.Initialize(throwableDamage, throwableType, position, rotation);
        noiseVolume = throwableDamage;
        makeNoiseTimer = 0f;
        hasSpawnedParticle = false;

        StartCoroutine(DespawnAfter(noiseActiveDuration));
    }

    /// <summary>
    /// Called when the activation delay passes - make the first noise and spawn particle
    /// </summary>
    protected override void OnEffectActivated()
    {
        base.OnEffectActivated();

        // Make first noise immediately when activated
        MakeNoise(true);
        DebugLog($"Lure grenade activated! First noise made at {transform.position}");
    }

    protected override void Update()
    {
        base.Update();

        // Only make periodic noises after the effect is active
        if (isActive && isEffectActive)
        {
            makeNoiseTimer += Time.deltaTime;
            if (makeNoiseTimer >= noiseInterval)
            {
                MakeNoise(false);
                makeNoiseTimer = 0f;
            }
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StopAllCoroutines();
    }

    private IEnumerator DespawnAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        hasSpawnedParticle = false;
        ReturnToPool();
    }

    private void MakeNoise(bool isFirstNoise)
    {
        // Spawn noise
        NoisePool.Instance.GetNoise(transform.position, noiseVolume);

        // Spawn particle effect only on first noise
        if (isFirstNoise && !hasSpawnedParticle)
        {
            hasSpawnedParticle = true;
            ParticleFXPool.Instance.GetLureGrenade(transform.position, transform.rotation);
            DebugLog($"Spawned lure grenade particle at {transform.position}");
        }

        DebugLog($"Spawned noise at {transform.position} (volume: {noiseVolume})");
    }

    protected override void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerLureGrenade] {message}");
        }
    }
}