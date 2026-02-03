using UnityEngine;

public class WanderState : NPCState
{
    private float combatEngageDistance;

    public WanderState(NPCStateMachine stateMachine) : base("Wander", stateMachine)
    {
        // Get combat distance if this is a hostile NPC
        if (sm.npc.config != null && sm.npc.config.isHostile)
        {
            combatEngageDistance = sm.npc.config.CombatEngageDistance;
        }
    }

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

        // Check if health is 0 or below - flee
        if (npc.health.currentHealth <= 0)
        {
            stateMachine.ChangeState(sm.fleeState);
            return;
        }

        // Check if this is a hostile NPC and player is in combat range
        if (sm.npc.config != null && sm.npc.config.isHostile && sm.combatState != null)
        {
            if (IsPlayerInCombatRange())
            {
                stateMachine.ChangeState(sm.combatState);
            }
        }
    }

    /// <summary>
    /// Check if the player is within combat engage distance.
    /// Only called for hostile NPCs.
    /// </summary>
    private bool IsPlayerInCombatRange()
    {
        if (NPCManager.Instance == null || NPCManager.Instance.PlayerTransform == null)
            return false;

        float distanceToPlayer = Vector3.Distance(
            npc.transform.position,
            NPCManager.Instance.PlayerTransform.position
        );

        return distanceToPlayer <= combatEngageDistance;
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