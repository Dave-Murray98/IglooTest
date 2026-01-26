using Opsive.BehaviorDesigner.Runtime.Tasks;

/// <summary>
/// DEPRECATED: This node is replaced by WaitForArrival in the new system.
/// 
/// OLD PATTERN (Don't use):
/// MoveTowardsDestination (constantly checks HasReachedDestination)
/// 
/// NEW PATTERN (Better):
/// SetPatrolDestination → WaitForArrival
/// 
/// However, if you still need this node for some reason, here's the simplified version
/// that works with the new movement system.
/// </summary>
public class MoveTowardsDestination : EnemyAction
{
    public override void OnStart()
    {
        base.OnStart();
        // No need to activate movement - it's always active in the new system!
        // The movement system automatically follows controller.targetPosition
    }

    public override TaskStatus OnUpdate()
    {
        // Check if we've arrived
        // Note: In the new system, it's better to use WaitForArrival which is event-driven
        if (controller.movement.GetDistanceToTarget() <= 0.5f)
        {
            return TaskStatus.Success;
        }

        return TaskStatus.Running;
    }
}

// RECOMMENDED: Use this pattern instead in your behavior trees:
// Sequence
// ├─ SetPatrolDestination
// ├─ WaitForArrival  ← Use this instead of MoveTowardsDestination!
// └─ Idle