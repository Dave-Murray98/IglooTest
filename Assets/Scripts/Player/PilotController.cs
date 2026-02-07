using System;
using System.Collections;
using NWH.Common.CoM;
using NWH.DWP2.ShipController;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls the submarine based on pilot input.
/// Renamed from PlayerController to clarify its role in multiplayer context.
/// Now reads from PilotInputHandler instead of the singleton InputManager.
/// </summary>
public class PilotController : MonoBehaviour
{
    [Header("Vehicle Controllers")]
    [SerializeField] private AdvancedShipController shipController;
    [SerializeField] private SubmarineBallastController ballastController;

    [Header("Input Response")]
    [SerializeField] private float throttleResponseSpeed = 2f;
    [SerializeField] private float steeringResponseSpeed = 3f;
    [SerializeField] private bool invertSteering = false;

    [Header("Submarine Config")]
    [SerializeField] private float maxThrottleInput = 1f;
    [SerializeField] private float maxSteeringInput = 1f;
    [SerializeField] private float maxBrake = 1f;


    [Header("Rumble Settings")]
    [SerializeField] private float lowFrequency = 0.4f;
    [SerializeField] private float highFrequency = 0.4f;
    [SerializeField] private float rumbleDuration = 0.1f;


    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Input handler reference
    private PilotInputHandler inputHandler;

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
        PlayerRoleManager.OnPilotAssigned += OnPilotAssigned;

        // Check if pilot already exists
        if (PlayerRoleManager.Instance != null && PlayerRoleManager.Instance.HasPilot)
        {
            inputHandler = PlayerRoleManager.Instance.GetPilotHandler();
            DebugLog($"Connected to existing pilot (Player {inputHandler.PlayerIndex})");
        }
        else
        {
            DebugLog("Waiting for pilot to connect...");
        }

        SubmarineHealthManager.Instance.OnSubmarineTakenDamage += RumblePulse;
    }

    private void OnPilotAssigned(PilotInputHandler handler)
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
        // DWP2 uses Throttle for forward/backward movement
        shipController.input.Throttle = currentThrottleInput;

        if (currentBrake > 0.1f)
        {
            // When braking, override throttle to 0 or slight reverse
            shipController.input.Throttle = 0f;
        }
    }

    private void ApplySteeringInputs()
    {
        // DWP2 uses Steering for left/right turning
        shipController.input.Steering = -currentSteeringInput;
        shipController.input.BowThruster = currentSteeringInput;
    }

    private void ApplyDepthInput()
    {
        SubmarineBallastController.BuoyancyState desiredState;

        // Determine state from input
        if (currentSurfaceInput && !currentDiveInput)
        {
            desiredState = SubmarineBallastController.BuoyancyState.Positive;
        }
        else if (currentDiveInput && !currentSurfaceInput)
        {
            desiredState = SubmarineBallastController.BuoyancyState.Negative;
        }
        else // Both or neither pressed
        {
            desiredState = SubmarineBallastController.BuoyancyState.Neutral;
        }

        // Apply to ballast controller
        if (ballastController != null)
        {
            ballastController.SetBuoyancyState(desiredState);
        }
    }


    public void RumblePulse(float lowFrequency, float highFrequency, float duration)
    {
        if (assignedGamepad != null)
        {
            //start rumble 
            assignedGamepad.SetMotorSpeeds(lowFrequency, highFrequency);

            //stop rumble after duration
            StartCoroutine(StopRumbleAfterDuration(duration));
        }
    }

    private IEnumerator StopRumbleAfterDuration(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (assignedGamepad != null)
        {
            assignedGamepad.SetMotorSpeeds(0f, 0f);
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        PlayerRoleManager.OnPilotAssigned -= OnPilotAssigned;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PilotController] {message}");
        }
    }
}