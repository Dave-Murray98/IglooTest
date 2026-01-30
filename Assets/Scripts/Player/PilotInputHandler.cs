using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles input for the pilot role.
/// Wraps a PlayerInput component and provides submarine control inputs.
/// </summary>
public class PilotInputHandler : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private PlayerInput playerInput;

    // Input Actions
    private InputAction moveAction;
    private InputAction surfaceAction;
    private InputAction diveAction;

    // Public Properties - matches old InputManager interface
    public Vector2 MovementInput { get; private set; }
    public bool SurfaceHeld { get; private set; }
    public bool DiveHeld { get; private set; }
    public bool SurfacePressed { get; private set; }
    public bool DivePressed { get; private set; }

    // Events
    public event Action OnSurfacePressed;
    public event Action OnSurfaceReleased;
    public event Action OnDivePressed;
    public event Action OnDiveReleased;

    // State
    public bool IsActive { get; private set; } = true;
    public int PlayerIndex { get; private set; } = -1;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            Debug.LogError("[PilotInputHandler] No PlayerInput component found!");
            return;
        }

        PlayerIndex = playerInput.playerIndex;
        SetupInputActions();
        DebugLog($"Pilot input handler initialized for player {PlayerIndex}");
    }

    private void SetupInputActions()
    {
        // Get the Pilot action map
        var pilotMap = playerInput.actions.FindActionMap("Pilot");
        if (pilotMap == null)
        {
            Debug.LogError("[PilotInputHandler] Pilot action map not found!");
            return;
        }

        // Find actions
        moveAction = pilotMap.FindAction("Move");
        surfaceAction = pilotMap.FindAction("Surface");
        diveAction = pilotMap.FindAction("Dive");

        // Subscribe to button events
        if (surfaceAction != null)
        {
            surfaceAction.performed += OnSurfacePerformed;
            surfaceAction.canceled += OnSurfaceCanceled;
        }

        if (diveAction != null)
        {
            diveAction.performed += OnDivePerformed;
            diveAction.canceled += OnDiveCanceled;
        }

        DebugLog("Input actions setup complete");
    }

    private void Update()
    {
        if (!IsActive || playerInput == null) return;

        UpdateInputValues();
    }

    private void UpdateInputValues()
    {
        // Read movement input
        MovementInput = moveAction?.ReadValue<Vector2>() ?? Vector2.zero;

        // Update held states
        SurfaceHeld = surfaceAction?.IsPressed() ?? false;
        DiveHeld = diveAction?.IsPressed() ?? false;

        // Reset pressed states after they've been read
        if (SurfacePressed) SurfacePressed = false;
        if (DivePressed) DivePressed = false;
    }

    #region Input Event Handlers

    private void OnSurfacePerformed(InputAction.CallbackContext context)
    {
        SurfacePressed = true;
        OnSurfacePressed?.Invoke();
        DebugLog("Surface pressed");
    }

    private void OnSurfaceCanceled(InputAction.CallbackContext context)
    {
        OnSurfaceReleased?.Invoke();
        DebugLog("Surface released");
    }

    private void OnDivePerformed(InputAction.CallbackContext context)
    {
        DivePressed = true;
        OnDivePressed?.Invoke();
        DebugLog("Dive pressed");
    }

    private void OnDiveCanceled(InputAction.CallbackContext context)
    {
        OnDiveReleased?.Invoke();
        DebugLog("Dive released");
    }

    #endregion

    public void SetActive(bool active)
    {
        IsActive = active;
        DebugLog($"Input handler set to {(active ? "active" : "inactive")}");
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (surfaceAction != null)
        {
            surfaceAction.performed -= OnSurfacePerformed;
            surfaceAction.canceled -= OnSurfaceCanceled;
        }

        if (diveAction != null)
        {
            diveAction.performed -= OnDivePerformed;
            diveAction.canceled -= OnDiveCanceled;
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PilotInputHandler P{PlayerIndex}] {message}");
        }
    }

    // Utility methods
    public bool IsMoving() => MovementInput.magnitude > 0.1f;
}