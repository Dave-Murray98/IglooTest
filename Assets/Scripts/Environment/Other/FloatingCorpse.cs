using Unity.Entities.UniversalDelegates;
using Unity.VisualScripting;
using UnityEngine;

public class FloatingCorpse : MonoBehaviour
{
    private float minRandomOffset = 0.05f;
    private float maxRandomOffset = 0.08f;

    private Rigidbody rb;

    private float knockbackForce = 15f;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        Vector3 randomOffset = Vector3.one * Random.Range(minRandomOffset, maxRandomOffset);
        ParticleFXPool.CreateBloodSplat(collision.transform.position + randomOffset, collision.transform.rotation);
        ParticleFXPool.CreateBloodSplat(collision.transform.position, collision.transform.rotation);

        PlayerHurtbox playerHurtbox = collision.gameObject.GetComponentInParent<PlayerHurtbox>();

        if (playerHurtbox != null)
        {
            Vector3 knockbackDirection = collision.contacts[0].normal;
            rb.AddForce(knockbackDirection * knockbackForce, ForceMode.Impulse);
        }
    }
}
