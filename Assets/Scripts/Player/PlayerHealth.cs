using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float currentHealth = 100f;
    [SerializeField] private float maxHealth = 100f;

    public void TakeDamage(float damageAmount)
    {
        currentHealth -= damageAmount;
    }

}
