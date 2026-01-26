using UnityEngine;

public class PatrollingState : EnemyState
{

    public PatrollingState(EnemyStateMachine stateMachine) : base("Patrolling", stateMachine) { }

    private float idleNoiseTimer = 0f;

    private float idleNoiseDelay = 10f;

    private float maxIdleNoiseDelay = 20f;

    private float minIdleNoiseDelay = 10f;

    public override void Enter()
    {
        base.Enter();
        patrollingBehaviourTree.enabled = true;

        if (MusicManager.Instance != null) MusicManager.Instance.SetLowIntensity();

        sm.DebugLog("PATROLLING STATE ENTERED");
    }

    public override void Exit()
    {
        base.Exit();
        patrollingBehaviourTree.enabled = false;
    }

    public override void UpdateLogic()
    {
        base.UpdateLogic();

        if (controller.vision.CanSeePlayer)
        {
            stateMachine.ChangeState(((EnemyStateMachine)stateMachine).engageState);
        }

        if (controller.hearing.HasHeardRecentNoise)
        {
            stateMachine.ChangeState(((EnemyStateMachine)stateMachine).investigateState);
        }

        idleNoiseTimer += Time.deltaTime;

        if (idleNoiseTimer >= idleNoiseDelay)
            PlayIdleNoise();

    }

    private void PlayIdleNoise()
    {
        idleNoiseTimer = 0f;

        idleNoiseDelay = Random.Range(minIdleNoiseDelay, maxIdleNoiseDelay);

        controller.TriggerPlayIdleSound();
    }

}
