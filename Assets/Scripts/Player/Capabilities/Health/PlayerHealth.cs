using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Handles all player health functionality including current health tracking,
/// health modification, regeneration, and death management.
/// This component is separate from PlayerManager to provide better organization
/// and modularity for the health system.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Current Health")]
    [ShowInInspector, ReadOnly]
    public float currentHealth;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // References
    private PlayerData playerData;
    private bool isDead = false;
    private bool isInitialized = false;

    // Public Properties
    [ShowInInspector, ReadOnly]
    public bool IsDead => isDead;
    public float MaxHealth => playerData?.maxHealth ?? 100f;
    public float HealthPercentage => MaxHealth > 0 ? currentHealth / MaxHealth : 0f;
    public bool IsInitialized => isInitialized;

    // Events (using existing GameEvents system)
    // OnPlayerHealthChanged and OnPlayerDeath are already defined in GameEvents



    private void Awake()
    {
        // Health will be initialized by PlayerManager or save system
        DebugLog("PlayerHealth component created");
    }

    private void Start()
    {
        // Ensure we have player data reference
        RefreshReferences();

        // If not initialized yet, set to max health
        if (!isInitialized)
        {
            InitializeWithMaxHealth();
        }
    }

    /// <summary>
    /// Refreshes references to PlayerData and other dependencies
    /// </summary>
    public void RefreshReferences()
    {
        if (playerData == null && GameManager.Instance != null)
        {
            playerData = GameManager.Instance.playerData;
            DebugLog("PlayerData reference refreshed");
        }

        if (GameManager.Instance.uiManager.vignetteManager == null)
        {
            GameManager.Instance.uiManager.vignetteManager = FindFirstObjectByType<PlayerVignetteManager>();
            DebugLog("VignetteManager reference refreshed");
        }
    }

    /// <summary>
    /// Initializes health with default max health value
    /// </summary>
    public void InitializeWithMaxHealth()
    {
        RefreshReferences();

        if (playerData != null)
        {
            currentHealth = playerData.maxHealth;
            isInitialized = true;
            DebugLog($"Health initialized with max health: {currentHealth}");

            // Trigger UI update
            GameEvents.TriggerPlayerHealthChanged(currentHealth, MaxHealth);
        }
        else
        {
            // Fallback if PlayerData not available
            currentHealth = 100f;
            isInitialized = true;
            DebugLog("Health initialized with fallback value: 100");
        }
    }

    /// <summary>
    /// Initializes health with a specific value (used by save system)
    /// </summary>
    public void InitializeWithHealth(float health)
    {
        RefreshReferences();
        currentHealth = Mathf.Clamp(health, 0, MaxHealth);
        isInitialized = true;
        DebugLog($"Health initialized with specific value: {currentHealth}");

        // Trigger UI update
        GameEvents.TriggerPlayerHealthChanged(currentHealth, MaxHealth);
    }

    /// <summary>
    /// Main method for modifying player health (damage or healing)
    /// </summary>
    [Button("Modify Health")]
    public void ModifyHealth(float amount)
    {
        if (isDead || !isInitialized)
        {
            DebugLog($"Cannot modify health - Dead: {isDead}, Initialized: {isInitialized}");
            return;
        }

        float previousHealth = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, MaxHealth);

        DebugLog($"Health modified by {amount}: {previousHealth} -> {currentHealth}");

        // Trigger health changed event
        GameEvents.TriggerPlayerHealthChanged(currentHealth, MaxHealth);

        // Check for death
        if (currentHealth <= 0 && !isDead)
        {
            HandleDeath();
        }
    }

    /// <summary>
    /// Applies damage to the player
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (damage <= 0) return;

        DebugLog($"Taking damage: {damage}");

        GameManager.Instance.uiManager.vignetteManager.TriggerDamageVignette();

        if ((currentHealth / MaxHealth) < GameManager.Instance.uiManager.vignetteManager.lowHealthDamageVignetteActiveThreshold)
        {
            GameManager.Instance.uiManager.vignetteManager.SetDamageVignetteActive(true);
        }

        ModifyHealth(-damage);

    }

    /// <summary>
    /// Heals the player
    /// </summary>
    public void Heal(float healAmount)
    {
        if (healAmount <= 0) return;

        DebugLog($"Healing: {healAmount}");
        ModifyHealth(healAmount);

        GameManager.Instance.uiManager.vignetteManager.TriggerHealVignette();

        if ((currentHealth / MaxHealth) > GameManager.Instance.uiManager.vignetteManager.lowHealthDamageVignetteActiveThreshold)
        {
            GameManager.Instance.uiManager.vignetteManager.SetDamageVignetteActive(false);
        }

    }

    /// <summary>
    /// Sets health to a specific value (useful for save/load or special effects)
    /// </summary>
    public void SetHealth(float newHealth)
    {
        if (!isInitialized)
        {
            InitializeWithHealth(newHealth);
            return;
        }

        float previousHealth = currentHealth;
        currentHealth = Mathf.Clamp(newHealth, 0, MaxHealth);

        DebugLog($"Health set from {previousHealth} to {currentHealth}");

        // Trigger health changed event
        GameEvents.TriggerPlayerHealthChanged(currentHealth, MaxHealth);

        // Check for death or revival
        if (currentHealth <= 0 && !isDead)
        {
            HandleDeath();
        }
        else if (currentHealth > 0 && isDead)
        {
            HandleRevival();
        }
    }

    /// <summary>
    /// Handles player death
    /// </summary>
    private void HandleDeath()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log("Player has died");

        // Trigger death event
        GameEvents.TriggerPlayerDeath();
    }

    /// <summary>
    /// Handles player revival (sets alive state)
    /// </summary>
    private void HandleRevival()
    {
        if (!isDead) return;

        isDead = false;
        DebugLog("Player has been revived");

        // Trigger health update to refresh UI
        GameEvents.TriggerPlayerHealthChanged(currentHealth, MaxHealth);
    }

    /// <summary>
    /// Respawns the player with full health
    /// </summary>
    public void Respawn()
    {
        currentHealth = MaxHealth;
        isDead = false;
        isInitialized = true;

        DebugLog("Player respawned with full health");

        // Trigger health update
        GameEvents.TriggerPlayerHealthChanged(currentHealth, MaxHealth);
    }

    /// <summary>
    /// Handles health regeneration if enabled in PlayerData
    /// </summary>
    private void Update()
    {
        if (isDead || !isInitialized || playerData == null)
            return;

        // Health regeneration
        if (playerData.healthRegenRate > 0 && currentHealth < MaxHealth)
        {
            float regenAmount = playerData.healthRegenRate * Time.deltaTime;
            ModifyHealth(regenAmount);
        }
    }

    /// <summary>
    /// Applies effects from consumable items
    /// </summary>
    public void ApplyConsumableEffects(ConsumableData consumable)
    {
        if (consumable == null || isDead) return;

        DebugLog($"Applying consumable effects from: {consumable}");

        // This would be expanded based on your ConsumableData structure
        // Example: if (consumable.healAmount > 0) Heal(consumable.healAmount);
    }

    /// <summary>
    /// Gets current health data for saving/UI display
    /// </summary>
    public PlayerHealthData GetHealthData()
    {
        return new PlayerHealthData
        {
            currentHealth = currentHealth,
            isDead = isDead,
            isInitialized = isInitialized
        };
    }

    /// <summary>
    /// Restores health data from save system
    /// </summary>
    public void RestoreHealthData(PlayerHealthData healthData)
    {
        if (healthData == null)
        {
            DebugLog("No health data provided for restoration");
            return;
        }

        currentHealth = healthData.currentHealth;
        isDead = healthData.isDead;
        isInitialized = healthData.isInitialized;

        DebugLog($"Health data restored - Health: {currentHealth}, Dead: {isDead}, Initialized: {isInitialized}");

        // Ensure health is within valid bounds
        currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);

        // Trigger UI update
        GameEvents.TriggerPlayerHealthChanged(currentHealth, MaxHealth);

        if ((currentHealth / MaxHealth) < GameManager.Instance.uiManager.vignetteManager.lowHealthDamageVignetteActiveThreshold)
        {
            GameManager.Instance.uiManager.vignetteManager.SetDamageVignetteActive(true);
        }
        else
        {
            GameManager.Instance.uiManager.vignetteManager.SetDamageVignetteActive(false);
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerHealth] {message}");
        }
    }

    #region Validation

    private void OnValidate()
    {
        // Ensure current health stays within bounds in editor
        if (Application.isPlaying && isInitialized)
        {
            currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);
        }
    }

    #endregion
}

/// <summary>
/// Data structure for saving/loading player health information
/// </summary>
[System.Serializable]
public class PlayerHealthData
{
    public float currentHealth;
    public bool isDead;
    public bool isInitialized;

    public PlayerHealthData()
    {
        currentHealth = 100f;
        isDead = false;
        isInitialized = false;
    }
}