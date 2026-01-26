using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// REFACTORED: ToolHandler implementation with energy source system.
/// Primary action: Melee attack (through unified system)
/// Secondary action: Use tool (instant or held based on ToolData.isActionHeld)
/// Reload action: Swap energy source (for tools that require energy)
/// Supports both instant tools (like placing C4) and held tools (like blowtorch)
/// UPDATED: Full energy source system integration with inventory consumption and swapping
/// </summary>
public class ToolHandler : BaseEquippedItemHandler
{
    [Header("Tool Audio")]
    [SerializeField] private AudioClip toolStartSound;
    [SerializeField] private AudioClip toolCompleteSound;
    [SerializeField] private AudioClip toolLoopSound;
    [SerializeField] private AudioClip emptyEnergySound;
    [SerializeField] private AudioClip reloadSound;

    [Header("Tool State")]
    [SerializeField, ReadOnly] private bool isUsingTool = false;
    [SerializeField, ReadOnly] private bool isToolReady = false;
    [SerializeField, ReadOnly] private bool isReloading = false;

    [Header("PlayerTool Component")]
    [SerializeField, ReadOnly] private PlayerTool currentPlayerToolComponent;

    [Header("Energy Source Display")]
    [SerializeField, ReadOnly] private int equippedEnergy = 0;
    [SerializeField, ReadOnly] private int inventoryEnergy = 0;
    [SerializeField, ReadOnly] private bool hasEnergySource = false;

    [Header("Energy Consumption")]
    [SerializeField, ReadOnly] private float energyConsumeTimer = 0f;
    [SerializeField, ReadOnly] private bool isConsumingEnergy = false;

    // Components
    private AudioSource audioSource;

    // Quick access to tool instance data
    private ToolInstanceData ToolInstanceData =>
        currentItemInstance?.ToolInstanceData;

    // Template data
    private ToolData ToolData => currentItemData?.ToolData;

    // Events
    public System.Action<ItemData> OnToolEquipped;
    public System.Action OnToolUnequipped;
    public System.Action OnToolUsed;
    public System.Action OnToolCompleted;
    public System.Action OnToolReloaded;
    public System.Action<int, int, int> OnEnergyChanged; // equipped, inventory, max

    public override ItemType HandledItemType => ItemType.Tool;

    #region Initialization

    protected override void Awake()
    {
        base.Awake();
        SetupComponents();
    }

    private void SetupComponents()
    {
        // Audio setup
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f;
            audioSource.playOnAwake = false;
        }
    }

    #endregion

    #region Base Handler Implementation

    protected override void OnItemEquippedInternal(ItemData itemData)
    {
        if (itemData?.itemType != ItemType.Tool)
        {
            Debug.LogError($"ToolHandler received invalid item: {itemData?.itemName ?? "null"}");
            return;
        }

        // Reset tool state
        isUsingTool = false;
        isToolReady = false;
        isReloading = false;
        isConsumingEnergy = false;
        energyConsumeTimer = 0f;

        // Verify instance data is available
        if (!HasValidInstance())
        {
            Debug.LogWarning($"ToolHandler equipped {itemData.itemName} but has no valid ItemInstance!");
        }

        // Update energy display
        RefreshEnergyDisplay();

        bool isHeldTool = ToolData?.isActionHeld ?? false;
        bool requiresEnergy = ToolData?.requiresEnergySource ?? false;
        DebugLog($"Equipped tool: {itemData.itemName} (Type: {(isHeldTool ? "Held" : "Instant")}, Requires Energy: {requiresEnergy})");

        UpdateFXProjectileSpawnPointPos(itemData.ToolData.fxSpawnPoint, itemData.ToolData.fxSpawnRotation);

        if (equipmentManager.visualManager == null)
        {
            Debug.LogWarning($"ToolHandler equipped {itemData.itemName} but has no valid VisualManager!");
            return;
        }

        if (equipmentManager.visualManager.currentActiveItem == null)
        {
            Debug.LogWarning($"ToolHandler equipped {itemData.itemName} but has no current active item!");
            return;
        }

        //Debug.Log("Trying to get PlayerTool component");
        currentPlayerToolComponent = equipmentManager.visualManager.currentActiveItem.GetComponent<PlayerTool>();

        if (currentPlayerToolComponent != null)
            currentPlayerToolComponent.activeEffectNoiseVolume = itemData.effectNoiseVolume;

        OnToolEquipped?.Invoke(itemData);
        OnEnergyChanged?.Invoke(equippedEnergy, inventoryEnergy, GetMaxEnergyCapacity());

    }

    protected override void OnItemUnequippedInternal()
    {
        base.OnItemUnequippedInternal();

        // Reset tool state
        isUsingTool = false;
        isToolReady = false;
        isReloading = false;
        isConsumingEnergy = false;
        energyConsumeTimer = 0f;

        if (currentPlayerToolComponent != null)
        {
            currentPlayerToolComponent.StopToolEffect();
            currentPlayerToolComponent = null;
        }

        playerController.playerAudio.StopCurrentToolUseLoopNoise();

        DebugLog("Unequipped tool");
        OnToolUnequipped?.Invoke();

    }

    protected override void HandlePrimaryActionInternal(InputContext context)
    {
        // Primary = Melee attack (using tool as improvised weapon)
        if (context.isPressed)
        {
            HandleMeleeAction(context);
        }
    }

    protected override void HandleSecondaryActionInternal(InputContext context)
    {
        // Secondary = Use tool (instant version only)
        // Held tools are handled by the unified system automatically
        if (context.isPressed && !IsCurrentToolHeld())
        {
            UseInstantTool();
        }
    }

    protected override void HandleReloadActionInternal(InputContext context)
    {
        // Reload = Swap energy source (for tools that require energy)
        if (context.isPressed && RequiresEnergySource())
        {
            StartEnergySourceSwap();
        }
        else
        {
            DebugLog("Reload not applicable for this tool (no energy source required)");
        }
    }

    protected override bool CanPerformActionInternal(PlayerAnimationType actionType, PlayerStateType playerState)
    {
        switch (actionType)
        {
            case PlayerAnimationType.PrimaryAction:
            case PlayerAnimationType.MeleeAction:
            case >= PlayerAnimationType.HeldMeleeActionStart and <= PlayerAnimationType.HeldMeleeActionCancel:
                // Can melee if not using tool (or if using held tool and ready)
                return !isUsingTool || (IsCurrentToolHeld() && currentActionState == ActionState.None);

            case PlayerAnimationType.SecondaryAction:
            case >= PlayerAnimationType.HeldSecondaryActionStart and <= PlayerAnimationType.CancelHeldSecondaryAction:
                return CanUseTool(playerState);

            case PlayerAnimationType.ReloadAction:
                return CanSwapEnergySource();

            default:
                return false;
        }
    }

    protected override void UpdateHandlerInternal(float deltaTime)
    {
        // Handle energy consumption for active tools
        if (isConsumingEnergy)
        {
            ConsumeEnergyOverTime(deltaTime);
        }
    }

    #endregion

    #region Unified System Configuration

    /// <summary>
    /// Primary action is instant (melee with tool)
    /// </summary>
    protected override bool ShouldPrimaryActionBeHeld(InputContext context) => false;

    /// <summary>
    /// Secondary action depends on tool type
    /// </summary>
    protected override bool ShouldSecondaryActionBeHeld(InputContext context) => IsCurrentToolHeld();

    /// <summary>
    /// Override melee damage for tool (weaker than dedicated weapons)
    /// </summary>
    protected override float GetMeleeDamage() => meleeDamage * 0.85f; // 85% of base damage

    #endregion

    #region Instant Tool Usage

    /// <summary>
    /// Use instant tool (like placing C4, using scanner, etc.)
    /// </summary>
    private void UseInstantTool()
    {
        if (!CanUseTool(GetCurrentPlayerState()))
        {
            DebugLog("Cannot use instant tool");
            return;
        }

        isUsingTool = true;
        DebugLog($"Using instant tool: {currentItemData.itemName}");

        PlaySound(toolStartSound);
        TriggerInstantAction(PlayerAnimationType.SecondaryAction);
    }

    /// <summary>
    /// Complete instant tool usage (called by animation completion)
    /// </summary>
    protected override void OnActionCompletedInternal(PlayerAnimationType actionType)
    {
        base.OnActionCompletedInternal(actionType);

        if (actionType == PlayerAnimationType.SecondaryAction)
        {
            CompleteInstantToolUse();
        }
        else if (actionType == PlayerAnimationType.ReloadAction)
        {
            CompleteEnergySourceSwap();
        }
    }

    /// <summary>
    /// Complete instant tool use and apply effects
    /// </summary>
    private void CompleteInstantToolUse()
    {
        if (!isUsingTool || IsCurrentToolHeld())
        {
            return; // Not using instant tool
        }

        DebugLog($"Instant tool completed: {currentItemData.itemName}");

        // Apply tool effects
        ApplyToolEffects();

        // Play completion sound
        PlaySound(toolCompleteSound);

        currentPlayerToolComponent?.TriggerToolEffect();

        // Fire events
        OnToolUsed?.Invoke();
        OnToolCompleted?.Invoke();

        // Reset state
        isUsingTool = false;
    }

    #endregion

    #region Held Tool Events

    /// <summary>
    /// Called when starting to use a held tool (like blowtorch)
    /// </summary>
    protected override void OnHeldActionStarting(HeldActionType heldType, PlayerAnimationType actionType, InputContext context)
    {
        base.OnHeldActionStarting(heldType, actionType, context);

        if (heldType == HeldActionType.Secondary)
        {
            isUsingTool = true;
            DebugLog($"Starting held tool: {currentItemData.itemName}");
            //playerController.playerAudio.PlayCurrentToolStartLoopNoise(); //we'll call this via an animation event instead
        }
    }

    /// <summary>
    /// Called when held tool is ready for use
    /// </summary>
    protected override void OnSecondaryActionReady()
    {
        base.OnSecondaryActionReady();
        isToolReady = true;
        DebugLog("Held tool ready - starting energy consumption");

        // Start energy consumption for held tools that require energy
        if (RequiresEnergySource())
        {
            StartEnergyConsumption();
        }

        currentPlayerToolComponent?.TriggerToolEffect();

        // Start loop sound for held tools
        playerController.playerAudio.PlayCurrentToolUseLoopNoise();
    }

    protected override void HandleEndAction()
    {
        base.HandleEndAction();

        currentPlayerToolComponent?.StopToolEffect();
        playerController.playerAudio.StopCurrentToolUseLoopNoise();
        playerController.playerAudio.PlayCurrentToolStopLoopNoise();

        // Stop energy consumption for held tools that require energy
        if (RequiresEnergySource())
        {
            StopEnergyConsumption();
        }
    }

    /// <summary>
    /// Called when held tool action is executed (on release)
    /// </summary>
    protected override void ExecuteSecondaryAction(PlayerAnimationType actionType)
    {
        base.ExecuteSecondaryAction(actionType);
        DebugLog("Held tool action executed");

        currentPlayerToolComponent?.StopToolEffect();

        // Stop energy consumption
        StopEnergyConsumption();

        // Apply final tool effects
        ApplyToolEffects();
        OnToolUsed?.Invoke();
    }

    /// <summary>
    /// Called when held tool action completes
    /// </summary>
    protected override void OnSecondaryActionCompleted(PlayerAnimationType actionType)
    {
        base.OnSecondaryActionCompleted(actionType);

        // Reset tool state
        isUsingTool = false;
        isToolReady = false;
        DebugLog("Held tool completed");

        currentPlayerToolComponent?.StopToolEffect();

        PlaySound(toolCompleteSound);
        OnToolCompleted?.Invoke();
    }

    /// <summary>
    /// Called when held tool action is cancelled
    /// </summary>
    protected override void OnHeldActionCancelled(PlayerAnimationType actionType)
    {
        base.OnHeldActionCancelled(actionType);

        if (currentHeldActionType == HeldActionType.Secondary)
        {
            isUsingTool = false;
            isToolReady = false;
            StopEnergyConsumption();
            DebugLog($"Held tool cancelled: {currentItemData.itemName}");

            currentPlayerToolComponent?.StopToolEffect();
            playerController.playerAudio.StopCurrentToolUseLoopNoise();
        }
    }

    #endregion

    #region Energy Source System

    /// <summary>
    /// Start swapping energy source (reload action)
    /// </summary>
    private void StartEnergySourceSwap()
    {
        if (!CanSwapEnergySource())
        {
            DebugLog("Cannot swap energy source");
            HandleEmptyReload();
            return;
        }

        // Stop any active tool usage during reload
        if (isUsingTool)
        {
            StopEnergyConsumption();
            currentPlayerToolComponent?.StopToolEffect();
            playerController.playerAudio.StopCurrentToolUseLoopNoise();
            playerController.playerAudio.PlayCurrentToolStopLoopNoise();
        }

        isReloading = true;
        DebugLog($"Starting energy source swap - Current: {equippedEnergy}/{GetMaxEnergyCapacity()}, Inventory: {inventoryEnergy}");

        PlaySound(reloadSound);
        TriggerInstantAction(PlayerAnimationType.ReloadAction);
    }

    /// <summary>
    /// Complete energy source swap (called by animation completion)
    /// </summary>
    private void CompleteEnergySourceSwap()
    {
        if (!isReloading) return;

        if (ToolInstanceData == null || ToolData == null)
        {
            DebugLog("Cannot complete energy source swap - missing data");
            isReloading = false;
            return;
        }

        // Perform the swap via helper
        bool swapSuccess = ToolEnergyInventoryHelper.SwapEnergySource(
            ToolInstanceData,
            ToolData.requiredEnergySourceType
        );

        if (swapSuccess)
        {
            DebugLog($"Energy source swap complete - New energy: {ToolInstanceData.equippedEnergySourceAmount}/{GetMaxEnergyCapacity()}");
        }
        else
        {
            DebugLog("Energy source swap failed - no sources available");
        }

        // Update display
        RefreshEnergyDisplay();

        isReloading = false;
        OnToolReloaded?.Invoke();
        OnEnergyChanged?.Invoke(equippedEnergy, inventoryEnergy, GetMaxEnergyCapacity());
    }

    /// <summary>
    /// Start consuming energy (called when held tool becomes ready)
    /// </summary>
    private void StartEnergyConsumption()
    {
        if (!RequiresEnergySource())
            return;

        if (!HasEquippedEnergy())
        {
            DebugLog("Cannot start energy consumption - no energy available");
            HandleOutOfEnergy();
            return;
        }

        isConsumingEnergy = true;
        energyConsumeTimer = 0f;
        Debug.Log("STARTED ENERGY CONSUMPTION");
    }

    /// <summary>
    /// Stop consuming energy (called when held tool ends)
    /// </summary>
    private void StopEnergyConsumption()
    {
        isConsumingEnergy = false;
        energyConsumeTimer = 0f;
        Debug.Log("STOPPED ENERGY CONSUMPTION");
    }

    /// <summary>
    /// Consume energy over time while tool is active
    /// </summary>
    private void ConsumeEnergyOverTime(float deltaTime)
    {
        if (ToolData == null || ToolInstanceData == null)
            return;

        energyConsumeTimer += deltaTime;

        // Consume energy at the rate specified in tool data
        int consumptionRate = ToolData.energyConsumptionRate;
        float consumeInterval = 1f / consumptionRate; // Time between each energy consumption

        while (energyConsumeTimer >= consumeInterval)
        {
            energyConsumeTimer -= consumeInterval;

            // Try to consume 1 energy
            if (!ToolInstanceData.TryConsumeEnergy(1))
            {
                // Out of energy!
                DebugLog("Tool ran out of energy during use");
                HandleOutOfEnergy();
                return;
            }

            // Update display
            RefreshEnergyDisplay();
        }
    }

    /// <summary>
    /// Handle tool running out of energy
    /// </summary>
    private void HandleOutOfEnergy()
    {
        DebugLog("Tool out of energy - stopping action");

        // Stop energy consumption
        StopEnergyConsumption();

        // Play empty sound
        PlaySound(emptyEnergySound);

        // Cancel held action if active
        if (currentActionState == ActionState.Looping || currentActionState == ActionState.Starting)
        {
            CancelCurrentAction();
        }

        // Update display
        RefreshEnergyDisplay();
        OnEnergyChanged?.Invoke(equippedEnergy, inventoryEnergy, GetMaxEnergyCapacity());

        // Try auto-reload if available
        if (HasAvailableEnergySources())
        {
            DebugLog("Auto-reloading energy source");
            Invoke(nameof(AutoReload), 0.2f);
        }
    }

    /// <summary>
    /// Handle empty reload attempt
    /// </summary>
    private void HandleEmptyReload()
    {
        DebugLog("Empty reload - no energy sources available");
        PlaySound(emptyEnergySound);
    }

    /// <summary>
    /// Auto-reload after running out of energy
    /// </summary>
    private void AutoReload()
    {
        if (currentActionState == ActionState.None && !isReloading && HasAvailableEnergySources())
        {
            DebugLog("Starting auto-reload");
            StartEnergySourceSwap();
        }
    }

    /// <summary>
    /// Refresh energy display values from instance data and inventory
    /// </summary>
    private void RefreshEnergyDisplay()
    {
        if (!RequiresEnergySource())
        {
            equippedEnergy = 0;
            inventoryEnergy = 0;
            hasEnergySource = false;
            return;
        }

        equippedEnergy = GetEquippedEnergy();
        inventoryEnergy = GetInventoryEnergy();
        hasEnergySource = ToolInstanceData?.HasEnergySourceEquipped() ?? false;
    }

    /// <summary>
    /// Get current equipped energy from instance data
    /// </summary>
    private int GetEquippedEnergy()
    {
        return ToolInstanceData?.equippedEnergySourceAmount ?? 0;
    }

    /// <summary>
    /// Get total energy available in inventory
    /// </summary>
    private int GetInventoryEnergy()
    {
        if (ToolData?.requiredEnergySourceType == null)
            return 0;

        return ToolEnergyInventoryHelper.GetTotalEnergyCount(ToolData.requiredEnergySourceType);
    }

    /// <summary>
    /// Get maximum energy capacity
    /// </summary>
    private int GetMaxEnergyCapacity()
    {
        return ToolData?.maxEnergyCapacity ?? 100;
    }

    #endregion

    #region Tool Effects System

    /// <summary>
    /// Apply tool effects based on tool type
    /// </summary>
    private void ApplyToolEffects()
    {
        if (ToolData == null) return;

        string toolType = ToolData.isActionHeld ? "held" : "instant";
        DebugLog($"Applying {toolType} tool effects: {currentItemData.itemName}");

        // TODO: Implement specific tool effects based on tool type and name:

        // INSTANT TOOLS:
        // - "C4" or "Explosive": Place explosive device at current location
        // - "Scanner": Scan area for items, enemies, or secrets
        // - "GPS": Update map or add waypoint
        // - "Lockpick": Attempt to unlock nearby door/chest
        // - "Hacking Device": Interact with nearby terminal/computer
        // - "Medical Kit": Apply healing over time
        // - "Repair Kit": Fix nearby damaged objects

        // HELD TOOLS:
        // - "Blowtorch": Continuous damage/melting to objects in front
        // - "Drill": Continuous drilling through materials
        // - "Welder": Continuous repair of objects
        // - "Metal Detector": Continuous scanning while held
        // - "Flashlight": Continuous illumination
        // - "Radio": Continuous communication channel

        string toolName = currentItemData.itemName.ToLower();

        if (toolName.Contains("scanner"))
        {
            PerformScannerEffect();
        }
        else if (toolName.Contains("c4") || toolName.Contains("explosive"))
        {
            PlaceExplosive();
        }
        else if (toolName.Contains("blowtorch"))
        {
            ApplyBlowtorchEffect();
        }
        else if (toolName.Contains("lockpick"))
        {
            AttemptLockpicking();
        }
        else if (toolName.Contains("medical") || toolName.Contains("medkit"))
        {
            ApplyMedicalKit();
        }
        else if (toolName.Contains("repair"))
        {
            PerformRepair();
        }
        else if (toolName.Contains("flashlight"))
        {
            ToggleFlashlight();
        }
        else
        {
            DebugLog($"Generic tool effect applied for: {currentItemData.itemName}");
        }
    }

    // Tool effect implementations (from original ToolHandler)
    private void PerformScannerEffect()
    {
        DebugLog("Scanner activated - revealing nearby objects");
        Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, 10f);
        int itemCount = 0;

        foreach (var obj in nearbyObjects)
        {
            if (obj.GetComponent<IInteractable>() != null)
            {
                itemCount++;
                DebugLog($"Scanner detected interactable: {obj.name}");
            }
        }

        DebugLog($"Scanner found {itemCount} interactable objects nearby");
    }

    private void PlaceExplosive()
    {
        DebugLog("C4 placed at current location");
        Vector3 placementPosition = transform.position + transform.forward * 1f;
        DebugLog($"Explosive placed at position: {placementPosition}");
    }

    private void ApplyBlowtorchEffect()
    {
        DebugLog("Blowtorch effect applied - heating/melting objects");

        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 3f))
        {
            DebugLog($"Blowtorch targeting: {hit.collider.name}");

            var destructible = hit.collider.GetComponent<IDestructible>();
            if (destructible != null)
            {
                destructible.TakeDamage(Time.deltaTime * 50f);
            }
        }
    }

    private void AttemptLockpicking()
    {
        DebugLog("Attempting to pick lock");

        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 2f))
        {
            var lockable = hit.collider.GetComponent<ILockable>();
            if (lockable != null && lockable.IsLocked())
            {
                DebugLog($"Attempting to pick lock on: {hit.collider.name}");
            }
            else
            {
                DebugLog("No locked object found to pick");
            }
        }
    }

    private void ApplyMedicalKit()
    {
        DebugLog("Applying medical kit - restoring health");

        if (GameManager.Instance?.playerManager != null)
        {
            float healAmount = 25f;
            GameManager.Instance.playerManager.ModifyHealth(healAmount);
            DebugLog($"Medical kit restored {healAmount} health");
        }
    }

    private void PerformRepair()
    {
        DebugLog("Using repair kit on nearby objects");

        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 2f))
        {
            var repairable = hit.collider.GetComponent<IRepairable>();
            if (repairable != null && repairable.NeedsRepair())
            {
                repairable.Repair(50f);
                DebugLog($"Repaired object: {hit.collider.name}");
            }
            else
            {
                DebugLog("No repairable object found");
            }
        }
    }

    private void ToggleFlashlight()
    {
        DebugLog("Toggling flashlight");

        Light flashlight = GetComponentInChildren<Light>();
        if (flashlight != null)
        {
            flashlight.enabled = !flashlight.enabled;
            DebugLog($"Flashlight {(flashlight.enabled ? "ON" : "OFF")}");
        }
        else
        {
            DebugLog("No flashlight component found");
        }
    }

    #endregion

    #region State Validation

    /// <summary>
    /// Check if can use tool in current state
    /// </summary>
    private bool CanUseTool(PlayerStateType playerState)
    {
        if (ToolData == null)
        {
            DebugLog("Cannot use tool - no tool data");
            return false;
        }

        // For held tools, allow during Starting/Looping if already using
        if (ToolData.isActionHeld)
        {
            if (!isUsingTool)
            {
                // Check energy requirements before starting
                if (RequiresEnergySource() && !HasEquippedEnergy())
                {
                    DebugLog("Cannot use tool - no energy available");
                    return false;
                }
                return currentActionState == ActionState.None;
            }
            else
            {
                return currentActionState == ActionState.Starting ||
                       currentActionState == ActionState.Looping ||
                       currentActionState == ActionState.None;
            }
        }
        else
        {
            // For instant tools, must not be performing any action
            if (currentActionState != ActionState.None)
            {
                DebugLog("Cannot use instant tool - already performing action");
                return false;
            }

            // Check energy requirements for instant tools
            if (RequiresEnergySource() && !HasEquippedEnergy())
            {
                DebugLog("Cannot use instant tool - no energy available");
                return false;
            }
        }

        if (!currentItemData.CanUseInState(playerState))
        {
            DebugLog($"Cannot use tool - item cannot be used in state: {playerState}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Check if can swap energy source (reload)
    /// </summary>
    private bool CanSwapEnergySource()
    {
        if (!RequiresEnergySource())
        {
            DebugLog("Tool does not require energy source");
            return false;
        }

        if (ToolData == null || ToolInstanceData == null)
        {
            DebugLog("Cannot swap energy source - missing data");
            return false;
        }

        if (isReloading)
        {
            DebugLog("Cannot swap energy source - already reloading");
            return false;
        }

        if (currentActionState != ActionState.None && currentActionState != ActionState.Looping)
        {
            DebugLog("Cannot swap energy source - action in progress");
            return false;
        }

        // Check if alternative energy sources available in inventory
        if (!HasAvailableEnergySources())
        {
            DebugLog("Cannot swap energy source - no sources available in inventory");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Check if tool requires energy source
    /// </summary>
    private bool RequiresEnergySource()
    {
        return ToolData?.requiresEnergySource ?? false;
    }

    /// <summary>
    /// Check if tool has equipped energy available
    /// </summary>
    private bool HasEquippedEnergy()
    {
        if (!RequiresEnergySource())
            return true; // Tools without energy requirements always have "energy"

        return ToolInstanceData?.HasEnergyAvailable() ?? false;
    }

    /// <summary>
    /// Check if alternative energy sources available in inventory
    /// </summary>
    private bool HasAvailableEnergySources()
    {
        if (ToolData?.requiredEnergySourceType == null)
            return false;

        string currentSourceId = ToolInstanceData?.equippedEnergySourceInstanceId ?? "";
        return ToolEnergyInventoryHelper.HasAvailableEnergySources(
            ToolData.requiredEnergySourceType,
            currentSourceId
        );
    }

    #endregion

    #region Utility

    /// <summary>
    /// Check if current tool is held type
    /// </summary>
    private bool IsCurrentToolHeld() => ToolData?.isActionHeld ?? false;

    /// <summary>
    /// Play audio clip
    /// </summary>
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    #endregion

    #region Public API

    /// <summary>Check if currently using any tool</summary>
    public bool IsUsingTool() => isUsingTool;

    /// <summary>Check if performing held tool action</summary>
    public bool IsPerformingHeldToolAction() => isUsingTool && IsCurrentToolHeld() &&
                                                (currentActionState == ActionState.Starting ||
                                                 currentActionState == ActionState.Looping);

    /// <summary>Check if tool is ready (held tools only)</summary>
    public bool IsToolReady() => isToolReady;

    /// <summary>Check if currently reloading energy source</summary>
    public bool IsReloading() => isReloading;

    /// <summary>Check if tool has energy available</summary>
    public bool HasEnergy() => HasEquippedEnergy() || !RequiresEnergySource();

    /// <summary>Get current tool data</summary>
    public ToolData GetCurrentToolData() => ToolData;

    /// <summary>Check if current tool is instant type</summary>
    public bool IsCurrentToolInstant() => !IsCurrentToolHeld();

    /// <summary>Get tool name for effect routing</summary>
    public string GetToolName() => currentItemData?.itemName ?? "";

    /// <summary>Get energy status string for UI</summary>
    public string GetEnergyStatus()
    {
        if (!RequiresEnergySource())
            return "N/A";

        return $"{equippedEnergy}/{GetMaxEnergyCapacity()} ({inventoryEnergy})";
    }

    /// <summary>Force stop all tool actions</summary>
    public void ForceStopAllActions()
    {
        if (currentActionState != ActionState.None) ForceCompleteAction();
        isUsingTool = false;
        isToolReady = false;
        isReloading = false;
        StopEnergyConsumption();
        DebugLog("Force stopped all tool actions");
    }

    /// <summary>Debug info</summary>
    public override string GetDebugInfo()
    {
        return $"{GetType().Name} - Active: {isActive}, " +
               $"Item: {currentItemData?.itemName ?? "None"}, " +
               $"Action State: {currentActionState} ({currentActionAnimation}), " +
               $"Held Action: {currentHeldActionType}, " +
               $"Using Tool: {isUsingTool}, Tool Ready: {isToolReady}, " +
               $"Tool Type: {(IsCurrentToolHeld() ? "Held" : "Instant")}, " +
               $"Energy: {equippedEnergy}/{GetMaxEnergyCapacity()} (Inventory: {inventoryEnergy}), " +
               $"Consuming Energy: {isConsumingEnergy}, Reloading: {isReloading}";
    }

    #endregion
}

// Interface definitions (kept from original)
public interface IDestructible
{
    void TakeDamage(float damage);
    bool IsDestroyed();
}

public interface ILockable
{
    bool IsLocked();
    void Unlock();
    void Lock();
}

public interface IRepairable
{
    bool NeedsRepair();
    void Repair(float amount);
    float GetDurability();
}