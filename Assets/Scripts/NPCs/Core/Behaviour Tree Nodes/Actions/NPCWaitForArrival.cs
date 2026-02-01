using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

/// <summary>
/// Waits for the monster to arrive at its destination.
/// This is a BLOCKING action that keeps running until arrival.
/// 
/// This prevents the behavior tree from prematurely setting new destinations.
/// It also has a timeout to prevent infinite waiting if pathfinding fails.
/// 
/// Usage in behavior tree:
/// Sequence -> SetPatrolDestination -> WaitForArrival -> Idle -> (repeat)
/// </summary>
public class NPCWaitForArrival : NPCAction
{
    [Tooltip("Maximum time to wait for arrival before giving up (0 = wait forever)")]
    [SerializeField] private float timeout = 30f;

    [Tooltip("If true, give up if path calculation fails")]
    [SerializeField] private bool failOnPathFailure = true;

    private bool hasArrived = false;
    private bool pathFailed = false;
    private float waitTimer = 0f;

    public override void OnAwake()
    {
        base.OnAwake();

        // Subscribe to movement events
        if (nPC.movementScript != null)
        {
            nPC.movementScript.OnDestinationReached += OnDestinationReached;
            nPC.movementScript.OnPathFailed += OnPathFailed;
        }
    }

    public override void OnStart()
    {
        base.OnStart();

        // Reset state
        hasArrived = false;
        pathFailed = false;
        waitTimer = 0f;
    }

    public override TaskStatus OnUpdate()
    {
        // Check if we've arrived
        if (hasArrived)
        {
            return TaskStatus.Success;
        }

        // Check if pathfinding failed
        if (pathFailed && failOnPathFailure)
        {
            Debug.LogWarning($"[WaitForArrival] Path failed for {gameObject.name}");
            return TaskStatus.Failure;
        }

        // Check timeout
        if (timeout > 0)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= timeout)
            {
                Debug.LogWarning($"[WaitForArrival] Timeout reached for {gameObject.name} after {timeout} seconds");
                return TaskStatus.Failure;
            }
        }

        // Still waiting
        return TaskStatus.Running;
    }

    /// <summary>
    /// Called when the movement system reaches its destination.
    /// </summary>
    private void OnDestinationReached()
    {
        hasArrived = true;
    }

    /// <summary>
    /// Called when the movement system fails to find a path.
    /// </summary>
    private void OnPathFailed()
    {
        pathFailed = true;
    }

    public override void OnEnd()
    {
        base.OnEnd();
        // Reset state
        hasArrived = false;
        pathFailed = false;
        waitTimer = 0f;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        // Unsubscribe from events
        if (nPC != null && nPC.movementScript != null)
        {
            nPC.movementScript.OnDestinationReached -= OnDestinationReached;
            nPC.movementScript.OnPathFailed -= OnPathFailed;
        }
    }
}