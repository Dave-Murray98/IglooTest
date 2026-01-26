using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// REFACTORED: BowHandler implementation using enum-based animation system.
/// Primary action: Held draw/shoot (start → loop → end)
/// Secondary action: Toggle ADS (instant)
/// Reload action: Manual reload (instant)
/// Melee: Available through unified system
/// UPDATED: Full arrow system integration with inventory arrow consumption
/// </summary>
public class BowHandler : BaseEquippedItemHandler
{
    [Header("Bow Audio")]
    [SerializeField] private AudioClip drawSound;
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioClip reloadSound;

    [Header("Bow State")]
    [SerializeField, ReadOnly] private bool isDrawn = false;
    [SerializeField, ReadOnly] private bool isReloading = false;
    [SerializeField, ReadOnly] private bool isAiming = false;

    [Header("Arrow Display")]
    [SerializeField, ReadOnly] private bool arrowNocked = false;
    [SerializeField, ReadOnly] private int inventoryArrows = 0;

    [Header("ADS System")]
    [SerializeField] private ADSController adsController;

    // Components
    private AudioSource audioSource;
    private Camera playerCamera;

    // Quick access to bow data
    private BowData BowData => currentItemData?.BowData;

    // Quick access to bow instance data
    private BowInstanceData BowInstanceData => currentItemInstance?.BowInstanceData;

    // Events
    public System.Action<ItemData> OnBowEquipped;
    public System.Action OnBowUnequipped;
    public System.Action OnBowShot;
    public System.Action OnBowReloaded;
    public System.Action<bool, int> OnArrowStatusChanged; // hasArrow, inventoryCount

    public override ItemType HandledItemType => ItemType.Bow;

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

        // Camera reference
        playerCamera = Camera.main ?? FindFirstObjectByType<Camera>();

        // ADS controller
        if (adsController == null)
            adsController = GetComponent<ADSController>() ?? FindFirstObjectByType<ADSController>();
    }

    #endregion

    #region Base Handler Implementation

    protected override void OnItemEquippedInternal(ItemData itemData)
    {
        if (itemData?.itemType != ItemType.Bow)
        {
            Debug.LogError($"BowHandler received invalid item: {itemData?.itemName ?? "null"}");
            return;
        }

        // Reset bow state
        isDrawn = false;
        isReloading = false;
        isAiming = false;

        // Verify instance data
        if (!HasValidInstance())
        {
            Debug.LogWarning($"BowHandler equipped {itemData.itemName} but has no valid ItemInstance!");
        }

        // Auto-nock arrow if available
        AutoNockArrow();

        // Update display
        RefreshArrowDisplay();

        UpdateFXProjectileSpawnPointPos(itemData.BowData.projectileSpawnPoint, itemData.BowData.projectileSpawnRotation);

        DebugLog($"Equipped bow: {itemData.itemName} - Arrow nocked: {arrowNocked}, Inventory: {inventoryArrows}");
        OnBowEquipped?.Invoke(itemData);
        OnArrowStatusChanged?.Invoke(arrowNocked, inventoryArrows);
    }

    protected override void OnItemUnequippedInternal()
    {
        base.OnItemUnequippedInternal();

        // Clean up bow state
        if (isAiming) StopAiming();
        isDrawn = false;
        isReloading = false;

        DebugLog("Unequipped bow");
        OnBowUnequipped?.Invoke();
    }

    protected override void HandlePrimaryActionInternal(InputContext context)
    {
        // This won't be called since we return true for ShouldPrimaryActionBeHeld
        DebugLog("HandlePrimaryActionInternal - should not be called for bow");
    }

    protected override void HandleSecondaryActionInternal(InputContext context)
    {
        // Secondary = Toggle ADS
        if (context.isPressed)
        {
            if (isAiming) StopAiming();
            else StartAiming();
        }
    }

    protected override void HandleReloadActionInternal(InputContext context)
    {
        // Manual nock arrow (reload)
        if (context.isPressed)
        {
            StartNockingArrow();
        }
    }

    protected override bool CanPerformActionInternal(PlayerAnimationType actionType, PlayerStateType playerState)
    {
        switch (actionType)
        {
            case PlayerAnimationType.PrimaryAction:
            case >= PlayerAnimationType.HeldPrimaryActionStart and <= PlayerAnimationType.CancelHeldPrimaryAction:
                return CanDrawBow(playerState);

            case PlayerAnimationType.SecondaryAction:
                return CanAim();

            case PlayerAnimationType.ReloadAction:
                return CanNockArrow();

            case PlayerAnimationType.MeleeAction:
            case >= PlayerAnimationType.HeldMeleeActionStart and <= PlayerAnimationType.HeldMeleeActionCancel:
                return currentActionState == ActionState.None;

            default:
                return false;
        }
    }

    protected override void UpdateHandlerInternal(float deltaTime)
    {
        // Base class handles held action continuation
    }

    #endregion

    #region Unified System Configuration

    /// <summary>
    /// Primary action is held for bow (draw and shoot)
    /// </summary>
    protected override bool ShouldPrimaryActionBeHeld(InputContext context) => true;

    /// <summary>
    /// Secondary action is instant (ADS toggle)
    /// </summary>
    protected override bool ShouldSecondaryActionBeHeld(InputContext context) => false;

    #endregion

    #region Held Action Events

    /// <summary>
    /// Called when starting to draw the bow
    /// </summary>
    protected override void OnHeldActionStarting(HeldActionType heldType, PlayerAnimationType actionType, InputContext context)
    {
        base.OnHeldActionStarting(heldType, actionType, context);

        if (heldType == HeldActionType.Primary)
        {
            DebugLog("Starting bow draw");
            PlaySound(drawSound);
        }
    }

    /// <summary>
    /// Called when bow draw is complete and ready to shoot
    /// </summary>
    protected override void OnPrimaryActionReady()
    {
        base.OnPrimaryActionReady();
        isDrawn = true;
        DebugLog("Bow fully drawn - ready to shoot");
    }

    /// <summary>
    /// Called when bow is shot (held action executed)
    /// UPDATED: Consumes nocked arrow and auto-nocks next
    /// </summary>
    protected override void ExecutePrimaryAction(PlayerAnimationType actionType)
    {
        base.ExecutePrimaryAction(actionType);
        DebugLog("Executing bow shot");

        // Play shoot sound
        PlaySound(shootSound);

        // Execute the shot
        FireArrow();

        // Fire events
        OnBowShot?.Invoke();
    }

    /// <summary>
    /// Called when primary action completes
    /// </summary>
    protected override void OnPrimaryActionCompleted(PlayerAnimationType actionType)
    {
        base.OnPrimaryActionCompleted(actionType);

        // Reset bow state
        isDrawn = false;
        DebugLog("Bow shot completed - ready for new actions");

        // Auto-nock next arrow after a short delay
        Invoke(nameof(AutoNockArrow), 0.1f);
    }

    /// <summary>
    /// Called when held action is cancelled
    /// </summary>
    protected override void OnHeldActionCancelled(PlayerAnimationType actionType)
    {
        base.OnHeldActionCancelled(actionType);

        if (currentHeldActionType == HeldActionType.Primary)
        {
            isDrawn = false;
            DebugLog("Bow draw cancelled");
        }
    }

    #endregion

    #region Bow Shooting

    /// <summary>
    /// Fire an arrow from the bow
    /// UPDATED: Consumes nocked arrow from instance data
    /// </summary>
    private void FireArrow()
    {
        if (BowData == null || BowInstanceData == null)
        {
            DebugLog("Cannot fire - no bow data or instance");
            return;
        }

        // Consume the nocked arrow
        if (!BowInstanceData.ConsumeArrow())
        {
            DebugLog("Cannot fire - no arrow nocked");
            return;
        }

        // Update display
        RefreshArrowDisplay();

        DebugLog($"Firing arrow - Damage: {BowData.damage}, Range: {BowData.range}");
        DebugLog($"Arrows remaining: {inventoryArrows}");

        // TODO: Implement arrow projectile spawning
        // TODO: Apply damage to targets

        // Fire events
        OnArrowStatusChanged?.Invoke(arrowNocked, inventoryArrows);

        DebugLog("Arrow fired successfully");
    }

    #endregion

    #region Arrow Nocking System

    /// <summary>
    /// Start nocking arrow process (manual reload)
    /// </summary>
    private void StartNockingArrow()
    {
        if (!CanNockArrow())
        {
            DebugLog("Cannot nock arrow");
            return;
        }

        isReloading = true;
        DebugLog("Starting to nock arrow");

        PlaySound(reloadSound);
        TriggerInstantAction(PlayerAnimationType.ReloadAction);
    }

    /// <summary>
    /// Auto-nock arrow after shooting or on equip
    /// </summary>
    private void AutoNockArrow()
    {
        if (currentActionState != ActionState.None || isReloading)
        {
            DebugLog("Cannot auto-nock - action in progress");
            return;
        }

        if (HasArrowNocked())
        {
            DebugLog("Arrow already nocked");
            return;
        }

        if (GetInventoryArrows() <= 0)
        {
            DebugLog("No arrows available to nock");
            return;
        }

        DebugLog("Auto-nocking arrow");
        NockArrowFromInventory();
    }

    /// <summary>
    /// Complete nocking arrow process
    /// UPDATED: Takes arrow from inventory
    /// </summary>
    protected override void OnActionCompletedInternal(PlayerAnimationType actionType)
    {
        base.OnActionCompletedInternal(actionType);

        if (actionType == PlayerAnimationType.ReloadAction)
        {
            CompleteNockingArrow();
        }
    }

    /// <summary>
    /// Complete the arrow nocking process
    /// </summary>
    private void CompleteNockingArrow()
    {
        if (!isReloading) return;

        isReloading = false;

        if (NockArrowFromInventory())
        {
            DebugLog("Arrow nocking completed successfully");
            OnBowReloaded?.Invoke();
        }
        else
        {
            DebugLog("Arrow nocking failed - no arrows available");
        }
    }

    /// <summary>
    /// Nock an arrow from inventory
    /// UPDATED: Uses AmmoInventoryHelper to consume arrow from inventory
    /// </summary>
    private bool NockArrowFromInventory()
    {
        if (BowData == null || BowInstanceData == null)
        {
            DebugLog("Cannot nock arrow - missing bow data");
            return false;
        }

        // Get required arrow type
        ItemData requiredArrows = BowData.requiredAmmoType;
        if (requiredArrows == null)
        {
            Debug.LogWarning($"[BowHandler] Bow {currentItemData.itemName} has no required ammo type!");
            return false;
        }

        // Check if arrows available
        if (!AmmoInventoryHelper.HasCompatibleAmmo(requiredArrows))
        {
            DebugLog("No arrows available in inventory");
            BowInstanceData.ClearArrow();
            RefreshArrowDisplay();
            OnArrowStatusChanged?.Invoke(false, 0);
            return false;
        }

        // Take one arrow from inventory (this will remove empty stacks automatically)
        bool arrowTaken = AmmoInventoryHelper.TakeSingleAmmo(requiredArrows);

        if (arrowTaken)
        {
            // Nock the arrow in instance data
            BowInstanceData.NockArrow();
            DebugLog("Arrow nocked successfully");

            // Update display
            RefreshArrowDisplay();
            OnArrowStatusChanged?.Invoke(true, inventoryArrows);
            return true;
        }
        else
        {
            DebugLog("Failed to take arrow from inventory");
            return false;
        }
    }

    #endregion

    #region ADS System

    /// <summary>
    /// Start aiming down sights
    /// </summary>
    private void StartAiming()
    {
        if (!CanAim())
        {
            DebugLog("Cannot start aiming");
            return;
        }

        isAiming = true;
        DebugLog("Started aiming down sights");

        if (adsController != null)
            adsController.StartAimingDownSights();
    }

    /// <summary>
    /// Stop aiming down sights
    /// </summary>
    private void StopAiming()
    {
        if (!isAiming) return;

        isAiming = false;
        DebugLog("Stopped aiming down sights");

        if (adsController != null)
            adsController.StopAimingDownSights();
    }

    #endregion

    #region Arrow Display & Tracking

    /// <summary>
    /// Refresh arrow display values from instance data and inventory
    /// </summary>
    private void RefreshArrowDisplay()
    {
        arrowNocked = HasArrowNocked();
        inventoryArrows = GetInventoryArrows();
    }

    /// <summary>
    /// Check if arrow is nocked from instance data
    /// </summary>
    private bool HasArrowNocked()
    {
        return BowInstanceData?.hasArrowNocked ?? false;
    }

    /// <summary>
    /// Get total arrows available in inventory
    /// </summary>
    private int GetInventoryArrows()
    {
        if (BowData?.requiredAmmoType == null)
            return 0;

        return AmmoInventoryHelper.GetTotalAmmoCount(BowData.requiredAmmoType);
    }

    #endregion

    #region State Validation

    /// <summary>
    /// Check if bow can be drawn
    /// UPDATED: Checks for nocked arrow
    /// </summary>
    private bool CanDrawBow(PlayerStateType playerState)
    {
        if (BowData == null) return false;
        if (isReloading) return false;
        if (!HasArrowNocked()) return false;
        if (!currentItemData.CanUseInState(playerState)) return false;

        // Allow drawing during Starting and Looping for held actions
        if (currentActionState != ActionState.None &&
            currentActionState != ActionState.Starting &&
            currentActionState != ActionState.Looping)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Check if can aim
    /// </summary>
    private bool CanAim()
    {
        if (BowData == null) return false;
        if (isReloading) return false;
        return IsPlayerInValidState();
    }

    /// <summary>
    /// Check if can nock arrow
    /// UPDATED: Checks inventory for arrows
    /// </summary>
    private bool CanNockArrow()
    {
        if (BowData == null || BowInstanceData == null) return false;
        if (isReloading) return false;
        if (currentActionState != ActionState.None) return false;
        if (HasArrowNocked()) return false;

        // Check if arrows available in inventory
        if (GetInventoryArrows() <= 0)
        {
            DebugLog("Cannot nock - no arrows in inventory");
            return false;
        }

        return true;
    }

    #endregion

    #region Utility

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

    /// <summary>Check if currently drawing</summary>
    public bool IsDrawing() => currentHeldActionType == HeldActionType.Primary &&
                               (currentActionState == ActionState.Starting || currentActionState == ActionState.Looping);

    /// <summary>Check if ready to shoot</summary>
    public bool IsReadyToShoot() => isDrawn && currentActionState == ActionState.Looping && HasArrowNocked();

    /// <summary>Check if currently nocking arrow</summary>
    public bool IsNocking() => isReloading;

    /// <summary>Check if currently aiming</summary>
    public bool IsAiming() => isAiming;

    /// <summary>Check if arrow is nocked</summary>
    public bool HasArrow() => HasArrowNocked();

    /// <summary>Check if arrows available (nocked or inventory)</summary>
    public bool HasArrows() => HasArrowNocked() || GetInventoryArrows() > 0;

    /// <summary>Get arrow status string for UI</summary>
    public string GetArrowStatus()
    {
        if (HasArrowNocked())
            return $"Ready ({inventoryArrows})";
        else if (inventoryArrows > 0)
            return $"No Arrow ({inventoryArrows})";
        else
            return "No Arrows";
    }

    /// <summary>Get current bow damage</summary>
    public float GetBowDamage() => BowData?.damage ?? 0f;

    /// <summary>Get current bow range</summary>
    public float GetBowRange() => BowData?.range ?? 50f;

    /// <summary>Force stop all bow actions</summary>
    public void ForceStopAllActions()
    {
        if (isAiming) StopAiming();
        if (currentActionState != ActionState.None) ForceCompleteAction();
        isDrawn = false;
        isReloading = false;
        DebugLog("Force stopped all bow actions");
    }

    /// <summary>Debug info</summary>
    public override string GetDebugInfo()
    {
        return $"{GetType().Name} - Active: {isActive}, " +
               $"Item: {currentItemData?.itemName ?? "None"}, " +
               $"Action State: {currentActionState} ({currentActionAnimation}), " +
               $"Held Action: {currentHeldActionType}, " +
               $"Arrow Nocked: {arrowNocked}, Inventory: {inventoryArrows}, " +
               $"Is Drawn: {isDrawn}, Nocking: {isReloading}, Aiming: {isAiming}";
    }

    #endregion
}