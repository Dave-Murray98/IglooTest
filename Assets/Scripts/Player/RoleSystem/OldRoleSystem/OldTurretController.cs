using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls a turret based on gunner input.
/// Handles rotation with constraints and shooting functionality.
/// Each turret is assigned to a specific gunner via OldPlayerRoleManager.
/// </summary>
public class OldTurretController : MonoBehaviour
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
    [SerializeField] private float minYaw = -160f;
    [SerializeField] private float maxYaw = 30f;

    [Tooltip("Vertical rotation limits (up/down in degrees)")]
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Shooting")]
    [Tooltip("Point where projectiles spawn")]
    [SerializeField] private Transform firePoint;

    [Tooltip("Projectile for this gun to shoot")]
    [SerializeField] private ItemData turretProjectile;

    [Tooltip("Force applied to projectile")]
    [SerializeField] private float projectileForce = 50f;
    [SerializeField] private float projectileDamage = 1f;
    [SerializeField] private float knockBackForce = 10f;
    [SerializeField] private float fireRate = 0.5f;

    [Header("Rumble Settings")]
    [SerializeField] private float lowFrequency = 0.5f;
    [SerializeField] private float highFrequency = 0.5f;
    [SerializeField] private float rumbleDuration = 0.2f;

    [Header("Audio")]
    [SerializeField] private AudioClip[] shootSounds;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showGizmos = true;

    // Input handler reference
    private OldGunnerInputHandler inputHandler;

    // Current rotation values
    [ShowInInspector, ReadOnly] private float currentYaw = 0f;
    [ShowInInspector, ReadOnly] private float currentPitch = 0f;

    // Shooting state
    private float lastFireTime = 0f;
    private float FireDelay => 1f / fireRate;

    // State
    [ShowInInspector, ReadOnly] private bool isAssigned = false;

    private Gamepad assignedGamepad;

    private void Start()
    {
        if (yawPivot == null) Debug.LogError($"[OldTurretController] Yaw pivot not assigned on {gameObject.name}!");
        if (pitchPivot == null) Debug.LogError($"[OldTurretController] Pitch pivot not assigned on {gameObject.name}!");

        // Initialize rotation from current transforms
        if (yawPivot != null)
        {
            currentYaw = yawPivot.localEulerAngles.y;
            if (currentYaw > 180f) currentYaw -= 360f;
        }

        if (pitchPivot != null)
        {
            currentPitch = pitchPivot.localEulerAngles.x;
            if (currentPitch > 180f) currentPitch -= 360f;
        }

        // Subscribe to gunner assignments
        OldPlayerRoleManager.OnGunnerAssigned += OnGunnerAssigned;

        // Check if gunner already exists
        if (OldPlayerRoleManager.Instance != null)
        {
            var existingHandler = OldPlayerRoleManager.Instance.GetGunnerHandler(assignedGunnerNumber);
            if (existingHandler != null)
                AssignToGunner(existingHandler);
            else
                DebugLog($"Waiting for Gunner {assignedGunnerNumber + 1} to connect...");
        }

        SubmarineHealthManager.Instance.OnSubmarineTakenDamage += HandleRumble;
    }

    private void OnGunnerAssigned(OldGunnerInputHandler handler, int gunnerNumber)
    {
        if (gunnerNumber == assignedGunnerNumber)
            AssignToGunner(handler);
    }

    private void AssignToGunner(OldGunnerInputHandler handler)
    {
        inputHandler = handler;
        isAssigned = true;
        assignedGamepad = handler.GetAssignedGamepad();

        inputHandler.OnShootPressed += HandleShoot;

        DebugLog($"Assigned to Gunner {assignedGunnerNumber + 1} (Player {handler.PlayerIndex})");
    }

    private void Update()
    {
        if (!isAssigned || inputHandler == null || !inputHandler.IsActive) return;
        UpdateRotation();
    }

    private void UpdateRotation()
    {
        Vector2 lookInput = inputHandler.LookInput;
        if (lookInput.magnitude < 0.01f) return;

        float yawDelta = lookInput.x * rotationSpeed * Time.deltaTime;
        float pitchDelta = -lookInput.y * rotationSpeed * Time.deltaTime;

        currentYaw = Mathf.Clamp(currentYaw + yawDelta, minYaw, maxYaw);
        currentPitch = Mathf.Clamp(currentPitch + pitchDelta, minPitch, maxPitch);

        if (yawPivot != null) yawPivot.localEulerAngles = new Vector3(0f, currentYaw, 0f);
        if (pitchPivot != null) pitchPivot.localEulerAngles = new Vector3(currentPitch, 0f, 0f);
    }

    private void HandleShoot()
    {
        if (Time.time - lastFireTime < FireDelay) return;
        lastFireTime = Time.time;
        Fire();
    }

    private void Fire()
    {
        DebugLog("FIRE!");

        PlayerProjectile projectile = PlayerBulletPool.Instance.GetProjectile(
            turretProjectile, firePoint.position, firePoint.rotation);

        projectile.Initialize(projectileDamage, knockBackForce, turretProjectile,
            firePoint.position, firePoint.rotation);

        if (shootSounds != null && shootSounds.Length > 0)
            AudioManager.Instance.PlaySound(
                shootSounds[Random.Range(0, shootSounds.Length)],
                transform.position, AudioCategory.PlayerSFX, layer: AudioLayer.Exterior);

        Rigidbody projectileRb = projectile.GetComponent<Rigidbody>();
        if (projectileRb != null)
            projectileRb.AddForce(projectile.transform.forward * projectileForce, ForceMode.Impulse);

        HandleRumble(lowFrequency, highFrequency, rumbleDuration);
        ParticleFXPool.Instance.GetTurretShootFX(firePoint.position, firePoint.rotation);
    }

    private void HandleRumble(float lowFrequency, float highFrequency, float duration)
    {
        RumbleManager.Instance.RumblePulse(assignedGamepad, lowFrequency, highFrequency, duration);
    }

    public void SetAssignedGunnerNumber(int gunnerNum)
    {
        if (gunnerNum < 0 || gunnerNum > 3)
        {
            Debug.LogWarning($"[OldTurretController] Invalid gunner number: {gunnerNum}. Must be 0-3.");
            return;
        }

        assignedGunnerNumber = gunnerNum;
        DebugLog($"Assigned gunner number changed to {gunnerNum}");

        if (OldPlayerRoleManager.Instance != null)
        {
            var handler = OldPlayerRoleManager.Instance.GetGunnerHandler(gunnerNum);
            if (handler != null)
                AssignToGunner(handler);
        }
    }

    public void RumblePulse(float lowFrequency, float highFrequency, float duration)
    {
        if (assignedGamepad != null)
        {
            assignedGamepad.SetMotorSpeeds(lowFrequency, highFrequency);
            StartCoroutine(StopRumbleAfterDuration(duration));
        }
    }

    private IEnumerator StopRumbleAfterDuration(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (assignedGamepad != null)
            assignedGamepad.SetMotorSpeeds(0f, 0f);
    }

    private void OnDestroy()
    {
        OldPlayerRoleManager.OnGunnerAssigned -= OnGunnerAssigned;

        if (inputHandler != null)
            inputHandler.OnShootPressed -= HandleShoot;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        if (yawPivot != null && pitchPivot != null)
        {
            Gizmos.color = isAssigned ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(yawPivot.position, 0.2f);

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

        Gizmos.color = Color.cyan;
        Vector3 center = yawPivot.position;
        float radius = 1.5f;
        int segs = 20;
        float step = (maxYaw - minYaw) / segs;

        for (int i = 0; i < segs; i++)
        {
            float a1 = minYaw + step * i;
            float a2 = minYaw + step * (i + 1);
            Gizmos.DrawLine(
                center + Quaternion.Euler(0, a1, 0) * Vector3.forward * radius,
                center + Quaternion.Euler(0, a2, 0) * Vector3.forward * radius);
        }

        if (pitchPivot != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(pitchPivot.position,
                pitchPivot.position + Quaternion.Euler(minPitch, yawPivot.localEulerAngles.y, 0) * Vector3.forward * radius);
            Gizmos.DrawLine(pitchPivot.position,
                pitchPivot.position + Quaternion.Euler(maxPitch, yawPivot.localEulerAngles.y, 0) * Vector3.forward * radius);
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[OldTurretController G{assignedGunnerNumber + 1}] {message}");
    }
}