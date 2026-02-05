using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Controls the visual appearance of damage cracks on a glass region.
/// This script updates a shader property to gradually reveal crack textures as health decreases.
/// Attach this to each cracked glass GameObject (one per health region).
/// </summary>
public class CrackVisualController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The material that uses the CrackRevealShader (will be automatically created from renderer if not assigned)")]
    [SerializeField] private Material crackMaterial;

    [Tooltip("Optional: The renderer component (auto-found if not assigned)")]
    [SerializeField] private Renderer glassRenderer;

    [Header("Reveal Settings")]
    [Tooltip("How quickly the crack appears/disappears (higher = faster transitions)")]
    [SerializeField] private float transitionSpeed = 5f;

    [Tooltip("Should the crack appear gradually or instantly when health changes?")]
    [SerializeField] private bool smoothTransition = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showDebugInfo = true;

    // Shader property names (these must match the properties in your Shader Graph)
    private static readonly int RevealAmountProperty = Shader.PropertyToID("_RevealAmount");

    // Current state
    [ShowInInspector, ReadOnly, ShowIf("showDebugInfo")]
    private float currentRevealAmount = 0f;

    [ShowInInspector, ReadOnly, ShowIf("showDebugInfo")]
    private float targetRevealAmount = 0f;

    private void Awake()
    {
        // Find the renderer if not assigned
        if (glassRenderer == null)
        {
            glassRenderer = GetComponent<Renderer>();

            if (glassRenderer == null)
            {
                Debug.LogError($"[CrackVisualController] No Renderer found on {gameObject.name}! Please assign one or add a Renderer component.");
                enabled = false;
                return;
            }
        }

        // Create a unique material instance for this crack
        // This prevents all cracks from showing the same damage level
        if (crackMaterial == null)
        {
            crackMaterial = glassRenderer.material; // This automatically creates an instance
            DebugLog("Created material instance from renderer");
        }
        else
        {
            // If a material was assigned, create an instance of it
            crackMaterial = new Material(crackMaterial);
            glassRenderer.material = crackMaterial;
            DebugLog("Created material instance from assigned material");
        }

        // Start with no crack visible
        currentRevealAmount = 0f;
        targetRevealAmount = 0f;
        UpdateShaderProperty(0f);
    }

    private void Update()
    {
        // Smoothly interpolate to the target reveal amount if smooth transition is enabled
        if (smoothTransition && !Mathf.Approximately(currentRevealAmount, targetRevealAmount))
        {
            currentRevealAmount = Mathf.Lerp(currentRevealAmount, targetRevealAmount, Time.deltaTime * transitionSpeed);

            // Snap to target if we're very close (prevents infinite lerping)
            if (Mathf.Abs(currentRevealAmount - targetRevealAmount) < 0.001f)
            {
                currentRevealAmount = targetRevealAmount;
            }

            UpdateShaderProperty(currentRevealAmount);
        }
    }

    /// <summary>
    /// Update the crack visibility based on health percentage.
    /// Call this whenever the health region's health changes.
    /// </summary>
    /// <param name="healthPercentage">Current health as a percentage (0.0 to 1.0)</param>
    public void UpdateCrackVisibility(float healthPercentage)
    {
        // Clamp to valid range
        healthPercentage = Mathf.Clamp01(healthPercentage);

        // Convert health percentage to reveal amount
        // When health is 100% (1.0), reveal amount should be 0 (no crack visible)
        // When health is 0% (0.0), reveal amount should be 1 (full crack visible)
        float newRevealAmount = 1f - healthPercentage;

        DebugLog($"Health: {healthPercentage:P0} -> Reveal: {newRevealAmount:F2}");

        targetRevealAmount = newRevealAmount;

        // If smooth transition is disabled, update immediately
        if (!smoothTransition)
        {
            currentRevealAmount = targetRevealAmount;
            UpdateShaderProperty(currentRevealAmount);
        }
    }

    /// <summary>
    /// Immediately set the crack visibility to a specific reveal amount (0 = hidden, 1 = fully visible)
    /// </summary>
    public void SetRevealAmountImmediate(float revealAmount)
    {
        revealAmount = Mathf.Clamp01(revealAmount);
        currentRevealAmount = revealAmount;
        targetRevealAmount = revealAmount;
        UpdateShaderProperty(revealAmount);

        DebugLog($"Set reveal amount immediately to: {revealAmount:F2}");
    }

    /// <summary>
    /// Hide the crack completely (useful for repairs)
    /// </summary>
    public void HideCrack()
    {
        UpdateCrackVisibility(1f); // 100% health = no visible crack
    }

    /// <summary>
    /// Show the crack fully (useful for complete destruction)
    /// </summary>
    public void ShowFullCrack()
    {
        UpdateCrackVisibility(0f); // 0% health = full crack visible
    }

    /// <summary>
    /// Update the shader's reveal amount property
    /// </summary>
    private void UpdateShaderProperty(float revealAmount)
    {
        if (crackMaterial != null)
        {
            crackMaterial.SetFloat(RevealAmountProperty, revealAmount);
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[CrackVisualController - {gameObject.name}] {message}");
        }
    }

    private void OnDestroy()
    {
        // Clean up the material instance when this object is destroyed
        if (crackMaterial != null)
        {
            Destroy(crackMaterial);
        }
    }

#if UNITY_EDITOR
    // Inspector buttons for testing the crack visuals
    [Button("Show No Crack (100% Health)"), PropertyOrder(200)]
    private void TestNoCrack()
    {
        UpdateCrackVisibility(1f);
    }

    [Button("Show Light Crack (75% Health)"), PropertyOrder(201)]
    private void TestLightCrack()
    {
        UpdateCrackVisibility(0.75f);
    }

    [Button("Show Medium Crack (50% Health)"), PropertyOrder(202)]
    private void TestMediumCrack()
    {
        UpdateCrackVisibility(0.5f);
    }

    [Button("Show Heavy Crack (25% Health)"), PropertyOrder(203)]
    private void TestHeavyCrack()
    {
        UpdateCrackVisibility(0.25f);
    }

    [Button("Show Full Crack (0% Health)"), PropertyOrder(204)]
    private void TestFullCrack()
    {
        UpdateCrackVisibility(0f);
    }
#endif
}