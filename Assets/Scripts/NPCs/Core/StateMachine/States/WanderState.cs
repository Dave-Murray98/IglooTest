using UnityEngine;

public class WanderState : NPCState
{
    public WanderState(NPCStateMachine stateMachine) : base("Wander", stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        wanderBehaivourTree.enabled = true;

        sm.DebugLog("WANDER STATE ENTERED");
    }

    public override void Exit()
    {
        base.Exit();
        wanderBehaivourTree.enabled = false;
    }

    public override void UpdateLogic()
    {
        base.UpdateLogic();

        if (npc.health.currentHealth <= 0)
            stateMachine.ChangeState(sm.fleeState);

        // Note: Behavior tree updates are handled by NPCStateMachineManager
        // No need to manually call UpdateCurrentBehaviourTree() here
    }

    /// <summary>
    /// Called by NPCStateMachineManager to update the behavior tree
    /// Behaviour Trees are set to update manually
    /// </summary>
    public override void UpdateCurrentBehaviourTree()
    {
        base.UpdateCurrentBehaviourTree();
        wanderBehaivourTree.Tick();
    }
}