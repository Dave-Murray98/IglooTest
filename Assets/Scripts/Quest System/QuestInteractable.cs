using UnityEngine;
using Sirenix.OdinInspector;
using Unity.VisualScripting;

/// <summary>
/// Interactable object that completes a quest when activated.
/// IMPROVED: Now notifies HandlerSpeechQuestTrigger components via direct event calls.
/// 
/// How it works:
/// 1. QuestManager stores the "source of truth" for quest completion (player-dependent)
/// 2. QuestInteractable restores its state FROM QuestManager on load
/// 3. Visual state is refreshed AFTER QuestManager data is loaded
/// 4. HandlerSpeechTriggers are notified via direct method calls (event-driven)
/// </summary>
public class QuestInteractable : InteractableBase, IConditionalInteractable
{
    [Header("Quest Configuration")]
    [Tooltip("The quest data that defines this objective")]
    [SerializeField] private QuestData questData;

    [Header("Visual Feedback")]
    [Tooltip("Optional object to enable/disable when quest completes")]
    [SerializeField] private GameObject completionVisual;

    [SerializeField] private DoorHandler doorHandler;

    [Tooltip("Optional object to show when quest isn't complete")]
    [SerializeField] private GameObject incompleteVisual;

    [Header("Audio")]
    [SerializeField] private AudioClip questCompleteSound;

    // Local state - this gets synchronized from QuestManager
    [ShowInInspector] private bool questComplete = false;

    protected override void Awake()
    {
        base.Awake();

        if (doorHandler == null)
        {
            doorHandler = GetComponentInChildren<DoorHandler>();
        }

        // Validate quest data
        if (questData == null)
        {
            Debug.LogError($"[QuestInteractable:{gameObject.name}] No QuestData assigned!");
        }
    }

    protected override void Start()
    {
        base.Start();

        // IMPORTANT: Sync state from QuestManager as source of truth
        SyncFromQuestManager();

        // Update visuals based on synced state
        RefreshVisualState();
    }

    /// <summary>
    /// Syncs local state from QuestManager (the source of truth)
    /// </summary>
    private void SyncFromQuestManager()
    {
        if (QuestManager.Instance != null && questData != null)
        {
            questComplete = QuestManager.Instance.IsQuestComplete(questData.questID);

            if (questComplete && !questData.isRepeatable)
            {
                DebugLog($"Quest already complete in QuestManager: {questData.questID}");
                canInteract = false;
                hasBeenUsed = true;
            }
            else
            {
                canInteract = true;
                hasBeenUsed = false;
            }
        }
    }

    #region IInteractable Implementation

    public override bool CanInteract
    {
        get
        {
            if (!base.CanInteract)
                return false;

            // Can't interact if quest is already complete (unless repeatable)
            if (questComplete && questData != null && !questData.isRepeatable)
                return false;

            return true;
        }
    }

    public override string GetInteractionPrompt()
    {
        if (!CanInteract)
            return "";

        if (questData == null)
            return interactionPrompt;

        // Check if requirements are met
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null && !MeetsInteractionRequirements(player.gameObject))
        {
            return GetRequirementFailureMessage();
        }

        // Show quest-specific prompt if available, otherwise use default
        return string.IsNullOrEmpty(questData.questName)
            ? interactionPrompt
            : questData.questName;
    }

    protected override bool PerformInteraction(GameObject player)
    {
        // Validate quest data
        if (questData == null)
        {
            DebugLog("Cannot interact - no quest data assigned");
            return false;
        }

        // Check if already complete (and not repeatable)
        if (questComplete && !questData.isRepeatable)
        {
            DebugLog($"Quest already complete: {questData.questID}");
            return false;
        }

        // Check requirements
        if (!MeetsInteractionRequirements(player))
        {
            DebugLog($"Requirements not met: {GetRequirementFailureMessage()}");
            return false;
        }

        // NEW: Notify interaction triggers BEFORE checking completion
        NotifyHandlerSpeechTriggers_Interaction();

        // Complete the quest
        CompleteQuest();
        return true;
    }

    #endregion

    #region IConditionalInteractable Implementation

    public bool MeetsInteractionRequirements(GameObject player)
    {
        if (questData == null)
            return false;

        return questData.AreRequirementsMet(player);
    }

    public string GetRequirementFailureMessage()
    {
        if (questData == null)
            return "Quest not configured";

        var player = FindFirstObjectByType<PlayerController>();
        if (player == null)
            return "Requirements not met";

        string message = questData.GetFirstFailureMessage(player.gameObject);
        return string.IsNullOrEmpty(message) ? "Requirements not met" : message;
    }

    #endregion

    #region Quest Completion

    private void CompleteQuest()
    {
        DebugLog($"Completing quest: {questData.questID}");

        // Mark quest as complete locally
        questComplete = true;

        // Register with quest manager (the source of truth)
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.CompleteQuest(questData.questID);
        }
        else
        {
            Debug.LogError("[QuestInteractable] QuestManager not found!");
        }

        // Play completion sound
        if (questCompleteSound != null)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound2D(questCompleteSound, AudioCategory.PlayerSFX);
            }
        }

        // NEW: Notify completion triggers
        NotifyHandlerSpeechTriggers_Completion();

        // Disable interaction if not repeatable
        if (!questData.isRepeatable)
        {
            canInteract = false;
            DebugLog($"Quest {questData.questID} is not repeatable - interaction disabled");
        }

        // Update visuals
        RefreshVisualState();

        //if the quest requires an item (it'll be a key item) remove that item from the player's inventory
        ItemData questItem = questData.GetRequiredItem();

        if (questItem != null)
        {
            string questItemID = PlayerInventoryManager.Instance.GetItemIDByItemData(questItem);
            if (questItemID != string.Empty)
            {
                PlayerInventoryManager.Instance.RemoveItem(questItemID);

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySound2D(questItem.KeyItemData.useCompleteSound, AudioCategory.PlayerSFX);
                }
            }
        }

        DebugLog($"Quest completed successfully: {questData.questID}");
    }

    #endregion

    #region Handler Speech Trigger Notifications

    /// <summary>
    /// NEW: Notifies any HandlerSpeechQuestTrigger components when quest is interacted with
    /// This happens BEFORE completion check, allowing for "on interaction" triggers
    /// </summary>
    private void NotifyHandlerSpeechTriggers_Interaction()
    {
        HandlerSpeechQuestTrigger trigger = GetHandlerSpeechQuestTriggers();

        if (trigger != null)
        {
            DebugLog("Quest interacted - triggering handler speech");
            trigger.OnQuestInteracted();
        }
    }

    /// <summary>
    /// NEW: Notifies HandlerSpeechQuestTrigger component when quest is completed
    /// </summary>
    private void NotifyHandlerSpeechTriggers_Completion()
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
    private HandlerSpeechQuestTrigger GetHandlerSpeechQuestTriggers()
    {
        // Use get component to find trigger on this object
        HandlerSpeechQuestTrigger trigger = GetComponent<HandlerSpeechQuestTrigger>();
        return trigger;
    }

    #endregion

    #region Save/Load Implementation

    protected override object GetCustomSaveData()
    {
        // We don't actually need to save completion state here anymore!
        // QuestManager is the source of truth for quest completion.
        // We only save if this specific interactable should be disabled independently.

        return new QuestInteractableSaveData
        {
            questComplete = this.questComplete,
            wasManuallyDisabled = !canInteract && !questComplete // Track if disabled for other reasons
        };
    }

    protected override void LoadCustomSaveData(object customData)
    {
        if (customData is QuestInteractableSaveData saveData)
        {
            DebugLog($"Loading quest interactable save data - Stored completion: {saveData.questComplete}");

            // CRITICAL FIX: Don't use the saved completion state directly!
            // Instead, sync from QuestManager which is the source of truth
            SyncFromQuestManager();

            // Only apply manual disable state
            if (saveData.wasManuallyDisabled)
            {
                canInteract = false;
            }

            DebugLog($"Loaded quest state - Final completion: {questComplete}, CanInteract: {canInteract}");
        }
    }

    /// <summary>
    /// Called after all save data is loaded - perfect time to refresh visuals
    /// </summary>
    public override void OnAfterLoad()
    {
        base.OnAfterLoad();

        // CRITICAL: Re-sync from QuestManager to ensure we have the correct state
        SyncFromQuestManager();

        // Now refresh visuals with the correct state
        RefreshVisualState();

        DebugLog($"OnAfterLoad complete - Quest: {questData?.questID}, Complete: {questComplete}, CanInteract: {canInteract}");
    }

    protected override void RefreshVisualState()
    {
        DebugLog($"Refreshing visual state - Complete: {questComplete}");

        // Update completion visual
        if (completionVisual != null)
        {
            completionVisual.SetActive(questComplete);
            DebugLog($"Completion visual set to: {questComplete}");
        }

        if (doorHandler != null)
        {
            doorHandler.SetDoorState(questComplete);
            DebugLog($"Door state set to open: {questComplete}");
        }

        // Update incomplete visual (show when quest not complete)
        if (incompleteVisual != null)
        {
            incompleteVisual.SetActive(!questComplete);
            DebugLog($"Incomplete visual set to: {!questComplete}");
        }
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
            Debug.LogWarning("[QuestInteractable] Cannot force complete - no quest data");
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
        canInteract = true;

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.ResetQuest(questData.questID);
        }

        RefreshVisualState();
        DebugLog($"Quest reset: {questData.questID}");
    }

    /// <summary>
    /// Check if this quest is complete
    /// </summary>
    public bool IsQuestComplete => questComplete;

    /// <summary>
    /// Get the quest ID
    /// </summary>
    public string QuestID => questData != null ? questData.questID : "";

    #endregion

    #region Editor Helpers

    [Button("Force Complete Quest")]
    private void EditorForceComplete()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Can only complete quests during play mode");
            return;
        }

        ForceComplete();
    }

    [Button("Reset Quest")]
    private void EditorResetQuest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Can only reset quests during play mode");
            return;
        }

        ResetQuest();
    }

    [Button("Sync From Quest Manager")]
    private void EditorSyncFromQuestManager()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Can only sync during play mode");
            return;
        }

        SyncFromQuestManager();
        RefreshVisualState();
        Debug.Log($"Synced from QuestManager - Complete: {questComplete}, CanInteract: {canInteract}");
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // Draw quest info in scene view
        if (questData != null)
        {
#if UNITY_EDITOR
            string statusText = Application.isPlaying
                ? (questComplete ? "✓ COMPLETE" : "○ INCOMPLETE")
                : "QUEST";

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f,
                $"{statusText}: {questData.questName}\nID: {questData.questID}"
            );
#endif
        }
    }

    #endregion
}

/// <summary>
/// Save data for quest interactable
/// UPDATED: Now only saves interactable-specific state, not quest completion
/// </summary>
[System.Serializable]
public class QuestInteractableSaveData
{
    public bool questComplete; // Kept for backwards compatibility but not the source of truth
    public bool wasManuallyDisabled; // Track if disabled for reasons other than quest completion
}