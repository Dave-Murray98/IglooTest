using Opsive.BehaviorDesigner.Runtime.Tasks;

/// <summary>
/// Sets the patrol destination to a random position NEAR the last heard noise.
/// This is a ONE-SHOT action that completes immediately.
/// 
/// Used after the monster reaches the exact noise location and wants to search the area.
/// 
/// Usage in behavior tree:
/// Sequence -> SetPatrolDestinationToPosNearNoise -> WaitForArrival -> Idle -> (repeat)
/// </summary>
public class SetPatrolDestinationToPosNearNoise : EnemyAction
{
    public override void OnStart()
    {
        base.OnStart();
        controller.SetPatrolPositionToRandomPositionNearLastHeardNoise();
    }

    public override TaskStatus OnUpdate()
    {
        // Completes immediately
        return TaskStatus.Success;
    }
}