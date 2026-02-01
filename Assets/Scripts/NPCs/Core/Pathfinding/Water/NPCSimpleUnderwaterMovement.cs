using Infohazard.HyperNav;
using Infohazard.Core;
using UnityEngine;

public class NPCSimpleUnderwaterMovement : MonoBehaviour
{

    [Header("References")]
    [SerializeField] private SplineNavAgent navAgent;
    [SerializeField] private Rigidbody rb;

    [Header("Pathfinding Settings")]
    [SerializeField] private Transform destinationTransform;

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

    //logic variables
    private bool hasHadFirstUpdate;
    private bool dataChanged;

    /// <summary>
    /// Last destination set.
    /// </summary>
    protected Vector3 lastDestination;

    /// <summary>
    /// Reset state so next Update will calculate a new path.
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
    }

    /// <summary>
    /// Update the path if needed, and move the agent if not using a Rigidbody.
    /// </summary>
    protected virtual void Update()
    {
        navAgent.AccelerationEstimate = acceleration;

        if (destinationTransform &&
            (NavVolume.NativeDataMap.IsCreated || NavSurface.NativeDataMap.IsCreated) &&
            (!hasHadFirstUpdate || dataChanged || CheckRepath()))
        {
            hasHadFirstUpdate = true;
            UpdatePath();
        }
    }

    /// <summary>
    /// Calculate a new path to <see cref="DestinationTransform"/>.
    /// </summary>
    protected virtual void UpdatePath()
    {
        if (!destinationTransform && !dataChanged) return;

        dataChanged = false;
        if (destinationTransform)
        {
            lastDestination = destinationTransform.position;
        }

        navAgent.Destination = lastDestination;
    }

    private void FixedUpdate()
    {
        UpdateMovementRigidbody();
    }

    /// <summary>
    /// Update the Rigidbody's velocity and rotation based on the desired velocity.
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
        if ((destinationTransform.position - lastDestination).sqrMagnitude >
            repathDistanceThreshold * repathDistanceThreshold)
            return true;

        if (repathOnReachEnd && navAgent.RemainingDistance < repathOnReachEndDistance)
            return true;

        return false;
    }

    private void ChangeNavDataDataChanging()
    {
        if (!destinationTransform)
            lastDestination = navAgent.Destination;

        navAgent.Stop(true);
    }

    private void ChangeNavDataDataChanged()
    {
        dataChanged = true;
    }

}
