using System;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// SIMPLIFIED underwater monster controller.
/// Acts as a "coordinator" between different systems:
/// - Exposes simple methods for behavior trees to use
/// - Manages references to all monster components
/// - Delegates work to specialized systems (movement, attack, etc.)
/// 
/// Key Philosophy:
/// - No complex logic here - just coordination
/// - Behavior trees tell it WHERE to go
/// - Movement system handles HOW to get there
/// - State machine decides WHICH behavior to use
/// </summary>
public class UnderwaterMonsterController : MonoBehaviour
{
    [Header("Core References")]
    public Transform player;
    public Rigidbody rb;

    [Header("Components")]
    public MonsterUnderwaterMovement movement;
    public MonsterAwarenessOfPlayerPostionSystem awarenessSystem;
    [SerializeField] private MonsterAnimationHandler animationHandler;

    public EnemyStateMachine stateMachine;

    [Header("Capabilities")]
    public MonsterHealth health;
    public MonsterAttack attack;
    public EnemyVision vision;
    public EnemyHearing hearing;
    public EnemyEnvironmentalDestructibleDetector destructibleDetector;

    [Header("State Configuration")]
    [Tooltip("Where the monster retreats to when dying")]
    public Transform monsterDeathRetreatTransform;

    [Tooltip("Maximum distance to chase player before giving up")]
    public float maxEngageDistance = 10f;

    [Tooltip("How long to pursue player before giving up")]
    public float maxPursuitTime = 10f;

    [Tooltip("How long to investigate a noise location")]
    public float investigateStateTime = 10f;

    [Tooltip("Radius around noise to search")]
    public float investigateStateRadius = 10f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    /// <summary>
    /// The current target position the monster is trying to reach.
    /// This is the SINGLE SOURCE OF TRUTH for where the monster wants to go.
    /// The movement system automatically follows this.
    /// </summary>
    [ShowInInspector, ReadOnly]
    public Vector3 targetPosition { get; private set; }

    // Events for external systems
    public event Action OnMonsterDespawned;
    public event Action OnPlayIdleSound;

    #region Initialization

    private void Awake()
    {
        GetComponents();
    }

    private void GetComponents()
    {
        // Auto-find components if not assigned
        if (stateMachine == null)
            stateMachine = GetComponent<EnemyStateMachine>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (movement == null)
            movement = GetComponent<MonsterUnderwaterMovement>();

        if (animationHandler == null)
            animationHandler = GetComponent<MonsterAnimationHandler>();

        if (health == null)
            health = GetComponent<MonsterHealth>();

        if (attack == null)
            attack = GetComponentInChildren<MonsterAttack>();

        if (awarenessSystem == null)
            awarenessSystem = GetComponent<MonsterAwarenessOfPlayerPostionSystem>();

        if (destructibleDetector == null)
            destructibleDetector = GetComponentInChildren<EnemyEnvironmentalDestructibleDetector>();
    }

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        // Initialize movement system
        if (movement != null)
        {
            movement.Initialize(this);
        }

        // Initialize awareness system
        if (awarenessSystem != null && player != null)
        {
            awarenessSystem.Initialize(player);
        }
        else
        {
            if (awarenessSystem == null)
                DebugLog("Warning: No PlayerAwarenessSystem found! AI will use basic random patrol.");
            if (player == null)
                DebugLog("Warning: No player reference assigned!");
        }
    }

    #endregion

    #region Target Position Management - Used by Behavior Trees

    /// <summary>
    /// Sets the target position for the monster to move toward.
    /// The movement system automatically picks up this change and starts pathfinding.
    /// 
    /// This is the PRIMARY method behavior trees use to control movement.
    /// </summary>
    public void SetTargetPosition(Vector3 newTarget)
    {
        targetPosition = newTarget;
        DebugLog($"Target position set to: {newTarget}");
    }

    /// <summary>
    /// Sets a patrol destination using the awareness system.
    /// The awareness system intelligently chooses positions based on distance to player
    /// (closer when far from player, further when close to player - like Alien Isolation).
    /// </summary>
    [Button("Set Influenced Patrol Destination")]
    public void SetPatrolDestinationBasedOnDistanceToPlayer()
    {
        if (awarenessSystem == null)
        {
            DebugLog("Warning: No PlayerAwarenessSystem found! Using random patrol.");
            SetRandomPatrolDestination();
            return;
        }

        Vector3 newPatrolPosition = awarenessSystem.GetInfluencedPatrolPosition(transform.position);

        DebugLog($"Set influenced patrol destination: {newPatrolPosition} (Proximity: {awarenessSystem.CurrentProximity})");

        // Fallback to random patrol if awareness system returns invalid position
        if (newPatrolPosition == Vector3.zero)
        {
            DebugLog("Awareness system returned invalid position, using random patrol");
            newPatrolPosition = NPCPathfindingUtilities.Instance.GetRandomValidPosition(transform.position);
        }

        SetTargetPosition(newPatrolPosition);
    }

    /// <summary>
    /// Sets a patrol destination near the last heard noise.
    /// Used for investigating sounds.
    /// </summary>
    public void SetPatrolPositionToRandomPositionNearLastHeardNoise()
    {
        if (hearing == null || hearing.LastHeardNoisePosition == Vector3.zero)
        {
            DebugLog("No valid noise position to investigate");
            SetRandomPatrolDestination();
            return;
        }

        DebugLog($"Setting patrol position near last heard noise: {hearing.LastHeardNoisePosition}");

        Vector3 newPatrolPosition = NPCPathfindingUtilities.Instance.GetRandomValidPositionNearPoint(
            hearing.LastHeardNoisePosition,
            investigateStateRadius
        );

        // Fallback if no valid position found
        if (newPatrolPosition == Vector3.zero)
        {
            DebugLog("Failed to find position near noise, using random patrol");
            newPatrolPosition = NPCPathfindingUtilities.Instance.GetRandomValidPosition(transform.position);
        }

        SetTargetPosition(newPatrolPosition);
    }

    /// <summary>
    /// Sets a completely random patrol destination.
    /// Used as a fallback or for simple wandering behavior.
    /// </summary>
    [Button("Set Random Patrol Destination")]
    public void SetRandomPatrolDestination()
    {
        Vector3 randomPosition = NPCPathfindingUtilities.Instance.GetRandomValidPosition(transform.position);

        if (randomPosition == Vector3.zero)
        {
            DebugLog("Warning: Failed to get random valid position");
            return;
        }

        SetTargetPosition(randomPosition);
        DebugLog($"Set random patrol destination: {randomPosition}");
    }

    /// <summary>
    /// Sets target to player's current position.
    /// Used when chasing the player.
    /// </summary>
    public void SetTargetToPlayerPosition()
    {
        if (player == null)
        {
            DebugLog("Warning: No player reference!");
            return;
        }

        SetTargetPosition(player.position);
    }

    #endregion

    #region Query Methods - Used by Behavior Trees

    /// <summary>
    /// Returns the distance to the current target position.
    /// </summary>
    public float GetDistanceToTarget()
    {
        if (movement == null)
            return float.MaxValue;

        return movement.GetDistanceToTarget();
    }

    /// <summary>
    /// Returns the distance to the player.
    /// </summary>
    public float GetDistanceToPlayer()
    {
        if (player == null)
            return float.MaxValue;

        return Vector3.Distance(transform.position, player.position);
    }

    /// <summary>
    /// Returns whether the monster is currently moving.
    /// </summary>
    public bool IsMoving()
    {
        if (movement == null)
            return false;

        return movement.IsMoving();
    }

    #endregion

    #region Action Methods - Used by Behavior Trees

    /// <summary>
    /// Triggers an attack with buildup and cooldown animations.
    /// Used for attacking the player.
    /// </summary>
    [Button("Attack Player")]
    public void Attack()
    {
        if (attack == null || attack.isAttacking)
            return;

        attack.StopAllCoroutines();
        StartCoroutine(attack.AttackCoroutine());
    }

    /// <summary>
    /// Triggers an immediate attack without buildup or cooldown.
    /// Used for breaking environmental objects.
    /// </summary>
    public void DirectAttack()
    {
        if (attack == null || attack.isAttacking)
            return;

        attack.StopAllCoroutines();
        attack.PerformDirectAttack();
    }

    /// <summary>
    /// Triggers idle sound effect.
    /// </summary>
    public void TriggerPlayIdleSound()
    {
        OnPlayIdleSound?.Invoke();
    }

    /// <summary>
    /// Despawns the monster (called when dying or retreating).
    /// </summary>
    [Button("Despawn Monster")]
    public void Despawn()
    {
        DebugLog("Monster despawning");
        OnMonsterDespawned?.Invoke();
        gameObject.SetActive(false);
    }

    #endregion

    #region Debug Helpers

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[UnderwaterMonsterController] {message}");
        }
    }

    // Visual debug in scene view
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
            return;

        // Draw line to current target
        if (targetPosition != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, targetPosition);
            Gizmos.DrawWireSphere(targetPosition, 0.5f);
        }

        // Draw line to player
        if (player != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, player.position);
        }
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        StopAllCoroutines();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    #endregion
}