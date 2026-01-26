using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Interface for all equipped item handlers.
/// Each item type (weapon, consumable, tool, etc.) implements this to handle type-specific behavior.
/// REFACTORED: Now supports both instant and held actions properly.
/// </summary>
public interface IEquippedItemHandler
{
    /// <summary>The item type this handler manages</summary>
    ItemType HandledItemType { get; }

    /// <summary>Whether this handler is currently active</summary>
    bool IsActive { get; }

    /// <summary>The currently handled item data</summary>
    ItemData CurrentItemData { get; }

    /// <summary>Called when this handler's item type is equipped</summary>
    void OnItemEquipped(ItemData itemData);

    /// <summary>Called when this handler's item is unequipped</summary>
    void OnItemUnequipped();

    /// <summary>Handle primary action input (left click, shoot, use, consume, etc.)</summary>
    void HandlePrimaryAction(InputContext context);

    /// <summary>Handle secondary action input (right click, aim, reload, etc.)</summary>
    void HandleSecondaryAction(InputContext context);

    /// <summary>Handle reload action input (R key)</summary>
    void HandleReloadAction(InputContext context);

    /// <summary>Handle cancel action input (for cancelling held actions)</summary>
    void HandleCancelAction(InputContext context);

    /// <summary>Check if a specific action can be performed in current state</summary>
    bool CanPerformAction(string actionType, PlayerStateType playerState);

    /// <summary>Update handler state (called every frame while active)</summary>
    void UpdateHandler(float deltaTime);

    /// <summary>Get debug information about this handler</summary>
    string GetDebugInfo();
}

/// <summary>
/// Input context passed to handlers containing all relevant input and state information
/// </summary>
[System.Serializable]
public struct InputContext
{
    [Header("Action State")]
    public bool isPressed;      // Action was just pressed this frame
    public bool isHeld;         // Action is currently being held
    public bool isReleased;     // Action was just released this frame

    [Header("Player State")]
    public PlayerStateType currentPlayerState;
    public bool isMoving;
    public bool isCrouching;
    public bool isRunning;

    [Header("Look Input")]
    public Vector2 lookInput;
    public Vector3 lookDirection;

    [Header("Context")]
    public float deltaTime;
    public bool canPerformActions; // Whether player can perform actions (not paused, not in UI, etc.)

    /// <summary>Create an input context from current game state</summary>
    public static InputContext Create(bool pressed, bool held, bool released, PlayerStateType playerState, bool canAct = true)
    {
        var inputManager = InputManager.Instance;
        var playerController = Object.FindFirstObjectByType<PlayerController>();

        return new InputContext
        {
            isPressed = pressed,
            isHeld = held,
            isReleased = released,
            currentPlayerState = playerState,
            isMoving = playerController?.IsMoving ?? false,
            isCrouching = playerController?.IsCrouching ?? false,
            isRunning = playerController?.IsSprinting ?? false,
            lookInput = inputManager?.LookInput ?? Vector2.zero,
            lookDirection = playerController?.transform.forward ?? Vector3.forward,
            deltaTime = Time.deltaTime,
            canPerformActions = canAct && !(GameManager.Instance?.isPaused ?? false)
        };
    }
}

/// <summary>
/// REFACTORED: Action types to distinguish between instant and held actions
/// </summary>
public enum ActionType
{
    Instant,    // Single animation, complete when done (melee, consume, reload)
    Held        // Start → Loop → End/Cancel pattern (bow draw, throwable aim)
}

/// <summary>
/// REFACTORED: Action state for tracking held actions properly
/// </summary>
public enum ActionState
{
    None,           // No action being performed
    Starting,       // Held action starting (playing start animation)
    Looping,        // Held action looping (playing loop animation)
    Ending,         // Held action ending (playing end animation)
    Cancelling,     // Held action being cancelled (playing cancel animation)
    Instant         // Instant action being performed
}