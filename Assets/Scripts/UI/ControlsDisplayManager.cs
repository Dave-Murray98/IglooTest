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
/// Why this uses PlayerRoleManager events instead of PlayerInputManager:
///   PlayerInputManager is set to "Invoke Unity Events", which means it does NOT
///   fire the C# onPlayerJoined / onPlayerLeft events. PlayerRoleManager wraps
///   those joins and exposes its own clean C# events that we can subscribe to safely.
/// </summary>
public class ControlsDisplayManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Canvas GameObject to show/hide.")]
    [SerializeField] private GameObject controlsUIObject;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Each registered PlayerInput maps to its ShowControls action
    // so we can unsubscribe cleanly when that player leaves.
    private readonly Dictionary<PlayerInput, InputAction> showControlsActions
        = new Dictionary<PlayerInput, InputAction>();

    // How many players are currently holding ShowControls.
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
        // Subscribe to PlayerRoleManager's C# events — these fire regardless
        // of which notification behaviour PlayerInputManager is using.
        PlayerRoleManager.OnPlayerJoinedEvent += HandlePlayerJoined;
        PlayerRoleManager.OnPlayerLeftEvent += HandlePlayerLeft;

        // Register any players already connected (e.g. if this script enables late)
        foreach (var pi in FindObjectsByType<PlayerInput>(FindObjectsSortMode.None))
            RegisterPlayer(pi);

        DebugLog("Subscribed to PlayerRoleManager events.");
    }

    private void OnDisable()
    {
        PlayerRoleManager.OnPlayerJoinedEvent -= HandlePlayerJoined;
        PlayerRoleManager.OnPlayerLeftEvent -= HandlePlayerLeft;

        // Unsubscribe from all input actions before clearing
        foreach (var kvp in showControlsActions)
        {
            kvp.Value.performed -= OnShowControlsPressed;
            kvp.Value.canceled -= OnShowControlsReleased;
        }

        showControlsActions.Clear();
        holdCount = 0;

        if (controlsUIObject != null)
            controlsUIObject.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // Player join / leave
    // PlayerRoleManager.OnPlayerJoined passes the PlayerInput's playerIndex (int),
    // so we find the matching PlayerInput ourselves.
    // -------------------------------------------------------------------------

    private void HandlePlayerJoined(int playerIndex)
    {
        DebugLog($"Player {playerIndex} joined — searching for PlayerInput...");

        // Find the PlayerInput that matches this index
        foreach (var pi in FindObjectsByType<PlayerInput>(FindObjectsSortMode.None))
        {
            if (pi.playerIndex == playerIndex)
            {
                RegisterPlayer(pi);
                return;
            }
        }

        Debug.LogWarning($"[ControlsDisplayManager] Could not find PlayerInput for index {playerIndex}.");
    }

    private void HandlePlayerLeft(int playerIndex)
    {
        // Find the registered entry that matches this player index
        PlayerInput leaving = null;
        foreach (var kvp in showControlsActions)
        {
            if (kvp.Key.playerIndex == playerIndex)
            {
                leaving = kvp.Key;
                break;
            }
        }

        if (leaving == null) return;

        // If they were holding the button when they left, correct the count
        if (showControlsActions[leaving].IsPressed())
            DecrementHoldCount();

        showControlsActions[leaving].performed -= OnShowControlsPressed;
        showControlsActions[leaving].canceled -= OnShowControlsReleased;
        showControlsActions.Remove(leaving);

        DebugLog($"Player {playerIndex} unregistered.");
    }

    /// <summary>
    /// Finds the ShowControls action in the player's Pilot action map and subscribes to it.
    /// </summary>
    private void RegisterPlayer(PlayerInput playerInput)
    {
        if (showControlsActions.ContainsKey(playerInput)) return;

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

        DebugLog($"Player {playerInput.playerIndex} registered successfully.");
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