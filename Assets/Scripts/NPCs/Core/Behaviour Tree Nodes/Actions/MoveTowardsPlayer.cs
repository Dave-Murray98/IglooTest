using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

/// <summary>
/// Continuously moves towards the player by updating the destination.
/// This node always returns Running, allowing the selector to check attack range each update.
/// 
/// Efficient: Only updates destination when player moves beyond threshold distance.
/// 
/// Usage in behavior tree:
/// Selector ->
///   1. Sequence -> IsPlayerInAttackRange -> AttackPlayer -> Wait
///   2. MoveTowardsPlayer (this node - continuously runs)
/// </summary>
public class MoveTowardsPlayer : NPCAction
{
    [Header("Update Settings")]
    [SerializeField]
    [Tooltip("Only update destination if player moves this far (prevents excessive pathfinding updates)")]
    private float updateThreshold = 2f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private Vector3 lastPlayerPosition;
    private bool isFirstUpdate = true;

    public override void OnStart()
    {
        base.OnStart();

        // Reset tracking on start
        isFirstUpdate = true;
        lastPlayerPosition = Vector3.zero;

        DebugLog("MoveTowardsPlayer started");
    }

    public override TaskStatus OnUpdate()
    {
        // Validate we have the necessary references
        if (NPCManager.Instance == null || NPCManager.Instance.PlayerTransform == null)
        {
            DebugLog("Warning: NPCManager or PlayerTransform is null!");
            return TaskStatus.Running; // Keep trying
        }

        Vector3 currentPlayerPosition = NPCManager.Instance.PlayerTransform.position;

        // Check if we should update the destination
        bool shouldUpdate = isFirstUpdate ||
                           Vector3.Distance(currentPlayerPosition, lastPlayerPosition) >= updateThreshold;

        if (shouldUpdate)
        {
            // Update destination to current player position
            nPC.movementScript.SetDestination(currentPlayerPosition);

            // Track this position
            lastPlayerPosition = currentPlayerPosition;
            isFirstUpdate = false;

            DebugLog($"Updated destination to player position: {currentPlayerPosition}");
        }

        // Always return Running - this allows the selector to keep checking
        // if the player is in attack range (priority 1 in the selector)
        return TaskStatus.Running;
    }

    public override void OnEnd()
    {
        base.OnEnd();
        DebugLog("MoveTowardsPlayer ended");
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[MoveTowardsPlayer - {gameObject.name}] {message}");
        }
    }
}