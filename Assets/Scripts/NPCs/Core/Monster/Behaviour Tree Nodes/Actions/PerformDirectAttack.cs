using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

public class PerformDirectAttack : EnemyAction
{
    private float backupTimer;
    private float timeout = 5f;

    public override void OnStart()
    {
        base.OnStart();

        backupTimer = 0;
        controller.DirectAttack();
    }

    public override TaskStatus OnUpdate()
    {
        backupTimer += Time.deltaTime;
        if (backupTimer >= timeout)
        {
            controller.attack.isAttacking = false;
            return TaskStatus.Success;
        }

        while (controller.attack.isAttacking)
        {
            return TaskStatus.Running;
        }

        return TaskStatus.Success;
    }
}
