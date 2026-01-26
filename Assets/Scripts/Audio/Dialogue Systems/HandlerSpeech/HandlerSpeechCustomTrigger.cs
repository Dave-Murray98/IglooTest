using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Flexible handler speech trigger that fires based on custom conditions.
/// Can be triggered manually via code or by monitoring a boolean condition.
/// Useful for complex trigger logic that doesn't fit other trigger types.
/// </summary>
public class HandlerSpeechCustomTrigger : HandlerSpeechTriggerBase
{
    [Header("Custom Trigger Settings")]
    [Tooltip("Should this trigger monitor a condition continuously?")]
    [SerializeField] private bool monitorCondition = false;

    [Tooltip("How often to check the condition (seconds)")]
    [SerializeField] private float conditionCheckInterval = 0.5f;

    [Header("Unity Events")]
    [Tooltip("Called when checking if trigger condition is met - return true to trigger")]
    [SerializeField] private UnityEvent<HandlerSpeechCustomTrigger> onCheckCondition;

    [Tooltip("Called when speech is triggered")]
    [SerializeField] private UnityEvent onTriggered;

    [Header("Debug")]
    [Tooltip("Force trigger via inspector (Play mode only)")]
    [SerializeField] private bool debugForceTrigger = false;

    // Delegate for custom condition checking
    public delegate bool ConditionCheckDelegate();
    private ConditionCheckDelegate customConditionCheck;

    protected override void Start()
    {
        base.Start();

        // Start monitoring condition if enabled
        if (monitorCondition)
        {
            StartCoroutine(MonitorConditionRoutine());
        }
    }

    private void Update()
    {
        // Debug force trigger
        if (debugForceTrigger && Application.isPlaying)
        {
            debugForceTrigger = false;
            ManualTrigger();
        }
    }

    /// <summary>
    /// Manually triggers the speech from external code
    /// </summary>
    public void ManualTrigger()
    {
        if (!CanTrigger())
        {
            DebugLog("Cannot trigger - conditions not met");
            return;
        }

        DebugLog("Manual trigger activated");
        TriggerSpeech();
    }

    /// <summary>
    /// Sets a custom condition check delegate
    /// </summary>
    public void SetConditionCheck(ConditionCheckDelegate conditionCheck)
    {
        customConditionCheck = conditionCheck;
        DebugLog("Custom condition check delegate set");
    }

    /// <summary>
    /// Monitors the custom condition continuously
    /// </summary>
    private System.Collections.IEnumerator MonitorConditionRoutine()
    {
        DebugLog("Started monitoring custom condition");

        while (!hasTriggered || !triggerOnce)
        {
            yield return new WaitForSeconds(conditionCheckInterval);

            if (CheckCustomCondition())
            {
                DebugLog("Custom condition met - triggering speech");
                TriggerSpeech();

                if (triggerOnce)
                    break;
            }
        }
    }

    /// <summary>
    /// Checks if the custom condition is met
    /// </summary>
    private bool CheckCustomCondition()
    {
        // Check delegate first
        if (customConditionCheck != null)
        {
            return customConditionCheck.Invoke();
        }

        // Invoke Unity Event (if connected to a component that returns bool)
        onCheckCondition?.Invoke(this);

        // Default: no condition set, return false
        return false;
    }

    protected override void OnSpeechTriggered()
    {
        base.OnSpeechTriggered();

        // Invoke Unity Event
        onTriggered?.Invoke();
    }

    /// <summary>
    /// Example: Check if player has specific item
    /// </summary>
    public bool CheckPlayerHasItem(ItemData itemData)
    {
        if (PlayerInventoryManager.Instance != null && itemData != null)
        {
            return PlayerInventoryManager.Instance.HasItem(itemData);
        }
        return false;
    }

    /// <summary>
    /// Example: Check if quest is complete
    /// </summary>
    public bool CheckQuestComplete(string questID)
    {
        if (QuestManager.Instance != null && !string.IsNullOrEmpty(questID))
        {
            return QuestManager.Instance.IsQuestComplete(questID);
        }
        return false;
    }

#if UNITY_EDITOR
    [Button("Manual Trigger (Play Mode)")]
    private void EditorManualTrigger()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Manual trigger only works in Play mode");
            return;
        }

        ManualTrigger();
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // Show monitoring status
        if (handlerSpeechData != null)
        {
            string monitorStatus = monitorCondition ? "MONITORING" : "MANUAL";
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2.5f,
                $"Mode: {monitorStatus}\nInterval: {conditionCheckInterval}s"
            );
        }

        // Draw a special icon for custom triggers
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
#endif
}