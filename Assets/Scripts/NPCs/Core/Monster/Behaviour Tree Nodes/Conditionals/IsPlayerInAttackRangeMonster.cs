using Opsive.BehaviorDesigner.Runtime.Tasks;

public class IsPlayerInAttackRangeMonster : EnemyConditional
{
    public override TaskStatus OnUpdate()
    {
        return controller.attack.playerInAttackRange ? TaskStatus.Success : TaskStatus.Failure;

    }
}
