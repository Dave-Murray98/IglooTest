using UnityEngine;

public class NPCHurtBox : MonoBehaviour
{
    public float damage = 10f;
    public float attackKnockBackForce = 100f;

    public Vector3 attackDirection;

    private void OnTriggerEnter(Collider other)
    {
        SubmarineDamageDetector damageDetector = other.GetComponent<SubmarineDamageDetector>();
        if (damageDetector != null)
        {
            damageDetector.TakeDamageFromAttack(transform.position, damage, attackDirection, attackKnockBackForce);
        }
    }

}