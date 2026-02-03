using Opsive.BehaviorDesigner.Runtime;
using UnityEngine;

public class NPCStateMachine : StateMachine
{
    #region Behaviour Trees
    [Header("Behaviour Trees")]
    public BehaviorTree wanderBehaviourTree;
    public BehaviorTree fleeBehaviourTree;
    public BehaviorTree combatBehaviourTree; // Only used by hostile NPCs
    #endregion

    #region States
    [HideInInspector]
    public WanderState wanderState;
    [HideInInspector]
    public FleeState fleeState;
    [HideInInspector]
    public CombatState combatState; // Only initialized for hostile NPCs

    #endregion

    [Header("References")]
    public NPC npc;


    protected override void Awake()
    {
        base.Awake();
        AssignComponents();

        InitializeStates();

        GameEvents.OnGamePaused += PauseCurrentBehaviorTree;
        GameEvents.OnGameResumed += ResumeCurrentBehaviorTree;
    }

    private void AssignComponents()
    {
        if (npc == null)
        {
            npc = GetComponent<NPC>();
        }
    }

    protected override State GetInitialState()
    {
        DebugLog("Getting initial state");
        return wanderState;
    }

    protected virtual void InitializeStates()
    {
        // All NPCs have wander and flee states
        wanderState = new WanderState(this);
        fleeState = new FleeState(this);

        // Only hostile NPCs get a combat state
        if (npc.config != null && npc.config.isHostile)
        {
            combatState = new CombatState(this, npc.config.CombatDisengageDistance);
            DebugLog("Combat state initialized (Hostile NPC)");
        }
        else
        {
            DebugLog("Combat state skipped (Passive NPC)");
        }
    }

    public void ForceChangeState(NPCState newState)
    {
        if (newState == null)
            return;

        DebugLog("Forcing change to" + newState);

        ChangeState(newState);
    }

    public virtual void PauseCurrentBehaviorTree()
    {
        NPCState currentNPCState = (NPCState)currentState;
        if (currentNPCState != null)
        {
            BehaviorTree currentBehaviorTree = GetBehaviorTreeForState(currentNPCState);
            if (currentBehaviorTree != null)
            {
                currentBehaviorTree.StopBehavior();
            }
        }
    }

    public virtual void ResumeCurrentBehaviorTree()
    {
        NPCState currentNPCState = (NPCState)currentState;
        if (currentNPCState != null)
        {
            BehaviorTree currentBehaviorTree = GetBehaviorTreeForState(currentNPCState);
            if (currentBehaviorTree != null)
            {
                currentBehaviorTree.StartBehavior();
            }
        }
    }

    protected BehaviorTree GetBehaviorTreeForState(NPCState state)
    {
        if (state == wanderState)
            return wanderBehaviourTree;
        else if (state == fleeState)
            return fleeBehaviourTree;
        else if (state == combatState)
            return combatBehaviourTree;
        else
            return null;
    }

    public virtual void UpdateCurrentBehaviourTree()
    {
        if (currentState != null)
        {
            NPCState currentNPCState = (NPCState)currentState;
            currentNPCState.UpdateCurrentBehaviourTree();
        }
    }

    private void OnDestroy()
    {
        GameEvents.OnGamePaused -= PauseCurrentBehaviorTree;
        GameEvents.OnGameResumed -= ResumeCurrentBehaviorTree;
    }
}