using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shows or hides the controls canvas based on whether any connected player
/// is currently holding the ShowControls button (Options / Menu button).
///
/// Setup:
///   - Attach this script to any GameObject in the scene.
///   - Assign the controlsUIObject field to your Canvas GameObject.
///   - Add a "ShowControls" action to the "Pilot" action map in your Input Actions
///     asset, bound to the Options/Menu button on gamepad.
///
/// How it works:
///   When any player presses ShowControls, a counter increments and the canvas
///   is shown. When they release it, the counter decrements. The canvas hides
///   when the counter reaches zero (i.e. all players have released the button).
///   This is more efficient than polling every frame.
/// </summary>
public class ControlsDisplayManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Canvas GameObject to show/hide.")]
    [SerializeField] private GameObject controlsUIObject;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Each registered player maps to their ShowControls InputAction,
    // so we can unsubscribe cleanly when they leave.
    private readonly Dictionary<PlayerInput, InputAction> showControlsActions
        = new Dictionary<PlayerInput, InputAction>();

    // Tracks how many players are currently holding ShowControls.
    // Canvas is visible whenever this is greater than zero.
    private int holdCount = 0;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (controlsUIObject != null)
            controlsUIObject.SetActive(false);
    }

    private void OnEnable()
    {
        var inputManager = FindFirstObjectByType<PlayerInputManager>();
        if (inputManager != null)
        {
            inputManager.onPlayerJoined += HandlePlayerJoined;
            inputManager.onPlayerLeft += HandlePlayerLeft;
        }
        else
        {
            Debug.LogWarning("[ControlsDisplayManager] No PlayerInputManager found in scene.");
        }

        // Register any players that already exist (e.g. if this script enables late)
        foreach (var pi in FindObjectsByType<PlayerInput>(FindObjectsSortMode.None))
            RegisterPlayer(pi);
    }

    private void OnDisable()
    {
        // Unsubscribe from all actions before clearing the dictionary
        foreach (var kvp in showControlsActions)
        {
            kvp.Value.performed -= OnShowControlsPressed;
            kvp.Value.canceled -= OnShowControlsReleased;
        }

        showControlsActions.Clear();
        holdCount = 0;

        // Ensure canvas is hidden if this script is disabled mid-hold
        if (controlsUIObject != null)
            controlsUIObject.SetActive(false);

        var inputManager = FindFirstObjectByType<PlayerInputManager>();
        if (inputManager != null)
        {
            inputManager.onPlayerJoined -= HandlePlayerJoined;
            inputManager.onPlayerLeft -= HandlePlayerLeft;
        }
    }

    // No Update() needed — canvas state is driven entirely by input events.

    // -------------------------------------------------------------------------
    // Player join / leave
    // -------------------------------------------------------------------------

    private void HandlePlayerJoined(PlayerInput playerInput) => RegisterPlayer(playerInput);

    private void HandlePlayerLeft(PlayerInput playerInput)
    {
        if (!showControlsActions.TryGetValue(playerInput, out var action)) return;

        // If this player was holding the button when they left, decrement the count
        if (action.IsPressed())
            DecrementHoldCount();

        action.performed -= OnShowControlsPressed;
        action.canceled -= OnShowControlsReleased;
        showControlsActions.Remove(playerInput);

        DebugLog($"Player {playerInput.playerIndex} unregistered.");
    }

    /// <summary>
    /// Finds the ShowControls action in the player's Pilot action map and subscribes to it.
    /// </summary>
    private void RegisterPlayer(PlayerInput playerInput)
    {
        if (showControlsActions.ContainsKey(playerInput)) return;

        // ShowControls lives in the Pilot map — the single map all players use
        var pilotMap = playerInput.actions.FindActionMap("Pilot");
        if (pilotMap == null)
        {
            Debug.LogWarning($"[ControlsDisplayManager] Player {playerInput.playerIndex} " +
                             $"has no 'Pilot' action map.");
            return;
        }

        var action = pilotMap.FindAction("ShowControls");
        if (action == null)
        {
            Debug.LogWarning($"[ControlsDisplayManager] No 'ShowControls' action found " +
                             $"in the Pilot map for Player {playerInput.playerIndex}.");
            return;
        }

        action.performed += OnShowControlsPressed;
        action.canceled += OnShowControlsReleased;
        showControlsActions[playerInput] = action;

        DebugLog($"Player {playerInput.playerIndex} registered.");
    }

    // -------------------------------------------------------------------------
    // Input callbacks
    // -------------------------------------------------------------------------

    private void OnShowControlsPressed(InputAction.CallbackContext ctx)
    {
        holdCount++;
        UpdateCanvasVisibility();
        DebugLog($"ShowControls pressed — hold count: {holdCount}");
    }

    private void OnShowControlsReleased(InputAction.CallbackContext ctx)
    {
        DecrementHoldCount();
        DebugLog($"ShowControls released — hold count: {holdCount}");
    }

    private void DecrementHoldCount()
    {
        holdCount = Mathf.Max(0, holdCount - 1);
        UpdateCanvasVisibility();
    }

    private void UpdateCanvasVisibility()
    {
        if (controlsUIObject != null)
            controlsUIObject.SetActive(holdCount > 0);
    }

    // -------------------------------------------------------------------------
    // Debug
    // -------------------------------------------------------------------------

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[ControlsDisplayManager] {message}");
    }
}