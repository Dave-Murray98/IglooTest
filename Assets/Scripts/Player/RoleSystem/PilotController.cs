using NWH.DWP2.ShipController;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls the submarine based on pilot input.
/// Updated to use MultiRoleInputHandler instead of the old PilotInputHandler.
///
/// Key additions vs the original:
///   - AssignHandler() / DetachHandler() called by PlayerRoleManager on reconfiguration.
///   - SuppressMovementInput() called by SoloPilotEngineerBridge when the pilot is
///     also the engineer and enters engineer select mode.
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

    private Gamepad assignedGamepad;

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
            // No pilot or input suppressed — hold still
            ApplyMovementInputs(0f, 0f, false, false);
            return;
        }

        float throttle = Mathf.Clamp(inputHandler.MovementInput.y, -maxThrottleInput, maxThrottleInput);
        float steering = Mathf.Clamp(inputHandler.MovementInput.x, -maxSteeringInput, maxSteeringInput);

        ApplyMovementInputs(throttle, steering, inputHandler.SurfaceHeld, inputHandler.DiveHeld);
    }

    private void OnDestroy()
    {
        SubmarineHealthManager.Instance.OnSubmarineTakenDamage -= HandleDamageRumble;
    }

    // -------------------------------------------------------------------------
    // Public API — called by PlayerRoleManager
    // -------------------------------------------------------------------------

    /// <summary>
    /// Assigns a new input handler. Called every time roles are reconfigured.
    /// </summary>
    public void AssignHandler(MultiRoleInputHandler handler)
    {
        inputHandler = handler;
        assignedGamepad = handler?.GetAssignedGamepad();
        DebugLog($"Handler assigned (Player {handler?.PlayerIndex})");
    }

    /// <summary>
    /// Removes the current handler. Called before reconfiguration so stale
    /// references don't linger.
    /// </summary>
    public void DetachHandler()
    {
        inputHandler = null;
        assignedGamepad = null;
        DebugLog("Handler detached");
    }

    /// <summary>
    /// Called by SoloPilotEngineerBridge to freeze submarine movement while
    /// the player is in engineer select mode.
    /// </summary>
    public void SuppressMovementInput(bool suppress)
    {
        suppressMovement = suppress;
    }

    // -------------------------------------------------------------------------
    // Internal movement application (unchanged from original)
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

        if (surface && !dive)
            state = SubmarineBallastController.BuoyancyState.Positive;
        else if (dive && !surface)
            state = SubmarineBallastController.BuoyancyState.Negative;
        else
            state = SubmarineBallastController.BuoyancyState.Neutral;

        ballastController.SetBuoyancyState(state);
    }

    private void HandleDamageRumble(float low, float high, float duration)
    {
        if (assignedGamepad != null)
            RumbleManager.Instance.RumblePulse(assignedGamepad, low, high, duration);
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PilotController] {message}");
    }
}