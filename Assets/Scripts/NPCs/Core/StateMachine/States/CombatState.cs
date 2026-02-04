using UnityEngine;

/// <summary>
/// Combat state for hostile NPCs.
/// Enables combat behavior tree and monitors player distance to transition back to wander.
/// </summary>
public class CombatState : NPCState
{
    private float combatDisengageDistance;

    public CombatState(NPCStateMachine stateMachine, float disengageDistance) : base("Combat", stateMachine)
    {
        combatDisengageDistance = disengageDistance;
    }

    public override void Enter()
    {
        base.Enter();

        if (sm.combatBehaviourTree != null)
        {
            sm.combatBehaviourTree.enabled = true;
        }

        sm.DebugLog("COMBAT STATE ENTERED");
    }

    public override void Exit()
    {
        base.Exit();

        if (sm.combatBehaviourTree != null)
        {
            sm.combatBehaviourTree.enabled = false;
        }
    }

    public override void UpdateLogic()
    {
        base.UpdateLogic();

        // Check if health is 0 or below - transition to flee
        if (npc.health.currentHealth <= 0)
        {
            stateMachine.ChangeState(sm.fleeState);
            return;
        }

        // Check if player is too far away - return to wander
        if (IsPlayerTooFar())
        {
            stateMachine.ChangeState(sm.wanderState);
        }
    }

    /// <summary>
    /// Check if the player is outside the disengage distance.
    /// </summary>
    private bool IsPlayerTooFar()
    {
        if (NPCManager.Instance == null || NPCManager.Instance.PlayerTransform == null)
            return true;

        float distanceToPlayer = Vector3.Distance(
            npc.transform.position,
            NPCManager.Instance.PlayerTransform.position
        );

        return distanceToPlayer > combatDisengageDistance;
    }

    /// <summary>
    /// Called by NPCStateMachineManager to update the behavior tree.
    /// Behaviour Trees are set to update manually.
    /// </summary>
    public override void UpdateCurrentBehaviourTree()
    {
        base.UpdateCurrentBehaviourTree();
        if (sm.combatBehaviourTree != null)
        {
            sm.combatBehaviourTree.Tick();
        }
    }

}