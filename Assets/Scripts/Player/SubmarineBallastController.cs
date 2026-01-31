using NWH.Common.CoM;
using UnityEngine;

/// <summary>
/// Provides submarine buoyancy control through a simple state-based ballast system.
/// Implements IMassAffector to work with VariableCenterOfMass for physics calculations.
/// </summary>
[RequireComponent(typeof(VariableCenterOfMass))]
public class SubmarineBallastController : MonoBehaviour, IMassAffector
{
    public enum BuoyancyState { Positive, Neutral, Negative }

    [Header("Ballast Mass Values")]
    [Tooltip("Ballast mass for positive buoyancy (rising to surface)")]
    [SerializeField] private float positiveBuoyancyMass = 0f;

    [Tooltip("Ballast mass for neutral buoyancy (maintaining depth)")]
    [SerializeField] private float neutralBuoyancyMass = 19600f;

    [Tooltip("Ballast mass for negative buoyancy (diving deeper)")]
    [SerializeField] private float negativeBuoyancyMass = 30000f;

    [Header("Transition")]
    [Tooltip("Speed at which ballast mass changes (kg/second)")]
    [SerializeField] private float ballastTransitionSpeed = 2000f;

    [Header("References")]
    [SerializeField] private VariableCenterOfMass vcom;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private float currentBallastMass = 0f;
    private BuoyancyState targetBuoyancyState = BuoyancyState.Neutral;

    private void Start()
    {
        vcom = GetComponent<VariableCenterOfMass>();

        // Start at neutral
        currentBallastMass = neutralBuoyancyMass;

        DebugLog($"Initialized with neutral ballast: {neutralBuoyancyMass}kg");
    }

    private void FixedUpdate()
    {
        // Calculate target mass based on state
        float targetMass = targetBuoyancyState switch
        {
            BuoyancyState.Positive => positiveBuoyancyMass,
            BuoyancyState.Neutral => neutralBuoyancyMass,
            BuoyancyState.Negative => negativeBuoyancyMass,
            _ => neutralBuoyancyMass
        };

        // Smoothly transition to target
        currentBallastMass = Mathf.MoveTowards(
            currentBallastMass,
            targetMass,
            ballastTransitionSpeed * Time.fixedDeltaTime
        );

        // Mark VCOM for recalculation
        if (vcom != null)
        {
            vcom.MarkDirty();
        }
    }

    /// <summary>
    /// Sets the desired buoyancy state for the submarine.
    /// </summary>
    public void SetBuoyancyState(BuoyancyState state)
    {
        if (targetBuoyancyState != state)
        {
            targetBuoyancyState = state;
            DebugLog($"Buoyancy state changed to: {state}");
        }
    }

    // IMassAffector implementation
    public float GetMass() => currentBallastMass;
    public Vector3 GetWorldCenterOfMass() => transform.position;
    public Transform GetTransform() => transform;

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[SubmarineBallastController] {message}");
        }
    }
}