using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Handles all player stamina functionality including current stamina tracking,
/// stamina consumption (fast swimming, actions), regeneration, and depletion management.
/// Monitors PlayerController for automatic stamina consumption during fast swimming.
/// ENHANCED: Added regeneration delay when stamina reaches 0 - player must wait before regen starts.
/// </summary>
public class PlayerStamina : MonoBehaviour
{
    [Header("Current Stamina")]
    [ShowInInspector, ReadOnly]
    public float currentStamina;

    [Header("Stamina Settings")]
    [SerializeField] private float performActionStaminaCost = 10f;
    [SerializeField] private float fastSwimmingStaminaRate = 5f; // Stamina consumed per second while fast swimming

    [Header("Regeneration Delay")]
    [SerializeField] private float staminaRegenDelay = 3f; // Delay in seconds after depletion before regen starts
    [ShowInInspector, ReadOnly] private float regenDelayTimer = 0f;
    [ShowInInspector, ReadOnly] private bool isInRegenDelay = false;

    [Header("External Control")]
    [ShowInInspector]
    public bool usingStamina = false; // Set by external scripts to prevent regeneration

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // References
    private PlayerController playerController;
    private PlayerData playerData;
    private bool isInitialized = false;
    private bool isDepleted = false;

    // Cached references for efficiency
    private SwimmingMovementController swimmingController;
    private bool hasSwimmingController = false;

    // Public Properties
    public float MaxStamina => playerData?.maxStamina ?? 100f;
    public float StaminaPercentage => MaxStamina > 0 ? currentStamina / MaxStamina : 0f;
    public bool IsInitialized => isInitialized;
    public bool IsDepleted => isDepleted;

    // Enhanced regeneration check - includes delay timer
    public bool IsRegenerating => !usingStamina && !IsFastSwimming && !isInRegenDelay;

    // New properties for regen delay system
    public bool IsInRegenDelay => isInRegenDelay;
    public float RegenDelayTimeRemaining => Mathf.Max(0f, regenDelayTimer);
    public float RegenDelayPercentage => staminaRegenDelay > 0 ? Mathf.Clamp01(RegenDelayTimeRemaining / staminaRegenDelay) : 0f;

    // Fast swimming check property
    private bool IsFastSwimming
    {
        get
        {
            if (!hasSwimmingController) return false;
            return swimmingController != null && swimmingController.isFastSwimming;
        }
    }

    private void Awake()
    {
        FindPlayerController();
        DebugLog("PlayerStamina component created");
    }

    private void Start()
    {
        RefreshReferences();

        if (!isInitialized)
        {
            InitializeWithMaxStamina();
        }
    }

    /// <summary>
    /// Finds and caches PlayerController and SwimmingController references
    /// </summary>
    private void FindPlayerController()
    {
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();
        }

        // Cache swimming controller reference for efficiency
        if (playerController != null)
        {
            swimmingController = playerController.swimmingMovementController;
            hasSwimmingController = swimmingController != null;
            DebugLog($"PlayerController found, SwimmingController available: {hasSwimmingController}");
        }
        else
        {
            Debug.LogError("[PlayerStamina] PlayerController not found! Stamina consumption won't work properly.");
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

            // Update regen delay from PlayerData if available
            if (playerData != null && playerData.staminaRegenDelay > 0)
            {
                staminaRegenDelay = playerData.staminaRegenDelay;
            }

            DebugLog("PlayerData reference refreshed");
        }

        // Refresh controller reference if needed
        if (playerController == null)
        {
            FindPlayerController();
        }
    }

    /// <summary>
    /// Initializes stamina with default max stamina value
    /// </summary>
    public void InitializeWithMaxStamina()
    {
        RefreshReferences();

        if (playerData != null)
        {
            currentStamina = playerData.maxStamina;
            isInitialized = true;
            isDepleted = false;
            isInRegenDelay = false;
            regenDelayTimer = 0f;
            DebugLog($"Stamina initialized with max stamina: {currentStamina}");
        }
        else
        {
            // Fallback if PlayerData not available
            currentStamina = 100f;
            isInitialized = true;
            isDepleted = false;
            isInRegenDelay = false;
            regenDelayTimer = 0f;
            DebugLog("Stamina initialized with fallback value: 100");
        }

        TriggerStaminaChangedEvent();
    }

    /// <summary>
    /// Initializes stamina with a specific value (used by save system)
    /// </summary>
    public void InitializeWithStamina(float stamina)
    {
        RefreshReferences();
        currentStamina = Mathf.Clamp(stamina, 0, MaxStamina);
        isInitialized = true;
        isDepleted = currentStamina <= 0;

        // If starting with depleted stamina, don't start regen delay
        isInRegenDelay = false;
        regenDelayTimer = 0f;

        DebugLog($"Stamina initialized with specific value: {currentStamina}");

        TriggerStaminaChangedEvent();
    }

    /// <summary>
    /// Public method to consume stamina for actions (called by external scripts)
    /// </summary>
    [Button("Consume Stamina")]
    public void ConsumeStamina(bool heavyAction)
    {
        if (heavyAction)
            ConsumeStamina(performActionStaminaCost * 2);
        else
            ConsumeStamina(performActionStaminaCost);
    }

    /// <summary>
    /// Consumes a specific amount of stamina
    /// </summary>
    public void ConsumeStamina(float amount)
    {
        if (!isInitialized || amount <= 0) return;

        float previousStamina = currentStamina;
        currentStamina = Mathf.Clamp(currentStamina - amount, 0, MaxStamina);

        DebugLog($"Stamina consumed: {amount} ({previousStamina} -> {currentStamina})");

        TriggerStaminaChangedEvent();

        // Check for depletion (reaching exactly 0)
        if (currentStamina <= 0 && !isDepleted)
        {
            HandleStaminaDepletion();
        }
    }

    /// <summary>
    /// Regenerates stamina by a specific amount
    /// </summary>
    public void RegenerateStamina(float amount)
    {
        if (!isInitialized || amount <= 0) return;

        float previousStamina = currentStamina;
        currentStamina = Mathf.Clamp(currentStamina + amount, 0, MaxStamina);

        if (previousStamina != currentStamina)
        {
            DebugLog($"Stamina regenerated: {amount} ({previousStamina} -> {currentStamina})");
            TriggerStaminaChangedEvent();

            // Check for recovery from depletion when stamina increases from 0
            if (isDepleted && currentStamina > 0)
            {
                HandleStaminaRecovery();
            }
        }
    }

    /// <summary>
    /// Sets stamina to a specific value (useful for save/load or special effects)
    /// </summary>
    public void SetStamina(float newStamina)
    {
        if (!isInitialized)
        {
            InitializeWithStamina(newStamina);
            return;
        }

        float previousStamina = currentStamina;
        currentStamina = Mathf.Clamp(newStamina, 0, MaxStamina);

        DebugLog($"Stamina set from {previousStamina} to {currentStamina}");

        TriggerStaminaChangedEvent();

        // Check for state changes
        if (currentStamina <= 0 && !isDepleted)
        {
            HandleStaminaDepletion();
        }
        else if (isDepleted && currentStamina > 0)
        {
            HandleStaminaRecovery();
        }
    }

    /// <summary>
    /// Handles stamina depletion - starts regeneration delay timer
    /// </summary>
    private void HandleStaminaDepletion()
    {
        if (isDepleted) return;

        isDepleted = true;
        isInRegenDelay = true;
        regenDelayTimer = staminaRegenDelay;

        DebugLog($"Stamina depleted! Regen delay started: {staminaRegenDelay} seconds");

        // Use GameEvents system
        GameEvents.TriggerPlayerStaminaDepleted();
    }

    /// <summary>
    /// Handles stamina recovery from depletion
    /// </summary>
    private void HandleStaminaRecovery()
    {
        if (!isDepleted) return;

        isDepleted = false;
        // Note: Don't clear regen delay here - let it finish naturally

        DebugLog("Stamina recovered from depletion");

        // Use GameEvents system
        GameEvents.TriggerPlayerStaminaRecovered();
    }

    /// <summary>
    /// Manually clears the regeneration delay (useful for power-ups or special abilities)
    /// </summary>
    public void ClearRegenDelay()
    {
        isInRegenDelay = false;
        regenDelayTimer = 0f;
        DebugLog("Regeneration delay cleared manually");
    }

    /// <summary>
    /// Restores stamina to maximum and clears any delays
    /// </summary>
    public void RestoreStamina()
    {
        currentStamina = MaxStamina;
        isDepleted = false;
        isInRegenDelay = false;
        regenDelayTimer = 0f;
        isInitialized = true;

        DebugLog("Stamina restored to maximum");
        TriggerStaminaChangedEvent();

        if (isDepleted)
        {
            HandleStaminaRecovery();
        }
    }

    /// <summary>
    /// Enhanced update loop with regeneration delay system
    /// </summary>
    private void Update()
    {
        if (!isInitialized || playerData == null)
            return;

        float deltaTime = Time.deltaTime;

        // Handle regeneration delay timer
        if (isInRegenDelay)
        {
            regenDelayTimer -= deltaTime;

            if (regenDelayTimer <= 0f)
            {
                isInRegenDelay = false;
                regenDelayTimer = 0f;
                DebugLog("Regeneration delay expired - stamina can now regenerate");
            }
        }

        // Check if we should consume stamina (fast swimming)
        if (IsFastSwimming && currentStamina > 0)
        {
            ConsumeStamina(fastSwimmingStaminaRate * deltaTime);
        }

        // Check regeneration conditions - now includes delay check
        bool canRegenerate = IsRegenerating && currentStamina < MaxStamina && playerData.staminaRegenRate > 0;

        if (canRegenerate)
        {
            RegenerateStamina(playerData.staminaRegenRate * deltaTime);
        }

        // Enhanced debug logging for troubleshooting
        if (enableDebugLogs && (IsFastSwimming || canRegenerate || isInRegenDelay))
        {
            DebugLog($"Stamina Update - FastSwimming: {IsFastSwimming}, UsingStamina: {usingStamina}, " +
                    $"IsDepleted: {isDepleted}, InRegenDelay: {isInRegenDelay}, " +
                    $"DelayRemaining: {RegenDelayTimeRemaining:F1}s, CanRegenerate: {canRegenerate}, " +
                    $"Current: {currentStamina:F1}/{MaxStamina}");
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
        // Example: if (consumable.staminaBoost > 0) RegenerateStamina(consumable.staminaBoost);
        // Example: if (consumable.clearRegenDelay) ClearRegenDelay();
    }

    /// <summary>
    /// Gets current stamina data for saving/UI display
    /// </summary>
    public PlayerStaminaData GetStaminaData()
    {
        return new PlayerStaminaData
        {
            currentStamina = currentStamina,
            isDepleted = isDepleted,
            isInitialized = isInitialized,
            usingStamina = usingStamina,
            isInRegenDelay = isInRegenDelay,
            regenDelayTimer = regenDelayTimer
        };
    }

    /// <summary>
    /// Restores stamina data from save system
    /// </summary>
    public void RestoreStaminaData(PlayerStaminaData staminaData)
    {
        if (staminaData == null)
        {
            DebugLog("No stamina data provided for restoration");
            return;
        }

        currentStamina = staminaData.currentStamina;
        isDepleted = staminaData.isDepleted;
        isInitialized = staminaData.isInitialized;
        usingStamina = staminaData.usingStamina;
        isInRegenDelay = staminaData.isInRegenDelay;
        regenDelayTimer = staminaData.regenDelayTimer;

        DebugLog($"Stamina data restored - Stamina: {currentStamina}, Depleted: {isDepleted}, " +
                $"Initialized: {isInitialized}, UsingStamina: {usingStamina}, " +
                $"InRegenDelay: {isInRegenDelay}, DelayTimer: {regenDelayTimer:F1}s");

        // Ensure stamina is within valid bounds
        currentStamina = Mathf.Clamp(currentStamina, 0, MaxStamina);

        TriggerStaminaChangedEvent();
    }

    /// <summary>
    /// Triggers stamina changed event for UI updates using GameEvents
    /// </summary>
    private void TriggerStaminaChangedEvent()
    {
        GameEvents.TriggerPlayerStaminaChanged(currentStamina, MaxStamina);
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerStamina] {message}");
        }
    }

    #region Validation

    private void OnValidate()
    {
        // Ensure current stamina stays within bounds in editor
        if (Application.isPlaying && isInitialized)
        {
            currentStamina = Mathf.Clamp(currentStamina, 0, MaxStamina);
        }

        // Ensure positive values for costs and rates
        if (performActionStaminaCost < 0)
            performActionStaminaCost = 0;
        if (fastSwimmingStaminaRate < 0)
            fastSwimmingStaminaRate = 0;
        if (staminaRegenDelay < 0)
            staminaRegenDelay = 0;
    }

    #endregion
}

/// <summary>
/// Enhanced data structure for saving/loading player stamina information with regen delay
/// </summary>
[System.Serializable]
public class PlayerStaminaData
{
    public float currentStamina;
    public bool isDepleted;
    public bool isInitialized;
    public bool usingStamina;
    public bool isInRegenDelay;
    public float regenDelayTimer;

    public PlayerStaminaData()
    {
        currentStamina = 100f;
        isDepleted = false;
        isInitialized = false;
        usingStamina = false;
        isInRegenDelay = false;
        regenDelayTimer = 0f;
    }
}