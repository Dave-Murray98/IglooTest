using Opsive.BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

/// <summary>
/// Conditional node that checks if the player is within the NPC's attack range
/// </summary>
public class IsPlayerInAttackRange : NPCConditional
{
    [Tooltip("Distance to check for player")]
    [SerializeField] private float detectionRange = 10f;

    public override TaskStatus OnUpdate()
    {
        return nPC.attack.playerInAttackRange ? TaskStatus.Success : TaskStatus.Failure;
    }
}