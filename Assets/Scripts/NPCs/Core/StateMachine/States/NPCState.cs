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
public class NPCState : State
{
    protected NPCStateMachine sm;
    protected NPC npc;

    // References to behavior trees for each state
    protected BehaviorTree wanderBehaivourTree;
    protected BehaviorTree fleeBehaviourTree;
    protected BehaviorTree combatBehaviourTree;

    public NPCState(string name, NPCStateMachine stateMachine) : base(name, stateMachine)
    {
        sm = (NPCStateMachine)this.stateMachine;

        // Get references to all behavior trees
        wanderBehaivourTree = sm.wanderBehaviourTree;
        fleeBehaviourTree = sm.fleeBehaviourTree;
        combatBehaviourTree = sm.combatBehaviourTree;

        npc = sm.npc;
    }

    /// <summary>
    /// Called by NPCStateMachineManager to update the behavior tree
    /// Behaviour Trees are set to update manually
    /// </summary>
    public virtual void UpdateCurrentBehaviourTree() { }


    protected void DebugLog(string message)
    {
        if (sm.enableDebugLogs)
            DebugLog(message);

    }
}