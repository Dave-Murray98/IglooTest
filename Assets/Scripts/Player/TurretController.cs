using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Controls a turret based on gunner input.
/// Handles rotation with constraints and shooting functionality.
/// Each turret is assigned to a specific gunner via PlayerRoleManager.
/// </summary>
public class TurretController : MonoBehaviour
{
    [Header("Turret Configuration")]
    [Tooltip("Which gunner controls this turret (0-3 for Gunners 1-4)")]
    [SerializeField] private int assignedGunnerNumber = 0;

    [Header("Rotation Settings")]
    [Tooltip("The transform that rotates horizontally (yaw)")]
    [SerializeField] private Transform yawPivot;

    [Tooltip("The transform that rotates vertically (pitch)")]
    [SerializeField] private Transform pitchPivot;

    [SerializeField] private float rotationSpeed = 100f;

    [Header("Rotation Constraints")]
    [Tooltip("Horizontal rotation limits (left/right in degrees)")]
    [SerializeField] private float minYaw = -180f;
    [SerializeField] private float maxYaw = 180f;

    [Tooltip("Vertical rotation limits (up/down in degrees)")]
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;

    [Header("Shooting")]
    [Tooltip("Point where projectiles spawn")]
    [SerializeField] private Transform firePoint;

    [Tooltip("Projectile for this gun to shoot")]
    [SerializeField] private ItemData turretProjectile;

    [Tooltip("Force applied to projectile")]
    [SerializeField] private float projectileForce = 10f;
    [SerializeField] private float projectileDamage = 1f;

    [SerializeField] private float fireRate = 0.5f; // Shots per second

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showGizmos = true;

    // Input handler reference
    private GunnerInputHandler inputHandler;

    // Current rotation values
    [ShowInInspector, ReadOnly] private float currentYaw = 0f;
    [ShowInInspector, ReadOnly] private float currentPitch = 0f;

    // Shooting state
    private float lastFireTime = 0f;
    private float fireDelay => 1f / fireRate;

    // State
    [ShowInInspector, ReadOnly] private bool isAssigned = false;

    private void Start()
    {
        // Validate references
        if (yawPivot == null)
        {
            Debug.LogError($"[TurretController] Yaw pivot not assigned on {gameObject.name}!");
        }

        if (pitchPivot == null)
        {
            Debug.LogError($"[TurretController] Pitch pivot not assigned on {gameObject.name}!");
        }

        // Initialize rotation from current transforms
        if (yawPivot != null)
        {
            currentYaw = yawPivot.localEulerAngles.y;
            // Normalize to -180 to 180 range
            if (currentYaw > 180f) currentYaw -= 360f;
        }

        if (pitchPivot != null)
        {
            currentPitch = pitchPivot.localEulerAngles.x;
            // Normalize to -180 to 180 range
            if (currentPitch > 180f) currentPitch -= 360f;
        }

        // Subscribe to gunner assignments
        PlayerRoleManager.OnGunnerAssigned += OnGunnerAssigned;

        // Check if gunner already exists
        if (PlayerRoleManager.Instance != null)
        {
            var existingHandler = PlayerRoleManager.Instance.GetGunnerHandler(assignedGunnerNumber);
            if (existingHandler != null)
            {
                AssignToGunner(existingHandler);
            }
            else
            {
                DebugLog($"Waiting for Gunner {assignedGunnerNumber + 1} to connect...");
            }
        }
    }

    private void OnGunnerAssigned(GunnerInputHandler handler, int gunnerNumber)
    {
        // Check if this is our assigned gunner
        if (gunnerNumber == assignedGunnerNumber)
        {
            AssignToGunner(handler);
        }
    }

    private void AssignToGunner(GunnerInputHandler handler)
    {
        inputHandler = handler;
        isAssigned = true;

        // Subscribe to shooting events
        inputHandler.OnShootPressed += HandleShoot;

        DebugLog($"Assigned to Gunner {assignedGunnerNumber + 1} (Player {handler.PlayerIndex})");
    }

    private void Update()
    {
        // Only process input if we have an assigned gunner
        if (!isAssigned || inputHandler == null || !inputHandler.IsActive)
        {
            return;
        }

        UpdateRotation();
    }

    private void UpdateRotation()
    {
        // Get input from gunner
        Vector2 lookInput = inputHandler.LookInput;

        if (lookInput.magnitude < 0.01f)
        {
            // No input, don't rotate
            return;
        }

        // Calculate rotation deltas
        float yawDelta = lookInput.x * rotationSpeed * Time.deltaTime;
        float pitchDelta = -lookInput.y * rotationSpeed * Time.deltaTime; // Inverted for natural feel

        // Apply rotation with constraints
        currentYaw = Mathf.Clamp(currentYaw + yawDelta, minYaw, maxYaw);
        currentPitch = Mathf.Clamp(currentPitch + pitchDelta, minPitch, maxPitch);

        // Apply to transforms
        if (yawPivot != null)
        {
            yawPivot.localEulerAngles = new Vector3(0f, currentYaw, 0f);
        }

        if (pitchPivot != null)
        {
            pitchPivot.localEulerAngles = new Vector3(currentPitch, 0f, 0f);
        }
    }

    private void HandleShoot()
    {
        // Check fire rate
        if (Time.time - lastFireTime < fireDelay)
        {
            return; // Too soon to fire again
        }

        lastFireTime = Time.time;

        // Fire logic
        Fire();
    }

    private void Fire()
    {
        DebugLog("FIRE!");

        // // If we have a projectile prefab and fire point, spawn projectile
        // if (projectilePrefab != null && firePoint != null)
        // {
        //     GameObject projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        //     DebugLog($"Spawned projectile: {projectile.name}");
        // }
        // else if (firePoint != null)
        // {
        //     // No projectile prefab - do a simple raycast for hit detection
        //     RaycastHit hit;
        //     if (Physics.Raycast(firePoint.position, firePoint.forward, out hit, 1000f))
        //     {
        //         DebugLog($"Hit: {hit.collider.gameObject.name} at distance {hit.distance:F2}m");

        //         // Visualize hit in Scene view
        //         Debug.DrawLine(firePoint.position, hit.point, Color.red, 0.5f);
        //     }
        //     else
        //     {
        //         DebugLog("Missed - no hit");
        //     }
        // }

        PlayerProjectile projectile = PlayerBulletPool.Instance.GetProjectile(turretProjectile, firePoint.position, firePoint.rotation);

        projectile.Initialize(projectileDamage, turretProjectile, firePoint.position, firePoint.rotation);

        // Apply force using projectile's forward direction
        Rigidbody projectileRb = projectile.GetComponent<Rigidbody>();
        if (projectileRb != null)
        {
            Vector3 forceDirection = projectile.transform.up;

            projectileRb.AddForce(forceDirection * projectileForce, ForceMode.Impulse);

            DebugLog($"Applied force: {projectileForce} in direction {forceDirection}");
        }

    }

    /// <summary>
    /// Manual method to assign this turret to a specific gunner number
    /// </summary>
    public void SetAssignedGunnerNumber(int gunnerNum)
    {
        if (gunnerNum < 0 || gunnerNum > 3)
        {
            Debug.LogWarning($"[TurretController] Invalid gunner number: {gunnerNum}. Must be 0-3.");
            return;
        }

        assignedGunnerNumber = gunnerNum;
        DebugLog($"Assigned gunner number changed to {gunnerNum}");

        // Try to get the handler if it already exists
        if (PlayerRoleManager.Instance != null)
        {
            var handler = PlayerRoleManager.Instance.GetGunnerHandler(gunnerNum);
            if (handler != null)
            {
                AssignToGunner(handler);
            }
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        PlayerRoleManager.OnGunnerAssigned -= OnGunnerAssigned;

        if (inputHandler != null)
        {
            inputHandler.OnShootPressed -= HandleShoot;
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Draw rotation constraints visualization
        if (yawPivot != null && pitchPivot != null)
        {
            Gizmos.color = isAssigned ? Color.green : Color.yellow;

            // Draw a small sphere at the turret base
            Gizmos.DrawWireSphere(yawPivot.position, 0.2f);

            // Draw forward direction
            if (firePoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(firePoint.position, firePoint.position + firePoint.forward * 2f);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || yawPivot == null) return;

        // Draw rotation arc for yaw constraints
        Gizmos.color = Color.cyan;
        Vector3 center = yawPivot.position;
        float radius = 1.5f;

        // Draw yaw arc
        int segments = 20;
        float angleStep = (maxYaw - minYaw) / segments;
        for (int i = 0; i < segments; i++)
        {
            float angle1 = minYaw + (angleStep * i);
            float angle2 = minYaw + (angleStep * (i + 1));

            Vector3 point1 = center + Quaternion.Euler(0, angle1, 0) * Vector3.forward * radius;
            Vector3 point2 = center + Quaternion.Euler(0, angle2, 0) * Vector3.forward * radius;

            Gizmos.DrawLine(point1, point2);
        }

        // Draw pitch limits
        if (pitchPivot != null)
        {
            Gizmos.color = Color.magenta;

            Vector3 minPitchDir = Quaternion.Euler(minPitch, yawPivot.localEulerAngles.y, 0) * Vector3.forward;
            Vector3 maxPitchDir = Quaternion.Euler(maxPitch, yawPivot.localEulerAngles.y, 0) * Vector3.forward;

            Gizmos.DrawLine(pitchPivot.position, pitchPivot.position + minPitchDir * radius);
            Gizmos.DrawLine(pitchPivot.position, pitchPivot.position + maxPitchDir * radius);
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[Turret G{assignedGunnerNumber + 1}] {message}");
        }
    }
}