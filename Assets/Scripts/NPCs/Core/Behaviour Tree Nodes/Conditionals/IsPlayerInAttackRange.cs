using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

/// <summary>
/// Conditional node that checks if the player is within the NPC's attack range
/// </summary>
public class IsPlayerInAttackRange : NPCConditional
{
    public override TaskStatus OnUpdate()
    {
        return nPC.attack.playerInAttackRange ? TaskStatus.Success : TaskStatus.Failure;
    }
}