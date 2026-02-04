using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

/// <summary>
/// Triggers the NPC's attack and waits for it to complete.
/// This is a BLOCKING action that stays running until the attack finishes.
/// 
/// Usage in behavior tree:
/// Sequence -> IsPlayerInRange -> NPCAttackPlayer -> Wait
/// </summary>
public class AttackPlayer : NPCAction
{
    [Tooltip("Safety timeout in case attack gets stuck")]
    [SerializeField] private float timeout = 5f;

    private float startTime;

    public override void OnStart()
    {
        base.OnStart();

        startTime = Time.time;
        nPC.Attack();
    }

    public override TaskStatus OnUpdate()
    {
        // Safety check: timeout
        if (Time.time - startTime >= timeout)
        {
            Debug.LogWarning($"[NPCAttackPlayer] Attack timed out after {timeout} seconds");
            nPC.attack.isAttacking = false;
            return TaskStatus.Success;
        }

        // Check if player left attack range during attack
        if (!nPC.attack.playerInAttackRange)
        {
            return TaskStatus.Failure;
        }

        // Keep running while attacking
        if (nPC.attack.isAttacking)
        {
            return TaskStatus.Running;
        }

        // Attack completed successfully
        return TaskStatus.Success;
    }
}