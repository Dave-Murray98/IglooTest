using System.Collections;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Controls a flashing warning light that can be triggered on/off.
/// Perfect for indicating low health or danger states.
/// </summary>
public class FlashingWarningLight : MonoBehaviour
{
    [Header("Light Reference")]
    [Tooltip("The light component to control. Will auto-find if not assigned.")]
    [SerializeField] private Light warningLight;

    [Header("Flash Settings")]
    [Tooltip("How fast the light flashes (higher = faster)")]
    [SerializeField] private float flashSpeed = 3f;

    [Tooltip("Minimum intensity when flashing")]
    [SerializeField] private float minIntensity = 0f;

    [Tooltip("Maximum intensity when flashing")]
    [SerializeField] private float maxIntensity = 2f;

    [Tooltip("Color of the warning light")]
    [SerializeField] private Color warningColor = Color.red;

    [Header("Behavior")]
    [Tooltip("Should the light start flashing automatically when the game starts?")]
    [SerializeField] private bool startFlashingOnAwake = false;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Properties for easy access
    public bool IsFlashing => isFlashing;

    // State tracking
    [ShowInInspector, ReadOnly] private bool isFlashing = false;
    private Coroutine flashCoroutine;
    private float originalIntensity;
    private Color originalColor;

    private void Awake()
    {
        // Try to find the Light component if not assigned
        if (warningLight == null)
        {
            warningLight = GetComponent<Light>();

            if (warningLight == null)
            {
                Debug.LogError($"[FlashingWarningLight] No Light component found on {gameObject.name}! Please add one or assign it manually.");
                return;
            }
            else
            {
                DebugLog("Auto-found Light component");
            }
        }

        // Store original values so we can restore them later
        originalIntensity = warningLight.intensity;
        originalColor = warningLight.color;

        // Set warning color
        warningLight.color = warningColor;

        // Start with light off
        warningLight.intensity = 0f;
        warningLight.enabled = false;
    }

    private void Start()
    {
        // Start flashing if requested
        if (startFlashingOnAwake)
        {
            StartFlashing();
        }
    }

    /// <summary>
    /// Starts the flashing animation
    /// </summary>
    public void StartFlashing()
    {
        if (isFlashing)
        {
            DebugLog("Already flashing, ignoring start request");
            return;
        }

        if (warningLight == null)
        {
            Debug.LogWarning("[FlashingWarningLight] Cannot start flashing - no light assigned!");
            return;
        }

        isFlashing = true;
        warningLight.enabled = true;

        // Start the flash coroutine
        flashCoroutine = StartCoroutine(FlashCoroutine());

        DebugLog("Started flashing");
    }

    /// <summary>
    /// Stops the flashing animation and turns off the light
    /// </summary>
    public void StopFlashing()
    {
        if (!isFlashing)
        {
            DebugLog("Not currently flashing, ignoring stop request");
            return;
        }

        isFlashing = false;

        // Stop the coroutine if it's running
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }

        // Turn off the light
        if (warningLight != null)
        {
            warningLight.intensity = 0f;
            warningLight.enabled = false;
        }

        DebugLog("Stopped flashing");
    }

    /// <summary>
    /// Toggles flashing on/off
    /// </summary>
    public void ToggleFlashing()
    {
        if (isFlashing)
        {
            StopFlashing();
        }
        else
        {
            StartFlashing();
        }
    }

    /// <summary>
    /// Coroutine that handles the flashing animation
    /// </summary>
    private IEnumerator FlashCoroutine()
    {
        while (isFlashing)
        {
            // Use PingPong to create a smooth min -> max -> min loop
            float intensity = Mathf.Lerp(minIntensity, maxIntensity,
                Mathf.PingPong(Time.time * flashSpeed, 1f));

            warningLight.intensity = intensity;

            yield return null;
        }

        // Clean up
        flashCoroutine = null;
    }

    /// <summary>
    /// Restore the light to its original state
    /// </summary>
    public void RestoreOriginalState()
    {
        StopFlashing();

        if (warningLight != null)
        {
            warningLight.color = originalColor;
            warningLight.intensity = originalIntensity;
            warningLight.enabled = true;
        }
    }

    private void OnDestroy()
    {
        // Clean up when destroyed
        StopFlashing();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[FlashingWarningLight] {message}");
        }
    }

#if UNITY_EDITOR
    // Inspector buttons for testing
    [Button("Start Flashing"), PropertyOrder(100)]
    private void TestStartFlashing()
    {
        StartFlashing();
    }

    [Button("Stop Flashing"), PropertyOrder(101)]
    private void TestStopFlashing()
    {
        StopFlashing();
    }

    [Button("Toggle Flashing"), PropertyOrder(102)]
    private void TestToggle()
    {
        ToggleFlashing();
    }
#endif
}