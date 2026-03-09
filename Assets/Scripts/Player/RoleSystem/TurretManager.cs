using UnityEngine;

/// <summary>
/// Manages which turrets are active on the submarine.
/// All turrets are placed in the scene at design time and assigned here via the Inspector.
/// This script simply activates/deactivates them based on the current role configuration.
/// </summary>
public class TurretManager : MonoBehaviour
{
    public static TurretManager Instance { get; private set; }

    [Header("Turrets")]
    [Tooltip("Single front-facing turret. Used in 1 and 2-player configs.")]
    [SerializeField] private GameObject frontTurret;

    [Tooltip("Single rear-facing turret. Used in 2 and 4-player configs.")]
    [SerializeField] private GameObject rearTurret;

    [Tooltip("Left side turret. Used in 3, 4 and 5-player configs.")]
    [SerializeField] private GameObject leftTurret;

    [Tooltip("Right side turret. Used in 3, 4 and 5-player configs.")]
    [SerializeField] private GameObject rightTurret;

    [Tooltip("Front-left corner turret. Used in 6-player config only.")]
    [SerializeField] private GameObject frontLeftTurret;

    [Tooltip("Front-right corner turret. Used in 6-player config only.")]
    [SerializeField]
    private GameObject frontRightTurret;

    [Tooltip("Rear-left corner turret. Used in 6-player config only.")]
    [SerializeField] private GameObject rearLeftTurret;

    [Tooltip("Rear-right corner turret. Used in 6-player config only.")]
    [SerializeField] private GameObject rearRightTurret;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("[TurretManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }

        DeactivateAll();
    }

    /// <summary>
    /// Activates only the turrets listed in activeTurretNames and deactivates all others.
    /// Names must match: "FrontTurret", "RearTurret", "LeftTurret", "RightTurret",
    /// "FrontLeftTurret", "FrontRightTurret", "RearLeftTurret", "RearRightTurret".
    /// </summary>
    public void ApplyTurretLayout(string[] activeTurretNames)
    {
        // First deactivate everything cleanly
        DeactivateAll();

        // Then activate only what this config needs
        foreach (string name in activeTurretNames)
        {
            var turret = GetTurretByName(name);
            if (turret != null)
            {
                turret.SetActive(true);
                DebugLog($"Turret '{name}': ACTIVE");
            }
            else
            {
                Debug.LogWarning($"[TurretManager] No turret assigned for name '{name}'. " +
                                 $"Check the TurretManager Inspector references.");
            }
        }
    }

    /// <summary>
    /// Returns the TurretController for a given turret name, or null if not found/assigned.
    /// Used by PlayerRoleManager to hand a handler to the right turret.
    /// </summary>
    public TurretController GetTurretController(string turretName)
    {
        var turretObject = GetTurretByName(turretName);
        if (turretObject == null)
        {
            Debug.LogWarning($"[TurretManager] No turret assigned for '{turretName}'.");
            return null;
        }

        var controller = turretObject.GetComponent<TurretController>();
        if (controller == null)
            Debug.LogWarning($"[TurretManager] Turret '{turretName}' has no TurretController component!");

        return controller;
    }

    /// <summary>
    /// Maps a turret name string to its serialized GameObject reference.
    /// </summary>
    private GameObject GetTurretByName(string turretName)
    {
        return turretName switch
        {
            "FrontTurret" => frontTurret,
            "RearTurret" => rearTurret,
            "LeftTurret" => leftTurret,
            "RightTurret" => rightTurret,
            "FrontLeftTurret" => frontLeftTurret,
            "FrontRightTurret" => frontRightTurret,
            "RearLeftTurret" => rearLeftTurret,
            "RearRightTurret" => rearRightTurret,
            _ => null
        };
    }

    private void DeactivateAll()
    {
        frontTurret?.SetActive(false);
        rearTurret?.SetActive(false);
        leftTurret?.SetActive(false);
        rightTurret?.SetActive(false);
        frontLeftTurret?.SetActive(false);
        frontRightTurret?.SetActive(false);
        rearLeftTurret?.SetActive(false);
        rearRightTurret?.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[TurretManager] {message}");
    }
}