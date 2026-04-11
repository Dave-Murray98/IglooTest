using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shows or hides the controls canvas based on whether any connected player
/// is currently holding the ShowControls button (Options / Menu button).
///
/// Setup:
///   - Attach this script to any persistent GameObject in the scene.
///   - Assign the controlsCanvas field to your in-world Canvas GameObject.
///   - Add "ShowControls" to the "UI" action map in your Input Actions asset,
///     bound to the Options/Menu button on gamepad.
///   - Make sure every player's PlayerInput has the "UI" action map available.
///
/// How it works:
///   Each frame it checks every connected PlayerInput for whether their
///   ShowControls action is held. If any one of them is held, the canvas
///   is shown. The moment all players release it, the canvas is hidden.
/// </summary>
public class ControlsDisplayManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The in-world Canvas GameObject to show/hide.")]
    [SerializeField] private GameObject controlsUIObject;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Tracks the ShowControls action for each connected PlayerInput.
    // Key = PlayerInput, Value = the resolved ShowControls InputAction.
    private readonly Dictionary<PlayerInput, InputAction> showControlsActions
        = new Dictionary<PlayerInput, InputAction>();

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Start hidden
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
        var inputManager = FindFirstObjectByType<PlayerInputManager>();
        if (inputManager != null)
        {
            inputManager.onPlayerJoined -= HandlePlayerJoined;
            inputManager.onPlayerLeft -= HandlePlayerLeft;
        }

        showControlsActions.Clear();
    }

    private void Update()
    {
        if (controlsUIObject == null) return;

        controlsUIObject.SetActive(IsAnyPlayerHoldingShowControls());
    }

    // -------------------------------------------------------------------------
    // Player join / leave
    // -------------------------------------------------------------------------

    private void HandlePlayerJoined(PlayerInput playerInput)
    {
        RegisterPlayer(playerInput);
    }

    private void HandlePlayerLeft(PlayerInput playerInput)
    {
        showControlsActions.Remove(playerInput);
        DebugLog($"Player {playerInput.playerIndex} unregistered.");
    }

    /// <summary>
    /// Finds the ShowControls action in the player's UI action map and caches it.
    /// </summary>
    private void RegisterPlayer(PlayerInput playerInput)
    {
        if (showControlsActions.ContainsKey(playerInput)) return;

        InputActionMap uiMap = playerInput.actions.FindActionMap("UI");
        if (uiMap == null)
        {
            Debug.LogWarning($"[ControlsDisplayManager] Player {playerInput.playerIndex} " +
                             $"has no 'UI' action map.");
            return;
        }

        InputAction action = uiMap.FindAction("ShowControls");
        if (action == null)
        {
            Debug.LogWarning($"[ControlsDisplayManager] No 'ShowControls' action found " +
                             $"in the UI map for Player {playerInput.playerIndex}.");
            return;
        }

        showControlsActions[playerInput] = action;
        DebugLog($"Player {playerInput.playerIndex} registered.");
    }

    // -------------------------------------------------------------------------
    // Core logic
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true if at least one registered player is currently holding
    /// the ShowControls button.
    /// </summary>
    private bool IsAnyPlayerHoldingShowControls()
    {
        foreach (KeyValuePair<PlayerInput, InputAction> kvp in showControlsActions)
        {
            if (kvp.Value.IsPressed())
                return true;
        }
        return false;
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