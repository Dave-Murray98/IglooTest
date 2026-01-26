using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

/// <summary>
/// Moves the monster toward its death retreat position.
/// Used in the death behavior tree to make the monster flee before despawning.
/// 
/// This continuously updates the target to the retreat transform's position
/// in case the retreat point is moving or to ensure accurate pathfinding.
/// 
/// Usage in Death Behavior Tree:
/// Sequence
/// ├─ MoveTowardsDeathRetreat (keeps running until arrival)
/// └─ Despawn
/// </summary>
public class MoveTowardsDeathRetreat : EnemyAction
{
    [Tooltip("How often to update the target position (in seconds)")]
    [SerializeField] private float pathfindingUpdateFrequency = 0.5f;

    [Tooltip("Optional timeout - how long to try before giving up (0 = never give up)")]
    [SerializeField] private float timeout = 30f;

    private float updateTimer = 0f;
    private float totalTimer = 0f;

    public override void OnStart()
    {
        base.OnStart();

        // Validate retreat transform
        if (controller.monsterDeathRetreatTransform == null)
        {
            Debug.LogError($"[MoveTowardsDeathRetreat] No death retreat transform assigned on {gameObject.name}!");
            return;
        }

        // Set initial target
        // No need to call ActivateMovement() - the new system is always active!
        controller.SetTargetPosition(controller.monsterDeathRetreatTransform.position);

        updateTimer = 0f;
        totalTimer = 0f;
    }

    public override TaskStatus OnUpdate()
    {
        // Safety check
        if (controller.monsterDeathRetreatTransform == null)
        {
            Debug.LogError($"[MoveTowardsDeathRetreat] Death retreat transform is null!");
            return TaskStatus.Failure;
        }

        // Update timers
        updateTimer += Time.deltaTime;
        totalTimer += Time.deltaTime;

        // Update target position periodically
        if (updateTimer >= pathfindingUpdateFrequency)
        {
            controller.SetTargetPosition(controller.monsterDeathRetreatTransform.position);
            updateTimer = 0f;
        }

        // Check for timeout
        if (timeout > 0 && totalTimer >= timeout)
        {
            Debug.LogWarning($"[MoveTowardsDeathRetreat] Timeout reached after {timeout} seconds. Giving up.");
            return TaskStatus.Success; // Success so death sequence can continue
        }

        // Check if we've arrived at the retreat position
        float distanceToRetreat = controller.GetDistanceToTarget();
        if (distanceToRetreat <= 1f) // Close enough to retreat point
        {
            return TaskStatus.Success;
        }

        // Still moving toward retreat
        return TaskStatus.Running;
    }

    public override void OnEnd()
    {
        base.OnEnd();
        updateTimer = 0f;
        totalTimer = 0f;
    }
}