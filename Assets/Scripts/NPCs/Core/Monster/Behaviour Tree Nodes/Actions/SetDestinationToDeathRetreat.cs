using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

/// <summary>
/// Sets the monster's destination to the death retreat position.
/// This is a ONE-SHOT action that completes immediately after setting the destination.
/// 
/// Use this if you want to set the retreat destination once and then use WaitForArrival.
/// If you want continuous updates (in case retreat point moves), use MoveTowardsDeathRetreat instead.
/// 
/// Usage Pattern 1 (Simple - set and wait):
/// Sequence
/// ├─ SetDestinationToDeathRetreat
/// ├─ WaitForArrival (timeout: 30s)
/// └─ Despawn
/// 
/// Usage Pattern 2 (Continuous updates - safer):
/// Sequence
/// ├─ MoveTowardsDeathRetreat (handles updates automatically)
/// └─ Despawn
/// </summary>
public class SetDestinationToDeathRetreat : EnemyAction
{
    public override void OnStart()
    {
        base.OnStart();

        // Validate retreat transform
        if (controller.monsterDeathRetreatTransform == null)
        {
            Debug.LogError($"[SetDestinationToDeathRetreat] No death retreat transform assigned on {gameObject.name}!");
            return;
        }

        // Set the target position
        // No need to call ActivateMovement() - the new system is always active!
        controller.SetTargetPosition(controller.monsterDeathRetreatTransform.position);
    }

    public override TaskStatus OnUpdate()
    {
        // This action completes immediately
        return TaskStatus.Success;
    }
}