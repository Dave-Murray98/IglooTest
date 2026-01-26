using UnityEngine;

/// <summary>
/// Triggers handler speech when the player enters a collider.
/// Most basic trigger type - useful for location-based speech triggers.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HandlerSpeechColliderTrigger : HandlerSpeechTriggerBase
{
    [Header("Collider Settings")]
    [Tooltip("The collider must be marked as trigger")]
    [SerializeField] private Collider[] triggerColliders;


    protected override void Awake()
    {
        base.Awake();

        // Find collider if not assigned
        triggerColliders = GetComponents<Collider>();


        // Validate collider setup
        if (triggerColliders != null)
        {
            foreach (Collider collider in triggerColliders)
                if (!collider.isTrigger)
                {
                    Debug.LogWarning($"[HandlerSpeechColliderTrigger:{gameObject.name}] Collider is not marked as trigger - fixing");
                    collider.isTrigger = true;
                }
        }
        else
        {
            Debug.LogError($"[HandlerSpeechColliderTrigger:{gameObject.name}] No collider found!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if we can trigger
        if (!CanTrigger())
        {
            DebugLog("Cannot trigger - conditions not met");
            return;
        }

        DebugLog($"Player entered trigger - triggering speech");
        TriggerSpeech();
    }

    protected override void OnTriggerDisabled()
    {
        base.OnTriggerDisabled();

        // Optionally disable collider after trigger
        if (triggerColliders != null && triggerOnce)
        {
            foreach (Collider collider in triggerColliders)
                collider.enabled = false;
            DebugLog("Collider disabled after one-time trigger");
        }
    }

    protected override void OnTriggerReset()
    {
        base.OnTriggerReset();

        // Re-enable collider when reset
        if (triggerColliders != null)
        {
            foreach (Collider collider in triggerColliders)
                collider.enabled = true;
            DebugLog("Collider re-enabled after reset");
        }
    }

#if UNITY_EDITOR
    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw trigger volume
        if (triggerColliders != null)
        {
            foreach (Collider collider in triggerColliders)
            {
                Gizmos.color = hasTriggered ? Color.gray : Color.yellow;
                Gizmos.matrix = transform.localToWorldMatrix;

                if (collider is BoxCollider boxCollider)
                {
                    Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
                }
                else if (collider is SphereCollider sphereCollider)
                {
                    Gizmos.DrawWireSphere(sphereCollider.center, sphereCollider.radius);
                }
                else if (collider is CapsuleCollider capsuleCollider)
                {
                    // Approximate capsule with sphere
                    Gizmos.DrawWireSphere(capsuleCollider.center, capsuleCollider.radius);
                }
            }

        }
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // Draw filled volume when selected
        if (triggerColliders != null)
        {
            Gizmos.color = hasTriggered ? new Color(0.5f, 0.5f, 0.5f, 0.3f) : new Color(1f, 1f, 0f, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;

            foreach (Collider collider in triggerColliders)
            {
                if (collider is BoxCollider boxCollider)
                {
                    Gizmos.DrawCube(boxCollider.center, boxCollider.size);
                }
                else if (collider is SphereCollider sphereCollider)
                {
                    Gizmos.DrawSphere(sphereCollider.center, sphereCollider.radius);
                }
            }
        }
    }
#endif
}