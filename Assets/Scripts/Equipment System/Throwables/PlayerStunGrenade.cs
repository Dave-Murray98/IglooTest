using UnityEngine;

/// <summary>
/// Stun grenade that will stun any monster that comes in contact with its stun trigger collider.
/// The stun effect only activates after the configured activation delay has passed.
/// </summary>
public class PlayerStunGrenade : PlayerThrowable
{
    [Header("Stun Grenade Settings")]
    [SerializeField] private float stunDuration = 5f;
    [SerializeField] private Collider stunCol;

    [Header("Visual Feedback")]
    [SerializeField] private bool spawnParticleOnActivation = true;

    private bool hasSpawnedParticle = false;

    protected virtual void Awake()
    {
        // Ensure collider is trigger
        if (stunCol == null)
            stunCol = GetComponentInChildren<Collider>();

        if (stunCol != null)
        {
            stunCol.isTrigger = true;
            // Start with collider disabled until activation delay passes
            stunCol.enabled = false;
        }
        else
        {
            Debug.LogError($"[PlayerStunGrenade] {gameObject.name} has no stun Collider component!");
        }
    }

    /// <summary>
    /// Initialize throwable when spawned from pool.
    /// Called by PlayerThrowablePool AFTER position/rotation are set.
    /// </summary>
    public override void Initialize(float throwableDamage, ItemData throwableType, Vector3 position, Quaternion rotation)
    {
        base.Initialize(throwableDamage, throwableType, position, rotation);
        hasSpawnedParticle = false;

        // Ensure collider is disabled until activation
        if (stunCol != null)
        {
            stunCol.enabled = false;
        }
    }

    /// <summary>
    /// Called when the activation delay passes - enable the stun collider and spawn particle
    /// </summary>
    protected override void OnEffectActivated()
    {
        base.OnEffectActivated();

        // Enable the stun collider
        if (stunCol != null)
        {
            stunCol.enabled = true;
            DebugLog($"Stun grenade activated! Collider enabled at {transform.position}");
        }

        // Spawn particle effect on activation
        if (spawnParticleOnActivation && !hasSpawnedParticle)
        {
            hasSpawnedParticle = true;
            ParticleFXPool.Instance.GetStunGrenade(transform.position, transform.rotation);
            DebugLog($"Spawned stun grenade particle at {transform.position}");
        }

        NoisePool.CreateNoise(transform.position, sourcethrowableType.effectNoiseVolume);
    }

    protected void OnTriggerEnter(Collider other)
    {
        // Only stun if the effect is active (delay has passed)
        if (!isActive || !isEffectActive)
        {
            DebugLog($"Ignored collision with {other.gameObject.name} - effect not yet active");
            return;
        }

        DebugLog($"throwable hit: {other.gameObject.name}, at position {transform.position}");

        // Check for MonsterHitBox component
        MonsterHitBox monsterHitBox = other.GetComponent<MonsterHitBox>();

        if (monsterHitBox != null)
        {
            // Apply stun to monster
            monsterHitBox.StunMonster(stunDuration);
            DebugLog($"Stunned Monster for {stunDuration} seconds");
        }
        else
        {
            DebugLog($"Hit {other.gameObject.name} (no MonsterHitbox component)");
        }
    }

    /// <summary>
    /// Reset state when returning to pool
    /// </summary>
    public override void ResetState()
    {
        base.ResetState();
        hasSpawnedParticle = false;

        // Disable collider when returning to pool
        if (stunCol != null)
        {
            stunCol.enabled = false;
        }
    }

    protected override void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerStunGrenade] {message}");
        }
    }
}