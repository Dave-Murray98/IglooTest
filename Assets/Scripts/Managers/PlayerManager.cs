using System;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Complete PlayerManager that coordinates health, stamina, and oxygen capabilities.
/// Provides central access point for other systems to interact with all player subsystems.
/// </summary>
public class PlayerManager : MonoBehaviour, IManager
{
    [Header("Capabilities")]
    public PlayerHealth health;
    public PlayerStamina stamina;
    public PlayerOxygen oxygen;
    private PlayerData playerData;

    public PlayerController controller;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    public void Initialize()
    {
        DebugLog("PlayerManager Initialized");
        RefreshReferences();
    }

    public void RefreshReferences()
    {
        DebugLog("PlayerManager: Refreshing references");
        playerData = GameManager.Instance?.playerData;

        UnsubscribeFromEvents();
        SubscribeToEvents();

        FindCapabilities();
        InitializeCapabilities();
    }

    /// <summary>
    /// Finds all player capability components
    /// </summary>
    private void FindCapabilities()
    {
        // Find PlayerHealth component
        if (health == null)
        {
            health = FindFirstObjectByType<PlayerHealth>();
        }

        // Find PlayerStamina component
        if (stamina == null)
        {
            stamina = FindFirstObjectByType<PlayerStamina>();
        }

        // Find PlayerOxygen component
        if (oxygen == null)
        {
            oxygen = FindFirstObjectByType<PlayerOxygen>();
        }

        if (controller == null)
        {
            controller = FindFirstObjectByType<PlayerController>();
        }

        DebugLog($"Found capabilities - Health: {health != null}, Stamina: {stamina != null}, Oxygen: {oxygen != null}");
    }

    /// <summary>
    /// Initializes player capabilities with default values if needed
    /// </summary>
    private void InitializeCapabilities()
    {
        // Initialize health if found and not already initialized
        if (health != null && !health.IsInitialized)
        {
            if (playerData != null)
            {
                health.RefreshReferences();
                health.InitializeWithMaxHealth();
                DebugLog("Health capability initialized");
            }
        }

        // Initialize stamina if found and not already initialized
        if (stamina != null && !stamina.IsInitialized)
        {
            if (playerData != null)
            {
                stamina.RefreshReferences();
                stamina.InitializeWithMaxStamina();
                DebugLog("Stamina capability initialized");
            }
        }

        // Initialize oxygen if found and not already initialized
        if (oxygen != null && !oxygen.IsInitialized)
        {
            if (playerData != null)
            {
                oxygen.RefreshReferences();
                oxygen.InitializeWithMaxOxygen();
                DebugLog("Oxygen capability initialized");
            }
        }
    }

    /// <summary>
    /// Subscribe to relevant game events
    /// </summary>
    private void SubscribeToEvents()
    {
        DebugLog("PlayerManager: Subscribing to events");
        GameEvents.OnPlayerDeath += HandlePlayerDeath;
        GameEvents.OnPlayerStaminaDepleted += HandleStaminaDepleted;
        GameEvents.OnPlayerStaminaRecovered += HandleStaminaRecovered;
        GameEvents.OnPlayerOxygenDepleted += HandleOxygenDepleted;
    }

    /// <summary>
    /// Unsubscribe from game events
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        DebugLog("PlayerManager: Unsubscribing from events");
        GameEvents.OnPlayerDeath -= HandlePlayerDeath;
        GameEvents.OnPlayerStaminaDepleted -= HandleStaminaDepleted;
        GameEvents.OnPlayerStaminaRecovered -= HandleStaminaRecovered;
        GameEvents.OnPlayerOxygenDepleted -= HandleOxygenDepleted;
    }

    public void Cleanup()
    {
        DebugLog("PlayerManager: Cleaning up");
        UnsubscribeFromEvents();
    }

    #region Health Delegation Methods

    /// <summary>
    /// Modifies player health through the PlayerHealth component
    /// </summary>
    [Button("Modify Health")]
    public void ModifyHealth(float amount)
    {
        if (health != null)
        {
            health.ModifyHealth(amount);
        }
        else
        {
            Debug.LogError("PlayerHealth component not found! Cannot modify health.");
        }
    }

    /// <summary>
    /// Applies damage to the player
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (health != null)
        {
            health.TakeDamage(damage);
        }
        else
        {
            Debug.LogError("PlayerHealth component not found! Cannot apply damage.");
        }
    }

    /// <summary>
    /// Heals the player
    /// </summary>
    public void Heal(float healAmount)
    {
        if (health != null)
        {
            health.Heal(healAmount);
        }
        else
        {
            Debug.LogError("PlayerHealth component not found! Cannot heal player.");
        }
    }

    /// <summary>
    /// Respawns the player with full health, stamina, and oxygen
    /// </summary>
    public void Respawn()
    {
        if (health != null)
        {
            health.Respawn();
            DebugLog("Player respawned through PlayerHealth component");
        }
        else
        {
            Debug.LogError("PlayerHealth component not found! Cannot respawn player.");
        }

        // Also restore stamina and oxygen on respawn
        if (stamina != null)
        {
            stamina.RestoreStamina();
            DebugLog("Player stamina restored on respawn");
        }

        if (oxygen != null)
        {
            oxygen.RestoreOxygen();
            DebugLog("Player oxygen restored on respawn");
        }
    }

    #endregion

    #region Consumable Effects

    /// <summary>
    /// Applies effects from consumable items to appropriate player systems
    /// </summary>
    public void ApplyConsumableEffects(ConsumableData consumable)
    {
        if (consumable == null) return;

        DebugLog($"Applying consumable effects: {consumable}");

        // Delegate health effects to PlayerHealth component
        if (health != null)
        {
            health.ApplyConsumableEffects(consumable);
        }

        // Delegate stamina effects to PlayerStamina component
        if (stamina != null)
        {
            stamina.ApplyConsumableEffects(consumable);
        }

        // Delegate oxygen effects to PlayerOxygen component
        if (oxygen != null)
        {
            oxygen.ApplyConsumableEffects(consumable);
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles player death events
    /// </summary>
    private void HandlePlayerDeath()
    {
        Debug.Log("PlayerManager: Handling player death event");

        GameManager.Instance.uiManager.ShowPlayerDeathMenu();
    }

    /// <summary>
    /// Handles stamina depletion events
    /// </summary>
    private void HandleStaminaDepleted()
    {
        DebugLog("PlayerManager: Handling stamina depletion event");

        // Perform any manager-level stamina depletion handling
        // This could include disabling certain actions, showing UI warnings, etc.
    }

    /// <summary>
    /// Handles stamina recovery events
    /// </summary>
    private void HandleStaminaRecovered()
    {
        DebugLog("PlayerManager: Handling stamina recovery event");

        // Perform any manager-level stamina recovery handling
        // This could include re-enabling actions, hiding UI warnings, etc.
    }

    /// <summary>
    /// Handles oxygen depletion events
    /// </summary>
    private void HandleOxygenDepleted()
    {
        DebugLog("PlayerManager: Handling oxygen depletion event");

        // Perform any manager-level oxygen depletion handling
        // This could include:
        // - Applying health damage over time
        // - Disabling certain actions
        // - Triggering critical warnings
        // - Starting drowning effects
    }


    #endregion

    private void OnDestroy()
    {
        Cleanup();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PlayerManager] {message}");
    }
}