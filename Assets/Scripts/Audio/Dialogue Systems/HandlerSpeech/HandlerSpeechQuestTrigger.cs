using UnityEngine;

/// <summary>
/// IMPROVED: Triggers handler speech when a specific quest is interacted with or triggered.
/// Now works with BOTH QuestInteractable and QuestTrigger components.
/// Uses direct event-driven approach instead of inefficient polling.
/// </summary>
public class HandlerSpeechQuestTrigger : HandlerSpeechTriggerBase
{

    [Header("Quest Component (Auto-Find)")]
    [Tooltip("Will automatically find QuestInteractable or QuestTrigger on this object")]
    [SerializeField] private bool autoFindQuestComponent = true;

    [Header("Manual Assignment (Optional)")]
    [Tooltip("Manually assign a QuestInteractable if not using auto-find")]
    [SerializeField] private QuestInteractable questInteractable;

    [Tooltip("Manually assign a QuestTrigger if not using auto-find")]
    [SerializeField] private QuestTrigger questTrigger;

    [Header("Trigger Settings")]
    [Tooltip("When should the speech trigger?")]
    [SerializeField] private QuestTriggerTiming triggerTiming = QuestTriggerTiming.OnQuestComplete;

    public enum QuestTriggerTiming
    {
        OnQuestComplete,        // Trigger when quest completes
        OnQuestInteraction,     // Trigger when player interacts/enters trigger (before completion check)
        OnQuestStart            // Trigger when quest becomes available/starts
    }

    // Track which component type we're using
    private enum QuestComponentType
    {
        None,
        Interactable,
        Trigger
    }
    private QuestComponentType componentType = QuestComponentType.None;

    protected override void Awake()
    {
        base.Awake();

        // Auto-find quest components if needed
        if (autoFindQuestComponent)
        {
            FindQuestComponents();
        }

        // Determine which component type we have
        DetermineComponentType();

        // Validate setup
        if (componentType == QuestComponentType.None)
        {
            DebugLog($"[HandlerSpeechQuestTrigger:{gameObject.name}] No QuestInteractable or QuestTrigger found! " +
                          "Either enable auto-find or manually assign a quest component.");
        }
    }

    /// <summary>
    /// Auto-find quest components on this GameObject
    /// </summary>
    private void FindQuestComponents()
    {
        // Try to find QuestInteractable first
        if (questInteractable == null)
        {
            questInteractable = GetComponent<QuestInteractable>();
            if (questInteractable == null)
            {
                questInteractable = GetComponentInChildren<QuestInteractable>();
            }
        }

        // Try to find QuestTrigger
        if (questTrigger == null)
        {
            questTrigger = GetComponent<QuestTrigger>();
            if (questTrigger == null)
            {
                questTrigger = GetComponentInChildren<QuestTrigger>();
            }
        }
    }

    /// <summary>
    /// Determine which type of quest component we're working with
    /// </summary>
    private void DetermineComponentType()
    {
        if (questInteractable != null)
        {
            componentType = QuestComponentType.Interactable;
            DebugLog("Using QuestInteractable component");
        }
        else if (questTrigger != null)
        {
            componentType = QuestComponentType.Trigger;
            DebugLog("Using QuestTrigger component");
        }
        else
        {
            componentType = QuestComponentType.None;
        }
    }

    protected override void Start()
    {
        base.Start();

        // Subscribe to quest events based on timing
        switch (triggerTiming)
        {
            case QuestTriggerTiming.OnQuestComplete:
                DebugLog("Quest trigger configured for OnQuestComplete (event-driven)");
                // Will be called directly by quest component
                break;

            case QuestTriggerTiming.OnQuestInteraction:
                DebugLog("Quest trigger configured for OnQuestInteraction (event-driven)");
                // Will be called directly by quest component
                break;

            case QuestTriggerTiming.OnQuestStart:
                // Check if quest is already available/started
                CheckQuestStartCondition();
                break;
        }
    }

    /// <summary>
    /// Check if quest is available and trigger if needed
    /// </summary>
    private void CheckQuestStartCondition()
    {
        bool isAvailable = false;

        // Check based on component type
        switch (componentType)
        {
            case QuestComponentType.Interactable:
                isAvailable = questInteractable != null && questInteractable.CanInteract;
                break;

            case QuestComponentType.Trigger:
                isAvailable = questTrigger != null && !questTrigger.IsQuestComplete;
                break;
        }

        if (isAvailable)
        {
            DebugLog("Quest is available - triggering speech");
            TriggerSpeech();
        }
        else
        {
            // Monitor until quest becomes available (only for OnQuestStart timing)
            StartCoroutine(MonitorQuestStart());
        }
    }

    /// <summary>
    /// Monitor quest availability for OnQuestStart timing
    /// </summary>
    private System.Collections.IEnumerator MonitorQuestStart()
    {
        while (!hasTriggered || !triggerOnce)
        {
            yield return new WaitForSeconds(0.5f);

            bool isAvailable = false;

            // Check based on component type
            switch (componentType)
            {
                case QuestComponentType.Interactable:
                    if (questInteractable == null) yield break;
                    isAvailable = questInteractable.CanInteract;
                    break;

                case QuestComponentType.Trigger:
                    if (questTrigger == null) yield break;
                    isAvailable = !questTrigger.IsQuestComplete;
                    break;

                case QuestComponentType.None:
                    yield break;
            }

            if (isAvailable)
            {
                DebugLog("Quest became available - triggering speech");
                TriggerSpeech();

                if (triggerOnce)
                    break;
            }
        }
    }

    /// <summary>
    /// Called directly by QuestInteractable or QuestTrigger when quest is completed
    /// This is the event-driven approach for OnQuestComplete timing
    /// </summary>
    public void OnQuestCompleted()
    {
        if (triggerTiming != QuestTriggerTiming.OnQuestComplete)
        {
            DebugLog($"Quest completed but trigger timing is {triggerTiming} - ignoring");
            return;
        }

        if (!CanTrigger())
        {
            DebugLog("Cannot trigger - conditions not met");
            return;
        }

        DebugLog("Quest completed - triggering handler speech");
        TriggerSpeech();
    }

    /// <summary>
    /// Called directly by QuestInteractable or QuestTrigger when quest is interacted with
    /// This is the event-driven approach for OnQuestInteraction timing
    /// </summary>
    public void OnQuestInteracted()
    {
        if (triggerTiming != QuestTriggerTiming.OnQuestInteraction)
        {
            DebugLog($"Quest interacted but trigger timing is {triggerTiming} - ignoring");
            return;
        }

        if (!CanTrigger())
        {
            DebugLog("Cannot trigger - conditions not met");
            return;
        }

        DebugLog("Quest interacted - triggering handler speech");
        TriggerSpeech();
    }

    protected override void OnSpeechTriggered()
    {
        base.OnSpeechTriggered();

        string componentName = componentType == QuestComponentType.Interactable ? "QuestInteractable" :
                              componentType == QuestComponentType.Trigger ? "QuestTrigger" : "None";

        DebugLog($"Handler speech triggered for quest ({triggerTiming}) via {componentName}");
    }

    #region Public Accessors

    /// <summary>
    /// Get the quest ID from whichever component is active
    /// </summary>
    public string GetQuestID()
    {
        switch (componentType)
        {
            case QuestComponentType.Interactable:
                return questInteractable != null ? questInteractable.QuestID : "";
            case QuestComponentType.Trigger:
                return questTrigger != null ? questTrigger.QuestID : "";
            default:
                return "";
        }
    }

    /// <summary>
    /// Check if the quest is complete
    /// </summary>
    public bool IsQuestComplete()
    {
        switch (componentType)
        {
            case QuestComponentType.Interactable:
                return questInteractable != null && questInteractable.IsQuestComplete;
            case QuestComponentType.Trigger:
                return questTrigger != null && questTrigger.IsQuestComplete;
            default:
                return false;
        }
    }

    #endregion

#if UNITY_EDITOR
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // Draw connection line to quest component
        Transform questTransform = null;
        Color gizmoColor = Color.cyan;

        if (questInteractable != null)
        {
            questTransform = questInteractable.transform;
            gizmoColor = Color.green; // Green for interactable
        }
        else if (questTrigger != null)
        {
            questTransform = questTrigger.transform;
            gizmoColor = Color.yellow; // Yellow for trigger
        }

        if (questTransform != null && questTransform != transform)
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawLine(transform.position, questTransform.position);

            // Draw icon at quest location
            Gizmos.DrawWireSphere(questTransform.position, 0.5f);
        }

        // Show timing mode and component type in inspector
        if (handlerSpeechData != null)
        {
            string componentTypeStr = componentType == QuestComponentType.Interactable ? "Interactable" :
                                     componentType == QuestComponentType.Trigger ? "Trigger" : "None";

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2.5f,
                $"Type: {componentTypeStr}\nTiming: {triggerTiming}"
            );
        }
    }

    protected override void OnValidate()
    {
        // Help developer by auto-finding components in editor
        if (autoFindQuestComponent && !Application.isPlaying)
        {
            if (questInteractable == null && questTrigger == null)
            {
                questInteractable = GetComponent<QuestInteractable>();
                questTrigger = GetComponent<QuestTrigger>();
            }
        }
    }
#endif
}