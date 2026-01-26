using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base hurtbox script for player weapons that deal instant damage on hit.
/// Perfect for weapons like baseball bats, swords, etc.
/// </summary>
public class PlayerHurtbox : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("How much damage this weapon deals per hit")]
    [SerializeField] protected float damageAmount = 10f;

    [Header("Debug")]
    [SerializeField] protected bool enableDebugLogs = false;

    /// <summary>
    /// Called when something enters the hurtbox trigger.
    /// Virtual so child classes can override this behavior.
    /// </summary>
    protected virtual void OnTriggerEnter(Collider other)
    {
        // Check if the object we hit is a monster hitbox
        MonsterHitBox monsterHitbox = other.GetComponent<MonsterHitBox>();

        if (monsterHitbox != null)
        {
            // Deal damage to the monster
            DealDamage(monsterHitbox, other);

            DebugLog($"Dealt {damageAmount} damage to {other.gameObject.name}");
        }
    }

    /// <summary>
    /// Deals damage to a monster hitbox.
    /// Virtual so child classes can override how damage is dealt.
    /// </summary>
    protected virtual void DealDamage(MonsterHitBox hitbox, Collider other)
    {
        // get the hit point 
        Vector3 hitPoint = other.ClosestPoint(transform.position);

        hitbox.TakeDamage(damageAmount, hitPoint);
    }

    protected virtual void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log("[PlayerHurtBox]" + message);
    }
}