using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

/// <summary>
/// Conditional node that checks if the player is within a specified range.
/// Useful for triggering behaviors based on player proximity.
/// 
/// Usage in behavior tree:
/// Selector -> IsPlayerInRange -> ChasePlayer
///          -> Wander
/// </summary>
public class IsPlayerInRange : NPCConditional
{
    [Tooltip("Distance to check for player")]
    [SerializeField] private float detectionRange = 10f;

    public override TaskStatus OnUpdate()
    {
        if (NPCManager.Instance == null || NPCManager.Instance.PlayerTransform == null)
        {
            return TaskStatus.Failure;
        }

        float distanceToPlayer = Vector3.Distance(
            nPC.transform.position,
            NPCManager.Instance.PlayerTransform.position
        );

        return distanceToPlayer <= detectionRange ? TaskStatus.Success : TaskStatus.Failure;
    }
}