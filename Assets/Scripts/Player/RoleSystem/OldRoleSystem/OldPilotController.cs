using NWH.Common.CoM;
using NWH.DWP2.ShipController;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls the submarine based on pilot input.
/// Renamed from PlayerController to clarify its role in multiplayer context.
/// Now reads from OldPilotInputHandler instead of the singleton InputManager.
/// </summary>
public class OldPilotController : MonoBehaviour
{
    [Header("Vehicle Controllers")]
    [SerializeField] private AdvancedShipController shipController;
    [SerializeField] private SubmarineBallastController ballastController;

    [Header("Submarine Config")]
    [SerializeField] private float maxThrottleInput = 1f;
    [SerializeField] private float maxSteeringInput = 1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Input handler reference
    private OldPilotInputHandler inputHandler;

    // Current input values
    [ShowInInspector, ReadOnly] private float currentThrottleInput = 0f;
    private float currentSteeringInput = 0f;
    private float currentBrake = 0f;

    [ShowInInspector, ReadOnly] private bool currentSurfaceInput;
    [ShowInInspector, ReadOnly] private bool currentDiveInput;

    private Gamepad assignedGamepad;

    private void Start()
    {
        if (shipController == null) shipController = GetComponent<AdvancedShipController>();
        if (ballastController == null) ballastController = GetComponent<SubmarineBallastController>();

        InitializeVehicleControllers();

        // Subscribe to pilot assignment
        OldPlayerRoleManager.OnPilotAssigned += OnPilotAssigned;

        // Check if pilot already exists
        if (OldPlayerRoleManager.Instance != null && OldPlayerRoleManager.Instance.HasPilot)
        {
            inputHandler = OldPlayerRoleManager.Instance.GetPilotHandler();
            DebugLog($"Connected to existing pilot (Player {inputHandler.PlayerIndex})");
        }
        else
        {
            DebugLog("Waiting for pilot to connect...");
        }

        SubmarineHealthManager.Instance.OnSubmarineTakenDamage += HandleRumble;
    }

    private void HandleRumble(float lowFrequency, float highFrequency, float duration)
    {
        RumbleManager.Instance.RumblePulse(assignedGamepad, lowFrequency, highFrequency, duration);
    }

    private void OnPilotAssigned(OldPilotInputHandler handler)
    {
        inputHandler = handler;
        DebugLog($"Pilot assigned (Player {handler.PlayerIndex})");

        // Get assigned gamepad for rumble
        assignedGamepad = handler.GetAssignedGamepad();
    }

    private void InitializeVehicleControllers()
    {
        shipController.input.Throttle = 0f;
        shipController.input.Steering = 0f;
    }

    private void Update()
    {
        // Only process input if we have a pilot
        if (inputHandler == null || !inputHandler.IsActive)
        {
            // No pilot - keep submarine stationary
            currentThrottleInput = 0f;
            currentSteeringInput = 0f;
            currentSurfaceInput = false;
            currentDiveInput = false;
        }
        else
        {
            GetPilotInputs();
        }

        ApplyMovementInputs();
    }

    private void GetPilotInputs()
    {
        currentThrottleInput = Mathf.Clamp(inputHandler.MovementInput.y, -maxThrottleInput, maxThrottleInput);
        currentSteeringInput = Mathf.Clamp(inputHandler.MovementInput.x, -maxSteeringInput, maxSteeringInput);

        currentSurfaceInput = inputHandler.SurfaceHeld;
        currentDiveInput = inputHandler.DiveHeld;
    }

    private void ApplyMovementInputs()
    {
        if (shipController == null) return;

        ApplyThrottleInputs();
        ApplySteeringInputs();
        ApplyDepthInput();
    }

    private void ApplyThrottleInputs()
    {
        shipController.input.Throttle = currentThrottleInput;

        if (currentBrake > 0.1f)
        {
            shipController.input.Throttle = 0f;
        }
    }

    private void ApplySteeringInputs()
    {
        shipController.input.Steering = -currentSteeringInput;
        shipController.input.BowThruster = currentSteeringInput;
    }

    private void ApplyDepthInput()
    {
        SubmarineBallastController.BuoyancyState desiredState;

        if (currentSurfaceInput && !currentDiveInput)
            desiredState = SubmarineBallastController.BuoyancyState.Positive;
        else if (currentDiveInput && !currentSurfaceInput)
            desiredState = SubmarineBallastController.BuoyancyState.Negative;
        else
            desiredState = SubmarineBallastController.BuoyancyState.Neutral;

        if (ballastController != null)
            ballastController.SetBuoyancyState(desiredState);
    }

    private void OnDestroy()
    {
        OldPlayerRoleManager.OnPilotAssigned -= OnPilotAssigned;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[OldPilotController] {message}");
    }
}