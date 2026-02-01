using Opsive.BehaviorDesigner.Runtime;
using UnityEngine;

public class NPCStateMachine : StateMachine
{
    #region Behaviour Trees
    [Header("Behaviour Trees")]
    public BehaviorTree wanderBehaviourTree;
    #endregion

    #region States
    [HideInInspector]
    public WanderState wanderState;

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
        wanderState = new WanderState(this);

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
        else
            return null;
    }

    private void OnDestroy()
    {
        GameEvents.OnGamePaused -= PauseCurrentBehaviorTree;
        GameEvents.OnGameResumed -= ResumeCurrentBehaviorTree;
    }
}