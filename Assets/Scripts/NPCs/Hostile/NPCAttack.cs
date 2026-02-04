using System;
using System.Collections;
using UnityEngine;

public class NPCAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NPC npc;

    [Header("Attack State")]
    public bool playerInAttackRange = false;
    public bool isAttacking = false;

    [Header("Components")]
    [SerializeField] private NPCHurtBox hurtBox;

    [Header("Attack Parameters")]
    [SerializeField] private float damage = 10f;

    [SerializeField] private float attackKnockBackForce = 400f;
    [Tooltip("The force applied to the enemy when it charges to attack")]
    [SerializeField] private float attackChargeForce = 400f;
    [SerializeField] private float attackDuration = 1.2f;

    [Header("Attack Timing")]
    [Tooltip("Time to gradually slow down before attacking")]
    [SerializeField] private float decelerationTime = 0.3f;
    [Tooltip("Brief pause at zero velocity before lunging")]
    [SerializeField] private float preparationPause = 0.2f;

    [Header("Attack Rotation")]
    [Tooltip("How fast the monster rotates to face the player during attack preparation")]
    [SerializeField] private float attackRotationSpeed = 5f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private Vector3 attackDirection;
    private Vector3 initialVelocity;
    private Quaternion targetRotation;
    public event Action OnPerformingAttack;

    private void Awake()
    {
        if (hurtBox == null)
            hurtBox = GetComponentInChildren<NPCHurtBox>();

        SetUpHurtBox();
    }

    private void SetUpHurtBox()
    {
        if (hurtBox == null)
            return;

        hurtBox.damage = damage;
        hurtBox.attackKnockBackForce = attackKnockBackForce;
        hurtBox.gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        SubmarineHealthManager submarineHealthManager = other.GetComponent<SubmarineHealthManager>();

        if (submarineHealthManager != null)
        {
            playerInAttackRange = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        SubmarineHealthManager submarineHealthManager = other.GetComponent<SubmarineHealthManager>();

        if (submarineHealthManager != null)
        {
            playerInAttackRange = false;
        }
    }

    public void Attack()
    {
        if (!isAttacking)
            StartCoroutine(AttackCoroutine());
    }

    #region Main Attack Sequence

    /// <summary>
    /// Full attack sequence with buildup, execution, and recovery.
    /// Used for attacking the player.
    /// </summary>
    public IEnumerator AttackCoroutine()
    {
        isAttacking = true;

        // Calculate attack direction and store initial velocity
        attackDirection = (NPCManager.Instance.PlayerTransform.position - transform.position).normalized;
        initialVelocity = npc.rb.linearVelocity;

        // Calculate target rotation to face the player
        targetRotation = Quaternion.LookRotation(attackDirection, Vector3.up);

        DebugLog($"Starting attack sequence. Initial velocity: {initialVelocity.magnitude:F2}");

        // Clear movement destination to pause pathfinding
        npc.movementScript.ClearDestination();

        // Phase 1: Gradual deceleration + rotation towards player
        yield return StartCoroutine(GradualDecelerationAndRotation());

        // Phase 2: Brief preparation pause (monster is now facing player and stationary)
        DebugLog("Preparation pause - monster is locked on target");
        yield return new WaitForSeconds(preparationPause);

        // Phase 3: Perform the actual attack
        PerformAttack();

        // Phase 4: Attack duration (cooldown)
        yield return new WaitForSeconds(attackDuration);

        OnAttackFinished();
    }

    /// <summary>
    /// Smoothly reduces the monster's velocity to zero while rotating to face the player.
    /// This creates a natural "winding up" effect with predator-like tracking behavior.
    /// </summary>
    private IEnumerator GradualDecelerationAndRotation()
    {
        DebugLog("Starting gradual deceleration and rotation towards player");

        float elapsedTime = 0f;
        Vector3 startVelocity = npc.rb.linearVelocity;
        Vector3 startAngularVelocity = npc.rb.angularVelocity;

        // Store starting rotation for smooth interpolation
        Quaternion startRotation = npc.transform.rotation;

        while (elapsedTime < decelerationTime)
        {
            // Calculate progress (0 to 1)
            float progress = elapsedTime / decelerationTime;

            // Use smooth interpolation for natural feel
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);

            // === MOVEMENT DECELERATION ===
            Vector3 currentVelocity = Vector3.Lerp(startVelocity, Vector3.zero, smoothProgress);
            npc.rb.linearVelocity = currentVelocity;

            // === ROTATION TOWARDS PLAYER ===
            // Smoothly rotate towards the target rotation
            npc.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, smoothProgress);

            // Stop any existing angular velocity during the rotation
            npc.rb.angularVelocity = Vector3.Lerp(startAngularVelocity, Vector3.zero, smoothProgress);

            elapsedTime += Time.deltaTime;
            yield return null; // Wait for next frame
        }

        // Ensure we're completely stopped and facing the right direction
        npc.rb.linearVelocity = Vector3.zero;
        npc.rb.angularVelocity = Vector3.zero;
        npc.transform.rotation = targetRotation;

        DebugLog("Deceleration and rotation complete - monster locked on target");
    }

    private void PerformAttack()
    {
        // Use the attack direction that was calculated at the start
        // (Monster is already facing this direction from the rotation phase)
        npc.rb.AddForce(attackDirection * attackChargeForce, ForceMode.Impulse);

        DebugLog($"Lunging towards player with force: {attackChargeForce}");

        // if (animationHandler != null)
        // {
        //     animationHandler.PlayAttackAnimation();
        // }

        hurtBox.attackDirection = attackDirection;
        hurtBox.gameObject.SetActive(true);

        OnPerformingAttack?.Invoke();
    }

    private void OnAttackFinished()
    {
        hurtBox.gameObject.SetActive(false);
        isAttacking = false;

        // Movement resumes automatically when behavior tree sets new destination
        DebugLog("Attack sequence complete");
    }

    public void OnAttackAborted()
    {
        hurtBox.gameObject.SetActive(false);
        isAttacking = false;

        StopAllCoroutines();

        // Movement resumes automatically when behavior tree sets new destination
        DebugLog("Attack sequence aborted");
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCAttack] {message}");
        }
    }
}