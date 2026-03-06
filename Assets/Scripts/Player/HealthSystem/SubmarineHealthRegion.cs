using System;
using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections;
using DistantLands.Cozy;

/// <summary>
/// Represents a single damageable region of the submarine (front, back, left, right, or bottom).
/// Each region has its own health pool and can be damaged/healed independently.
/// Now includes automatic crack visual feedback through CrackVisualController.
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
    [SerializeField] private float lowHealthThreshold = 30f; // Health threshold to consider as "low"

    [Header("Visual Feedback")]
    [Tooltip("Optional: GameObject to activate when this region is destroyed (like sparks or cracks)")]
    [SerializeField] private GameObject destroyedEffectPrefab;

    [Tooltip("Optional: Transform where the destroyed effect should spawn")]
    [SerializeField] private Transform effectSpawnPoint;

    [Tooltip("Optional: Reference to the crack visual controller for this region")]
    [SerializeField] private CrackVisualController crackVisualController;

    [Tooltip("Should crack visuals update automatically when health changes?")]
    [SerializeField] private bool autoUpdateCrackVisuals = true;

    [Header("Audio")]
    [SerializeField] private AudioClip[] glassCrackSounds;

    [Header("Repair")]
    public Outline repairOutline; // Outline to show when this region is selected for repair

    [Header("Particle Effects")]
    [Tooltip("Optional: Particle effect to play when this region is repaired")]
    [SerializeField] private ParticleSystem repairParticleEffect;
    [SerializeField] private ParticleSystem lowHealthParticleEffect;
    [SerializeField] private float lowHealthParticleEffectPlayChance = 0.6f; // Chance to play low health effect each second when health is low
    [SerializeField] private float lowHealthEffectPlayInterval = 1f; // How often to try to play the low health effect when health is below the threshold

    [Header("Audio")]
    [Tooltip("Optional: Audio to play when this region is repaired")]
    [SerializeField] private AudioClip repairAudioClip;
    [SerializeField] private AudioClip[] sparkSounds;

    [Header("Warning Light")]
    [Tooltip("Optional: Reference to the flashing warning light for this region")]
    [SerializeField] private FlashingWarningLight warningLight; // Add this line


    // Events that other systems can listen to
    public event Action<SubmarineHealthRegion, float> OnDamageTaken;  // Fires when damage is taken
    public event Action<SubmarineHealthRegion> OnRegionDestroyed;     // Fires when health reaches zero
    public event Action<SubmarineHealthRegion, float> OnHealthRestored; // Fires when healed

    public event Action<SubmarineHealthRegion> OnEnteredLowHealth;   // Fires when health drops into low health range
    public event Action<SubmarineHealthRegion> OnExitedLowHealth;    // Fires when health recovers above low health threshold

    // Properties for easy access
    public string RegionName => regionName;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDestroyed => currentHealth <= 0f;
    public float HealthPercentage => maxHealth > 0 ? (currentHealth / maxHealth) : 0f;

    private GameObject activeDestroyedEffect;

    [SerializeField] private bool enableDebugLogs = false;

    private bool lowHealthEffectCoroutineRunning = false;
    private Coroutine lowHealthEffectCoroutine;

    private void Awake()
    {
        // Start with full health
        currentHealth = maxHealth;

        // Try to find crack visual controller if not assigned
        if (crackVisualController == null && autoUpdateCrackVisuals)
        {
            crackVisualController = GetComponentInChildren<CrackVisualController>();

            if (crackVisualController != null)
            {
                DebugLog($"Auto-found CrackVisualController in children");
            }
        }

        // Try to find warning light if not assigned
        if (warningLight == null)
        {
            warningLight = GetComponentInChildren<FlashingWarningLight>();

            if (warningLight != null)
            {
                DebugLog($"Auto-found FlashingWarningLight in children");
            }
        }

        if (repairOutline == null)
        {
            repairOutline = GetComponentInChildren<Outline>();
        }

        repairOutline.OutlineWidth = 0f;

        // Initialize crack visuals to show full health (no cracks)
        UpdateCrackVisuals();
    }

    /// <summary>
    /// Apply damage to this region
    /// </summary>
    /// <param name="damageAmount">How much damage to apply</param>
    [Button]
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

        // Update crack visuals to reflect new health
        UpdateCrackVisuals();

        if (glassCrackSounds != null && glassCrackSounds.Length > 0)
        {
            // Play random crack sound
            int randomIndex = UnityEngine.Random.Range(0, glassCrackSounds.Length);
            AudioManager.Instance.PlaySound(glassCrackSounds[randomIndex], transform.position, AudioCategory.PlayerSFX, layer: AudioLayer.Interior);
        }

        if (currentHealth <= lowHealthThreshold && previousHealth > lowHealthThreshold)
        {
            OnEnteredLowHealth?.Invoke(this); // NEW: fire event on first crossing

            if (lowHealthEffectCoroutineRunning == false)
                lowHealthEffectCoroutine = StartCoroutine(LowHealthEffectCoroutine());

            if (warningLight != null && !warningLight.IsFlashing)
            {
                warningLight.StartFlashing();
                DebugLog($"[{regionName}] Warning light started - low health!");
            }
        }

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
    [Button]
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

            // Update crack visuals to reflect healing
            UpdateCrackVisuals();

            // Notify listeners
            OnHealthRestored?.Invoke(this, actualHealed);

            if (repairParticleEffect != null)
            {
                repairParticleEffect.Play();
            }

            if (currentHealth > lowHealthThreshold)
            {
                lowHealthEffectCoroutineRunning = false;

                OnExitedLowHealth?.Invoke(this); // fire event when recovering from low health

                // Stop the specific coroutine instance if it exists
                if (lowHealthEffectCoroutine != null)
                {
                    StopCoroutine(lowHealthEffectCoroutine);
                    lowHealthEffectCoroutine = null;
                }

                // Stop warning light if available
                if (warningLight != null)
                {
                    warningLight.StopFlashing();
                    DebugLog($"[{regionName}] Warning light stopped - health recovered!");
                }

                // If we were destroyed and now have health again, remove destroyed effects
                if (previousHealth <= 0f && currentHealth > 0f)
                {
                    HandleRegionRepaired();
                }
            }

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

        // Update crack visuals
        UpdateCrackVisuals();

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
    /// Update the crack visual controller to match current health
    /// </summary>
    private void UpdateCrackVisuals()
    {
        if (autoUpdateCrackVisuals && crackVisualController != null)
        {
            crackVisualController.UpdateCrackVisibility(HealthPercentage);
        }
    }

    /// <summary>
    /// Manually set a reference to the crack visual controller
    /// Useful if you want to set it up at runtime
    /// </summary>
    public void SetCrackVisualController(CrackVisualController controller)
    {
        crackVisualController = controller;
        UpdateCrackVisuals();
        DebugLog($"Crack visual controller assigned");
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

    #region Low Health Effect Coroutine

    private IEnumerator LowHealthEffectCoroutine()
    {
        lowHealthEffectCoroutineRunning = true;

        while (lowHealthEffectCoroutineRunning) // Changed from 'while (true)'
        {
            float randomChance = UnityEngine.Random.Range(0f, 1f);

            if (randomChance < lowHealthParticleEffectPlayChance)
            {
                PlayLowHealthEffect();
            }

            yield return new WaitForSeconds(lowHealthEffectPlayInterval);
        }

        // Clean up when the loop exits
        lowHealthEffectCoroutine = null;
    }

    private void PlayLowHealthEffect()
    {
        if (lowHealthParticleEffect != null)
        {
            lowHealthParticleEffect.Play();

            if (sparkSounds != null && sparkSounds.Length > 0)
            {
                AudioManager.Instance.PlaySound(sparkSounds[UnityEngine.Random.Range(0, sparkSounds.Length)], transform.position, AudioCategory.PlayerSFX, layer: AudioLayer.Interior);
            }
        }
    }

    #endregion

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

    [Button("Test Crack Visual Update"), PropertyOrder(103)]
    private void TestCrackVisualUpdate()
    {
        UpdateCrackVisuals();
        DebugLog($"Manually updated crack visuals - Health: {HealthPercentage:P0}");
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