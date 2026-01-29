using System;
using NWH.Common.CoM;
using NWH.DWP2.ShipController;
using Sirenix.OdinInspector;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Vehicle Controllers")]
    [SerializeField] private AdvancedShipController shipController;
    [SerializeField] private SubmarineBallastController ballastController; // CHANGED
    [SerializeField] private VariableCenterOfMass vcom;

    [Header("Input Response")]
    [SerializeField] private float throttleResponseSpeed = 2f;
    [SerializeField] private float steeringResponseSpeed = 3f;
    [SerializeField] private bool invertSteering = false;

    [Header("Submarine Config")]
    [SerializeField] private float maxThrottleInput = 1f;
    [SerializeField] private float maxSteeringInput = 1f;
    [SerializeField] private float maxBrake = 1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;


    // Current input values
    [ShowInInspector, ReadOnly] private float currentThrottleInput = 0f;
    private float currentSteeringInput = 0f;
    private float currentBrake = 0f;

    [ShowInInspector, ReadOnly] private bool currentSurfaceInput;

    [ShowInInspector, ReadOnly] private bool currentDiveInput;

    private void Start()
    {
        if (shipController == null) shipController = GetComponent<AdvancedShipController>();
        if (ballastController == null) ballastController = GetComponent<SubmarineBallastController>();
        if (vcom == null) vcom = GetComponent<VariableCenterOfMass>();

        InitializeVehicleControllers();

    }

    private void InitializeVehicleControllers()
    {
        shipController.input.Throttle = 0f;
        shipController.input.Steering = 0f;
    }

    private void Update()
    {
        GetPilotInputs();
        ApplyMovementInputs();
    }


    private void GetPilotInputs()
    {
        currentThrottleInput = Mathf.Clamp(InputManager.Instance.MovementInput.y, -maxThrottleInput, maxThrottleInput);
        currentSteeringInput = Mathf.Clamp(InputManager.Instance.MovementInput.x, -maxSteeringInput, maxSteeringInput);

        currentSurfaceInput = InputManager.Instance.SurfaceHeld;
        currentDiveInput = InputManager.Instance.DiveHeld;
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

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerController] {message}");
        }
    }

}
