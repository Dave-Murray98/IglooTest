using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A single unified input handler that exposes all possible inputs a player might need,
/// regardless of their current role. Replaces the three separate PilotInputHandler,
/// EngineerInputHandler, and GunnerInputHandler scripts.
///
/// Always reads from the "Pilot" action map, which must contain all bindings:
///   Left stick          → Move (pilot throttle/steering + engineer region selection)
///   Left trigger        → EngineerSelect (enter engineer select mode)
///   Right stick         → GunAim
///   Right trigger       → Shoot
///   Gamepad West (X/□)  → Repair
///   RB                  → Surface
///   LB                  → Dive
/// </summary>
public class MultiRoleInputHandler : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private PlayerInput playerInput;

    // -------------------------------------------------------------------------
    // Input Actions
    // -------------------------------------------------------------------------
    private InputAction moveAction;
    private InputAction surfaceAction;
    private InputAction diveAction;
    private InputAction engineerSelectAction;
    private InputAction gunAimAction;
    private InputAction shootAction;
    private InputAction repairAction;

    // -------------------------------------------------------------------------
    // Public Properties
    // -------------------------------------------------------------------------

    /// <summary>Left stick. Pilot throttle/steering; also engineer region selection.</summary>
    public Vector2 MovementInput { get; private set; }

    /// <summary>True while left trigger is held.</summary>
    public bool EngineerSelectHeld { get; private set; }

    /// <summary>Right stick. Gun aiming. Always active regardless of engineer mode.</summary>
    public Vector2 GunAimInput { get; private set; }

    /// <summary>True while right trigger is held.</summary>
    public bool ShootHeld { get; private set; }

    /// <summary>True while surface button is held.</summary>
    public bool SurfaceHeld { get; private set; }

    /// <summary>True while dive button is held.</summary>
    public bool DiveHeld { get; private set; }

    // -------------------------------------------------------------------------
    // Events
    // -------------------------------------------------------------------------

    /// <summary>Fired once when engineer select button (left trigger) is pressed.</summary>
    public event Action OnEngineerSelectEntered;

    /// <summary>Fired once when engineer select button is released.</summary>
    public event Action OnEngineerSelectExited;

    /// <summary>Fired once per shoot button press (right trigger).</summary>
    public event Action OnShootPressed;

    /// <summary>Fired once when shoot button is released.</summary>
    public event Action OnShootReleased;

    /// <summary>Fired once per repair button press (Gamepad West / X / □).</summary>
    public event Action OnRepairPressed;

    /// <summary>Fired once when surface button is pressed.</summary>
    public event Action OnSurfacePressed;

    /// <summary>Fired once when surface button is released.</summary>
    public event Action OnSurfaceReleased;

    /// <summary>Fired once when dive button is pressed.</summary>
    public event Action OnDivePressed;

    /// <summary>Fired once when dive button is released.</summary>
    public event Action OnDiveReleased;

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    public bool IsActive { get; private set; } = true;
    public int PlayerIndex { get; private set; } = -1;

    private bool wasEngineerSelectHeld = false;

    // -------------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------------

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            Debug.LogError("[MultiRoleInputHandler] No PlayerInput component found!");
            return;
        }

        PlayerIndex = playerInput.playerIndex;
        SetupInputActions();
        DebugLog($"Initialized for Player {PlayerIndex}");
    }

    private void SetupInputActions()
    {
        var pilotMap = playerInput.actions.FindActionMap("Pilot");
        if (pilotMap == null)
        {
            Debug.LogError("[MultiRoleInputHandler] 'Pilot' action map not found!");
            return;
        }

        moveAction = pilotMap.FindAction("Move");
        surfaceAction = pilotMap.FindAction("Surface");
        diveAction = pilotMap.FindAction("Dive");
        engineerSelectAction = pilotMap.FindAction("EngineerSelect");
        gunAimAction = pilotMap.FindAction("GunAim");
        shootAction = pilotMap.FindAction("Shoot");
        repairAction = pilotMap.FindAction("Repair");

        // Use named methods instead of lambdas so we can unsubscribe cleanly in OnDestroy
        if (shootAction != null)
        {
            shootAction.performed += OnShootPerformed;
            shootAction.canceled += OnShootCanceled;
        }

        if (repairAction != null)
        {
            repairAction.performed += OnRepairPerformed;
        }

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

    // -------------------------------------------------------------------------
    // Per-frame update
    // -------------------------------------------------------------------------

    private void Update()
    {
        if (!IsActive || playerInput == null) return;

        ReadInputValues();
        CheckEngineerSelectTransition();
    }

    private void ReadInputValues()
    {
        MovementInput = moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        GunAimInput = gunAimAction?.ReadValue<Vector2>() ?? Vector2.zero;
        SurfaceHeld = surfaceAction?.IsPressed() ?? false;
        DiveHeld = diveAction?.IsPressed() ?? false;
        ShootHeld = shootAction?.IsPressed() ?? false;
        EngineerSelectHeld = engineerSelectAction?.IsPressed() ?? false;
    }

    private void CheckEngineerSelectTransition()
    {
        if (EngineerSelectHeld && !wasEngineerSelectHeld)
        {
            OnEngineerSelectEntered?.Invoke();
            DebugLog("Engineer select entered");
        }
        else if (!EngineerSelectHeld && wasEngineerSelectHeld)
        {
            OnEngineerSelectExited?.Invoke();
            DebugLog("Engineer select exited");
        }

        wasEngineerSelectHeld = EngineerSelectHeld;
    }

    // -------------------------------------------------------------------------
    // Input callbacks — named methods so they can be unsubscribed cleanly
    // -------------------------------------------------------------------------

    private void OnShootPerformed(InputAction.CallbackContext ctx) => OnShootPressed?.Invoke();
    private void OnShootCanceled(InputAction.CallbackContext ctx) => OnShootReleased?.Invoke();
    private void OnRepairPerformed(InputAction.CallbackContext ctx) => OnRepairPressed?.Invoke();
    private void OnSurfacePerformed(InputAction.CallbackContext ctx) => OnSurfacePressed?.Invoke();
    private void OnSurfaceCanceled(InputAction.CallbackContext ctx) => OnSurfaceReleased?.Invoke();
    private void OnDivePerformed(InputAction.CallbackContext ctx) => OnDivePressed?.Invoke();
    private void OnDiveCanceled(InputAction.CallbackContext ctx) => OnDiveReleased?.Invoke();

    // -------------------------------------------------------------------------
    // Utility
    // -------------------------------------------------------------------------

    public void SetActive(bool active)
    {
        IsActive = active;
        DebugLog($"Set to {(active ? "active" : "inactive")}");
    }

    public Gamepad GetAssignedGamepad()
    {
        if (playerInput == null) return null;
        foreach (var device in playerInput.devices)
        {
            if (device is Gamepad gamepad) return gamepad;
        }
        return null;
    }

    public bool IsMoving() => MovementInput.magnitude > 0.1f;
    public bool IsAiming() => GunAimInput.magnitude > 0.1f;

    private void OnDestroy()
    {
        if (shootAction != null)
        {
            shootAction.performed -= OnShootPerformed;
            shootAction.canceled -= OnShootCanceled;
        }
        if (repairAction != null)
        {
            repairAction.performed -= OnRepairPerformed;
        }
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
            Debug.Log($"[MultiRoleInputHandler P{PlayerIndex}] {message}");
    }
}