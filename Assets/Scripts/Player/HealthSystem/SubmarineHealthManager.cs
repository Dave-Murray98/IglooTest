using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Central manager for the submarine's health system.
/// Routes damage to the appropriate region based on world position.
/// Tracks overall submarine health status.
/// </summary>
public class SubmarineHealthManager : MonoBehaviour
{
    [Header("Health Regions")]
    [Tooltip("Reference to the Front region health component")]
    [SerializeField] private SubmarineHealthRegion frontRegion;

    [Tooltip("Reference to the Back region health component")]
    [SerializeField] private SubmarineHealthRegion backRegion;

    [Tooltip("Reference to the Left region health component")]
    [SerializeField] private SubmarineHealthRegion leftRegion;

    [Tooltip("Reference to the Right region health component")]
    [SerializeField] private SubmarineHealthRegion rightRegion;

    [Tooltip("Reference to the Bottom region health component")]
    [SerializeField] private SubmarineHealthRegion bottomRegion;

    [Header("Submarine Status")]
    [Tooltip("Should the submarine be destroyed when all regions are destroyed?")]
    [SerializeField] private bool destroySubmarineWhenAllRegionsDestroyed = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showDebugGizmos = true;

    // Events for other systems to listen to
    public event Action<SubmarineHealthRegion, float> OnAnyRegionDamaged;
    public event Action<SubmarineHealthRegion> OnAnyRegionDestroyed;
    public event Action OnSubmarineDestroyed;

    // Easy access to all regions
    private List<SubmarineHealthRegion> allRegions;

    // Status tracking
    [ShowInInspector, ReadOnly]
    private int destroyedRegionCount = 0;

    public bool IsSubmarineDestroyed => destroyedRegionCount >= 5;
    public float OverallHealthPercentage
    {
        get
        {
            if (allRegions == null || allRegions.Count == 0) return 0f;
            float totalCurrentHealth = allRegions.Sum(r => r.CurrentHealth);
            float totalMaxHealth = allRegions.Sum(r => r.MaxHealth);
            return totalMaxHealth > 0 ? (totalCurrentHealth / totalMaxHealth) : 0f;
        }
    }

    private void Awake()
    {
        // Gather all regions into a list for easy iteration
        allRegions = new List<SubmarineHealthRegion>
        {
            frontRegion,
            backRegion,
            leftRegion,
            rightRegion,
            bottomRegion
        };

        // Remove any null entries (in case a region wasn't assigned)
        allRegions.RemoveAll(r => r == null);

        if (allRegions.Count == 0)
        {
            Debug.LogError("[SubmarineHealthManager] No health regions assigned! Please assign them in the Inspector.");
        }
    }

    private void Start()
    {
        // Subscribe to all region events
        foreach (var region in allRegions)
        {
            region.OnDamageTaken += HandleRegionDamaged;
            region.OnRegionDestroyed += HandleRegionDestroyed;
        }

        DebugLog("Health Manager initialized with " + allRegions.Count + " regions.");
    }

    /// <summary>
    /// Apply damage at a specific world position. 
    /// The system will automatically determine which region is closest and apply damage there.
    /// </summary>
    /// <param name="worldPosition">Where in the world the damage occurred</param>
    /// <param name="damageAmount">How much damage to apply</param>
    public void TakeDamageAtPoint(Vector3 worldPosition, float damageAmount)
    {
        // Find the closest region to the damage point
        SubmarineHealthRegion closestRegion = GetClosestRegion(worldPosition);

        if (closestRegion != null)
        {
            DebugLog($"Damage at {worldPosition} routed to {closestRegion.RegionName} region");
            closestRegion.TakeDamage(damageAmount);
        }
        else
        {
            Debug.LogWarning("[SubmarineHealthManager] Could not find closest region for damage!");
        }
    }

    /// <summary>
    /// Find which region is closest to a given world position
    /// </summary>
    private SubmarineHealthRegion GetClosestRegion(Vector3 worldPosition)
    {
        if (allRegions == null || allRegions.Count == 0) return null;

        SubmarineHealthRegion closest = null;
        float closestDistance = float.MaxValue;

        foreach (var region in allRegions)
        {
            if (region == null) continue;

            float distance = Vector3.Distance(worldPosition, region.transform.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = region;
            }
        }

        return closest;
    }

    /// <summary>
    /// Heal a specific region by name
    /// </summary>
    /// <param name="regionName">Name of the region (e.g., "Front", "Back", "Left", "Right", "Bottom")</param>
    /// <param name="healAmount">How much health to restore</param>
    public void HealRegion(string regionName, float healAmount)
    {
        SubmarineHealthRegion region = GetRegionByName(regionName);

        if (region != null)
        {
            region.RestoreHealth(healAmount);
        }
        else
        {
            Debug.LogWarning($"[SubmarineHealthManager] Could not find region named '{regionName}'");
        }
    }

    /// <summary>
    /// Heal all regions by the same amount
    /// </summary>
    public void HealAllRegions(float healAmount)
    {
        foreach (var region in allRegions)
        {
            if (region != null)
            {
                region.RestoreHealth(healAmount);
            }
        }

        DebugLog($"Healed all regions by {healAmount}");
    }

    /// <summary>
    /// Fully repair all regions to max health
    /// </summary>
    public void FullyRepairSubmarine()
    {
        foreach (var region in allRegions)
        {
            if (region != null)
            {
                region.FullyRepair();
            }
        }

        DebugLog("Submarine fully repaired!");
    }

    /// <summary>
    /// Get a region by its name
    /// </summary>
    public SubmarineHealthRegion GetRegionByName(string regionName)
    {
        return allRegions.FirstOrDefault(r => r != null &&
            string.Equals(r.RegionName, regionName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get all regions that are currently destroyed
    /// </summary>
    public List<SubmarineHealthRegion> GetDestroyedRegions()
    {
        return allRegions.Where(r => r != null && r.IsDestroyed).ToList();
    }

    /// <summary>
    /// Get all regions that still have health
    /// </summary>
    public List<SubmarineHealthRegion> GetIntactRegions()
    {
        return allRegions.Where(r => r != null && !r.IsDestroyed).ToList();
    }

    /// <summary>
    /// Called when any region takes damage
    /// </summary>
    private void HandleRegionDamaged(SubmarineHealthRegion region, float damageAmount)
    {
        DebugLog($"{region.RegionName} damaged: {region.CurrentHealth}/{region.MaxHealth}");

        // Notify listeners
        OnAnyRegionDamaged?.Invoke(region, damageAmount);
    }

    /// <summary>
    /// Called when any region is destroyed
    /// </summary>
    private void HandleRegionDestroyed(SubmarineHealthRegion region)
    {
        destroyedRegionCount++;

        DebugLog($"{region.RegionName} destroyed! ({destroyedRegionCount}/5 regions destroyed)");

        // Notify listeners
        OnAnyRegionDestroyed?.Invoke(region);

        // Check if entire submarine should be destroyed
        if (destroySubmarineWhenAllRegionsDestroyed && IsSubmarineDestroyed)
        {
            HandleSubmarineDestroyed();
        }
    }

    /// <summary>
    /// Called when the entire submarine is destroyed
    /// </summary>
    private void HandleSubmarineDestroyed()
    {
        Debug.Log("[SubmarineHealthManager] SUBMARINE DESTROYED!");

        // Notify listeners
        OnSubmarineDestroyed?.Invoke();

        // Here you could add:
        // - Game over logic
        // - Explosion effects
        // - Respawn logic
        // - etc.
    }

    private void OnDestroy()
    {
        // Unsubscribe from all region events to prevent memory leaks
        foreach (var region in allRegions)
        {
            if (region != null)
            {
                region.OnDamageTaken -= HandleRegionDamaged;
                region.OnRegionDestroyed -= HandleRegionDestroyed;
            }
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[SubmarineHealthManager] {message}");
        }
    }

    // Draw debug lines showing damage routing in the Scene view
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || allRegions == null) return;

        // Draw lines from submarine center to each region
        Gizmos.color = Color.cyan;
        foreach (var region in allRegions)
        {
            if (region != null)
            {
                Gizmos.DrawLine(transform.position, region.transform.position);
            }
        }
    }

#if UNITY_EDITOR
    // Inspector buttons for testing (only visible in Unity Editor)
    [Button("Damage Random Region (25)"), PropertyOrder(200)]
    private void TestDamageRandomRegion()
    {
        if (allRegions == null || allRegions.Count == 0) return;

        var randomRegion = allRegions[UnityEngine.Random.Range(0, allRegions.Count)];
        if (randomRegion != null)
        {
            randomRegion.TakeDamage(25f);
        }
    }

    [Button("Heal All Regions (50)"), PropertyOrder(201)]
    private void TestHealAll()
    {
        HealAllRegions(50f);
    }

    [Button("Fully Repair Submarine"), PropertyOrder(202)]
    private void TestFullRepair()
    {
        FullyRepairSubmarine();
    }
#endif
}