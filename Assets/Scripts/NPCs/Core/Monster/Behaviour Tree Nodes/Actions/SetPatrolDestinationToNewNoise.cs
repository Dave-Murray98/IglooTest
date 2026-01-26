using Opsive.BehaviorDesigner.Runtime.Tasks;

/// <summary>
/// Sets the patrol destination to the exact location of the last heard noise.
/// This is a ONE-SHOT action that completes immediately.
/// 
/// Used when the monster first hears a noise and wants to investigate it directly.
/// 
/// Usage in behavior tree:
/// Sequence -> HasHeardNewNoise -> SetPatrolDestinationToNewNoise -> WaitForArrival
/// </summary>
public class SetPatrolDestinationToNewNoise : EnemyAction
{
    public override void OnStart()
    {
        base.OnStart();

        if (controller.hearing != null && controller.hearing.LastHeardNoisePosition != UnityEngine.Vector3.zero)
        {
            // Set target to the exact noise location
            controller.SetTargetPosition(controller.hearing.LastHeardNoisePosition);
        }
        else
        {
            // Fallback if no valid noise position
            controller.SetRandomPatrolDestination();
        }
    }

    public override TaskStatus OnUpdate()
    {
        // Completes immediately
        return TaskStatus.Success;
    }
}