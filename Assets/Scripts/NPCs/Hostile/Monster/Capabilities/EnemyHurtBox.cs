using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

public class EnemyHurtBox : MonoBehaviour
{
    public float damage = 10f;
    public float attackKnockBackForce = 100f;
    public Vector3 forceDirection = Vector3.forward;

    public float cooldown = 1f;

    [SerializeField] private bool enableDebugLogs = false;

    private void OnTriggerEnter(Collider other)
    {

        DebugLog($"{other.name} entered hurtbox");

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();

        if (playerHealth != null)
        {
            DebugLog($"Dealing {damage} damage to {other.name}");

            playerHealth.TakeDamage(damage);

            ParticleFXPool.Instance.GetBloodSplat(other.ClosestPoint(transform.position), Quaternion.identity);

            Rigidbody rb = playerHealth.GetComponent<Rigidbody>();
            rb.AddForce(forceDirection * attackKnockBackForce, ForceMode.Impulse);
        }
        else
            DebugLog($"No PlayerHealth component found on {other.name}");

    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[EnemyHurtBox] {message}");
        }
    }

}
