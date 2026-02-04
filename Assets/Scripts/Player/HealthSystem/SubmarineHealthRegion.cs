using System;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Represents a single damageable region of the submarine (front, back, left, right, or bottom).
/// Each region has its own health pool and can be damaged/healed independently.
/// </summary>
public class SubmarineHealthRegion : MonoBehaviour
{
    [Header("Region Identification")]
    [Tooltip("The name of this region (e.g., 'Front', 'Back', 'Left', 'Right', 'Bottom')")]
    [SerializeField] private string regionName = "Unknown";

    [Header("Health Settings")]
    [Tooltip("Maximum health this region can have")]
    [SerializeField] private float maxHealth = 100f;

    [Tooltip("Current health of this region")]
    [ShowInInspector, ReadOnly]
    private float currentHealth;

    [Header("Visual Feedback")]
    [Tooltip("Optional: GameObject to activate when this region is destroyed (like sparks or cracks)")]
    [SerializeField] private GameObject destroyedEffectPrefab;

    [Tooltip("Optional: Transform where the destroyed effect should spawn")]
    [SerializeField] private Transform effectSpawnPoint;

    // Events that other systems can listen to
    public event Action<SubmarineHealthRegion, float> OnDamageTaken;  // Fires when damage is taken
    public event Action<SubmarineHealthRegion> OnRegionDestroyed;     // Fires when health reaches zero
    public event Action<SubmarineHealthRegion, float> OnHealthRestored; // Fires when healed

    // Properties for easy access
    public string RegionName => regionName;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDestroyed => currentHealth <= 0f;
    public float HealthPercentage => maxHealth > 0 ? (currentHealth / maxHealth) : 0f;

    private GameObject activeDestroyedEffect;

    [SerializeField] private bool enableDebugLogs = false;

    private void Awake()
    {
        // Start with full health
        currentHealth = maxHealth;
    }

    /// <summary>
    /// Apply damage to this region
    /// </summary>
    /// <param name="damageAmount">How much damage to apply</param>
    public void TakeDamage(float damageAmount)
    {
        // Can't damage a region that's already destroyed
        if (IsDestroyed) return;

        // Make sure damage is positive
        damageAmount = Mathf.Abs(damageAmount);

        // Calculate new health
        float previousHealth = currentHealth;
        currentHealth = Mathf.Max(0f, currentHealth - damageAmount);

        DebugLog($"[{regionName}] Took {damageAmount} damage. Health: {currentHealth}/{maxHealth}");

        // Notify listeners that damage was taken
        OnDamageTaken?.Invoke(this, damageAmount);

        // Check if this region was just destroyed
        if (currentHealth <= 0f && previousHealth > 0f)
        {
            HandleRegionDestroyed();
        }
    }

    /// <summary>
    /// Restore health to this region
    /// </summary>
    /// <param name="healAmount">How much health to restore</param>
    public void RestoreHealth(float healAmount)
    {
        // Make sure heal amount is positive
        healAmount = Mathf.Abs(healAmount);

        // Calculate new health (can't exceed max)
        float previousHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + healAmount);

        float actualHealed = currentHealth - previousHealth;

        if (actualHealed > 0)
        {
            DebugLog($"[{regionName}] Restored {actualHealed} health. Health: {currentHealth}/{maxHealth}");

            // Notify listeners
            OnHealthRestored?.Invoke(this, actualHealed);

            // If we were destroyed and now have health again, remove destroyed effects
            if (previousHealth <= 0f && currentHealth > 0f)
            {
                HandleRegionRepaired();
            }
        }
    }

    /// <summary>
    /// Restore this region to full health
    /// </summary>
    public void FullyRepair()
    {
        RestoreHealth(maxHealth);
    }

    /// <summary>
    /// Set health to a specific value (useful for testing or special mechanics)
    /// </summary>
    public void SetHealth(float newHealth)
    {
        float previousHealth = currentHealth;
        currentHealth = Mathf.Clamp(newHealth, 0f, maxHealth);

        // Check if we crossed the destroyed threshold
        if (currentHealth <= 0f && previousHealth > 0f)
        {
            HandleRegionDestroyed();
        }
        else if (currentHealth > 0f && previousHealth <= 0f)
        {
            HandleRegionRepaired();
        }
    }

    /// <summary>
    /// Called when this region's health reaches zero
    /// </summary>
    private void HandleRegionDestroyed()
    {
        Debug.LogWarning($"[{regionName}] Region destroyed!");

        // Spawn visual effect if we have one
        if (destroyedEffectPrefab != null)
        {
            Transform spawnTransform = effectSpawnPoint != null ? effectSpawnPoint : transform;
            activeDestroyedEffect = Instantiate(destroyedEffectPrefab, spawnTransform.position, spawnTransform.rotation, transform);
        }

        // Notify listeners
        OnRegionDestroyed?.Invoke(this);
    }

    /// <summary>
    /// Called when a destroyed region is repaired back above 0 health
    /// </summary>
    private void HandleRegionRepaired()
    {
        DebugLog($"[{regionName}] Region repaired!");

        // Remove destroyed effect if it exists
        if (activeDestroyedEffect != null)
        {
            Destroy(activeDestroyedEffect);
            activeDestroyedEffect = null;
        }
    }

    // Optional: Draw a gizmo in the editor to show where this region is
    private void OnDrawGizmos()
    {
        // Draw a colored sphere at this region's position
        Gizmos.color = IsDestroyed ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }

#if UNITY_EDITOR
    // Helper buttons in the Inspector for testing (only visible in Unity Editor)
    [Button("Test Damage (25)"), PropertyOrder(100)]
    private void TestDamage()
    {
        TakeDamage(25f);
    }

    [Button("Test Heal (25)"), PropertyOrder(101)]
    private void TestHeal()
    {
        RestoreHealth(25f);
    }

    [Button("Fully Repair"), PropertyOrder(102)]
    private void TestFullRepair()
    {
        FullyRepair();
    }
#endif

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log(message);
        }
    }
}