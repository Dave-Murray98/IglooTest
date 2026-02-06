using System;
using UnityEngine;

public class NPCHealth : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NPC nPC;

    [Header("Stats")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;
    public bool isDead = false;

    [Header("Debug")]
    public bool enableDebugLogs = false;

    public event Action OnDeath;



    private void Awake()
    {
        if (nPC == null)
            nPC = GetComponent<NPC>();
    }

    public void TakeDamage(float damage, Vector3 hitDirection, float hitForce)
    {
        if (isDead)
        {
            DebugLog("Take Damage called: NPC already dead");
            return;
        }

        DebugLog($"Dealt {damage} damage to {gameObject.name}");

        if (nPC != null)
            nPC.rb.AddForce(hitDirection * hitForce, ForceMode.Impulse);

        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            OnNPCDie();
        }
    }

    private void OnNPCDie()
    {
        isDead = true;
        DebugLog("NPC died");
        OnDeath?.Invoke();
    }

    public void Revive()
    {
        DebugLog("NPC revived");
        currentHealth = maxHealth;
        isDead = false;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log("[NPCHealth]" + message);
    }
}
