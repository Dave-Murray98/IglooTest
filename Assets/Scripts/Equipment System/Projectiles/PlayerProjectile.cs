using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Individual projectile behavior for player-fired bullets.
/// Handles damage application, collision detection, timeout, and pool return.
/// Attach to projectile prefabs referenced in AmmoData.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PlayerProjectile : MonoBehaviour
{
    [Header("Projectile Configuration")]
    [SerializeField] private float timeout = 10f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Runtime state
    private float damage;
    private ItemData sourceAmmoType;
    private float activeTime;
    private bool isActive;
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Ensure collider is trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        else
        {
            Debug.LogError($"[PlayerProjectile] {gameObject.name} has no Collider component!");
        }
    }


    /// <summary>
    /// Initialize projectile when spawned from pool.
    /// Called by PlayerBulletPool AFTER position/rotation are set.
    /// </summary>
    public void Initialize(float projectileDamage, ItemData ammoType, Vector3 position, Quaternion rotation)
    {
        damage = projectileDamage;
        sourceAmmoType = ammoType;
        activeTime = 0f;
        isActive = true;

        // GameObject should already be positioned by pool
        // Just activate it
        gameObject.SetActive(true);

        // Verify position matches what was set
        if (Vector3.Distance(transform.position, position) > 0.01f)
        {
            Debug.LogWarning($"[PlayerProjectile] Position mismatch! Expected {position}, got {transform.position}. Forcing correction.");
            transform.position = position;
            transform.rotation = rotation;
        }

        // NOW enable physics after everything is positioned
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        DebugLog($"Initialized projectile - Position: {transform.position}, Rotation: {transform.rotation.eulerAngles}");
    }

    private void Update()
    {
        if (!isActive) return;

        // Track active time
        activeTime += Time.deltaTime;

        // Check timeout
        if (activeTime >= timeout)
        {
            DebugLog($"Projectile timed out after {activeTime:F2}s");
            ReturnToPool();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;

        DebugLog($"Projectile hit: {other.gameObject.name}, at position {transform.position}");

        // Check for MonsterHealth component
        MonsterHitBox monsterHitBox = other.GetComponent<MonsterHitBox>();

        // get the position of the hit point
        Vector3 slightlyFurtherBackPos = transform.position - rb.linearVelocity.normalized * 0.5f;
        Vector3 hitPoint = other.ClosestPoint(slightlyFurtherBackPos);

        if (monsterHitBox != null)
        {
            // Apply damage to monster
            monsterHitBox.TakeDamage(damage, hitPoint);
            DebugLog($"Dealt {damage} damage to {other.gameObject.name}");
        }
        else
        {
            ParticleFXPool.Instance.GetImpactFX(hitPoint, quaternion.identity);
            DebugLog($"Hit {other.gameObject.name} (no MonsterHitbox component)");
        }

        NoisePool.CreateNoise(transform.position, sourceAmmoType.effectNoiseVolume);

        // Return to pool regardless of what we hit
        ReturnToPool();
    }

    /// <summary>
    /// Return this projectile to the pool for reuse.
    /// </summary>
    private void ReturnToPool()
    {
        if (!isActive) return;

        isActive = false;

        // Stop physics
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Return to pool
        if (PlayerBulletPool.Instance != null)
        {
            PlayerBulletPool.Instance.ReturnProjectile(this);
        }
        else
        {
            Debug.LogWarning("[PlayerProjectile] PlayerBulletPool.Instance is null, destroying projectile");
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Get the ammo type this projectile was fired from.
    /// Used by pool to route projectile to correct queue.
    /// </summary>
    public ItemData GetAmmoType()
    {
        return sourceAmmoType;
    }

    /// <summary>
    /// Reset projectile state when returning to pool.
    /// Called by PlayerBulletPool.
    /// </summary>
    public void ResetState()
    {
        damage = 0f;
        sourceAmmoType = null;
        activeTime = 0f;
        isActive = false;

        if (rb != null)
        {
            if (rb.isKinematic == false)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }

        gameObject.SetActive(false);
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerProjectile] {message}");
        }
    }

    private void OnDisable()
    {
        // Safety cleanup when disabled
        isActive = false;
        if (rb != null)
        {
            if (rb.isKinematic == false)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}