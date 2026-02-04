using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

/// <summary>
/// Sets the player's current position as the destination.
/// This is a ONE-SHOT action that completes immediately after setting the destination.
/// 
/// Usage in behavior tree:
/// Sequence -> SetDestinationToPlayer -> NPCWaitForArrival -> (repeat)
/// </summary>
public class SetDestinationToPlayer : NPCAction
{
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    public override void OnStart()
    {
        base.OnStart();

        if (NPCManager.Instance != null && NPCManager.Instance.PlayerTransform != null)
        {
            Vector3 playerPosition = NPCManager.Instance.PlayerTransform.position;
            float distanceToPlayer = Vector3.Distance(transform.position, playerPosition);

            DebugLog($"Setting destination to player position: {playerPosition}");
            DebugLog($"Current NPC position: {transform.position}");
            DebugLog($"Distance to player: {distanceToPlayer:F2}");

            nPC.movementScript.SetDestination(playerPosition);
        }
        else
        {
            Debug.LogWarning("[NPCMoveTowardsPlayer] NPCManager or PlayerTransform is null!");
        }
    }

    public override TaskStatus OnUpdate()
    {
        // This action completes immediately - just like SetWanderDestination
        return TaskStatus.Success;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCMoveTowardsPlayer] {message}");
        }
    }
}