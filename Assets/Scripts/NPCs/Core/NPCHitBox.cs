using System;
using Unity.Mathematics;
using UnityEngine;

public class NPCHitBox : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NPCHealth npcHealth;

    [Header("Debug")]
    public bool enableDebugLogs = false;

    private void Awake()
    {
        if (npcHealth == null)
        {
            npcHealth = GetComponentInParent<NPCHealth>();
        }
    }

    public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, float hitForce)
    {
        if (npcHealth != null)
        {
            npcHealth.TakeDamage(damage, hitDirection, hitForce);
            ParticleFXPool.Instance.GetBloodSplat(hitPoint, quaternion.identity);
            DebugLog($"Dealt {damage} damage to {gameObject.name}");
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log("[NPCHitBox]" + message);
    }
}
