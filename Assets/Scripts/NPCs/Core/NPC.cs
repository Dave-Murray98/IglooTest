using UnityEngine;

/// <summary>
/// Main NPC component that holds references to all NPC-related scripts.
/// This is the central component that other systems interact with.
/// Now supports both the original movement script and the new managed movement script.
/// </summary>
public class NPC : MonoBehaviour
{

    [Header("Configuration")]
    public NPCConfig config;
    [Header("NPC Components")]
    public NPCManagedUnderwaterMovement movementScript;
    public Rigidbody rb;
    public NPCStateMachine stateMachine;
    public NPCHealth health;
    public NPCAttack attack;

    private void Awake()
    {
        if (movementScript == null)
            movementScript = GetComponent<NPCManagedUnderwaterMovement>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (stateMachine == null)
            stateMachine = GetComponent<NPCStateMachine>();

        if (health == null)
            health = GetComponent<NPCHealth>();

        if (attack == null)
            attack = GetComponent<NPCAttack>();
    }

    public void Attack()
    {
        if (attack != null)
            attack.Attack();
    }
}