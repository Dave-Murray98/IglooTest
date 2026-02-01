using UnityEngine;

public class WanderState : NPCState
{
    public WanderState(NPCStateMachine stateMachine) : base("Wander", stateMachine) { }

    private float updateTreeDelay = 0.5f;
    private float updateTreeTimer = 0f;

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

        updateTreeTimer += Time.deltaTime;

        if (updateTreeTimer >= updateTreeDelay)
        {
            updateTreeTimer = 0f;
            UpdateCurrentBehaviourTree();
        }
    }

    public override void UpdateCurrentBehaviourTree()
    {
        base.UpdateCurrentBehaviourTree();
        wanderBehaivourTree.Tick();
    }
}
