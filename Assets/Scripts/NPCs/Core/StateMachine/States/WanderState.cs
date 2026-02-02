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

        if (npc.health.isDead)
            stateMachine.ChangeState(sm.retreatState);


        // Note: Behavior tree updates are handled by NPCStateMachineManager
        // No need to manually call UpdateCurrentBehaviourTree() here
    }

    public override void UpdateCurrentBehaviourTree()
    {
        base.UpdateCurrentBehaviourTree();
        wanderBehaivourTree.Tick();
    }
}