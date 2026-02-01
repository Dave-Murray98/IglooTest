using UnityEngine;

/// <summary>
/// Main NPC component that holds references to all NPC-related scripts.
/// This is the central component that other systems interact with.
/// </summary>
public class NPC : MonoBehaviour
{
    [Header("NPC Components")]
    [SerializeField] private NPCSimpleUnderwaterMovement movementScript;
    [SerializeField] private Rigidbody rb;

    public NPCSimpleUnderwaterMovement MovementScript => movementScript;
    public Rigidbody Rigidbody => rb;

    private void Awake()
    {
        // Auto-find components if not assigned
        if (movementScript == null)
            movementScript = GetComponent<NPCSimpleUnderwaterMovement>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();
    }
}