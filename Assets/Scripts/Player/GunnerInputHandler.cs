using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles input for a gunner role.
/// Wraps a PlayerInput component and provides turret control inputs.
/// </summary>
public class GunnerInputHandler : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private PlayerInput playerInput;

    // Input Actions
    private InputAction lookAction;
    private InputAction shootAction;

    // Public Properties
    public Vector2 LookInput { get; private set; }
    public bool ShootPressed { get; private set; }
    public bool ShootHeld { get; private set; }

    // Events
    public event Action OnShootPressed;
    public event Action OnShootReleased;

    // State
    public bool IsActive { get; private set; } = true;
    public int PlayerIndex { get; private set; } = -1;
    public int GunnerNumber { get; private set; } = 0; // 0-3 for gunners 1-4

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            Debug.LogError("[GunnerInputHandler] No PlayerInput component found!");
            return;
        }

        PlayerIndex = playerInput.playerIndex;
        SetupInputActions();
        DebugLog($"Gunner input handler initialized for player {PlayerIndex}");
    }

    private void SetupInputActions()
    {
        // Get the Gunner action map
        var gunnerMap = playerInput.actions.FindActionMap("Gunner");
        if (gunnerMap == null)
        {
            Debug.LogError("[GunnerInputHandler] Gunner action map not found!");
            return;
        }

        // Find actions
        lookAction = gunnerMap.FindAction("Look");
        shootAction = gunnerMap.FindAction("Shoot");

        // Subscribe to button events
        if (shootAction != null)
        {
            shootAction.performed += OnShootPerformed;
            shootAction.canceled += OnShootCanceled;
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
        // Read look input
        LookInput = lookAction?.ReadValue<Vector2>() ?? Vector2.zero;

        // Update held state
        ShootHeld = shootAction?.IsPressed() ?? false;

        // Reset pressed state after it's been read
        if (ShootPressed) ShootPressed = false;
    }

    #region Input Event Handlers

    private void OnShootPerformed(InputAction.CallbackContext context)
    {
        ShootPressed = true;
        OnShootPressed?.Invoke();
        DebugLog("Shoot pressed");
    }

    private void OnShootCanceled(InputAction.CallbackContext context)
    {
        OnShootReleased?.Invoke();
        DebugLog("Shoot released");
    }

    #endregion

    public void SetActive(bool active)
    {
        IsActive = active;
        DebugLog($"Input handler set to {(active ? "active" : "inactive")}");
    }

    public void SetGunnerNumber(int gunnerNum)
    {
        GunnerNumber = gunnerNum;
        DebugLog($"Assigned as Gunner {gunnerNum + 1}");
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (shootAction != null)
        {
            shootAction.performed -= OnShootPerformed;
            shootAction.canceled -= OnShootCanceled;
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[GunnerInputHandler P{PlayerIndex} G{GunnerNumber + 1}] {message}");
        }
    }

    // Utility methods
    public bool IsLooking() => LookInput.magnitude > 0.1f;
}