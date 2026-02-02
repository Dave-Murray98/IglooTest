using UnityEngine;

public class RetreatState : NPCState
{
    public RetreatState(NPCStateMachine stateMachine) : base("Retreat", stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        retreatBehaviourTree.enabled = true;

        sm.DebugLog("RETREAT STATE ENTERED");
    }

    public override void Exit()
    {
        base.Exit();
        retreatBehaviourTree.enabled = false;
    }

    public override void UpdateLogic()
    {
        base.UpdateLogic();
    }

    public override void UpdateCurrentBehaviourTree()
    {
        base.UpdateCurrentBehaviourTree();
        retreatBehaviourTree.Tick();
    }
}