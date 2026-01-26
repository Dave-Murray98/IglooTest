using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Trigger zone that completes a quest when the player enters it.
/// Unlike QuestInteractable, this works automatically without requiring player interaction.
/// 
/// How it works:
/// 1. Attach this to a GameObject with a Collider set to "Is Trigger"
/// 2. When player enters the trigger, the quest completes automatically
/// 3. Syncs with QuestManager (the source of truth) for save/load
/// 4. Can be set to trigger once or multiple times (repeatable)
/// 5. Saves hasTriggered state to prevent re-triggering after load
/// </summary>
public class QuestTrigger : MonoBehaviour, ISaveable
{
    [Header("Quest Configuration")]
    [Tooltip("The quest data that defines this objective")]
    [SerializeField] protected QuestData questData;

    [Header("Trigger Settings")]
    [Tooltip("Should this trigger only work once, or can it be triggered multiple times?")]
    [SerializeField] protected bool triggerOnce = true;


    [Header("Visual Feedback")]
    [Tooltip("Optional object to enable when quest completes")]
    [SerializeField] protected GameObject completionVisual;

    [Tooltip("Optional object to disable when quest completes")]
    [SerializeField] protected GameObject incompleteVisual;

    [Header("Audio")]
    [SerializeField] private AudioClip questCompleteSound;

    [Header("Debug")]
    [SerializeField] protected bool enableDebugLogs = false;

    // Local state - synced from QuestManager
    [ShowInInspector, ReadOnly] protected bool questComplete = false;
    [ShowInInspector, ReadOnly] protected bool hasTriggered = false;

    /// <summary>
    /// Should we apply the quest complete visual effects (true if first time triggered, false if restored from save)
    /// </summary>
    [ShowInInspector, ReadOnly] protected bool shouldApplyQuestCompleteFX = true;

    private Collider triggerCollider;

    // ISaveable implementation
    public string SaveID => $"QuestTrigger_{gameObject.name}_{transform.GetSiblingIndex()}";
    public SaveDataCategory SaveCategory => SaveDataCategory.SceneDependent;

    protected virtual void Awake()
    {
        // Get and validate collider
        triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            Debug.LogWarning($"[QuestTrigger:{gameObject.name}] Collider is not set as trigger! Setting it now.");
            triggerCollider.isTrigger = true;
        }

        // Validate quest data
        if (questData == null)
        {
            Debug.LogError($"[QuestTrigger:{gameObject.name}] No QuestData assigned!");
        }
    }

    protected virtual void Start()
    {
        // Subscribe to QuestManager events
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted += OnQuestManagerQuestCompleted;
            QuestManager.Instance.OnQuestManagerFinishedLoading += OnQuestManagerFinishedLoading;
        }

        // Sync state from QuestManager (the source of truth)
        SyncFromQuestManager();

        // Update visuals based on synced state
        RefreshVisualState();
    }

    protected virtual void OnDestroy()
    {
        // Unsubscribe from QuestManager events
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted -= OnQuestManagerQuestCompleted;
            QuestManager.Instance.OnQuestManagerFinishedLoading -= OnQuestManagerFinishedLoading;
        }
    }

    /// <summary>
    /// Called when any quest is completed in QuestManager
    /// </summary>
    protected virtual void OnQuestManagerQuestCompleted(string completedQuestID)
    {
        if (questData != null && completedQuestID == questData.questID)
        {
            DebugLog($"QuestManager reported quest completed: {completedQuestID}");
            SyncFromQuestManager();
        }
    }

    /// <summary>
    /// Called when QuestManager finishes loading save data
    /// </summary>
    protected virtual void OnQuestManagerFinishedLoading(string _)
    {
        DebugLog("QuestManager finished loading - syncing state");
        SyncFromQuestManager();
        RefreshVisualState();
    }

    /// <summary>
    /// Syncs local state from QuestManager (the source of truth)
    /// </summary>
    protected virtual void SyncFromQuestManager()
    {
        if (QuestManager.Instance != null && questData != null)
        {
            bool wasComplete = questComplete;
            questComplete = QuestManager.Instance.IsQuestComplete(questData.questID);


            if (questComplete != wasComplete)
            {
                DebugLog($"Quest completion state changed: {wasComplete} -> {questComplete}");
            }

            // If quest is complete and we only trigger once, mark as triggered
            if (questComplete && triggerOnce)
            {
                hasTriggered = true;
                DebugLog("Quest already complete - marking as triggered");
            }
        }
    }

    /// <summary>
    /// Called when something enters the trigger
    /// </summary>
    protected virtual void OnTriggerEnter(Collider other)
    {
        // Check if we should respond to this trigger
        if (!CanTrigger(other.gameObject))
        {
            return;
        }

        DebugLog($"Trigger entered by: {other.gameObject.name}");

        // Attempt to complete the quest
        TryCompleteQuest(other.gameObject);
    }

    /// <summary>
    /// Check if this object can trigger the quest
    /// </summary>
    protected virtual bool CanTrigger(GameObject triggeringObject)
    {
        // Check if quest data is assigned
        if (questData == null)
        {
            DebugLog("Cannot trigger - no quest data assigned");
            return false;
        }

        // Check if we've already triggered (and we only trigger once)
        if (hasTriggered && triggerOnce)
        {
            DebugLog("Cannot trigger - already triggered once");
            return false;
        }

        // Check if quest is already complete (and not repeatable)
        if (questComplete && !questData.isRepeatable)
        {
            DebugLog($"Cannot trigger - quest already complete and not repeatable");
            return false;
        }

        PlayerController playerController = triggeringObject.GetComponent<PlayerController>();
        if (playerController == null)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Attempt to complete the quest
    /// </summary>
    protected virtual void TryCompleteQuest(GameObject triggeringObject)
    {
        // Check quest requirements
        if (!questData.AreRequirementsMet(triggeringObject))
        {
            string failureMessage = questData.GetFirstFailureMessage(triggeringObject);
            DebugLog($"Requirements not met: {failureMessage}");

            // Optional: Show failure message to player
            // You could trigger a UI notification here if you have that system

            return;
        }

        // NEW: Notify interaction triggers BEFORE completing the quest
        NotifyHandlerSpeechTriggers_Interaction();

        // All checks passed - complete the quest!
        CompleteQuest();
    }

    /// <summary>
    /// Complete the quest
    /// </summary>
    protected virtual void CompleteQuest()
    {
        DebugLog($"Completing quest: {questData.questID}");

        // Mark as triggered
        hasTriggered = true;

        // Mark quest as complete locally
        questComplete = true;

        // Register with QuestManager (the source of truth)
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.CompleteQuest(questData.questID);
        }
        else
        {
            Debug.LogError("[QuestTrigger] QuestManager not found!");
        }

        // Play completion sound
        if (questCompleteSound != null)
        {
            AudioSource.PlayClipAtPoint(questCompleteSound, transform.position);
        }

        // NEW: Notify completion triggers
        NotifyHandlerSpeechTriggers_Completion();

        // Update visuals
        RefreshVisualState();

        DebugLog($"Quest completed successfully: {questData.questID}");
    }

    /// <summary>
    /// Remove quest item from player inventory if required
    /// </summary>
    protected virtual void RemoveQuestItemFromInventory()
    {
        ItemData questItem = questData.GetRequiredItem();

        if (questItem != null && PlayerInventoryManager.Instance != null)
        {
            string questItemID = PlayerInventoryManager.Instance.GetItemIDByItemData(questItem);
            if (!string.IsNullOrEmpty(questItemID))
            {
                PlayerInventoryManager.Instance.RemoveItem(questItemID);
                DebugLog($"Removed quest item from inventory: {questItem.itemName}");
            }
        }
    }

    /// <summary>
    /// Update visual feedback based on quest completion state
    /// </summary>
    protected virtual void RefreshVisualState()
    {
        DebugLog($"Refreshing visual state - Complete: {questComplete}");

        // Update completion visual
        if (completionVisual != null)
        {
            completionVisual.SetActive(questComplete);
            DebugLog($"Completion visual set to: {questComplete}");
        }

        // Update incomplete visual (show when quest not complete)
        if (incompleteVisual != null)
        {
            incompleteVisual.SetActive(!questComplete);
            DebugLog($"Incomplete visual set to: {!questComplete}");
        }
    }

    #region Handler Speech Trigger Notifications

    /// <summary>
    /// NEW: Notifies any HandlerSpeechQuestTrigger components when quest trigger is entered
    /// This happens BEFORE completion check, allowing for "on interaction" triggers
    /// </summary>
    protected virtual void NotifyHandlerSpeechTriggers_Interaction()
    {
        HandlerSpeechQuestTrigger trigger = GetHandlerSpeechQuestTriggers();

        if (trigger != null)
        {
            DebugLog("Quest trigger entered - triggering handler speech");
            trigger.OnQuestInteracted();
        }
    }

    /// <summary>
    /// NEW: Notifies HandlerSpeechQuestTrigger component when quest is completed
    /// </summary>
    protected virtual void NotifyHandlerSpeechTriggers_Completion()
    {
        HandlerSpeechQuestTrigger trigger = GetHandlerSpeechQuestTriggers();

        if (trigger != null)
        {
            DebugLog("Quest completed - triggering handler speech");
            trigger.OnQuestCompleted();
        }
    }

    /// <summary>
    /// NEW: Finds HandlerSpeechQuestTrigger component associated with this quest
    /// Searches on this object
    /// </summary>
    protected virtual HandlerSpeechQuestTrigger GetHandlerSpeechQuestTriggers()
    {
        // Use get component to find trigger on this object
        HandlerSpeechQuestTrigger trigger = GetComponent<HandlerSpeechQuestTrigger>();
        return trigger;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Manually complete this quest (for testing or special cases)
    /// </summary>
    public void ForceComplete()
    {
        if (questData == null)
        {
            Debug.LogWarning("[QuestTrigger] Cannot force complete - no quest data");
            return;
        }

        CompleteQuest();
    }

    /// <summary>
    /// Reset this quest (for repeatable quests or debugging)
    /// </summary>
    public void ResetQuest()
    {
        if (questData == null)
            return;

        questComplete = false;
        hasTriggered = false;

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.ResetQuest(questData.questID);
        }

        RefreshVisualState();
        DebugLog($"Quest reset: {questData.questID}");
    }

    /// <summary>
    /// Enable or disable the trigger
    /// </summary>
    public void SetTriggerEnabled(bool enabled)
    {
        if (triggerCollider != null)
        {
            triggerCollider.enabled = enabled;
            DebugLog($"Trigger {(enabled ? "enabled" : "disabled")}");
        }
    }

    /// <summary>
    /// Check if this quest is complete
    /// </summary>
    public bool IsQuestComplete => questComplete;

    /// <summary>
    /// Check if this trigger has been triggered
    /// </summary>
    public bool HasTriggered => hasTriggered;

    /// <summary>
    /// Get the quest ID
    /// </summary>
    public string QuestID => questData != null ? questData.questID : "";

    #endregion

    #region Editor Helpers

    private void OnDrawGizmos()
    {
        // Draw trigger bounds in scene view
        if (triggerCollider != null)
        {
            Gizmos.color = questComplete ? new Color(0, 1, 0, 0.3f) : new Color(1, 1, 0, 0.3f);

            if (triggerCollider is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
            }
            else if (triggerCollider is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
            }
        }
    }

    protected virtual void OnDrawGizmosSelected()
    {
        // Draw quest info in scene view
        if (questData != null)
        {
#if UNITY_EDITOR
            string statusText = Application.isPlaying
                ? (questComplete ? "✓ COMPLETE" : "○ INCOMPLETE")
                : "QUEST TRIGGER";

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f,
                $"{statusText}: {questData.questName}\nID: {questData.questID}\nTrigger Once: {triggerOnce}"
            );
#endif
        }
    }

    #endregion

    #region ISaveable Implementation

    public object GetDataToSave()
    {
        var saveData = new QuestTriggerSaveData
        {
            hasTriggered = this.hasTriggered,
            questComplete = this.questComplete,
            shouldApplyCompletionFX = this.shouldApplyQuestCompleteFX
        };

        DebugLog($"Saving quest trigger data - hasTriggered: {hasTriggered}, questComplete: {questComplete}");
        return saveData;
    }

    public void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (data is not QuestTriggerSaveData saveData)
        {
            DebugLog($"Invalid save data type - expected QuestTriggerSaveData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        DebugLog($"=== QUEST TRIGGER DATA RESTORATION (Context: {context}) ===");
        DebugLog($"Loading hasTriggered: {saveData.hasTriggered}, questComplete: {saveData.questComplete}");

        // Restore the hasTriggered state
        hasTriggered = saveData.hasTriggered;
        shouldApplyQuestCompleteFX = saveData.shouldApplyCompletionFX;

        // Always sync quest completion from QuestManager (source of truth)
        SyncFromQuestManager();

        DebugLog($"Quest trigger data restoration complete - hasTriggered: {hasTriggered}, questComplete: {questComplete}");
    }

    public virtual object ExtractRelevantData(object saveContainer)
    {
        if (saveContainer is QuestTriggerSaveData)
        {
            QuestTriggerSaveData saveData = new QuestTriggerSaveData(hasTriggered, questComplete, shouldApplyQuestCompleteFX);
            return saveData;
        }

        return new QuestManagerSaveData();
    }


    public void OnBeforeSave()
    {
        DebugLog($"Preparing to save quest trigger - hasTriggered: {hasTriggered}");
    }

    public virtual void OnAfterLoad()
    {
        DebugLog($"Quest trigger data loaded - hasTriggered: {hasTriggered}, questComplete: {questComplete}");

        // Refresh visuals after load
        RefreshVisualState();
    }

    #endregion

    #region Debug Helpers

    protected virtual void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[QuestTrigger:{gameObject.name}] {message}");
        }
    }


    #endregion
}

/// <summary>
/// Save data structure for quest trigger
/// </summary>
[System.Serializable]
public class QuestTriggerSaveData
{
    public QuestTriggerSaveData(bool newHasTriggered, bool newQuestComplete, bool newShouldApplyCompletionFX)
    {
        hasTriggered = newHasTriggered;
        questComplete = newQuestComplete;
        shouldApplyCompletionFX = newShouldApplyCompletionFX;
    }

    public QuestTriggerSaveData() { }

    public bool hasTriggered;
    public bool questComplete;
    public bool shouldApplyCompletionFX;
}