using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Base class for all handler speech triggers.
/// Handles common trigger logic, one-time triggers, and played state tracking.
/// NOW SUPPORTS SAVE/LOAD: Triggers persist their state across game sessions
/// Derived classes implement specific trigger conditions (collider, quest, item pickup, etc.)
/// </summary>
public abstract class HandlerSpeechTriggerBase : MonoBehaviour
{
    [Header("Handler Speech Settings")]
    [Tooltip("The handler speech to play when this trigger activates")]
    [SerializeField] protected HandlerSpeechData handlerSpeechData;

    [Header("Trigger Settings")]
    [Tooltip("Should this trigger only fire once?")]
    [SerializeField] protected bool triggerOnce = true;

    [Tooltip("Delay before triggering speech (useful for timing adjustments)")]
    [SerializeField] protected float triggerDelay = 0f;

    [Header("Trigger ID")]
    [Tooltip("Unique ID for save/load system - auto-generated if empty")]
    [SerializeField] protected string triggerID = "";
    [SerializeField] protected bool autoGenerateID = true;

    [Header("State Tracking")]
    [SerializeField][ReadOnly] protected bool hasTriggered = false;

    [Header("Debug")]
    [SerializeField] protected bool enableDebugLogs = false;

    // Public properties
    public HandlerSpeechData HandlerSpeechData => handlerSpeechData;
    public bool HasTriggered => hasTriggered;
    public bool TriggerOnce => triggerOnce;
    public string TriggerID => triggerID;

    protected virtual void Awake()
    {
        // Generate ID if needed
        if (autoGenerateID && string.IsNullOrEmpty(triggerID))
        {
            triggerID = GenerateUniqueTriggerID();
        }

        ValidateSetup();
    }

    protected virtual void Start()
    {
        // Register with HandlerSpeechManager if it exists
        if (HandlerSpeechManager.Instance != null)
        {
            HandlerSpeechManager.Instance.RegisterTrigger(this);
        }

        // Check if this speech was already played (from save data)
        // This will be called by HandlerSpeechManagerSaveComponent during restoration
    }

    /// <summary>
    /// Generates a unique trigger ID based on hierarchy and position
    /// </summary>
    protected virtual string GenerateUniqueTriggerID()
    {
        string typeName = GetType().Name;
        string sceneName = gameObject.scene.name;
        string hierarchyPath = GetHierarchyPath();
        string position = transform.position.ToString("F1");

        return $"{sceneName}_{typeName}_{hierarchyPath}_{position}";
    }

    /// <summary>
    /// Gets the hierarchy path for this object
    /// </summary>
    protected string GetHierarchyPath()
    {
        string path = gameObject.name;
        Transform current = transform.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path.Replace(" ", "_");
    }

    /// <summary>
    /// Validates that the trigger has all required components and data
    /// </summary>
    protected virtual void ValidateSetup()
    {
        if (handlerSpeechData == null)
        {
            Debug.LogError($"[HandlerSpeechTrigger:{gameObject.name}] No HandlerSpeechData assigned!");
        }
        else if (!handlerSpeechData.IsValid())
        {
            Debug.LogError($"[HandlerSpeechTrigger:{gameObject.name}] HandlerSpeechData is invalid!");
        }

        if (string.IsNullOrEmpty(triggerID))
        {
            Debug.LogWarning($"[HandlerSpeechTrigger:{gameObject.name}] No trigger ID assigned!");
        }
    }

    /// <summary>
    /// Attempts to trigger the handler speech.
    /// Checks trigger conditions and passes to manager if valid.
    /// </summary>
    protected void TriggerSpeech()
    {
        // Check if already triggered (for one-time triggers)
        if (hasTriggered && triggerOnce)
        {
            DebugLog("Trigger already fired (triggerOnce=true)");
            return;
        }

        // Validate speech data
        if (handlerSpeechData == null || !handlerSpeechData.IsValid())
        {
            DebugLog("Invalid handler speech data - cannot trigger");
            return;
        }

        // Check if manager exists
        if (HandlerSpeechManager.Instance == null)
        {
            Debug.LogError($"[HandlerSpeechTrigger:{gameObject.name}] HandlerSpeechManager not found!");
            return;
        }

        // Apply delay if specified
        if (triggerDelay > 0f)
        {
            DebugLog($"Triggering speech after {triggerDelay}s delay");
            StartCoroutine(TriggerWithDelay());
        }
        else
        {
            ExecuteTrigger();
        }
    }

    /// <summary>
    /// Triggers speech with delay
    /// </summary>
    private System.Collections.IEnumerator TriggerWithDelay()
    {
        yield return new WaitForSeconds(triggerDelay);
        ExecuteTrigger();
    }

    /// <summary>
    /// Executes the actual trigger logic
    /// </summary>
    private void ExecuteTrigger()
    {
        DebugLog($"Triggering handler speech: {handlerSpeechData.SpeechTitle}");

        // Mark as triggered
        hasTriggered = true;

        // Trigger through manager
        HandlerSpeechManager.Instance.PlaySpeech(handlerSpeechData);

        // Notify derived classes
        OnSpeechTriggered();

        // Disable trigger if one-time
        if (triggerOnce)
        {
            OnTriggerDisabled();
        }
    }

    /// <summary>
    /// Resets the trigger state (for non-one-time triggers or debugging)
    /// </summary>
    public virtual void ResetTrigger()
    {
        hasTriggered = false;
        DebugLog("Trigger reset");
        OnTriggerReset();
    }

    /// <summary>
    /// Sets the triggered state (used by save system)
    /// </summary>
    public virtual void SetTriggeredState(bool triggered)
    {
        hasTriggered = triggered;

        if (hasTriggered && triggerOnce)
        {
            OnTriggerDisabled();
        }

        DebugLog($"Trigger state set to: {triggered}");
    }

    /// <summary>
    /// Called when the speech is successfully triggered.
    /// Override for custom behavior in derived classes.
    /// </summary>
    protected virtual void OnSpeechTriggered()
    {
        // Override in derived classes for custom behavior
    }

    /// <summary>
    /// Called when the trigger is disabled (after one-time trigger fires).
    /// Override for custom behavior in derived classes (e.g., disable visuals).
    /// </summary>
    protected virtual void OnTriggerDisabled()
    {
        // Override in derived classes for custom behavior
        DebugLog("Trigger disabled");
    }

    /// <summary>
    /// Called when the trigger is reset.
    /// Override for custom behavior in derived classes.
    /// </summary>
    protected virtual void OnTriggerReset()
    {
        // Override in derived classes for custom behavior
    }

    /// <summary>
    /// Checks if the trigger can currently fire
    /// </summary>
    protected bool CanTrigger()
    {
        // Check if already triggered
        if (hasTriggered && triggerOnce)
        {
            DebugLog("Cannot trigger - already triggered once");
            return false;
        }

        // Check if speech data is valid
        if (handlerSpeechData == null || !handlerSpeechData.IsValid())
        {
            DebugLog("Cannot trigger - invalid speech data");
            return false;
        }

        // Check if manager exists
        if (HandlerSpeechManager.Instance == null)
        {
            DebugLog("Cannot trigger - HandlerSpeechManager not found");
            return false;
        }

        DebugLog("Can trigger");
        return true;
    }

    protected void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[HandlerSpeechTrigger:{triggerID}] {message}");
        }
    }

    protected virtual void OnDestroy()
    {
        // Unregister from manager
        if (HandlerSpeechManager.Instance != null)
        {
            HandlerSpeechManager.Instance.UnregisterTrigger(this);
        }
    }

#if UNITY_EDITOR
    [Button("Test Trigger")]
    protected virtual void TestTrigger()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Test trigger only works in Play mode");
            return;
        }

        TriggerSpeech();
    }

    [Button("Reset Trigger")]
    protected void EditorResetTrigger()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Reset trigger only works in Play mode");
            return;
        }

        ResetTrigger();
    }

    [Button("Generate Trigger ID")]
    protected void EditorGenerateTriggerID()
    {
        triggerID = GenerateUniqueTriggerID();
        Debug.Log($"Generated trigger ID: {triggerID}");
    }

    protected virtual void OnDrawGizmos()
    {
        // Override in derived classes for visual debugging
    }

    protected virtual void OnDrawGizmosSelected()
    {
        // Draw speech info in scene view
        if (handlerSpeechData != null)
        {
            string statusText = Application.isPlaying
                ? (hasTriggered ? "✓ TRIGGERED" : "○ READY")
                : "TRIGGER";

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f,
                $"{statusText}: {handlerSpeechData.SpeechTitle}\nID: {triggerID}"
            );
        }
    }

    protected virtual void OnValidate()
    {
        // Auto-generate ID in editor if needed
        if (autoGenerateID && string.IsNullOrEmpty(triggerID))
        {
            triggerID = GenerateUniqueTriggerID();
        }
    }
#endif
}