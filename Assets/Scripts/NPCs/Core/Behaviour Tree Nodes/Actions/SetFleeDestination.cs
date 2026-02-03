using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

/// <summary>
/// Sets a destination away from the player for the NPC to flee to.
/// This is a ONE-SHOT action that completes immediately after setting the destination.
/// 
/// Usage in behavior tree:
/// Sequence -> SetFleeDestination -> WaitForArrival -> Idle -> (repeat)
/// </summary>
public class SetFleeDestination : NPCAction
{
    [Tooltip("How far away from the player to flee")]
    [SerializeField] private float fleeDistance = 20f;

    public override void OnStart()
    {
        base.OnStart();

        if (NPCManager.Instance != null && NPCManager.Instance.PlayerTransform != null)
        {
            Vector3 fleeDestination = CalculateFleeDestination();
            nPC.movementScript.SetDestination(fleeDestination);
        }
    }

    public override TaskStatus OnUpdate()
    {
        // This action completes immediately
        return TaskStatus.Success;
    }

    /// <summary>
    /// Calculate a position away from the player.
    /// </summary>
    private Vector3 CalculateFleeDestination()
    {
        Vector3 playerPosition = NPCManager.Instance.PlayerTransform.position;
        Vector3 npcPosition = nPC.transform.position;

        // Get direction away from player
        Vector3 fleeDirection = (npcPosition - playerPosition).normalized;

        // Calculate target position
        Vector3 targetPosition = npcPosition + (fleeDirection * fleeDistance);

        // Try to get a valid navigation position near the target
        Vector3 validPosition = NPCPathfindingUtilities.Instance.GetRandomValidPositionNearPoint(
            targetPosition,
            fleeDistance * 0.5f
        );

        // If we couldn't find a valid position, use the calculated target
        if (validPosition == Vector3.zero)
        {
            validPosition = targetPosition;
        }

        return validPosition;
    }
}