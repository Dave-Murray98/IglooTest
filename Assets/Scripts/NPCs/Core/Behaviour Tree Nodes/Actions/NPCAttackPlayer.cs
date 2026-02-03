using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

public class NPCAttackPlayer : NPCAction
{
    private float backupTimer;
    private float timeout = 5f;

    public override void OnStart()
    {
        base.OnStart();

        backupTimer = 0;
        nPC.Attack();
    }

    public override TaskStatus OnUpdate()
    {
        backupTimer += Time.deltaTime;
        if (backupTimer >= timeout)
        {
            nPC.attack.isAttacking = false;
            return TaskStatus.Success;
        }

        while (nPC.attack.isAttacking)
        {
            if (!nPC.attack.playerInAttackRange)
            {
                return TaskStatus.Failure;
            }

            return TaskStatus.Running;
        }

        return TaskStatus.Success;
    }
}
