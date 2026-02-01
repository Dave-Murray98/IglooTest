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

    public NPCState(string name, NPCStateMachine stateMachine) : base(name, stateMachine)
    {
        sm = (NPCStateMachine)this.stateMachine;

        // Get references to all behavior trees
        wanderBehaivourTree = sm.wanderBehaviourTree;

        npc = sm.npc;
    }

    // each behaviour tree has it's update mode set to manual, so whenever we want to update the behaviour tree, we call this function
    public virtual void UpdateCurrentBehaviourTree() { }
}