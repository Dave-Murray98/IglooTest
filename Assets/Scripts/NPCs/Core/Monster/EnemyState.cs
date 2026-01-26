using Opsive.BehaviorDesigner.Runtime;

/// <summary>
/// Base class for all enemy states.
/// States represent high-level behaviors like "Patrolling", "Engaging Player", etc.
/// 
/// Key Philosophy:
/// - Each state activates its associated behavior tree
/// - States set the appropriate movement speed
/// - States handle global transitions (health, hit reactions, stun)
/// </summary>
public class EnemyState : State
{
    protected EnemyStateMachine sm;
    protected UnderwaterMonsterController controller;

    // References to behavior trees for each state
    protected BehaviorTree patrollingBehaviourTree;
    protected BehaviorTree engagingPlayerBehaviourTree;
    protected BehaviorTree pursuingPlayerBehaviourTree;
    protected BehaviorTree investigatingNoiseBehaviourTree;
    protected BehaviorTree deathBehaviourTree;

    public EnemyState(string name, EnemyStateMachine stateMachine) : base(name, stateMachine)
    {
        sm = (EnemyStateMachine)this.stateMachine;

        // Get references to all behavior trees
        patrollingBehaviourTree = sm.patrollingBehaviourTree;
        engagingPlayerBehaviourTree = sm.engagingPlayerBehaviourTree;
        investigatingNoiseBehaviourTree = sm.investigatingNoiseBehaviourTree;
        pursuingPlayerBehaviourTree = sm.pursuingPlayerBehaviourTree;
        deathBehaviourTree = sm.deathBehaviourTree;

        controller = sm.controller;
    }

    public override void Enter()
    {
        base.Enter();

        // Tell the movement system what speed to use for this state
        // This is the ONLY place movement speed is changed
        if (controller.movement != null)
        {
            controller.movement.SetMovementSpeedBasedOnState(this);
        }
    }

    public override void UpdateLogic()
    {
        base.UpdateLogic();

        // Handle global state transitions that can happen from any state
        HandleGlobalTransitions();
    }

    /// <summary>
    /// Checks for conditions that should force a state change regardless of current state.
    /// Examples: death, being hit, getting stunned
    /// </summary>
    private void HandleGlobalTransitions()
    {
        // Death takes priority over everything
        if (controller.health.CurrentHealth <= 0)
        {
            if (sm.currentState != sm.deathState)
            {
                stateMachine.ChangeState(sm.deathState);
            }
            return;
        }

        // Stunned state (can happen from any state except death)
        if (controller.health.isAlive && controller.health.isStunned)
        {
            if (sm.currentState != sm.stunnedState)
            {
                stateMachine.ChangeState(sm.stunnedState);
            }
            return;
        }

        // Hit reaction - switch to engage state (if alive and not stunned)
        if (controller.health.isHit)
        {
            if (controller.health.isAlive && !controller.health.isStunned)
            {
                stateMachine.ChangeState(sm.engageState);
            }

            // Reset hit flag after processing
            controller.health.isHit = false;
        }
    }
}