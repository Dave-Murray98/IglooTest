using System;
using UnityEngine;

public class NPCHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    public event Action OnDeath;

    public bool isDead = false;


    public void TakeDamage(float damage)
    {
        if (isDead)
            return;

        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            OnNPCDie();
        }
    }

    private void OnNPCDie()
    {
        isDead = true;
        OnDeath?.Invoke();
    }
}
