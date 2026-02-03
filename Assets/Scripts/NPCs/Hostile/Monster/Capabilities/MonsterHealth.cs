using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

public class MonsterHealth : MonoBehaviour
{
    public event Action OnDeath;
    public event Action OnMonsterTakeHit;
    public event Action OnMonsterStunned;

    public bool isAlive = true;
    public bool isStunned = false;
    public bool isHit = false;

    [SerializeField] private float currentHealth = 100f;
    [SerializeField] private float maxHealth = 100f;
    public float CurrentHealth { get => currentHealth; set => currentHealth = value; }
    public float MaxHealth { get => maxHealth; set => maxHealth = value; }

    [SerializeField] private MonsterHitBox[] hitBoxes;

    private void Start()
    {
        hitBoxes = GetComponentsInChildren<MonsterHitBox>();

        foreach (var hitBox in hitBoxes)
        {
            hitBox.health = this;
        }
    }

    [Button]
    public void TakeDamage(float damage)
    {
        if (!isAlive)
            return;

        currentHealth -= damage;

        OnMonsterTakeHit?.Invoke();
        isHit = true;

        if (currentHealth <= 0)
        {
            OnDeath?.Invoke();
            isAlive = false;
        }
    }

    public void StunMonster(float stunDuration)
    {
        if (isStunned || !isAlive)
            return;

        isStunned = true;
        OnMonsterStunned?.Invoke();
        StartCoroutine(StunCoroutine(stunDuration));
    }

    private IEnumerator StunCoroutine(float stunDuration)
    {
        yield return new WaitForSeconds(stunDuration);
        isStunned = false;
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
    }

    [Button]
    public void KillMonster()
    {
        TakeDamage(currentHealth);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }

}
