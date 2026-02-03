using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Detects collisions with the submarine and applies damage based on impact force.
/// Attach this to the submarine's collider or to the submarine root.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SubmarineDamageDetector : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the submarine's health manager")]
    [SerializeField] private SubmarineHealthManager healthManager;

    [Header("Collision Damage Settings")]
    [Tooltip("Minimum collision speed (in m/s) required to cause damage")]
    [SerializeField] private float minDamageSpeed = 3f;

    [Tooltip("Maximum collision speed we calculate damage for (speeds above this use this value)")]
    [SerializeField] private float maxDamageSpeed = 20f;

    [Tooltip("Damage multiplier - higher values mean collisions do more damage")]
    [SerializeField] private float damageMultiplier = 5f;

    [Tooltip("Maximum damage a single collision can do")]
    [SerializeField] private float maxDamagePerCollision = 50f;

    [Header("Collision Filtering")]
    [Tooltip("Optional: Only take damage from collisions with these layers")]
    [SerializeField] private LayerMask damageableLayers = ~0; // ~0 means all layers by default

    [Header("Cooldown")]
    [Tooltip("Minimum time (in seconds) between damage applications - prevents rapid repeated damage")]
    [SerializeField] private float damageCooldown = 0.5f;

    private float lastDamageTime = -999f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showCollisionGizmos = true;

    private Rigidbody submarineRigidbody;
    private Vector3 lastCollisionPoint;

    private void Awake()
    {
        // Get components
        submarineRigidbody = GetComponent<Rigidbody>();

        // Try to find health manager if not assigned
        if (healthManager == null)
        {
            healthManager = GetComponent<SubmarineHealthManager>();

            if (healthManager == null)
            {
                healthManager = GetComponentInChildren<SubmarineHealthManager>();
            }

            if (healthManager == null)
            {
                Debug.LogError("[SubmarineDamageDetector] Could not find SubmarineHealthManager! Please assign it in the Inspector.");
            }
        }
    }

    /// <summary>
    /// Called when this collider/rigidbody begins touching another collider/rigidbody
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        // Check if we should process this collision
        if (!ShouldProcessCollision(collision))
        {
            return;
        }

        // Calculate the impact force
        float impactSpeed = collision.relativeVelocity.magnitude;

        DebugLog($"Collision with {collision.gameObject.name}, speed: {impactSpeed:F2} m/s");

        // Check if impact is hard enough to cause damage
        if (impactSpeed < minDamageSpeed)
        {
            DebugLog("Impact too weak to cause damage");
            return;
        }

        // Check cooldown
        if (Time.time - lastDamageTime < damageCooldown)
        {
            DebugLog("Damage on cooldown");
            return;
        }

        // Calculate damage based on impact speed
        float damage = CalculateCollisionDamage(impactSpeed);

        // Get the collision point (use the first contact point)
        Vector3 collisionPoint = collision.contacts.Length > 0
            ? collision.contacts[0].point
            : collision.transform.position;

        lastCollisionPoint = collisionPoint;

        // Apply damage through the health manager
        if (healthManager != null)
        {
            healthManager.TakeDamageAtPoint(collisionPoint, damage, Vector3.zero);
            lastDamageTime = Time.time;

            DebugLog($"Applied {damage:F1} damage at {collisionPoint}");
        }
    }

    /// <summary>
    /// Calculate how much damage a collision should do based on impact speed
    /// </summary>
    private float CalculateCollisionDamage(float impactSpeed)
    {
        // Clamp speed to our max damage speed
        float clampedSpeed = Mathf.Min(impactSpeed, maxDamageSpeed);

        // Normalize speed to 0-1 range (0 = minDamageSpeed, 1 = maxDamageSpeed)
        float speedRange = maxDamageSpeed - minDamageSpeed;
        float normalizedSpeed = (clampedSpeed - minDamageSpeed) / speedRange;

        // Calculate damage (using a curve makes low-speed impacts less punishing)
        // You can adjust the power here: 
        // - Power of 1 = linear damage
        // - Power of 2 = quadratic (gentle collisions do much less damage)
        float damagePercent = Mathf.Pow(normalizedSpeed, 1.5f);

        float damage = damagePercent * damageMultiplier * maxDamageSpeed;

        // Clamp to max damage per collision
        damage = Mathf.Min(damage, maxDamagePerCollision);

        return damage;
    }

    /// <summary>
    /// Check if we should process this collision for damage
    /// </summary>
    private bool ShouldProcessCollision(Collision collision)
    {
        // Check if the object is on a damageable layer
        int objectLayer = collision.gameObject.layer;
        if ((damageableLayers.value & (1 << objectLayer)) == 0)
        {
            DebugLog($"Ignoring collision with {collision.gameObject.name} (wrong layer)");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Public method that can be called by enemy scripts to apply damage
    /// </summary>
    /// <param name="attackPosition">Where the attack hit in world space</param>
    /// <param name="damageAmount">How much damage the attack does</param>
    public void TakeDamageFromAttack(Vector3 attackPosition, float damageAmount, Vector3 attackDirection, float attackForce)
    {
        if (healthManager != null)
        {
            healthManager.TakeDamageAtPoint(attackPosition, damageAmount, attackDirection, attackForce);
            DebugLog($"Took {damageAmount} damage from attack at {attackPosition}");
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[SubmarineDamageDetector] {message}");
        }
    }

    // Draw debug visualization in Scene view
    private void OnDrawGizmos()
    {
        if (!showCollisionGizmos) return;

        // Draw a sphere at the last collision point
        if (lastCollisionPoint != Vector3.zero)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(lastCollisionPoint, 0.5f);
        }

        // Draw the damage range visualization
        if (submarineRigidbody != null)
        {
            // Draw minimum damage speed as yellow
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, minDamageSpeed * 0.1f);

            // Draw maximum damage speed as red
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, maxDamageSpeed * 0.1f);
        }
    }
}