using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;

/// <summary>
/// Makes the monster idle (pause) for a specified duration.
/// During this time, the monster stays still.
/// 
/// This is useful for:
/// - Pausing between patrol points (creates more natural movement)
/// - Waiting before searching after investigating a noise
/// - Adding variety to behavior patterns
/// 
/// Usage in behavior tree:
/// Sequence -> WaitForArrival -> Idle -> SetPatrolDestination
/// </summary>
public class Idle : EnemyAction
{
    [Tooltip("How long to stay idle (in seconds)")]
    [SerializeField] private float idleTime = 5f;

    [Tooltip("If true, completely stop the rigidbody during idle")]
    [SerializeField] private bool freezeVelocities = false;

    [Tooltip("If true, clear the target position (stops movement completely)")]
    [SerializeField] private bool clearTarget = true;

    private float idleTimer = 0f;
    private Vector3 previousTarget;

    public override void OnStart()
    {
        base.OnStart();

        idleTimer = 0f;

        // Store the previous target in case we need to restore it
        previousTarget = controller.targetPosition;

        // Clear target to stop movement system
        if (clearTarget)
        {
            controller.SetTargetPosition(Vector3.zero);
        }

        // Optionally freeze the rigidbody completely
        if (freezeVelocities && controller.rb != null)
        {
            controller.rb.linearVelocity = Vector3.zero;
            controller.rb.angularVelocity = Vector3.zero;
        }
    }

    public override TaskStatus OnUpdate()
    {
        // Increment timer
        idleTimer += Time.deltaTime;

        // Keep velocities at zero if requested
        if (freezeVelocities && controller.rb != null)
        {
            controller.rb.linearVelocity = Vector3.zero;
            controller.rb.angularVelocity = Vector3.zero;
        }

        // Check if idle time has elapsed
        if (idleTimer >= idleTime)
        {
            return TaskStatus.Success;
        }

        return TaskStatus.Running;
    }

    public override void OnEnd()
    {
        base.OnEnd();
        idleTimer = 0f;
    }
}