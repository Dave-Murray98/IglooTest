using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// Manages object pooling for all player projectile types.
/// Pre-initializes pools for all ammo types at start.
/// Singleton pattern for global access.
/// </summary>
public class PlayerBulletPool : MonoBehaviour
{
    public static PlayerBulletPool Instance { get; private set; }

    [Header("Pool Configuration")]
    [Tooltip("List of all ammo ItemData types to create pools for")]
    [SerializeField] private List<ItemData> ammoTypes = new List<ItemData>();

    [Tooltip("Initial pool size per ammo type")]
    [SerializeField] private int defaultPoolSize = 20;

    [Tooltip("Maximum pool size before warning (prevents runaway memory)")]
    [SerializeField] private int maxPoolSize = 100;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // Pool storage: AmmoType -> Queue of projectiles
    private Dictionary<ItemData, Queue<PlayerProjectile>> projectilePools;

    // Track current pool sizes for monitoring
    private Dictionary<ItemData, int> poolSizes;

    // Parent transforms for organization
    private Dictionary<ItemData, Transform> poolParents;

    private void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePools();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Initialize all projectile pools at start.
    /// </summary>
    private void InitializePools()
    {
        projectilePools = new Dictionary<ItemData, Queue<PlayerProjectile>>();
        poolSizes = new Dictionary<ItemData, int>();
        poolParents = new Dictionary<ItemData, Transform>();

        DebugLog("=== Initializing Projectile Pools ===");

        foreach (ItemData ammoType in ammoTypes)
        {
            if (ammoType == null)
            {
                Debug.LogWarning("[PlayerBulletPool] Null ammo type in ammoTypes list, skipping");
                continue;
            }

            if (ammoType.itemType != ItemType.Ammo)
            {
                Debug.LogWarning($"[PlayerBulletPool] {ammoType.itemName} is not an Ammo type, skipping");
                continue;
            }

            if (ammoType.AmmoData?.projectilePrefab == null)
            {
                Debug.LogWarning($"[PlayerBulletPool] {ammoType.itemName} has no projectile prefab, skipping");
                continue;
            }

            CreatePool(ammoType);
        }

        DebugLog($"Pool initialization complete - {projectilePools.Count} pools created");
    }

    /// <summary>
    /// Create a pool for a specific ammo type.
    /// </summary>
    private void CreatePool(ItemData ammoType)
    {
        // Create parent transform for organization
        GameObject poolParent = new GameObject($"{ammoType.itemName}_Pool");
        poolParent.transform.SetParent(transform);
        poolParents[ammoType] = poolParent.transform;

        // Create queue for this ammo type
        Queue<PlayerProjectile> pool = new Queue<PlayerProjectile>();

        // Pre-instantiate projectiles
        for (int i = 0; i < defaultPoolSize; i++)
        {
            PlayerProjectile projectile = CreateProjectile(ammoType, poolParent.transform);
            pool.Enqueue(projectile);
        }

        projectilePools[ammoType] = pool;
        poolSizes[ammoType] = defaultPoolSize;

        DebugLog($"Created pool for {ammoType.itemName}: {defaultPoolSize} projectiles");
    }

    /// <summary>
    /// Create a single projectile instance.
    /// </summary>
    private PlayerProjectile CreateProjectile(ItemData ammoType, Transform parent)
    {
        GameObject prefab = ammoType.AmmoData.projectilePrefab;
        GameObject instance = Instantiate(prefab, parent);
        instance.name = $"{ammoType.itemName}_Projectile_{poolSizes.GetValueOrDefault(ammoType, 0)}";

        PlayerProjectile projectile = instance.GetComponent<PlayerProjectile>();
        if (projectile == null)
        {
            Debug.LogError($"[PlayerBulletPool] Projectile prefab for {ammoType.itemName} is missing PlayerProjectile component!");
            projectile = instance.AddComponent<PlayerProjectile>();
        }

        instance.SetActive(false);
        return projectile;
    }

    /// <summary>
    /// Get a projectile from the pool for the specified ammo type.
    /// </summary>
    public PlayerProjectile GetProjectile(ItemData ammoType, Vector3 position, Quaternion rotation)
    {
        if (ammoType == null)
        {
            Debug.LogError("[PlayerBulletPool] Cannot get projectile - ammoType is null");
            return null;
        }

        if (!projectilePools.ContainsKey(ammoType))
        {
            Debug.LogWarning($"[PlayerBulletPool] No pool exists for {ammoType.itemName}, creating emergency pool");
            CreatePool(ammoType);
        }

        Queue<PlayerProjectile> pool = projectilePools[ammoType];

        PlayerProjectile projectile;

        if (pool.Count > 0)
        {
            projectile = pool.Dequeue();
            DebugLog($"Retrieved {ammoType.itemName} projectile from pool (remaining: {pool.Count})");
        }
        else
        {
            if (poolSizes[ammoType] >= maxPoolSize)
            {
                Debug.LogWarning($"[PlayerBulletPool] Pool for {ammoType.itemName} at max size ({maxPoolSize}), creating temporary projectile");
            }

            projectile = CreateProjectile(ammoType, poolParents[ammoType]);
            poolSizes[ammoType]++;

            DebugLog($"Expanded pool for {ammoType.itemName} (new size: {poolSizes[ammoType]})");
        }

        // CRITICAL FIX: Unparent and position BEFORE activation
        projectile.transform.SetParent(null);
        projectile.transform.position = position;
        projectile.transform.rotation = rotation;

        // Ensure rigidbody is kinematic before positioning
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (rb.isKinematic == false)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }

        DebugLog($"Projectile positioned at {position} with rotation {rotation.eulerAngles} BEFORE activation");

        return projectile;
    }

    /// <summary>
    /// Return a projectile to its appropriate pool.
    /// </summary>
    public void ReturnProjectile(PlayerProjectile projectile)
    {
        if (projectile == null)
        {
            Debug.LogWarning("[PlayerBulletPool] Attempted to return null projectile");
            return;
        }

        ItemData ammoType = projectile.GetAmmoType();

        if (ammoType == null)
        {
            Debug.LogWarning($"[PlayerBulletPool] Projectile {projectile.name} has no ammo type, destroying");
            Destroy(projectile.gameObject);
            return;
        }

        if (!projectilePools.ContainsKey(ammoType))
        {
            Debug.LogWarning($"[PlayerBulletPool] No pool exists for {ammoType.itemName}, destroying projectile");
            Destroy(projectile.gameObject);
            return;
        }

        // Reset and return to pool
        projectile.ResetState();
        projectile.transform.SetParent(poolParents[ammoType]);
        projectilePools[ammoType].Enqueue(projectile);

        DebugLog($"Returned {ammoType.itemName} projectile to pool (pool size: {projectilePools[ammoType].Count})");
    }

    /// <summary>
    /// Get current pool statistics for debugging.
    /// </summary>
    [Button("Show Pool Statistics")]
    public void ShowPoolStatistics()
    {
        Debug.Log("=== Projectile Pool Statistics ===");

        foreach (var kvp in projectilePools)
        {
            ItemData ammoType = kvp.Key;
            int available = kvp.Value.Count;
            int total = poolSizes[ammoType];
            int active = total - available;

            Debug.Log($"{ammoType.itemName}: {available}/{total} available, {active} active");
        }
    }

    /// <summary>
    /// Clear all pools (use with caution, mainly for cleanup).
    /// </summary>
    [Button("Clear All Pools")]
    public void ClearAllPools()
    {
        Debug.Log("[PlayerBulletPool] Clearing all pools");

        foreach (var kvp in projectilePools)
        {
            Queue<PlayerProjectile> pool = kvp.Value;
            while (pool.Count > 0)
            {
                PlayerProjectile projectile = pool.Dequeue();
                if (projectile != null)
                {
                    Destroy(projectile.gameObject);
                }
            }
        }

        foreach (var kvp in poolParents)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value.gameObject);
            }
        }

        projectilePools.Clear();
        poolSizes.Clear();
        poolParents.Clear();

        Debug.Log("[PlayerBulletPool] All pools cleared");
    }

    private void DebugLog(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[PlayerBulletPool] {message}");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}