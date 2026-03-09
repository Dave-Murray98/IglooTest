using NWH.DWP2.ShipController;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Controls the submarine based on pilot input.
/// Uses MultiRoleInputHandler.
///
/// Rumble fix: assignedGamepad is no longer cached at AssignHandler() time.
/// Instead, inputHandler.GetAssignedGamepad() is called at the moment rumble is needed.
/// </summary>
public class PilotController : MonoBehaviour
{
    [Header("Vehicle Controllers")]
    [SerializeField] private AdvancedShipController shipController;
    [SerializeField] private SubmarineBallastController ballastController;

    [Header("Submarine Config")]
    [SerializeField] private float maxThrottleInput = 1f;
    [SerializeField] private float maxSteeringInput = 1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    private MultiRoleInputHandler inputHandler;

    [ShowInInspector, ReadOnly] private float currentThrottle = 0f;
    [ShowInInspector, ReadOnly] private float currentSteering = 0f;
    [ShowInInspector, ReadOnly] private bool suppressMovement = false;

    // No cached assignedGamepad — always fetched fresh from inputHandler at call time.

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        if (shipController == null) shipController = GetComponent<AdvancedShipController>();
        if (ballastController == null) ballastController = GetComponent<SubmarineBallastController>();

        InitialiseVehicleControllers();
        SubmarineHealthManager.Instance.OnSubmarineTakenDamage += HandleDamageRumble;
    }

    private void Update()
    {
        if (inputHandler == null || !inputHandler.IsActive || suppressMovement)
        {
            ApplyMovementInputs(0f, 0f, false, false);
            return;
        }

        float throttle = Mathf.Clamp(inputHandler.MovementInput.y, -maxThrottleInput, maxThrottleInput);
        float steering = Mathf.Clamp(inputHandler.MovementInput.x, -maxSteeringInput, maxSteeringInput);
        ApplyMovementInputs(throttle, steering, inputHandler.SurfaceHeld, inputHandler.DiveHeld);
    }

    private void OnDestroy()
    {
        if (SubmarineHealthManager.Instance != null)
            SubmarineHealthManager.Instance.OnSubmarineTakenDamage -= HandleDamageRumble;
    }

    // -------------------------------------------------------------------------
    // Public API — called by PlayerRoleManager
    // -------------------------------------------------------------------------

    public void AssignHandler(MultiRoleInputHandler handler)
    {
        inputHandler = handler;
        DebugLog($"Handler assigned (Player {handler?.PlayerIndex})");
    }

    public void DetachHandler()
    {
        inputHandler = null;
        DebugLog("Handler detached");
    }

    public void SuppressMovementInput(bool suppress)
    {
        suppressMovement = suppress;
    }

    // -------------------------------------------------------------------------
    // Movement application
    // -------------------------------------------------------------------------

    private void InitialiseVehicleControllers()
    {
        if (shipController != null)
        {
            shipController.input.Throttle = 0f;
            shipController.input.Steering = 0f;
        }
    }

    private void ApplyMovementInputs(float throttle, float steering, bool surface, bool dive)
    {
        if (shipController == null) return;

        currentThrottle = throttle;
        currentSteering = steering;

        shipController.input.Throttle = throttle;
        shipController.input.Steering = -steering;
        shipController.input.BowThruster = steering;

        ApplyDepthInput(surface, dive);
    }

    private void ApplyDepthInput(bool surface, bool dive)
    {
        if (ballastController == null) return;

        SubmarineBallastController.BuoyancyState state;

        if (surface && !dive) state = SubmarineBallastController.BuoyancyState.Positive;
        else if (dive && !surface) state = SubmarineBallastController.BuoyancyState.Negative;
        else state = SubmarineBallastController.BuoyancyState.Neutral;

        ballastController.SetBuoyancyState(state);
    }

    private void HandleDamageRumble(float low, float high, float duration)
    {
        // Fetch the gamepad fresh at rumble time — not cached — so it is always valid
        RumbleManager.Instance.RumblePulse(
            inputHandler?.GetAssignedGamepad(), low, high, duration);
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PilotController] {message}");
    }
}