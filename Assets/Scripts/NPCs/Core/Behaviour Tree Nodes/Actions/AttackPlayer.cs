using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

/// <summary>
/// Triggers the NPC's attack and waits for it to complete.
/// This is a BLOCKING action that stays running until the attack finishes.
/// 
/// Usage in behavior tree:
/// Sequence -> IsPlayerInRange -> AttackPlayer -> Wait
/// </summary>
public class AttackPlayer : NPCAction
{
    [Tooltip("Safety timeout in case attack gets stuck")]
    [SerializeField] private float timeout = 5f;

    private float backupTimer = 0f;

    [SerializeField] private bool enableDebugLogs = false;

    public override void OnStart()
    {
        base.OnStart();

        backupTimer = 0f;
        nPC.Attack();
    }

    public override TaskStatus OnUpdate()
    {
        backupTimer += Time.deltaTime;

        // Safety check: timeout
        if (backupTimer >= timeout)
        {
            Debug.LogWarning($"[NPCAttackPlayer] Attack timed out after {timeout} seconds");
            nPC.attack.isAttacking = false;
            return TaskStatus.Success;
        }

        // Check if player left attack range during attack
        if (!nPC.attack.playerInAttackRange)
        {
            DebugLog($"Player left attack range during attack, aborting attack");
            nPC.attack.OnAttackAborted();
            return TaskStatus.Failure;
        }

        // Keep running while attacking
        if (nPC.attack.isAttacking)
        {
            return TaskStatus.Running;
        }

        DebugLog("Attack completed");
        // Attack completed successfully
        return TaskStatus.Success;
    }

    public override void OnEnd()
    {
        base.OnEnd();
        backupTimer = 0f;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log("[AttackPlayer Node]" + message);
        }
    }
}