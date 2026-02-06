using UnityEngine;

public class FleeState : NPCState
{
    public FleeState(NPCStateMachine stateMachine) : base("Flee", stateMachine) { }

    private float fleeTimer = 0f;
    private float fleeDuration = 20f;

    public override void Enter()
    {
        base.Enter();
        fleeBehaviourTree.enabled = true;

        fleeTimer = 0f;

        sm.DebugLog("RETREAT STATE ENTERED");
    }

    public override void Exit()
    {
        base.Exit();
        fleeBehaviourTree.enabled = false;
    }

    public override void UpdateLogic()
    {
        fleeTimer += Time.deltaTime;
        if (fleeTimer >= fleeDuration)
        {
            fleeTimer = 0f;
            OnFleeComplete();
        }

        base.UpdateLogic();
    }

    private void OnFleeComplete()
    {
        npc.health.Revive();
        stateMachine.ChangeState(sm.wanderState);
    }


    /// <summary>
    /// Called by NPCStateMachineManager to update the behavior tree
    /// Behaviour Trees are set to update manually
    /// </summary>
    public override void UpdateCurrentBehaviourTree()
    {
        base.UpdateCurrentBehaviourTree();

        if (!fleeBehaviourTree.didStart)
            return;


        fleeBehaviourTree.Tick();
    }
}