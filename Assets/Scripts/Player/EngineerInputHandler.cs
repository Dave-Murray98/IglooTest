using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles input for the engineer role.
/// Wraps a PlayerInput component and provides repair and region selection inputs.
/// </summary>
public class EngineerInputHandler : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private PlayerInput playerInput;

    // Input Actions
    private InputAction selectModeAction;
    private InputAction regionSelectionAction;
    private InputAction repairAction;

    // Public Properties
    public bool SelectModeActive { get; private set; }
    public Vector2 RegionSelectionInput { get; private set; }
    public bool RepairButtonPressed { get; private set; }
    public bool RepairButtonHeld { get; private set; }

    // Events
    public event Action OnSelectModeEntered;
    public event Action OnSelectModeExited;
    public event Action OnRepairButtonPressed;
    public event Action OnRepairButtonReleased;

    // State
    public bool IsActive { get; private set; } = true;
    public int PlayerIndex { get; private set; } = -1;

    private bool wasInSelectMode = false;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            Debug.LogError("[EngineerInputHandler] No PlayerInput component found!");
            return;
        }

        PlayerIndex = playerInput.playerIndex;
        SetupInputActions();
        DebugLog($"Engineer input handler initialized for player {PlayerIndex}");
    }

    private void SetupInputActions()
    {
        // Get the Engineer action map
        var engineerMap = playerInput.actions.FindActionMap("Engineer");
        if (engineerMap == null)
        {
            Debug.LogError("[EngineerInputHandler] Engineer action map not found!");
            return;
        }

        // Find actions
        selectModeAction = engineerMap.FindAction("SelectMode");
        regionSelectionAction = engineerMap.FindAction("RegionSelection");
        repairAction = engineerMap.FindAction("Repair");

        // Subscribe to button events
        if (repairAction != null)
        {
            repairAction.performed += OnRepairPerformed;
            repairAction.canceled += OnRepairCanceled;
        }

        DebugLog("Input actions setup complete");
    }

    private void Update()
    {
        if (!IsActive || playerInput == null) return;

        UpdateInputValues();
        CheckSelectModeTransitions();
    }

    private void UpdateInputValues()
    {
        // Read select mode (left trigger held)
        SelectModeActive = selectModeAction?.IsPressed() ?? false;

        // Read region selection input (D-pad or left stick)
        RegionSelectionInput = regionSelectionAction?.ReadValue<Vector2>() ?? Vector2.zero;

        // Update repair button held state
        RepairButtonHeld = repairAction?.IsPressed() ?? false;

        // Reset pressed state after it's been read
        if (RepairButtonPressed) RepairButtonPressed = false;
    }

    private void CheckSelectModeTransitions()
    {
        // Check if we just entered select mode
        if (SelectModeActive && !wasInSelectMode)
        {
            OnSelectModeEntered?.Invoke();
            DebugLog("Entered select mode");
        }
        // Check if we just exited select mode
        else if (!SelectModeActive && wasInSelectMode)
        {
            OnSelectModeExited?.Invoke();
            DebugLog("Exited select mode");
        }

        wasInSelectMode = SelectModeActive;
    }

    #region Input Event Handlers

    private void OnRepairPerformed(InputAction.CallbackContext context)
    {
        RepairButtonPressed = true;
        OnRepairButtonPressed?.Invoke();
        DebugLog("Repair button pressed");
    }

    private void OnRepairCanceled(InputAction.CallbackContext context)
    {
        OnRepairButtonReleased?.Invoke();
        DebugLog("Repair button released");
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
        if (repairAction != null)
        {
            repairAction.performed -= OnRepairPerformed;
            repairAction.canceled -= OnRepairCanceled;
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[EngineerInputHandler P{PlayerIndex}] {message}");
        }
    }

    // Utility methods
    public bool HasRegionSelectionInput() => RegionSelectionInput.magnitude > 0.1f;

    public Gamepad GetAssignedGamepad()
    {
        // Get the actual device this PlayerInput is using
        if (playerInput != null && playerInput.devices.Count > 0)
        {
            // Check each device to find a gamepad
            foreach (var device in playerInput.devices)
            {
                if (device is Gamepad gamepad)
                {
                    return gamepad;
                }
            }
        }

        // No gamepad found for this player
        return null;
    }
}