using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

public class HasDetectedEnvDestructible : EnemyConditional
{
    public override TaskStatus OnUpdate()
    {
        return controller.destructibleDetector.detectedDestructible ? TaskStatus.Success : TaskStatus.Failure;
    }
}

