using UnityEngine;

/// <summary>
/// Main NPC component that holds references to all NPC-related scripts.
/// This is the central component that other systems interact with.
/// Now supports both the original movement script and the new managed movement script.
/// </summary>
public class NPC : MonoBehaviour
{
    [Header("NPC Components")]
    // [SerializeField] private NPCSimpleUnderwaterMovement movementScript;
    [SerializeField] private NPCManagedUnderwaterMovement managedMovementScript;
    [SerializeField] private Rigidbody rb;

    // public NPCSimpleUnderwaterMovement MovementScript => movementScript;
    public NPCManagedUnderwaterMovement ManagedMovementScript => managedMovementScript;
    public Rigidbody Rigidbody => rb;

    private void Awake()
    {
        // Auto-find components if not assigned
        // if (movementScript == null)
        //     movementScript = GetComponent<NPCSimpleUnderwaterMovement>();

        if (managedMovementScript == null)
            managedMovementScript = GetComponent<NPCManagedUnderwaterMovement>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();
    }
}