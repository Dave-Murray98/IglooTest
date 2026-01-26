using System;
using System.Net.NetworkInformation;
using Sirenix.OdinInspector;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

/// <summary>
/// Handles all player oxygen functionality including current oxygen tracking,
/// constant oxygen depletion, oxygen addition, and depletion management.
/// Provides a simple oxygen system with constant consumption rate.
/// </summary>
public class PlayerOxygen : MonoBehaviour
{
    [Header("Current Oxygen")]
    [ShowInInspector, ReadOnly]
    public float currentOxygen;

    [Header("Oxygen Settings")]
    [SerializeField] private float oxygenDepletionRate = 2f; // Oxygen consumed per second
    [SerializeField] private float oxygenAddAmount = 20f; // Amount added when AddOxygen() is called
    [SerializeField] private float oxygenDepletedDamageRate = 2f; // How often to damage the player when they're out of oxygen
    [SerializeField] private float oxygenDepletionDamage = 5f;
    private float oxygenDepletionTimer = 0f;

    [Header("Bubbles Particles")]
    [SerializeField] private ParticleSystem bubblesParticles;

    [SerializeField] private float triggerParticleTimer = 5f;
    private float particleTimer;

    [Header("Low Oxygen Warning")]
    [SerializeField] private float oxygenLowThreshold = 20f; // Threshold for low oxygen warning
    private bool isBelowThreshold = true; // Track current threshold state

    [Header("Audio")]
    [SerializeField] private AudioClip[] oxygenBreathingClips;
    [SerializeField] private AudioClip[] outOfOxygenSuffocateClips;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // References
    private PlayerData playerData;
    private bool isInitialized = false;
    private bool isDepleted = false;

    private OxygenTankManager tankManager;

    // Events for low oxygen warning system
    public event Action OnOxygenPassedLowThreshold;
    public event Action OnOxygenReturnedToNormal;

    // Public Properties
    public float MaxOxygen
    {
        get
        {
            // Max oxygen now comes from the equipped tank's capacity
            if (tankManager != null && tankManager.HasTankEquipped())
            {
                return tankManager.GetMaxCapacity();
            }
            return playerData?.maxOxygen ?? 100f; // Fallback
        }
    }

    public float CurrentOxygen
    {
        get
        {
            // Current oxygen comes from the equipped tank
            if (tankManager != null && tankManager.HasTankEquipped())
            {
                return tankManager.GetCurrentOxygen();
            }
            return 0f; // No tank = no oxygen
        }
    }

    public float OxygenPercentage
    {
        get
        {
            if (tankManager != null && tankManager.HasTankEquipped())
            {
                return tankManager.GetOxygenPercentage();
            }
            return 0f; // No tank = 0%
        }
    }

    public bool IsInitialized => isInitialized;

    public bool IsDepleted
    {
        get
        {
            // Player is depleted if no tank equipped or tank is empty
            if (tankManager == null || !tankManager.HasTankEquipped())
            {
                return true; // No tank = depleted
            }
            return tankManager.IsTankEmpty();
        }
    }

    public bool IsConsuming
    {
        get
        {
            return isInitialized &&
                   tankManager != null &&
                   tankManager.HasTankEquipped() &&
                   !tankManager.IsTankEmpty();
        }
    }


    private void Awake()
    {
        DebugLog("PlayerOxygen component created");
    }

    private void Start()
    {
        RefreshReferences();

        if (!isInitialized)
        {
            InitializeWithMaxOxygen();
        }

        tankManager.OnTankEquipped += OnTankEquipped;
    }

    private void OnTankEquipped(OxygenTankSlot tankSlot, InventoryItemData inventoryItemData)
    {
        TriggerBubbleParticlesOnNewTank(tankSlot, inventoryItemData);
        CheckThresholdState(CurrentOxygen);
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

        if (tankManager == null)
        {
            tankManager = OxygenTankManager.Instance;
            DebugLog("OxygenTankManager reference refreshed");
        }
    }

    /// <summary>
    /// Initializes oxygen with default max oxygen value
    /// </summary>
    public void InitializeWithMaxOxygen()
    {
        RefreshReferences();
        isInitialized = true;

        // With tank system, initialization just sets the initialized flag
        // Oxygen comes from equipped tank
        DebugLog("Oxygen system initialized - oxygen comes from equipped tank");

        // Check initial threshold state
        CheckThresholdState(CurrentOxygen);

        TriggerOxygenChangedEvent();
    }


    /// <summary>
    /// Initializes oxygen with a specific value (used by save system)
    /// </summary>
    public void InitializeWithOxygen(float oxygen)
    {
        RefreshReferences();
        isInitialized = true;

        // Note: With tank system, this oxygen value is ignored
        // Oxygen state is loaded through the OxygenTankManager save system
        DebugLog("Oxygen system initialized from save - oxygen managed by tank system");

        // Check initial threshold state
        CheckThresholdState(CurrentOxygen);

        TriggerOxygenChangedEvent();
    }

    /// <summary>
    /// Public method to add oxygen (called by external scripts like oxygen tanks, surface breathing, etc.)
    /// </summary>
    [Button("Add Oxygen")]
    public void AddOxygen()
    {
        AddOxygen(oxygenAddAmount);
    }

    /// <summary>
    /// Adds oxygen to the equipped tank (refilling).
    /// </summary>
    public void AddOxygen(float amount)
    {
        if (!isInitialized || amount <= 0) return;

        RefreshReferences();

        if (tankManager == null || !tankManager.HasTankEquipped())
        {
            DebugLog("Cannot add oxygen - no tank equipped");
            return;
        }

        float previousOxygen = tankManager.GetCurrentOxygen();
        bool added = tankManager.AddTankOxygen(amount);

        if (added)
        {
            float currentOxygen = tankManager.GetCurrentOxygen();
            DebugLog($"Oxygen added to tank: {amount} ({previousOxygen:F1} -> {currentOxygen:F1})");

            // Check threshold crossing after adding oxygen
            CheckThresholdState(currentOxygen);

            TriggerOxygenChangedEvent();

            // Check for recovery from depletion
            if (IsDepleted && currentOxygen > 0)
            {
                HandleOxygenRecovery();
            }
        }
    }

    /// <summary>
    /// Consumes oxygen from the equipped tank.
    /// </summary>
    public void ConsumeOxygen(float amount)
    {
        if (!isInitialized || amount <= 0) return;

        if (!GameManager.Instance.PlayerStateManager.playerController.waterDetector.IsHeadUnderwater)
        {
            return;
        }

        RefreshReferences();

        if (tankManager == null || !tankManager.HasTankEquipped())
        {
            // No tank equipped - player is depleted
            if (!isDepleted)
            {
                HandleOxygenDepletion();
            }
            return;
        }

        float previousOxygen = tankManager.GetCurrentOxygen();
        bool consumed = tankManager.ConsumeTankOxygen(amount);

        if (consumed)
        {
            float currentOxygen = tankManager.GetCurrentOxygen();
            DebugLog($"Oxygen consumed from tank: {amount} ({previousOxygen:F1} -> {currentOxygen:F1})");

            // Check threshold crossing after consuming oxygen
            CheckThresholdState(currentOxygen);

            TriggerOxygenChangedEvent();

            // Check for depletion
            if (currentOxygen <= 0 && !isDepleted)
            {
                HandleOxygenDepletion();
            }

            HandleBubblesParticles();
        }
        else
        {
            // Failed to consume (tank empty or missing)
            if (!isDepleted)
            {
                HandleOxygenDepletion();
            }
        }
    }

    /// <summary>
    /// Checks if oxygen has crossed the low threshold and fires appropriate events.
    /// This is the core of the event-driven threshold system.
    /// </summary>
    private void CheckThresholdState(float currentOxygenValue)
    {
        bool wasBelow = isBelowThreshold;
        bool isNowBelow = currentOxygenValue <= oxygenLowThreshold;

        // Only fire events when state changes
        if (isNowBelow != wasBelow)
        {
            isBelowThreshold = isNowBelow;

            if (isNowBelow)
            {
                // Just crossed below threshold
                DebugLog($"Oxygen crossed LOW threshold: {currentOxygenValue:F1} <= {oxygenLowThreshold}");
                isBelowThreshold = true;
                OnOxygenPassedLowThreshold?.Invoke();
            }
            else
            {
                // Just crossed above threshold (recovered)
                DebugLog($"Oxygen returned to NORMAL: {currentOxygenValue:F1} > {oxygenLowThreshold}");
                isBelowThreshold = false;
                OnOxygenReturnedToNormal?.Invoke();
            }
        }
    }

    private void HandleBubblesParticles()
    {
        particleTimer += Time.deltaTime;
        if (particleTimer >= triggerParticleTimer)
        {
            TriggerBubblesParticles();
            AudioClip randomClip = oxygenBreathingClips[UnityEngine.Random.Range(0, oxygenBreathingClips.Length)];
            AudioManager.Instance.PlaySound2D(randomClip, AudioCategory.PlayerSFX);
        }

    }

    private void TriggerBubblesParticles()
    {
        particleTimer = 0f;

        if (bubblesParticles != null)
        {
            bubblesParticles.Play();
        }
    }

    private void TriggerBubbleParticlesOnNewTank(OxygenTankSlot tankSlot, InventoryItemData inventoryItemData)
    {
        if (bubblesParticles != null)
        {
            bubblesParticles.Play();
        }

        // Check threshold state when new tank is equipped
        CheckThresholdState(CurrentOxygen);
    }

    /// <summary>
    /// Sets oxygen is not supported with tank system.
    /// Use AddOxygen to refill or equip a different tank.
    /// </summary>
    public void SetOxygen(float newOxygen)
    {
        DebugLog("SetOxygen called but oxygen is managed by equipped tank. Use AddOxygen() to refill tank.");

        // We can't directly set oxygen with the tank system
        // This method exists for API compatibility but doesn't do anything
    }

    /// <summary>
    /// Handles oxygen depletion
    /// </summary>
    private void HandleOxygenDepletion()
    {
        if (isDepleted) return;

        isDepleted = true;

        // When depleted, ensure threshold events fire
        CheckThresholdState(0f);

        DebugLog("Oxygen depleted!");

        // Use GameEvents system
        GameEvents.TriggerPlayerOxygenDepleted();
    }

    /// <summary>
    /// Handles oxygen recovery from depletion
    /// </summary>
    private void HandleOxygenRecovery()
    {
        if (!isDepleted) return;

        isDepleted = false;
        DebugLog("Oxygen recovered from depletion");

        // Check threshold when recovering
        CheckThresholdState(CurrentOxygen);

        // Use GameEvents system
        GameEvents.TriggerPlayerOxygenRecovered();
    }

    /// <summary>
    /// Restores oxygen - with tank system this just clears depletion state.
    /// To actually restore oxygen, equip a full tank or refill current tank.
    /// </summary>
    public void RestoreOxygen()
    {
        isDepleted = false;
        isInitialized = true;

        DebugLog("Oxygen depletion state cleared - equip tank for oxygen");

        // Check threshold state after restoration
        CheckThresholdState(CurrentOxygen);

        TriggerOxygenChangedEvent();
    }

    /// <summary>
    /// Main update loop handling constant oxygen consumption
    /// </summary>
    private void Update()
    {
        if (!isInitialized || playerData == null)
            return;

        RefreshReferences();

        // If no tank or tank is empty, player takes damage
        if (IsDepleted)
        {
            oxygenDepletionTimer += Time.deltaTime;

            if (oxygenDepletionTimer >= oxygenDepletedDamageRate)
            {
                GameManager.Instance.playerManager.health.TakeDamage(oxygenDepletionDamage);
                AudioClip randomClip = outOfOxygenSuffocateClips[UnityEngine.Random.Range(0, outOfOxygenSuffocateClips.Length)];
                AudioManager.Instance.PlaySound2D(randomClip, AudioCategory.PlayerSFX);
                oxygenDepletionTimer = 0f;
            }

            return;
        }

        oxygenDepletionTimer = 0f;

        float deltaTime = Time.deltaTime;

        // Consume oxygen from equipped tank
        if (oxygenDepletionRate > 0 && tankManager != null && tankManager.HasTankEquipped())
        {
            ConsumeOxygen(oxygenDepletionRate * deltaTime);
        }

        // Debug logging for oxygen state changes
        if (enableDebugLogs && Time.frameCount % 60 == 0) // Log every 60 frames to reduce spam
        {
            DebugLog($"Oxygen Update - Tank: {(tankManager?.HasTankEquipped() ?? false)}, " +
                    $"Current: {CurrentOxygen:F1}/{MaxOxygen:F1}, Depleting at {oxygenDepletionRate}/sec, " +
                    $"Below Threshold: {isBelowThreshold}");
        }
    }

    /// <summary>
    /// Applies effects from consumable items
    /// </summary>
    public void ApplyConsumableEffects(ConsumableData consumable)
    {
        if (consumable == null || !isInitialized) return;

        DebugLog($"Applying consumable effects from: {consumable}");

        // This would be expanded based on your ConsumableData structure
        // Example: if (consumable.oxygenBoost > 0) AddOxygen(consumable.oxygenBoost);
    }

    /// <summary>
    /// Gets current oxygen data for UI display.
    /// Note: For saving, use OxygenTankManager save system instead.
    /// </summary>
    public PlayerOxygenData GetOxygenData()
    {
        return new PlayerOxygenData
        {
            currentOxygen = CurrentOxygen, // Read from tank
            isDepleted = isDepleted,
            isInitialized = isInitialized,
        };
    }

    /// <summary>
    /// Restores oxygen data from save system.
    /// Note: With tank system, oxygen state comes from equipped tank.
    /// This mainly restores initialization and depletion flags.
    /// </summary>
    public void RestoreOxygenData(PlayerOxygenData oxygenData)
    {
        if (oxygenData == null)
        {
            DebugLog("No oxygen data provided for restoration");
            return;
        }

        // Restore flags but not oxygen value (comes from tank)
        isDepleted = oxygenData.isDepleted;
        isInitialized = oxygenData.isInitialized;

        DebugLog($"Oxygen data restored - Depleted: {isDepleted}, Initialized: {isInitialized}, Below Threshold: {isBelowThreshold}");
        DebugLog("Oxygen value managed by equipped tank through OxygenTankManager");

        RefreshReferences();

        // Check if we need to fire events based on restored state
        // This ensures UI is updated correctly after load
        if (isBelowThreshold && CurrentOxygen <= oxygenLowThreshold)
        {
            // We're below threshold - ensure UI knows
            OnOxygenPassedLowThreshold?.Invoke();
        }
        else if (!isBelowThreshold && CurrentOxygen > oxygenLowThreshold)
        {
            // We're above threshold - ensure UI knows
            OnOxygenReturnedToNormal?.Invoke();
        }

        TriggerOxygenChangedEvent();
    }


    /// <summary>
    /// Triggers oxygen changed event for UI updates using GameEvents
    /// </summary>
    private void TriggerOxygenChangedEvent()
    {
        float current = CurrentOxygen; // Read from tank
        float max = MaxOxygen; // Read from tank capacity
        GameEvents.TriggerPlayerOxygenChanged(current, max);
    }

    /// <summary>
    /// Temporarily stops oxygen consumption (useful for special areas, equipment, etc.)
    /// </summary>
    [Button("Pause Consumption")]
    public void PauseOxygenConsumption()
    {
        var tempRate = oxygenDepletionRate;
        oxygenDepletionRate = 0;
        DebugLog($"Oxygen consumption paused (was {tempRate}/sec)");
    }

    /// <summary>
    /// Resumes oxygen consumption at normal rate
    /// </summary>
    [Button("Resume Consumption")]
    public void ResumeOxygenConsumption()
    {
        if (playerData != null)
        {
            oxygenDepletionRate = playerData.oxygenDepletionRate;
        }
        else
        {
            oxygenDepletionRate = 2f; // Fallback rate
        }
        DebugLog($"Oxygen consumption resumed at {oxygenDepletionRate}/sec");
    }

    /// <summary>
    /// Check if player has a tank equipped.
    /// </summary>
    public bool HasTankEquipped()
    {
        RefreshReferences();
        return tankManager != null && tankManager.HasTankEquipped();
    }

    /// <summary>
    /// Sets a custom oxygen depletion rate
    /// </summary>
    public void SetOxygenDepletionRate(float rate)
    {
        oxygenDepletionRate = Mathf.Max(0, rate);
        DebugLog($"Oxygen depletion rate set to: {oxygenDepletionRate}/sec");
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        tankManager.OnTankEquipped -= OnTankEquipped;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerOxygen] {message}");
        }
    }

    #region Validation

    private void OnValidate()
    {
        // Ensure current oxygen stays within bounds in editor
        if (Application.isPlaying && isInitialized)
        {
            currentOxygen = Mathf.Clamp(currentOxygen, 0, MaxOxygen);
        }

        // Ensure positive values for rates and amounts
        if (oxygenDepletionRate < 0)
            oxygenDepletionRate = 0;
        if (oxygenAddAmount < 0)
            oxygenAddAmount = 0;
        if (oxygenLowThreshold < 0)
            oxygenLowThreshold = 0;
    }

    #endregion
}

/// <summary>
/// Data structure for saving/loading player oxygen information
/// </summary>
[System.Serializable]
public class PlayerOxygenData
{
    public float currentOxygen;
    public bool isDepleted;
    public bool isInitialized;

    public PlayerOxygenData()
    {
        currentOxygen = 100f;
        isDepleted = false;
        isInitialized = false;
    }
}