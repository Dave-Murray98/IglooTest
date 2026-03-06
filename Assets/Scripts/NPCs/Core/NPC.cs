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
    public NPCAnimationHandler animationHandler;
    public Rigidbody rb;
    public NPCStateMachine stateMachine;
    public NPCHealth health;
    public NPCAttack attack;
    public NPCAudio nPCAudio;

    private void Awake()
    {
        if (movementScript == null)
            movementScript = GetComponent<NPCManagedUnderwaterMovement>();

        if (animationHandler == null)
            animationHandler = GetComponent<NPCAnimationHandler>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (stateMachine == null)
            stateMachine = GetComponent<NPCStateMachine>();

        if (health == null)
            health = GetComponent<NPCHealth>();

        if (attack == null)
            attack = GetComponent<NPCAttack>();

        if (nPCAudio == null)
            nPCAudio = GetComponent<NPCAudio>();
    }

    public void Attack()
    {
        if (attack != null)
            attack.Attack();
    }
}