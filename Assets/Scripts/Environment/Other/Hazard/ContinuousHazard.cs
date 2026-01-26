using UnityEngine;

/// <summary>
/// Hazard that deals continuous damage over time while the player is touching it.
/// Perfect for fire, acid pools, poison gas, steam vents, etc.
/// Uses a centralized manager for efficiency with many hazards in a level.
/// </summary>
public class ContinuousHazard : Hazard
{
    [Header("Continuous Damage Settings")]
    [Tooltip("How much damage to deal per second while player is in the hazard")]
    [SerializeField] private float damagePerSecond = 5f;
    public float DamagePerSecond => damagePerSecond;

    [Tooltip("How often to apply damage (in seconds). Lower = more frequent damage ticks")]
    [SerializeField] private float damageTickRate = 0.2f;
    public float DamageTickRate => damageTickRate;

    // Reference to the player's health component
    private PlayerHealth playerHealth;

    // Whether the player is currently in this hazard
    private bool playerInHazard = false;

    /// <summary>
    /// Overrides the base OnTriggerEnter to register with the manager
    /// instead of dealing instant damage.
    /// </summary>
    protected override void OnTriggerEnter(Collider other)
    {
        DebugLog($"{other.name} entered continuous hazard");

        PlayerHealth health = other.GetComponent<PlayerHealth>();

        if (health != null)
        {
            playerHealth = health;
            playerInHazard = true;

            // Register with the centralized manager
            if (HazardManager.Instance != null)
            {
                HazardManager.Instance.RegisterHazard(this);
                DebugLog($"Player entered - starting continuous damage ({damagePerSecond} damage/sec)");
            }
            else
            {
                Debug.LogError("[ContinuousHazard] ContinuousHazardManager not found in scene! Please add it to your scene.");
            }
        }
        else
        {
            DebugLog($"No PlayerHealth component found on {other.name}");
        }
    }

    /// <summary>
    /// Called when the player exits the hazard trigger.
    /// Unregisters from the manager to stop dealing damage.
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        PlayerHealth health = other.GetComponent<PlayerHealth>();

        if (health != null && health == playerHealth)
        {
            playerInHazard = false;

            // Unregister from the centralized manager
            if (HazardManager.Instance != null)
            {
                HazardManager.Instance.UnregisterHazard(this);
                DebugLog("Player exited - stopping continuous damage");
            }

            playerHealth = null;
        }
    }

    /// <summary>
    /// Called by the ContinuousHazardManager to deal damage.
    /// This is internal to prevent external calls - only the manager should call this.
    /// </summary>
    public void DealContinuousDamageInternal(float damageAmount)
    {
        if (playerHealth != null && playerInHazard)
        {
            DebugLog($"Dealing {damageAmount:F1} continuous damage");
            playerHealth.TakeDamage(damageAmount);

            if (damageAudioClips.Length > 0)
            {
                AudioClip randomClip = damageAudioClips[Random.Range(0, damageAudioClips.Length)];
                AudioManager.Instance.PlaySound2D(randomClip, AudioCategory.PlayerSFX);
            }

        }
    }

    /// <summary>
    /// Cleanup when the hazard is disabled or destroyed.
    /// Makes sure we unregister from the manager.
    /// </summary>
    private void OnDisable()
    {
        if (playerInHazard && HazardManager.Instance != null)
        {
            HazardManager.Instance.UnregisterHazard(this);
            playerInHazard = false;
            playerHealth = null;
        }
    }

    /// <summary>
    /// Optional: Visualize the hazard area in the editor.
    /// Shows in orange if player is taking damage, yellow if not.
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = playerInHazard ? new Color(1f, 0.5f, 0f, 0.3f) : new Color(1f, 1f, 0f, 0.3f);

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        }
    }
}