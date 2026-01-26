using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

/// <summary>
/// Continuously updates the target position to the player's current location.
/// This action keeps running and updates the target at regular intervals.
/// 
/// The movement system automatically follows the updated target.
/// 
/// Usage:
/// - In "Engaging" state: keeps the monster chasing the player
/// - In "Pursuing" state: same behavior
/// 
/// This node never completes on its own - it keeps running until the behavior tree
/// changes states or another node interrupts it.
/// </summary>
public class MoveTowardsPlayer : EnemyAction
{
    [Tooltip("How often to update the player's position (in seconds)")]
    [SerializeField] private float updateFrequency = 0.5f;

    private float updateTimer = 0f;

    public override void OnStart()
    {
        base.OnStart();

        // Immediately set the first target
        UpdateTargetToPlayer();
        updateTimer = 0f;
    }

    public override TaskStatus OnUpdate()
    {
        // Update timer
        updateTimer += Time.deltaTime;

        // Time to update the target?
        if (updateTimer >= updateFrequency)
        {
            UpdateTargetToPlayer();
            updateTimer = 0f;
        }

        // This action keeps running - it never completes on its own
        // The behavior tree or state machine must interrupt it to stop chasing
        return TaskStatus.Running;
    }

    /// <summary>
    /// Updates the controller's target position to match the player's current position.
    /// </summary>
    private void UpdateTargetToPlayer()
    {
        if (controller.player == null)
        {
            Debug.LogWarning($"[MoveTowardsPlayer] No player reference on {gameObject.name}");
            return;
        }

        controller.SetTargetToPlayerPosition();
    }

    public override void OnEnd()
    {
        base.OnEnd();
        updateTimer = 0f;
    }
}