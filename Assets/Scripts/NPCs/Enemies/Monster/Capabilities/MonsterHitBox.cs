using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class MonsterHitBox : MonoBehaviour
{
    [Title("Components")]
    public MonsterHealth health;

    [Header("Debug")]
    [SerializeField] protected bool enableDebugLogs = false;

    public Collider hitBoxCollider;

    private void Awake()
    {
        if (hitBoxCollider == null)
            hitBoxCollider = GetComponent<Collider>();
    }


    public virtual void TakeDamage(float damage, Vector3 hitPoint)
    {
        DebugLog($"Hit Monster for {damage} damage");

        if (health == null)
        {
            Debug.LogWarning("Health component not found!");
            return;
        }

        ParticleFXPool.Instance.GetBloodSplat(hitPoint, Quaternion.identity);

        health.TakeDamage(damage);
    }

    public virtual void StunMonster(float stunDuration)
    {
        DebugLog($"Stun Monster for {stunDuration} seconds");

        if (health == null)
        {
            Debug.LogWarning("Health component not found!");
            return;
        }

        health.StunMonster(stunDuration);
    }

    protected virtual void DebugLog(string message)
    {
        if (enableDebugLogs) Debug.Log($"[MonsterHitBox] {message}");
    }
}