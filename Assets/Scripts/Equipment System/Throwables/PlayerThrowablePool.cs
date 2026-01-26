using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// Manages object pooling for all player throwable types.
/// Pre-initializes pools for all throwable types at start.
/// Singleton pattern for global access.
/// </summary>
public class PlayerThrowablePool : MonoBehaviour
{
    public static PlayerThrowablePool Instance { get; private set; }

    [Header("Pool Configuration")]
    [Tooltip("List of all throwable ItemData types to create pools for")]
    [SerializeField] private List<ItemData> throwableTypes = new List<ItemData>();

    [Tooltip("Initial pool size per throwable type")]
    [SerializeField] private int defaultPoolSize = 20;

    [Tooltip("Maximum pool size before warning (prevents runaway memory)")]
    [SerializeField] private int maxPoolSize = 100;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // Pool storage: ThrowableTypes -> Queue of throwables
    private Dictionary<ItemData, Queue<PlayerThrowable>> throwablePools;

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
    /// Initialize all throwable pools at start.
    /// </summary>
    private void InitializePools()
    {
        throwablePools = new Dictionary<ItemData, Queue<PlayerThrowable>>();
        poolSizes = new Dictionary<ItemData, int>();
        poolParents = new Dictionary<ItemData, Transform>();

        DebugLog("=== Initializing throwable Pools ===");

        foreach (ItemData throwableType in throwableTypes)
        {
            if (throwableType == null)
            {
                Debug.LogWarning("[PlayerThrowablePool] Null throwable type in throwableTypes list, skipping");
                continue;
            }

            if (throwableType.itemType != ItemType.Throwable)
            {
                Debug.LogWarning($"[PlayerThrowablePool] {throwableType.itemName} is not an throwable type, skipping");
                continue;
            }

            if (throwableType.ThrowableData?.throwablePrefab == null)
            {
                Debug.LogWarning($"[PlayerThrowablePool] {throwableType.itemName} has no throwable prefab, skipping");
                continue;
            }

            CreatePool(throwableType);
        }

        DebugLog($"Pool initialization complete - {throwablePools.Count} pools created");
    }

    /// <summary>
    /// Create a pool for a specific throwable type.
    /// </summary>
    private void CreatePool(ItemData throwableData)
    {
        // Create parent transform for organization
        GameObject poolParent = new GameObject($"{throwableData.itemName}_Pool");
        poolParent.transform.SetParent(transform);
        poolParents[throwableData] = poolParent.transform;

        // Create queue for this throwable type
        Queue<PlayerThrowable> pool = new Queue<PlayerThrowable>();

        // Pre-instantiate throwable
        for (int i = 0; i < defaultPoolSize; i++)
        {
            PlayerThrowable throwable = CreateThrowable(throwableData, poolParent.transform);
            pool.Enqueue(throwable);
        }

        throwablePools[throwableData] = pool;
        poolSizes[throwableData] = defaultPoolSize;

        DebugLog($"Created pool for {throwableData.itemName}: {defaultPoolSize} throwables");
    }

    /// <summary>
    /// Create a single throwable instance.
    /// </summary>
    private PlayerThrowable CreateThrowable(ItemData throwableData, Transform parent)
    {
        GameObject prefab = throwableData.ThrowableData.throwablePrefab;
        GameObject instance = Instantiate(prefab, parent);
        instance.name = $"{throwableData.itemName}_throwable_{poolSizes.GetValueOrDefault(throwableData, 0)}";

        PlayerThrowable throwable = instance.GetComponent<PlayerThrowable>();
        if (throwable == null)
        {
            Debug.LogError($"[PlayerThrowablePool] throwable prefab for {throwableData.itemName} is missing PlayerThrowable component!");
            throwable = instance.AddComponent<PlayerThrowable>();
        }

        instance.SetActive(false);
        return throwable;
    }

    /// <summary>
    /// Get a throwable from the pool for the specified throwable type.
    /// </summary>
    public PlayerThrowable GetThrowable(ItemData throwableData, Vector3 position, Quaternion rotation)
    {
        if (throwableData == null)
        {
            Debug.LogError("[PlayerThrowablePool] Cannot get throwable - throwableData is null");
            return null;
        }

        if (!throwablePools.ContainsKey(throwableData))
        {
            Debug.LogWarning($"[PlayerThrowablePool] No pool exists for {throwableData.itemName}, creating emergency pool");
            CreatePool(throwableData);
        }

        Queue<PlayerThrowable> pool = throwablePools[throwableData];

        PlayerThrowable throwable;

        if (pool.Count > 0)
        {
            throwable = pool.Dequeue();
            DebugLog($"Retrieved {throwableData.itemName} throwable from pool (remaining: {pool.Count})");
        }
        else
        {
            if (poolSizes[throwableData] >= maxPoolSize)
            {
                Debug.LogWarning($"[PlayerThrowablePool] Pool for {throwableData.itemName} at max size ({maxPoolSize}), creating temporary throwable");
            }

            throwable = CreateThrowable(throwableData, poolParents[throwableData]);
            poolSizes[throwableData]++;

            DebugLog($"Expanded pool for {throwableData.itemName} (new size: {poolSizes[throwableData]})");
        }

        // CRITICAL FIX: Unparent and position BEFORE activation
        throwable.transform.SetParent(null);
        throwable.transform.position = position;
        throwable.transform.rotation = rotation;

        DebugLog($"throwable positioned at {position} with rotation {rotation.eulerAngles} BEFORE activation");

        return throwable;
    }

    /// <summary>
    /// Return a throwable to its appropriate pool.
    /// </summary>
    public void ReturnThrowable(PlayerThrowable throwable)
    {
        if (throwable == null)
        {
            Debug.LogWarning("[PlayerThrowablePool] Attempted to return null throwables");
            return;
        }

        ItemData throwableData = throwable.GetThrowableType();

        if (throwableData == null)
        {
            Debug.LogWarning($"[PlayerThrowablePool] Throwable {throwable.name} has no throwable type, destroying");
            Destroy(throwable.gameObject);
            return;
        }

        if (!throwablePools.ContainsKey(throwableData))
        {
            Debug.LogWarning($"[PlayerThrowablePool] No pool exists for {throwableData.itemName}, destroying throwable");
            Destroy(throwable.gameObject);
            return;
        }

        // Reset and return to pool
        throwable.ResetState();
        throwable.transform.SetParent(poolParents[throwableData]);
        throwablePools[throwableData].Enqueue(throwable);

        DebugLog($"Returned {throwableData.itemName} throwable to pool (pool size: {throwablePools[throwableData].Count})");
    }

    /// <summary>
    /// Get current pool statistics for debugging.
    /// </summary>
    [Button("Show Pool Statistics")]
    public void ShowPoolStatistics()
    {
        Debug.Log("=== throwable Pool Statistics ===");

        foreach (var kvp in throwablePools)
        {
            ItemData throwableData = kvp.Key;
            int available = kvp.Value.Count;
            int total = poolSizes[throwableData];
            int active = total - available;

            Debug.Log($"{throwableData.itemName}: {available}/{total} available, {active} active");
        }
    }

    /// <summary>
    /// Clear all pools (use with caution, mainly for cleanup).
    /// </summary>
    [Button("Clear All Pools")]
    public void ClearAllPools()
    {
        Debug.Log("[PlayerThrowablePool] Clearing all pools");

        foreach (var kvp in throwablePools)
        {
            Queue<PlayerThrowable> pool = kvp.Value;
            while (pool.Count > 0)
            {
                PlayerThrowable throwable = pool.Dequeue();
                if (throwable != null)
                {
                    Destroy(throwable.gameObject);
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

        throwablePools.Clear();
        poolSizes.Clear();
        poolParents.Clear();

        Debug.Log("[PlayerThrowablePool] All pools cleared");
    }

    private void DebugLog(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[PlayerThrowablePool] {message}");
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