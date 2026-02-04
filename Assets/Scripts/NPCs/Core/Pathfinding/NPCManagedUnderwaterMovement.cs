using Infohazard.HyperNav;
using Infohazard.Core;
using UnityEngine;
using System;

/// <summary>
/// Underwater movement script that works with NPCPathfindingManager.
/// Path updates are controlled by the manager to spread CPU load across frames.
/// Movement and rotation still happen every frame for smooth motion.
/// </summary>
public class NPCManagedUnderwaterMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SplineNavAgent navAgent;
    [SerializeField] private AvoidanceAgent avoidanceAgent;
    [SerializeField] private Rigidbody rb;

    [Header("Pathfinding Settings")]
    public Vector3 destination = Vector3.zero;

    [SerializeField]
    [Tooltip("A new path will be calculated if the destination moves by this distance.")]
    private float repathDistanceThreshold = 1.0f;

    [SerializeField]
    [Tooltip("Enable to repath when the agent reaches the end of its path.")]
    private bool repathOnReachEnd = true;

    [SerializeField]
    [Tooltip("Distance from the end of the path to repath.")]
    private float repathOnReachEndDistance = 1f;

    [Header("Movement Settings")]
    [SerializeField] private float maxSpeed = 4f;
    [SerializeField] private float acceleration = 6f;
    [SerializeField] private float deceleration = 6f;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 5f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Logic variables
    private bool hasHadFirstUpdate;
    private bool dataChanged;
    private bool hasReachedCurrentDestination = false;
    private bool hasValidPath = false; // NEW: Track if current path is valid

    /// <summary>
    /// Last destination set.
    /// </summary>
    protected Vector3 lastDestination;

    // Events - this is how the movement system communicates outward
    public event Action OnDestinationReached;
    public event Action OnPathFailed;

    /// <summary>
    /// Subscribe to nav data change events.
    /// </summary>
    protected void OnEnable()
    {
        hasHadFirstUpdate = false;

        ChangeNavAreaData.DataChanging += ChangeNavDataDataChanging;
        ChangeNavAreaData.DataChanged += ChangeNavDataDataChanged;
    }

    /// <summary>
    /// Unsubscribe from events.
    /// </summary>
    private void OnDisable()
    {
        ChangeNavAreaData.DataChanging -= ChangeNavDataDataChanging;
        ChangeNavAreaData.DataChanged -= ChangeNavDataDataChanged;
    }

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (navAgent == null) navAgent = GetComponent<SplineNavAgent>();
        if (avoidanceAgent == null) avoidanceAgent = GetComponent<AvoidanceAgent>();

        avoidanceAgent.MaxSpeed = maxSpeed;

        // FIXED: Subscribe to the actual callback method, not the event itself
        navAgent.PathFailed += HandlePathFailed;
    }

    /// <summary>
    /// NEW: Callback method for when HyperNav fails to find a path.
    /// This is the actual handler that gets called by the navAgent.
    /// </summary>
    private void HandlePathFailed()
    {
        DebugLog("Path calculation failed - destination unreachable");
        hasValidPath = false;

        // Fire the event so other systems (like state machine) can react
        OnPathFailed?.Invoke();
    }

    /// <summary>
    /// Update only handles movement - path updates are called by NPCPathfindingManager.
    /// This ensures smooth movement every frame while spreading pathfinding across frames.
    /// </summary>
    protected virtual void Update()
    {
        // Set acceleration estimate for the nav agent
        navAgent.AccelerationEstimate = acceleration;
    }

    /// <summary>
    /// Called by NPCPathfindingManager to check if this NPC needs a path update.
    /// This is the method that gets called in a staggered fashion across frames.
    /// </summary>
    public void TryUpdatePath()
    {
        // Check if we have the necessary data to pathfind
        if (destination != Vector3.zero && NavVolume.NativeDataMap.IsCreated && !hasHadFirstUpdate || dataChanged || CheckRepath())
        {
            hasHadFirstUpdate = true;
            UpdatePath();
        }
    }

    /// <summary>
    /// Calculate a new path to the destination transform.
    /// </summary>
    protected virtual void UpdatePath()
    {
        if (destination == Vector3.zero && !dataChanged) return;

        dataChanged = false;
        if (destination != Vector3.zero)
        {
            lastDestination = destination;
        }

        navAgent.Destination = lastDestination;

        // MODIFIED: Assume path is valid when we request it (will be set to false if it fails)
        hasValidPath = true;

        // MODIFIED: Only trigger arrival if we have a valid path
        if (navAgent.Arrived && hasValidPath && !hasReachedCurrentDestination)
        {
            OnArrivedAtDestination();
        }
    }

    private void OnArrivedAtDestination()
    {
        DebugLog("Arrived at destination");

        hasReachedCurrentDestination = true;

        // Stop the rigidbody
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        OnDestinationReached?.Invoke();

        // Clear destination after arrival
        destination = Vector3.zero;
    }

    private void FixedUpdate()
    {
        UpdateMovementRigidbody();
    }

    /// <summary>
    /// Update the Rigidbody's velocity and rotation based on the desired velocity.
    /// This runs every FixedUpdate for smooth movement regardless of pathfinding updates.
    /// </summary>
    protected virtual void UpdateMovementRigidbody()
    {
        Vector3 desiredVel = navAgent.DesiredVelocity * maxSpeed;
        Vector3 deltaV =
            Vector3.ClampMagnitude(desiredVel - rb.linearVelocity,
                                   acceleration * Time.fixedDeltaTime);

        rb.AddForce(deltaV, ForceMode.VelocityChange);

        rb.MoveRotation(GetNewRotation(rb.rotation, rb.linearVelocity,
                                       Time.fixedDeltaTime));
    }

    /// <summary>
    /// Get the updated rotation based on the desired velocity and time delta.
    /// </summary>
    /// <param name="current">Current rotation.</param>
    /// <param name="velocity">Current velocity.</param>
    /// <param name="deltaTime">Time since last rotation update.</param>
    /// <returns>New rotation to use.</returns>
    protected virtual Quaternion GetNewRotation(Quaternion current, Vector3 velocity, float deltaTime)
    {
        if (velocity.sqrMagnitude < 0.0001f) return current;

        Vector3 vel = velocity.normalized;

        float delta = deltaTime * rotationSpeed;

        Quaternion desired = MathUtility.ZYRotation(vel, current * Vector3.up);

        return Quaternion.Slerp(current, desired, delta);
    }

    /// <summary>
    /// Check if a new path should be calculated.
    /// </summary>
    /// <returns>True if a new path should be calculated.</returns>
    protected virtual bool CheckRepath()
    {
        if ((destination - lastDestination).sqrMagnitude >
            repathDistanceThreshold * repathDistanceThreshold)
            return true;

        if (repathOnReachEnd && navAgent.RemainingDistance < repathOnReachEndDistance)
            return true;

        return false;
    }

    /// <summary>
    /// Sets a completely random wander destination.
    /// </summary>
    public void SetRandomWanderDestination()
    {
        Vector3 randomPosition = NPCPathfindingUtilities.Instance.GetRandomValidPosition(transform.position);

        if (randomPosition == Vector3.zero)
        {
            DebugLog("Warning: Failed to get random valid position");
            return;
        }

        SetDestination(randomPosition);
        DebugLog($"Set random wander destination: {randomPosition}");
    }

    /// <summary>
    /// Sets the destination for the NPC to move toward.
    /// The movement system automatically picks up this change and starts pathfinding.
    /// This is the PRIMARY method behavior trees use to control movement.
    /// </summary>
    public void SetDestination(Vector3 newTarget)
    {
        lastDestination = destination;
        destination = newTarget;
        hasReachedCurrentDestination = false; // Reset arrival state when new destination is set
        DebugLog($"Destination set to: {newTarget}");
    }

    /// <summary>
    /// Clears the current destination and stops movement.
    /// Used when interrupting movement (e.g., during attacks).
    /// </summary>
    public void ClearDestination()
    {
        destination = Vector3.zero;
        lastDestination = Vector3.zero;
        hasReachedCurrentDestination = false;
        hasValidPath = false;
        navAgent.Stop(true);
        DebugLog("Destination cleared");
    }

    private void ChangeNavDataDataChanging()
    {
        if (destination != Vector3.zero)
            lastDestination = navAgent.Destination;

        navAgent.Stop(true);
    }

    private void ChangeNavDataDataChanged()
    {
        dataChanged = true;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log("[NPCManagedUnderwaterMovement]" + message);
    }

    // NEW: Public method to check if current path is valid (useful for debugging)
    public bool HasValidPath()
    {
        return hasValidPath;
    }
}