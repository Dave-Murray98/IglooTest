using UnityEngine;

/// <summary>
/// State when the monster is stunned and cannot move or act.
/// 
/// SIMPLIFIED VERSION:
/// - No need to manually activate/deactivate movement
/// - Just clears the target position to stop movement
/// - Movement resumes automatically when state exits
/// </summary>
public class StunnedState : EnemyState
{
    [Header("Stun Settings")]
    [Tooltip("How long the stun lasts")]
    public float stunDuration = 5f;

    private float stunTimer = 0f;

    public StunnedState(EnemyStateMachine stateMachine) : base("Stunned", stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        stunTimer = 0f;

        // SIMPLIFIED: Stop movement by clearing target
        // No need to call DeactivateMovement()
        controller.SetTargetPosition(Vector3.zero);

        // Optionally freeze the rigidbody completely
        if (controller.rb != null)
        {
            controller.rb.linearVelocity = Vector3.zero;
            controller.rb.angularVelocity = Vector3.zero;
        }

        sm.DebugLog("STUNNED STATE ENTERED - Monster frozen for " + stunDuration + " seconds");
    }

    public override void Exit()
    {
        base.Exit();

        // Clear the stunned flag
        controller.health.isStunned = false;

        // SIMPLIFIED: No need to call ActivateMovement()
        // The next state will set a new target position and movement will resume automatically

        sm.DebugLog("STUNNED STATE EXITED");
    }

    public override void UpdateLogic()
    {
        base.UpdateLogic();

        // Count down stun timer
        stunTimer += Time.deltaTime;

        // Stun duration expired?
        if (stunTimer >= stunDuration)
        {
            // Transition to pursuing the player
            stateMachine.ChangeState(sm.pursueState);
        }
    }
}