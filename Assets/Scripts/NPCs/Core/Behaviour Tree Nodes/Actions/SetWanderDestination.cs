using Opsive.BehaviorDesigner.Runtime.Tasks;

/// <summary>
/// Sets a new wander destination using the awareness system.
/// This is a ONE-SHOT action that completes immediately after setting the destination.
///  
/// Usage in behavior tree:
/// Sequence -> SetWanderDestination -> WaitForArrival -> Idle -> (repeat)
/// </summary>
public class SetWanderDestination : NPCAction
{
    public override void OnStart()
    {
        base.OnStart();
        nPC.movementScript.SetRandomWanderDestination();
    }

    public override TaskStatus OnUpdate()
    {
        // This action completes immediately
        return TaskStatus.Success;
    }
}