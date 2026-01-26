using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Individual throwable behavior for player-thrown throwables.
/// Handles damage application, collision detection, timeout, and pool return.
/// Attach to throwable prefabs referenced in throwableData.
/// </summary>
public class PlayerThrowable : MonoBehaviour
{
    [Header("throwable Configuration")]
    [SerializeField] protected float timeout = 10f;

    [SerializeField]
    [Tooltip("Delay in seconds before the throwable activates its effect (e.g., 5 seconds for grenades)")]
    protected float activationDelay = 0f;

    [Header("Debug")]
    [SerializeField] protected bool enableDebugLogs = false;

    // Runtime state
    protected float damage;
    protected ItemData sourcethrowableType;
    protected float activeTime;
    protected bool isActive;
    protected bool isEffectActive; // Tracks if the delay has passed and effect should be active
    private float delayTimer;

    // protected virtual void Awake()
    // {
    //     // Ensure collider is trigger
    //     Collider damageCol = GetComponentInChildren<Collider>();
    //     if (damageCol != null)
    //     {
    //         damageCol.isTrigger = true;
    //     }
    //     else
    //     {
    //         Debug.LogError($"[Playerthrowable] {gameObject.name} has no damage Collider component!");
    //     }
    // }


    /// <summary>
    /// Initialize throwable when spawned from pool.
    /// Called by PlayerThrowablePool AFTER position/rotation are set.
    /// </summary>
    public virtual void Initialize(float throwableDamage, ItemData throwableType, Vector3 position, Quaternion rotation)
    {
        damage = throwableDamage;
        sourcethrowableType = throwableType;
        activeTime = 0f;
        delayTimer = 0f;
        isActive = true;
        isEffectActive = false; // Effect starts inactive until delay passes

        if (throwableType.ThrowableData != null)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound2D(throwableType.ThrowableData.throwSound, AudioCategory.PlayerSFX);
        }

        // GameObject should already be positioned by pool
        // Just activate it
        gameObject.SetActive(true);

        // Verify position matches what was set
        if (Vector3.Distance(transform.position, position) > 0.01f)
        {
            Debug.LogWarning($"[Playerthrowable] Position mismatch! Expected {position}, got {transform.position}. Forcing correction.");
            transform.position = position;
            transform.rotation = rotation;
        }

        DebugLog($"Initialized throwable - Position: {transform.position}, Rotation: {transform.rotation.eulerAngles}, Activation Delay: {activationDelay}s");
    }

    protected virtual void Update()
    {
        if (!isActive) return;

        // Track active time
        activeTime += Time.deltaTime;

        // Handle activation delay
        if (!isEffectActive)
        {
            delayTimer += Time.deltaTime;
            if (delayTimer >= activationDelay)
            {
                isEffectActive = true;
                OnEffectActivated();
                DebugLog($"Effect activated after {delayTimer:F2}s delay");
            }
        }

        // Check timeout
        if (activeTime >= timeout)
        {
            DebugLog($"throwable timed out after {activeTime:F2}s");
            ReturnToPool();
        }
    }

    /// <summary>
    /// Called when the activation delay has passed and the throwable's effect should start.
    /// Override this in derived classes to implement specific activation behavior.
    /// </summary>
    protected virtual void OnEffectActivated()
    {
        // Base implementation does nothing
        // Derived classes can override to spawn particles, enable colliders, etc.
    }

    /// <summary>
    /// Return this throwable to the pool for reuse.
    /// </summary>
    protected virtual void ReturnToPool()
    {
        if (!isActive) return;

        isActive = false;

        // Return to pool
        if (PlayerThrowablePool.Instance != null)
        {
            PlayerThrowablePool.Instance.ReturnThrowable(this);
        }
        else
        {
            Debug.LogWarning("[Playerthrowable] PlayerThrowablePool.Instance is null, destroying throwable");
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Get the throwable type this throwable was fired from.
    /// Used by pool to route throwable to correct queue.
    /// </summary>
    public virtual ItemData GetThrowableType()
    {
        return sourcethrowableType;
    }

    /// <summary>
    /// Reset throwable state when returning to pool.
    /// Called by PlayerThrowablePool.
    /// </summary>
    public virtual void ResetState()
    {
        damage = 0f;
        sourcethrowableType = null;
        activeTime = 0f;
        delayTimer = 0f;
        isActive = false;
        isEffectActive = false;

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Check if the throwable's effect is currently active (delay has passed).
    /// </summary>
    public bool IsEffectActive()
    {
        return isEffectActive;
    }

    protected virtual void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[Playerthrowable] {message}");
        }
    }

    protected virtual void OnDisable()
    {

    }
}