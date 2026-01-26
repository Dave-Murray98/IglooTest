using Opsive.BehaviorDesigner.Runtime;
using UnityEngine;

public class EnemyStateMachine : StateMachine
{

    #region Behaviour Trees
    [Header("Behaviour Trees")]
    public BehaviorTree patrollingBehaviourTree;
    public BehaviorTree engagingPlayerBehaviourTree;
    public BehaviorTree pursuingPlayerBehaviourTree;
    public BehaviorTree investigatingNoiseBehaviourTree;
    public BehaviorTree deathBehaviourTree;
    #endregion

    #region States
    [HideInInspector]
    public PatrollingState patrolState;

    [HideInInspector]
    public EngagingState engageState;

    [HideInInspector]
    public PursuingState pursueState;

    [HideInInspector]
    public InvestigatingNoiseState investigateState;

    [HideInInspector] public DeathState deathState;

    [HideInInspector] public StunnedState stunnedState;

    #endregion

    [Header("References")]
    public UnderwaterMonsterController controller;

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
        if (controller == null)
        {
            controller = GetComponent<UnderwaterMonsterController>();
        }
    }

    protected override State GetInitialState()
    {
        DebugLog("Getting initial state");
        return patrolState;
    }

    protected virtual void InitializeStates()
    {
        patrolState = new PatrollingState(this);
        engageState = new EngagingState(this);
        pursueState = new PursuingState(this);
        investigateState = new InvestigatingNoiseState(this);
        deathState = new DeathState(this);
        stunnedState = new StunnedState(this);
    }

    public void ForceChangeState(EnemyState newState)
    {
        if (newState == null)
            return;

        DebugLog("Forcing change to" + newState);

        ChangeState(newState);
    }

    public virtual void PauseCurrentBehaviorTree()
    {
        EnemyState currentEnemyState = (EnemyState)currentState;
        if (currentEnemyState != null)
        {
            BehaviorTree currentBehaviorTree = GetBehaviorTreeForState(currentEnemyState);
            if (currentBehaviorTree != null)
            {
                currentBehaviorTree.StopBehavior();
            }
        }
    }

    public virtual void ResumeCurrentBehaviorTree()
    {
        EnemyState currentEnemyState = (EnemyState)currentState;
        if (currentEnemyState != null)
        {
            BehaviorTree currentBehaviorTree = GetBehaviorTreeForState(currentEnemyState);
            if (currentBehaviorTree != null)
            {
                currentBehaviorTree.StartBehavior();
            }
        }
    }

    protected BehaviorTree GetBehaviorTreeForState(EnemyState state)
    {
        if (state == patrolState)
            return patrollingBehaviourTree;
        else if (state == engageState)
            return engagingPlayerBehaviourTree;
        else if (state == pursueState)
            return pursuingPlayerBehaviourTree;
        else if (state == investigateState)
            return investigatingNoiseBehaviourTree;
        else if (state == deathState)
            return deathBehaviourTree;
        else
            return null;
    }

    private void OnDestroy()
    {
        GameEvents.OnGamePaused -= PauseCurrentBehaviorTree;
        GameEvents.OnGameResumed -= ResumeCurrentBehaviorTree;
    }
}