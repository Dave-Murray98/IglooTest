using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
///  ConsumableHandler now uses ItemInstance for per-item use tracking
/// Primary action: Consume item (instant)
/// Secondary action: Not used
/// UPDATED: Now uses GetEquippedInventoryItemId() for proper removal from inventory
/// </summary>
public class ConsumableHandler : BaseEquippedItemHandler
{
    [Header("Consumable State")]
    [SerializeField, ReadOnly] private bool isConsuming = false;

    //  Quick access to consumable instance data (not template!)
    private ConsumableInstanceData ConsumableInstanceData =>
        currentItemInstance?.ConsumableInstanceData;

    // Template data (read-only stats like effects, restore amounts)
    private ConsumableData ConsumableData => currentItemData?.ConsumableData;

    // Events
    public System.Action<ItemData> OnConsumableEquipped;
    public System.Action OnConsumableUnequipped;
    public System.Action<ItemData> OnConsumableUsed;
    // public System.Action<int> OnUsesChanged;

    public override ItemType HandledItemType => ItemType.Consumable;

    #region Base Handler Implementation

    protected override void OnItemEquippedInternal(ItemData itemData)
    {
        if (itemData?.itemType != ItemType.Consumable)
        {
            Debug.LogError($"ConsumableHandler received invalid item: {itemData?.itemName ?? "null"}");
            return;
        }

        isConsuming = false;

        //  Verify instance data
        if (!HasValidInstance())
        {
            Debug.LogWarning($"ConsumableHandler equipped {itemData.itemName} but has no valid ItemInstance!");
        }

        DebugLog($"Equipped consumable: {itemData.itemName}");
        OnConsumableEquipped?.Invoke(itemData);
        //OnUsesChanged?.Invoke(GetRemainingUses());
    }

    protected override void OnItemUnequippedInternal()
    {
        base.OnItemUnequippedInternal();
        isConsuming = false;
        DebugLog("Unequipped consumable");
        OnConsumableUnequipped?.Invoke();
    }

    protected override void HandlePrimaryActionInternal(InputContext context)
    {
        // Primary = Melee attack (using consumable as improvised weapon)
        if (context.isPressed)
        {
            HandleMeleeAction(context);
        }
    }

    protected override void HandleSecondaryActionInternal(InputContext context)
    {
        // Secondary = Consume item
        if (context.isPressed)
        {
            ConsumeItem();
        }
    }

    protected override void HandleReloadActionInternal(InputContext context)
    {
        // No reload action for consumables
    }

    protected override bool CanPerformActionInternal(PlayerAnimationType actionType, PlayerStateType playerState)
    {
        switch (actionType)
        {
            case PlayerAnimationType.SecondaryAction:
                return CanConsume(playerState);

            case PlayerAnimationType.PrimaryAction:
            case PlayerAnimationType.MeleeAction:
            case >= PlayerAnimationType.HeldMeleeActionStart and <= PlayerAnimationType.HeldMeleeActionCancel:
                return currentActionState == ActionState.None;

            default:
                return false;
        }
    }

    protected override void UpdateHandlerInternal(float deltaTime)
    {
        // No continuous updates needed
    }

    #endregion

    #region Consumption System

    /// <summary>
    /// Start consuming the item
    /// </summary>
    private void ConsumeItem()
    {
        if (!CanConsume(GetCurrentPlayerState()))
        {
            DebugLog("Cannot consume item in current state");
            return;
        }

        isConsuming = true;
        DebugLog($"Consuming item: {currentItemData.itemName}");

        TriggerInstantAction(PlayerAnimationType.SecondaryAction);

        //audio use sound will be called via an animation event
    }

    /// <summary>
    /// FIXED: Override OnActionCompletedInternal with consumption state protection
    /// </summary>
    protected override void OnActionCompletedInternal(PlayerAnimationType actionType)
    {
        DebugLog($"OnActionCompletedInternal called for: {actionType}, isConsuming: {isConsuming}");

        // Call base implementation first
        base.OnActionCompletedInternal(actionType);

        switch (actionType)
        {
            case PlayerAnimationType.SecondaryAction:
                // CRITICAL FIX: Only complete consumption if we're actually in consuming state
                if (isConsuming)
                {
                    CompleteConsumption();
                }
                else
                {
                    DebugLog("SecondaryAction completed but not in consuming state - ignoring");
                }
                break;

            case PlayerAnimationType.MeleeAction:
            case PlayerAnimationType.PrimaryAction:
            case >= PlayerAnimationType.HeldMeleeActionStart and <= PlayerAnimationType.HeldMeleeActionCancel:
                {
                    // Melee attack with consumable completed
                    DebugLog("Melee attack with consumable completed");
                }
                break;
        }
    }

    /// <summary>
    /// Execute the consumption and apply effects
    ///  Modifies instance data
    /// UPDATED: Now uses GetEquippedInventoryItemId() for proper removal
    /// </summary>
    private void CompleteConsumption()
    {
        if (!isConsuming) return;

        DebugLog("=== COMPLETING CONSUMPTION ===");

        // Apply consumable effects
        ApplyConsumableEffects();

        isConsuming = false;

        OnConsumableUsed?.Invoke(currentItemData);

        // CRITICAL: Get the INVENTORY item ID (not instance ID!)
        string inventoryItemId = EquippedItemManager.Instance.GetEquippedInventoryItemId();

        if (string.IsNullOrEmpty(inventoryItemId))
        {
            Debug.LogError($"[ConsumableHandler] Cannot remove consumed item - no inventory ID available! (ItemData: {currentItemData?.itemName})");
            return;
        }

        DebugLog($"Removing consumed item from inventory using ID: {inventoryItemId}");

        string currentItemName = currentItemData.itemName;
        string currentItemId = inventoryItemId;

        // Remove from inventory using the correct ID
        bool removed = PlayerInventoryManager.Instance.RemoveItem(inventoryItemId);

        if (removed)
        {
            DebugLog($"Successfully removed consumed item: {currentItemName} (InventoryID: {currentItemId})");
        }
        else
        {
            Debug.LogError($"[ConsumableHandler] Failed to remove consumed item from inventory! (InventoryID: {currentItemId})");
        }

        DebugLog($"Consumed item: {currentItemName}");
    }

    /// <summary>
    /// Apply the effects of consuming this item
    /// </summary>
    private void ApplyConsumableEffects()
    {
        if (ConsumableData == null) return;

        var playerHealth = playerController?.GetComponent<PlayerHealth>();
        var playerStamina = playerController?.GetComponent<PlayerStamina>();

        if (ConsumableData.effectAudioClip != null)
            AudioManager.Instance.PlaySound2D(ConsumableData.effectAudioClip, AudioCategory.UI);

        // Apply health restoration
        if (ConsumableData.healthRestore > 0 && playerHealth != null)
        {
            playerHealth.Heal(ConsumableData.healthRestore);
            DebugLog($"Restored {ConsumableData.healthRestore} health");
        }

        // TO DO: Apply other effects like stamina, buffs, etc.
    }

    #endregion

    #region State Validation

    /// <summary>
    /// Check if can consume item
    /// </summary>
    private bool CanConsume(PlayerStateType playerState)
    {
        if (ConsumableData == null)
        {
            DebugLog("Cannot consume - no consumable data");
            return false;
        }
        if (isConsuming)
        {
            DebugLog("Cannot consume - already consuming");
            return false;
        }

        if (!currentItemData.CanUseInState(playerState))
        {
            DebugLog($"Cannot consume - item cannot be used in state {playerState}");
            return false;
        }

        return true;
    }

    #endregion

    #region  Consumable Data Access (Instance-Aware)

    #endregion

    #region Public API

    /// <summary>Check if currently consuming</summary>
    public bool IsConsuming() => isConsuming;


    /// <summary>Debug info</summary>
    public override string GetDebugInfo()
    {
        return $"{GetType().Name} - Active: {isActive}, " +
               $"Item: {currentItemData?.itemName ?? "None"}, " +
               $"Instance Valid: {hasValidInstance}, " +
               $"Action State: {currentActionState}, " +
               $"Consuming: {isConsuming}";
    }

    #endregion
}