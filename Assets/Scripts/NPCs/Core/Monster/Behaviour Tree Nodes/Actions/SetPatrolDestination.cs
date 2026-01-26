using Opsive.BehaviorDesigner.Runtime.Tasks;

/// <summary>
/// Sets a new patrol destination using the awareness system.
/// This is a ONE-SHOT action that completes immediately after setting the destination.
/// 
/// The awareness system intelligently chooses patrol points based on player distance,
/// creating Alien Isolation-style behavior.
/// 
/// Usage in behavior tree:
/// Sequence -> SetPatrolDestination -> WaitForArrival -> Idle -> (repeat)
/// </summary>
public class SetPatrolDestination : EnemyAction
{
    public override void OnStart()
    {
        base.OnStart();
        controller.SetPatrolDestinationBasedOnDistanceToPlayer();
    }

    public override TaskStatus OnUpdate()
    {
        // This action completes immediately
        return TaskStatus.Success;
    }
}