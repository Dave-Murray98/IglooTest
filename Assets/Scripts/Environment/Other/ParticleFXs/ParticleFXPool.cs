using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Object pool manager for particle effects to optimize performance.
/// Manages separate pools for different particle effect types.
/// </summary>
public class ParticleFXPool : MonoBehaviour
{
    #region Particle Type Enum
    public enum ParticleType
    {
        BloodSplat,
        ImpactFX,
        BubbleFX,
        LureGrenade,
        StunGrenade,
        AudioLogDestroyFX,
        BreakEggFX
    }
    #endregion

    #region Pool Configuration Class
    [System.Serializable]
    public class ParticlePoolConfig
    {
        [HideInInspector]
        public string name;

        public ParticleType particleType;

        [Required("Particle prefab must be assigned")]
        [Tooltip("Prefab containing ParticleSystem component")]
        public GameObject particlePrefab;

        [Range(5, 50)]
        [Tooltip("Initial number of particles to create in this pool")]
        public int initialPoolSize = 10;

        [Range(5, 100)]
        [Tooltip("Maximum number of particles that can exist in this pool")]
        public int maxPoolSize = 30;

        [HideInInspector]
        public Queue<GameObject> availableParticles = new Queue<GameObject>();

        [HideInInspector]
        public List<GameObject> activeParticles = new List<GameObject>();

        [HideInInspector]
        public Transform poolParent;

        public int AvailableCount => availableParticles.Count;
        public int ActiveCount => activeParticles.Count;
        public int TotalPoolSize => availableParticles.Count + activeParticles.Count;
    }
    #endregion

    [Header("Particle Pool Configurations")]
    [SerializeField, ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "name")]
    [Tooltip("Configure individual pools for each particle type")]
    private List<ParticlePoolConfig> particlePools = new List<ParticlePoolConfig>();

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // Quick lookup dictionary
    private Dictionary<ParticleType, ParticlePoolConfig> poolLookup = new Dictionary<ParticleType, ParticlePoolConfig>();

    // Singleton pattern
    private static ParticleFXPool instance;
    public static ParticleFXPool Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<ParticleFXPool>();
                if (instance == null)
                {
                    GameObject poolObject = new GameObject("ParticleFXPool");
                    instance = poolObject.AddComponent<ParticleFXPool>();
                }
            }
            return instance;
        }
    }

    #region Unity Lifecycle
    private void Awake()
    {
        // Singleton setup
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        SetupPools();
    }

    private void Start()
    {
        InitializePools();
    }

    private void OnValidate()
    {
        // Update names for better inspector display
        foreach (var pool in particlePools)
        {
            pool.name = pool.particleType.ToString();
        }
    }
    #endregion

    #region Pool Setup
    private void SetupPools()
    {
        poolLookup.Clear();

        foreach (var poolConfig in particlePools)
        {
            if (poolConfig.particlePrefab == null)
            {
                Debug.LogError($"ParticleFXPool: No prefab assigned for {poolConfig.particleType}!");
                continue;
            }

            // Create parent object for this pool
            GameObject poolParentObject = new GameObject($"Pool_{poolConfig.particleType}");
            poolParentObject.transform.SetParent(transform);
            poolConfig.poolParent = poolParentObject.transform;

            // Add to lookup dictionary
            if (!poolLookup.ContainsKey(poolConfig.particleType))
            {
                poolLookup.Add(poolConfig.particleType, poolConfig);
            }
            else
            {
                Debug.LogWarning($"ParticleFXPool: Duplicate pool configuration for {poolConfig.particleType}!");
            }
        }

        DebugLog("ParticleFXPool setup complete");
    }

    private void InitializePools()
    {
        foreach (var poolConfig in particlePools)
        {
            if (poolConfig.particlePrefab == null) continue;

            // Create initial pool objects
            for (int i = 0; i < poolConfig.initialPoolSize; i++)
            {
                CreatePooledParticle(poolConfig);
            }

            DebugLog($"Initialized {poolConfig.particleType} pool with {poolConfig.initialPoolSize} objects");
        }
    }

    private GameObject CreatePooledParticle(ParticlePoolConfig poolConfig)
    {
        if (poolConfig.particlePrefab == null) return null;

        // Instantiate the particle prefab
        GameObject particleObject = Instantiate(poolConfig.particlePrefab, poolConfig.poolParent);
        particleObject.name = $"{poolConfig.particleType}_Pooled";

        // Ensure it has a ParticleSystem component
        ParticleSystem particleSystem = particleObject.GetComponent<ParticleSystem>();
        if (particleSystem == null)
        {
            particleSystem = particleObject.GetComponentInChildren<ParticleSystem>();
            if (particleSystem == null)
            {
                Debug.LogError($"ParticleFXPool: Prefab for {poolConfig.particleType} does not contain a ParticleSystem component!");
                Destroy(particleObject);
                return null;
            }
        }

        // Add or get the PooledParticle component
        PooledParticle pooledComponent = particleObject.GetComponent<PooledParticle>();
        if (pooledComponent == null)
        {
            pooledComponent = particleObject.AddComponent<PooledParticle>();
        }
        pooledComponent.Initialize(this, poolConfig.particleType);

        // Deactivate and add to available pool
        particleObject.SetActive(false);
        poolConfig.availableParticles.Enqueue(particleObject);

        return particleObject;
    }
    #endregion

    #region Public Interface - Specific Particle Types
    /// <summary>
    /// Get a blood splat particle effect from the pool
    /// </summary>
    public GameObject GetBloodSplat(Vector3 position, Quaternion rotation)
    {
        return GetParticle(ParticleType.BloodSplat, position, rotation);
    }

    /// <summary>
    /// Get an impact FX particle effect from the pool
    /// </summary>
    public GameObject GetImpactFX(Vector3 position, Quaternion rotation)
    {
        return GetParticle(ParticleType.ImpactFX, position, rotation);
    }

    /// <summary>
    /// Get a bubble FX particle effect from the pool
    /// </summary>
    public GameObject GetBubbleFX(Vector3 position, Quaternion rotation)
    {
        return GetParticle(ParticleType.BubbleFX, position, rotation);
    }

    /// <summary>
    /// Get a lure grenade particle effect from the pool
    /// </summary>
    public GameObject GetLureGrenade(Vector3 position, Quaternion rotation)
    {
        return GetParticle(ParticleType.LureGrenade, position, rotation);
    }

    /// <summary>
    /// Get a stun grenade particle effect from the pool
    /// </summary>
    public GameObject GetStunGrenade(Vector3 position, Quaternion rotation)
    {
        return GetParticle(ParticleType.StunGrenade, position, rotation);
    }

    public GameObject GetDestroyedAudioLogFX(Vector3 position, Quaternion rotation)
    {
        return GetParticle(ParticleType.AudioLogDestroyFX, position, rotation);
    }

    public GameObject GetBreakEggFX(Vector3 position, Quaternion rotation)
    {
        return GetParticle(ParticleType.BreakEggFX, position, rotation);
    }

    /// <summary>
    /// Static convenience method - Get blood splat particle
    /// </summary>
    public static GameObject CreateBloodSplat(Vector3 position, Quaternion rotation)
    {
        return Instance.GetBloodSplat(position, rotation);
    }

    /// <summary>
    /// Static convenience method - Get impact FX particle
    /// </summary>
    public static GameObject CreateImpactFX(Vector3 position, Quaternion rotation)
    {
        return Instance.GetImpactFX(position, rotation);
    }

    /// <summary>
    /// Static convenience method - Get bubble FX particle
    /// </summary>
    public static GameObject CreateBubbleFX(Vector3 position, Quaternion rotation)
    {
        return Instance.GetBubbleFX(position, rotation);
    }

    /// <summary>
    /// Static convenience method - Get lure grenade particle
    /// </summary>
    public static GameObject CreateLureGrenade(Vector3 position, Quaternion rotation)
    {
        return Instance.GetLureGrenade(position, rotation);
    }

    /// <summary>
    /// Static convenience method - Get stun grenade particle
    /// </summary>
    public static GameObject CreateStunGrenade(Vector3 position, Quaternion rotation)
    {
        return Instance.GetStunGrenade(position, rotation);
    }

    public static GameObject CreateDestroyedAudioLogFX(Vector3 position, Quaternion rotation)
    {
        return Instance.GetDestroyedAudioLogFX(position, rotation);
    }

    public static GameObject CreateBreakEggFX(Vector3 position, Quaternion rotation)
    {
        return Instance.GetBreakEggFX(position, rotation);
    }

    #endregion

    #region Generic Particle Management
    /// <summary>
    /// Get a particle effect from the specified pool
    /// </summary>
    private GameObject GetParticle(ParticleType particleType, Vector3 position, Quaternion rotation)
    {
        if (!poolLookup.TryGetValue(particleType, out ParticlePoolConfig poolConfig))
        {
            Debug.LogError($"ParticleFXPool: No pool configured for {particleType}!");
            return null;
        }

        GameObject particleObject = GetAvailableParticle(poolConfig);

        if (particleObject != null)
        {
            // Position and activate the particle
            particleObject.transform.position = position;
            particleObject.transform.rotation = rotation;
            particleObject.SetActive(true);

            // Play the particle system
            ParticleSystem particleSystem = particleObject.GetComponent<ParticleSystem>();
            if (particleSystem == null)
            {
                particleSystem = particleObject.GetComponentInChildren<ParticleSystem>();
            }

            if (particleSystem != null)
            {
                particleSystem.Play();
            }

            // Move to active list
            poolConfig.activeParticles.Add(particleObject);

            DebugLog($"Retrieved {particleType} from pool. Active: {poolConfig.ActiveCount}, Available: {poolConfig.AvailableCount}");
        }

        return particleObject;
    }

    /// <summary>
    /// Return a particle effect to its pool
    /// </summary>
    public void ReturnParticle(GameObject particleObject, ParticleType particleType)
    {
        if (particleObject == null) return;

        if (!poolLookup.TryGetValue(particleType, out ParticlePoolConfig poolConfig))
        {
            Debug.LogError($"ParticleFXPool: No pool configured for {particleType}!");
            return;
        }

        // Remove from active list
        if (poolConfig.activeParticles.Remove(particleObject))
        {
            // Stop the particle system
            ParticleSystem particleSystem = particleObject.GetComponent<ParticleSystem>();
            if (particleSystem == null)
            {
                particleSystem = particleObject.GetComponentInChildren<ParticleSystem>();
            }

            if (particleSystem != null)
            {
                particleSystem.Stop();
                particleSystem.Clear();
            }

            // Deactivate and return to pool
            particleObject.SetActive(false);
            particleObject.transform.SetParent(poolConfig.poolParent);
            poolConfig.availableParticles.Enqueue(particleObject);

            DebugLog($"Returned {particleType} to pool. Active: {poolConfig.ActiveCount}, Available: {poolConfig.AvailableCount}");
        }
        else
        {
            DebugLog($"Attempted to return {particleType} that wasn't in active list: {particleObject.name}");
        }
    }

    private GameObject GetAvailableParticle(ParticlePoolConfig poolConfig)
    {
        // Try to get from available pool
        if (poolConfig.availableParticles.Count > 0)
        {
            return poolConfig.availableParticles.Dequeue();
        }

        // Pool is empty, try to create a new one if under max size
        if (poolConfig.TotalPoolSize < poolConfig.maxPoolSize)
        {
            DebugLog($"{poolConfig.particleType} pool empty, creating new particle object");
            return CreatePooledParticle(poolConfig);
        }

        // Pool is at max capacity
        Debug.LogWarning($"ParticleFXPool: {poolConfig.particleType} pool at maximum capacity and no available objects!");
        return null;
    }
    #endregion

    #region Pool Utilities
    /// <summary>
    /// Return all active particles of a specific type to the pool
    /// </summary>
    [Button("Return All Active Particles")]
    public void ReturnAllActiveParticles(ParticleType particleType)
    {
        if (!poolLookup.TryGetValue(particleType, out ParticlePoolConfig poolConfig))
        {
            Debug.LogError($"ParticleFXPool: No pool configured for {particleType}!");
            return;
        }

        List<GameObject> toReturn = new List<GameObject>(poolConfig.activeParticles);
        foreach (GameObject particle in toReturn)
        {
            ReturnParticle(particle, particleType);
        }

        DebugLog($"Returned {toReturn.Count} active {particleType} particles to pool");
    }

    /// <summary>
    /// Return all active particles across all pools
    /// </summary>
    [Button("Return All Active Particles (All Pools)")]
    public void ReturnAllActiveParticles()
    {
        foreach (var poolConfig in particlePools)
        {
            List<GameObject> toReturn = new List<GameObject>(poolConfig.activeParticles);
            foreach (GameObject particle in toReturn)
            {
                ReturnParticle(particle, poolConfig.particleType);
            }
        }

        DebugLog("Returned all active particles across all pools");
    }

    /// <summary>
    /// Expand a specific pool by creating additional objects
    /// </summary>
    [Button("Expand Pool")]
    public void ExpandPool(ParticleType particleType, int additionalObjects = 5)
    {
        if (!poolLookup.TryGetValue(particleType, out ParticlePoolConfig poolConfig))
        {
            Debug.LogError($"ParticleFXPool: No pool configured for {particleType}!");
            return;
        }

        int created = 0;
        for (int i = 0; i < additionalObjects && poolConfig.TotalPoolSize < poolConfig.maxPoolSize; i++)
        {
            if (CreatePooledParticle(poolConfig) != null)
            {
                created++;
            }
        }

        DebugLog($"Expanded {particleType} pool by {created} objects. Total size: {poolConfig.TotalPoolSize}");
    }

    /// <summary>
    /// Get statistics for a specific pool
    /// </summary>
    public string GetPoolStats(ParticleType particleType)
    {
        if (!poolLookup.TryGetValue(particleType, out ParticlePoolConfig poolConfig))
        {
            return $"No pool configured for {particleType}";
        }

        return $"{particleType} Pool - Total: {poolConfig.TotalPoolSize}, Active: {poolConfig.ActiveCount}, Available: {poolConfig.AvailableCount}";
    }

    /// <summary>
    /// Get statistics for all pools
    /// </summary>
    public string GetAllPoolStats()
    {
        string stats = "=== Particle FX Pool Stats ===\n";
        foreach (var poolConfig in particlePools)
        {
            stats += $"{poolConfig.particleType}: Total={poolConfig.TotalPoolSize}, Active={poolConfig.ActiveCount}, Available={poolConfig.AvailableCount}\n";
        }
        return stats;
    }
    #endregion

    #region Debug
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ParticleFXPool] {message}");
        }
    }

    [Button("Print All Pool Stats")]
    private void PrintAllPoolStats()
    {
        Debug.Log(GetAllPoolStats());
    }

    [Button("Setup Default Pools")]
    private void SetupDefaultPools()
    {
        particlePools.Clear();

        // Add a config for each particle type
        foreach (ParticleType type in System.Enum.GetValues(typeof(ParticleType)))
        {
            particlePools.Add(new ParticlePoolConfig
            {
                particleType = type,
                initialPoolSize = 10,
                maxPoolSize = 30,
                name = type.ToString()
            });
        }

        Debug.Log("Created default pool configurations. Assign prefabs in inspector.");
    }

    private void OnDrawGizmosSelected()
    {
        if (particlePools == null || particlePools.Count == 0) return;

        foreach (var poolConfig in particlePools)
        {
            if (poolConfig.poolParent == null) continue;

            // Different color for each pool type
            Color poolColor = GetGizmoColorForType(poolConfig.particleType);
            Gizmos.color = poolColor;

            foreach (Transform child in poolConfig.poolParent)
            {
                // Draw connection line
                Gizmos.DrawLine(transform.position, child.position);

                // Draw differently for active vs inactive
                if (child.gameObject.activeInHierarchy)
                {
                    Gizmos.color = Color.Lerp(poolColor, Color.white, 0.5f);
                    Gizmos.DrawWireSphere(child.position, 0.3f);
                }
                else
                {
                    Gizmos.color = Color.Lerp(poolColor, Color.black, 0.5f);
                    Gizmos.DrawWireCube(child.position, Vector3.one * 0.2f);
                }
            }
        }
    }

    private Color GetGizmoColorForType(ParticleType type)
    {
        return type switch
        {
            ParticleType.BloodSplat => Color.red,
            ParticleType.ImpactFX => Color.yellow,
            ParticleType.BubbleFX => Color.cyan,
            ParticleType.LureGrenade => Color.green,
            ParticleType.StunGrenade => Color.magenta,
            _ => Color.white
        };
    }
    #endregion
}