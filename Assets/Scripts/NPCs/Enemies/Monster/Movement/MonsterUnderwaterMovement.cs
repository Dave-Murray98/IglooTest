using System;
using System.Collections;
using Infohazard.HyperNav;
using UnityEngine;

/// <summary>
/// SIMPLIFIED underwater movement system for the monster.
/// This component is ALWAYS ACTIVE and simply moves toward controller.targetPosition.
/// It doesn't decide WHERE to go or WHEN to stop - it just moves smoothly to the current target.
/// 
/// Key Philosophy:
/// - Always listening to targetPosition
/// - Automatically handles pathfinding
/// - Reports when destination is reached through events
/// - No manual activation/deactivation needed
/// </summary>
public class MonsterUnderwaterMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SplineNavAgent agent;
    private UnderwaterMonsterController controller;
    private Rigidbody rb;

    [Header("Movement Settings")]
    [Tooltip("Current movement speed - set by the state machine when states change")]
    public float movementSpeed = 5f;

    [SerializeField] private float acceleration = 300f;
    [SerializeField] private float deceleration = 500f;
    [SerializeField] private float rotationSpeed = 5f;

    [Header("Movement Speeds Per State")]
    [Tooltip("Speed when patrolling")]
    public float patrolSpeed = 4f;
    [Tooltip("Speed when engaging the player")]
    public float engageSpeed = 6f;
    [Tooltip("Speed when pursuing the player")]
    public float pursueSpeed = 6f;
    [Tooltip("Speed when investigating a noise")]
    public float investigateNoiseSpeed = 6f;
    [Tooltip("Speed when retreating/dying")]
    public float retreatSpeed = 8f;

    [Header("Distance Thresholds")]
    [SerializeField]
    [Tooltip("How close to get before considering arrived")]
    private float arriveAtDestinationDistance = 0.4f;

    [SerializeField]
    [Tooltip("Distance at which to start slowing down")]
    private float slowDownDistance = 1f;

    [Header("Physics")]
    [SerializeField] private float maxForce = 6000f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Private state - kept minimal
    private Vector3 currentTarget = Vector3.zero;
    private Vector3 moveDirection = Vector3.zero;
    private bool hasValidPath = false;
    private bool isPathCalculating = false;

    // Pre-calculated squared distances for performance
    private float arriveDistanceSqr;
    private float slowDownDistanceSqr;

    // Events - this is how the movement system communicates outward
    public event Action OnDestinationReached;
    public event Action OnPathFailed;

    #region Initialization

    private void Awake()
    {
        // Get or verify references
        if (agent == null)
        {
            agent = GetComponent<SplineNavAgent>();
            if (agent == null)
            {
                Debug.LogError($"{gameObject.name}: SplineNavAgent component not found!");
                enabled = false;
                return;
            }
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        // Pre-calculate squared distances for performance
        arriveDistanceSqr = arriveAtDestinationDistance * arriveAtDestinationDistance;
        slowDownDistanceSqr = slowDownDistance * slowDownDistance;

        // Subscribe to agent events
        agent.PathReady += OnAgentPathReady;
        agent.PathFailed += OnAgentPathFailed;
    }

    public void Initialize(UnderwaterMonsterController controller)
    {
        this.controller = controller;
        DebugLog("Movement system initialized and ready");
    }

    #endregion

    #region Update Loop

    private void Update()
    {
        // Check if we need to update our destination
        CheckForNewDestination();

        // Update movement direction based on current path
        if (hasValidPath && agent.CurrentPath != null)
        {
            moveDirection = agent.DesiredVelocity.normalized;

            // Check if we've arrived at destination
            CheckArrival();
        }
        else if (!isPathCalculating)
        {
            // No valid path and not calculating - we might be stuck
            moveDirection = Vector3.zero;
        }
    }

    private void FixedUpdate()
    {
        // Handle physics-based movement and rotation
        ApplyMovement();
        ApplyRotation();
    }

    #endregion

    #region Destination Management

    /// <summary>
    /// Checks if the controller has set a new destination and updates pathfinding accordingly.
    /// This is the ONLY place where we check for destination changes.
    /// </summary>
    private void CheckForNewDestination()
    {
        // If controller hasn't set a valid target, do nothing
        if (controller.targetPosition == Vector3.zero)
            return;

        // Check if this is a new destination (different from current)
        // We use a small threshold to avoid floating point comparison issues
        float distanceToNewTarget = Vector3.Distance(currentTarget, controller.targetPosition);
        if (distanceToNewTarget > 0.1f)
        {
            // New destination detected!
            SetNewDestination(controller.targetPosition);
        }
    }

    /// <summary>
    /// Sets a new destination and starts pathfinding.
    /// This method is called automatically when the controller sets a new targetPosition.
    /// </summary>
    private void SetNewDestination(Vector3 newDestination)
    {
        currentTarget = newDestination;
        hasValidPath = false;
        isPathCalculating = true;

        DebugLog($"New destination set: {newDestination}");

        // Tell the HyperNav agent to calculate a path
        if (agent != null && agent.enabled)
        {
            agent.Destination = newDestination;
        }
    }

    #endregion

    #region Arrival Checking

    /// <summary>
    /// Checks if the monster has arrived at its destination.
    /// Uses squared distance for performance.
    /// </summary>
    private void CheckArrival()
    {
        if (agent.CurrentPath == null)
            return;

        float remainingDistance = agent.RemainingDistance;
        float remainingDistanceSqr = remainingDistance * remainingDistance;

        // Have we arrived?
        if (remainingDistanceSqr <= arriveDistanceSqr)
        {
            OnArrivedAtDestination();
        }
    }

    /// <summary>
    /// Called when the monster reaches its destination.
    /// Notifies listeners (behavior trees) that arrival has occurred.
    /// </summary>
    private void OnArrivedAtDestination()
    {
        DebugLog("Arrived at destination");

        hasValidPath = false;
        moveDirection = Vector3.zero;

        // Stop the rigidbody
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Notify listeners (like behavior tree nodes)
        OnDestinationReached?.Invoke();

        // Clear the current target so we know we need a new one
        currentTarget = Vector3.zero;
    }

    #endregion

    #region Movement Application

    /// <summary>
    /// Applies physics-based movement based on current state.
    /// Automatically slows down as we approach the destination.
    /// </summary>
    private void ApplyMovement()
    {
        if (rb == null || !hasValidPath || agent.CurrentPath == null)
            return;

        float remainingDistance = agent.RemainingDistance;
        float remainingDistanceSqr = remainingDistance * remainingDistance;

        Vector3 desiredVelocity;

        // Are we in the "slowing down" zone?
        if (remainingDistanceSqr <= slowDownDistanceSqr)
        {
            // Calculate slowdown factor (0 to 1)
            float slowDownFactor = (remainingDistance - arriveAtDestinationDistance) /
                                  (slowDownDistance - arriveAtDestinationDistance);
            slowDownFactor = Mathf.Clamp01(slowDownFactor);

            float targetSpeed = movementSpeed * slowDownFactor;
            desiredVelocity = moveDirection * targetSpeed;

            // Apply deceleration force
            ApplyForce(desiredVelocity, deceleration);
        }
        else
        {
            // Move at full speed
            desiredVelocity = moveDirection * movementSpeed;

            // Apply acceleration force
            ApplyForce(desiredVelocity, acceleration);
        }
    }

    /// <summary>
    /// Applies a physics force to move the rigidbody toward the desired velocity.
    /// </summary>
    private void ApplyForce(Vector3 desiredVelocity, float forceMultiplier)
    {
        if (rb == null)
            return;

        Vector3 velocityDifference = desiredVelocity - rb.linearVelocity;
        Vector3 force = velocityDifference * forceMultiplier;
        force = Vector3.ClampMagnitude(force, maxForce);

        rb.AddForce(force, ForceMode.Force);
    }

    /// <summary>
    /// Rotates the monster to face the movement direction smoothly.
    /// </summary>
    private void ApplyRotation()
    {
        // Only rotate if we have a significant movement direction
        if (moveDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }

    #endregion

    #region Agent Event Handlers

    /// <summary>
    /// Called by the HyperNav agent when a path has been successfully calculated.
    /// </summary>
    private void OnAgentPathReady()
    {
        DebugLog("Path calculation complete - valid path found");
        hasValidPath = true;
        isPathCalculating = false;
    }

    /// <summary>
    /// Called by the HyperNav agent when pathfinding fails.
    /// This usually means the destination is unreachable.
    /// </summary>
    private void OnAgentPathFailed()
    {
        DebugLog("Path calculation failed - destination unreachable");
        hasValidPath = false;
        isPathCalculating = false;

        // Notify listeners that we couldn't reach this destination
        OnPathFailed?.Invoke();

        // Clear the current target
        currentTarget = Vector3.zero;
    }

    #endregion

    #region Public Interface - Used by State Machine

    /// <summary>
    /// Sets movement speed based on the current state.
    /// Called by the state machine when entering a new state.
    /// </summary>
    public void SetMovementSpeedBasedOnState(EnemyState state)
    {
        if (state is EngagingState)
        {
            movementSpeed = engageSpeed;
        }
        else if (state is PursuingState)
        {
            movementSpeed = pursueSpeed;
        }
        else if (state is InvestigatingNoiseState)
        {
            movementSpeed = investigateNoiseSpeed;
        }
        else if (state is DeathState)
        {
            movementSpeed = retreatSpeed;
        }
        else // Default for patrol, stunned, etc.
        {
            movementSpeed = patrolSpeed;
        }

        DebugLog($"Movement speed set to {movementSpeed} for state {state.name}");
    }

    /// <summary>
    /// Returns the remaining distance to the current destination.
    /// Returns float.MaxValue if no valid path exists.
    /// </summary>
    public float GetDistanceToTarget()
    {
        if (hasValidPath && agent.CurrentPath != null)
        {
            return agent.RemainingDistance;
        }
        return float.MaxValue;
    }

    /// <summary>
    /// Returns whether the movement system currently has a valid path it's following.
    /// </summary>
    public bool IsMoving()
    {
        return hasValidPath && agent.CurrentPath != null;
    }

    /// <summary>
    /// Returns whether the movement system is currently calculating a path.
    /// </summary>
    public bool IsCalculatingPath()
    {
        return isPathCalculating;
    }

    #endregion

    #region Debug

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[MonsterMovement] {message}");
        }
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        if (agent != null)
        {
            agent.PathReady -= OnAgentPathReady;
            agent.PathFailed -= OnAgentPathFailed;
        }
    }

    #endregion
}