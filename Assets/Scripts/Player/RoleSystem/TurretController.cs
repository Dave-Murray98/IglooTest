using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls a turret based on gunner input.
/// Uses MultiRoleInputHandler — reads GunAimInput (right stick) and OnShootPressed (right trigger).
///
/// Bug fix: HandleShoot now checks gameObject.activeInHierarchy before firing,
/// preventing an inactive turret from shooting if its handler fires an event.
/// </summary>
public class TurretController : MonoBehaviour
{
    [Header("Turret Identification")]
    [Tooltip("Must match the name used in TurretManager and RoleConfiguration exactly.")]
    [SerializeField] private string turretName = "FrontTurret";

    [Header("Rotation Settings")]
    [SerializeField] private Transform yawPivot;
    [SerializeField] private Transform pitchPivot;
    [SerializeField] private float rotationSpeed = 100f;

    [Header("Rotation Constraints")]
    [SerializeField] private float minYaw = -160f;
    [SerializeField] private float maxYaw = 30f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Shooting")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private ItemData turretProjectile;
    [SerializeField] private float projectileForce = 50f;
    [SerializeField] private float projectileDamage = 1f;
    [SerializeField] private float knockBackForce = 10f;
    [SerializeField] private float fireRate = 0.5f;

    [Header("Rumble Settings")]
    [SerializeField] private float shootRumbleLow = 0.5f;
    [SerializeField] private float shootRumbleHigh = 0.5f;
    [SerializeField] private float shootRumbleDuration = 0.2f;

    [Header("Audio")]
    [SerializeField] private AudioClip[] shootSounds;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showGizmos = true;

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    private MultiRoleInputHandler inputHandler;

    [ShowInInspector, ReadOnly] private float currentYaw = 0f;
    [ShowInInspector, ReadOnly] private float currentPitch = 0f;
    [ShowInInspector, ReadOnly] private bool isAssigned = false;

    private float lastFireTime = 0f;
    private float FireDelay => 1f / fireRate;
    private Gamepad assignedGamepad;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        ValidateReferences();
        InitialiseRotation();

        SubmarineHealthManager.Instance.OnSubmarineTakenDamage += HandleDamageRumble;
    }

    private void Update()
    {
        if (!isAssigned || inputHandler == null || !inputHandler.IsActive) return;

        UpdateRotation();
    }

    private void OnDestroy()
    {
        if (SubmarineHealthManager.Instance != null)
            SubmarineHealthManager.Instance.OnSubmarineTakenDamage -= HandleDamageRumble;

        DetachHandler();
    }

    // -------------------------------------------------------------------------
    // Public API — called by PlayerRoleManager
    // -------------------------------------------------------------------------

    public void AssignHandler(MultiRoleInputHandler handler)
    {
        DetachHandler();

        inputHandler = handler;
        assignedGamepad = handler?.GetAssignedGamepad();
        isAssigned = inputHandler != null;

        if (inputHandler != null)
            inputHandler.OnShootPressed += HandleShoot;

        DebugLog(isAssigned ? $"Assigned to Player {handler.PlayerIndex}" : "Handler is null");
    }

    public void DetachHandler()
    {
        if (inputHandler != null)
            inputHandler.OnShootPressed -= HandleShoot;

        inputHandler = null;
        assignedGamepad = null;
        isAssigned = false;
    }

    // -------------------------------------------------------------------------
    // Rotation
    // -------------------------------------------------------------------------

    private void UpdateRotation()
    {
        Vector2 aim = inputHandler.GunAimInput;
        if (aim.magnitude < 0.01f) return;

        currentYaw = Mathf.Clamp(currentYaw + aim.x * rotationSpeed * Time.deltaTime, minYaw, maxYaw);
        currentPitch = Mathf.Clamp(currentPitch + -aim.y * rotationSpeed * Time.deltaTime, minPitch, maxPitch);

        if (yawPivot != null) yawPivot.localEulerAngles = new Vector3(0f, currentYaw, 0f);
        if (pitchPivot != null) pitchPivot.localEulerAngles = new Vector3(currentPitch, 0f, 0f);
    }

    // -------------------------------------------------------------------------
    // Shooting
    // -------------------------------------------------------------------------

    private void HandleShoot()
    {
        // Guard: if this turret's GameObject has been deactivated by TurretManager,
        // do not fire even if the event still reaches us during the same frame.
        if (!gameObject.activeInHierarchy) return;

        if (Time.time - lastFireTime < FireDelay) return;
        lastFireTime = Time.time;

        Fire();
    }

    private void Fire()
    {
        DebugLog("FIRE!");

        var projectile = PlayerBulletPool.Instance.GetProjectile(
            turretProjectile, firePoint.position, firePoint.rotation);

        projectile.Initialize(
            projectileDamage, knockBackForce, turretProjectile,
            firePoint.position, firePoint.rotation);

        if (shootSounds != null && shootSounds.Length > 0)
            AudioManager.Instance.PlaySound(
                shootSounds[Random.Range(0, shootSounds.Length)],
                transform.position, AudioCategory.PlayerSFX, layer: AudioLayer.Exterior);

        var rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
            rb.AddForce(projectile.transform.forward * projectileForce, ForceMode.Impulse);

        RumbleManager.Instance.RumblePulse(assignedGamepad, shootRumbleLow, shootRumbleHigh, shootRumbleDuration);
        ParticleFXPool.Instance.GetTurretShootFX(firePoint.position, firePoint.rotation);
    }

    private void HandleDamageRumble(float low, float high, float duration)
    {
        if (assignedGamepad != null)
            RumbleManager.Instance.RumblePulse(assignedGamepad, low, high, duration);
    }

    // -------------------------------------------------------------------------
    // Setup helpers
    // -------------------------------------------------------------------------

    private void ValidateReferences()
    {
        if (yawPivot == null) Debug.LogError($"[TurretController '{turretName}'] Yaw pivot not assigned!");
        if (pitchPivot == null) Debug.LogError($"[TurretController '{turretName}'] Pitch pivot not assigned!");
        if (firePoint == null) Debug.LogError($"[TurretController '{turretName}'] Fire point not assigned!");
    }

    private void InitialiseRotation()
    {
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
    }

    // -------------------------------------------------------------------------
    // Debug / Gizmos
    // -------------------------------------------------------------------------

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[TurretController '{turretName}'] {message}");
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || yawPivot == null || pitchPivot == null) return;

        Gizmos.color = isAssigned ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(yawPivot.position, 0.2f);

        if (firePoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(firePoint.position, firePoint.position + firePoint.forward * 2f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || yawPivot == null) return;

        Gizmos.color = Color.cyan;
        int segs = 20;
        float step = (maxYaw - minYaw) / segs;
        float radius = 1.5f;

        for (int i = 0; i < segs; i++)
        {
            float a1 = minYaw + step * i;
            float a2 = minYaw + step * (i + 1);
            Gizmos.DrawLine(
                yawPivot.position + Quaternion.Euler(0, a1, 0) * Vector3.forward * radius,
                yawPivot.position + Quaternion.Euler(0, a2, 0) * Vector3.forward * radius);
        }

        if (pitchPivot != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(pitchPivot.position,
                pitchPivot.position + Quaternion.Euler(minPitch, currentYaw, 0) * Vector3.forward * radius);
            Gizmos.DrawLine(pitchPivot.position,
                pitchPivot.position + Quaternion.Euler(maxPitch, currentYaw, 0) * Vector3.forward * radius);
        }
    }
}