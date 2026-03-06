using UnityEngine;
using Crest;

/// <summary>
/// ROTATION-INDEPENDENT: PlayerWaterDetector now calculates detection points
/// relative to the main player transform, ignoring body rotation from swimming controller.
/// This ensures consistent water detection regardless of body orientation.
/// </summary>
public class PlayerWaterDetector : MonoBehaviour
{
    private Transform player;

    [Header("Water State Thresholds")]
    [SerializeField, Tooltip("Submarine is considered underwater when this deep")]
    private float submarineSubmersionDepthThreshold = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showDebugGizmos = true;

    // Crest water sampling helpers
    private SampleHeightHelper submarineSampleHelper;
    private SampleHeightHelper surfaceSampleHelper;
    private OceanRenderer oceanRenderer;

    // Water state tracking - immediate transitions
    private bool isSubmarineUnderwater = false;
    private bool wasSubmarineUnderwater = false;

    // Water height and depth data
    private float waterHeightAtSubmarine;
    private float submarineDepthInWater;

    // Events - immediate transitions
    public event System.Action OnSubmarineSubmerged;    // Submarine goes underwater
    public event System.Action OnSubmarineSurfaced;     // Submarine surfaces

    // Public properties - immediate water state
    public bool IsSubmarineUnderwater => isSubmarineUnderwater;
    public float SubmarineDepthInWater => submarineDepthInWater;

    // Swimming state properties for surface systems
    public float WaterHeightAtSubmarine => waterHeightAtSubmarine;

    // Scene compatibility
    public float SubmarineDepth => SubmarineDepthInWater;

    #region Initialization

    private void Awake()
    {
        player = GetComponent<Transform>();
        InitializeCrestComponents();
    }

    private void InitializeCrestComponents()
    {
        oceanRenderer = FindFirstObjectByType<OceanRenderer>();

        if (oceanRenderer == null) return;

        try
        {
            submarineSampleHelper = new SampleHeightHelper();
            surfaceSampleHelper = new SampleHeightHelper();

            DebugLog("Crest components initialized successfully with rotation-independent detection");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerWaterDetector] Failed to initialize Crest: {e.Message}");
            InitializeNonWaterScene();
        }
    }

    private void InitializeNonWaterScene()
    {
        isSubmarineUnderwater = false;
        wasSubmarineUnderwater = false;
        submarineDepthInWater = 0f;
        waterHeightAtSubmarine = 0f;

        DebugLog("Non-water scene initialization complete");
    }

    #endregion

    #region Water State Detection

    private void Update()
    {
        UpdateWaterDetection();
        CheckWaterStateChanges();
    }

    private void UpdateWaterDetection()
    {
        // Sample water height at the submarine's position (rotation-independent)
        waterHeightAtSubmarine = SampleWaterHeightAtPosition(player.position, submarineSampleHelper);
        submarineDepthInWater = Mathf.Max(0f, waterHeightAtSubmarine - player.position.y);

        UpdateWaterStateFlags();
    }

    private void UpdateWaterStateFlags()
    {
        wasSubmarineUnderwater = isSubmarineUnderwater;
        isSubmarineUnderwater = submarineDepthInWater > submarineSubmersionDepthThreshold;
    }

    /// <summary>
    /// Check for immediate water state changes and fire events
    /// </summary>
    private void CheckWaterStateChanges()
    {
        if (isSubmarineUnderwater != wasSubmarineUnderwater)
        {
            if (isSubmarineUnderwater)
            {
                DebugLog($"Submarine submerged - depth: {submarineDepthInWater:F2}m");
                OnSubmarineSubmerged?.Invoke();
            }
            else
            {
                DebugLog($"Submarine surfaced - depth: {submarineDepthInWater:F2}m");
                OnSubmarineSurfaced?.Invoke();
            }
        }
    }

    #endregion

    #region Water Sampling

    /// <summary>
    /// Sample water height at a specific position using Crest
    /// </summary>
    private float SampleWaterHeightAtPosition(Vector3 worldPosition, SampleHeightHelper helper)
    {
        if (helper == null || oceanRenderer == null)
            return 0f;

        try
        {
            helper.Init(worldPosition, 0f, false, this);

            if (helper.Sample(out float waterHeight))
            {
                return waterHeight;
            }

            return oceanRenderer.SeaLevel;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerWaterDetector] Error sampling water: {e.Message}");
            return 0f;
        }
    }

    /// <summary>
    /// Get water depth at any world position (utility method for other systems)
    /// </summary>
    public float GetWaterDepthAtPosition(Vector3 worldPosition)
    {
        float waterHeight = SampleWaterHeightAtPosition(worldPosition, surfaceSampleHelper);
        return Mathf.Max(0f, waterHeight - worldPosition.y);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Force a water state check (useful for external systems)
    /// </summary>
    public void ForceWaterStateCheck()
    {
        UpdateWaterDetection();
        CheckWaterStateChanges();
    }

    #endregion

    #region Debug

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerWaterDetector] {message}");
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        if (Application.isPlaying)
        {
            Gizmos.color = isSubmarineUnderwater ? Color.red : Color.green;
            Gizmos.DrawWireSphere(player.position, 0.1f);
        }
        else
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }
    }

    #endregion
}