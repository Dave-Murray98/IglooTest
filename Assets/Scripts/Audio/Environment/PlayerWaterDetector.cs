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
    [SerializeField, Tooltip("Head is considered underwater when this deep")]
    private float playerSubmersionDepthThreshold = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showDebugGizmos = true;


    // Crest water sampling helpers
    private SampleHeightHelper headSampleHelper;
    private SampleHeightHelper surfaceSampleHelper;
    private OceanRenderer oceanRenderer;

    // Water state tracking - immediate transitions
    private bool isHeadUnderwater = false;
    private bool wasHeadUnderwater = false;

    // Water height and depth data
    private float waterHeightAtHead;
    private float headDepthInWater;

    // Events - immediate transitions
    public event System.Action OnHeadSubmerged;      // Head goes underwater while swimming
    public event System.Action OnHeadSurfaced;       // Head surfaces while swimming

    // Public properties - immediate water state
    public bool IsHeadUnderwater => isHeadUnderwater;
    public float HeadDepthInWater => headDepthInWater;

    // Swimming state properties for surface systems
    public float WaterHeightAtHead => waterHeightAtHead;

    // Scene compatibility
    public float HeadDepth => HeadDepthInWater;

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
            // Create sample helpers for all detection points
            headSampleHelper = new SampleHeightHelper();
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
        // Reset all water state
        isHeadUnderwater = false;
        wasHeadUnderwater = false;
        headDepthInWater = 0f;
        waterHeightAtHead = 0f;

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
        // Sample water height at calculated detection positions (rotation-independent)
        waterHeightAtHead = SampleWaterHeightAtPosition(player.position, headSampleHelper);
        headDepthInWater = Mathf.Max(0f, waterHeightAtHead - player.position.y);

        UpdateWaterStateFlags();
    }

    private void UpdateWaterStateFlags()
    {
        wasHeadUnderwater = isHeadUnderwater;

        // Head submersion state (for underwater vs surface swimming distinction)
        isHeadUnderwater = headDepthInWater > playerSubmersionDepthThreshold;
    }

    /// <summary>
    /// Check for immediate water state changes
    /// </summary>
    private void CheckWaterStateChanges()
    {
        // Head submersion events (for underwater vs surface swimming distinction)
        if (isHeadUnderwater != wasHeadUnderwater)
        {
            if (isHeadUnderwater)
            {
                DebugLog($"Head submerged (rotation-independent) - head depth: {headDepthInWater:F2}m");
                OnHeadSubmerged?.Invoke();
            }
            else
            {
                DebugLog($"Head surfaced (rotation-independent) - head depth: {headDepthInWater:F2}m");
                OnHeadSurfaced?.Invoke();
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
    /// Get water depth at any position (utility method)
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

        // ROTATION-INDEPENDENT: Draw calculated detection positions (not bone positions)
        if (Application.isPlaying)
        {
            // Draw head detection position
            Gizmos.color = isHeadUnderwater ? Color.red : Color.green;
            Gizmos.DrawWireSphere(player.position, 0.1f);

        }
        else if (!Application.isPlaying)
        {
            // Show preview of detection points in editor
            Vector3 playerPos = transform.position;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(playerPos, 0.1f);
        }
    }

    #endregion
}