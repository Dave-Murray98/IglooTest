using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Controls access to level exit doorways based on quest completion.
/// UPDATED: Now properly integrates with save/load system to restore visual state.
/// Wraps around your existing Doorway component to add quest-based gating.
/// Attach this to the same GameObject as your Doorway component.
/// </summary>
[RequireComponent(typeof(DoorwayToOtherLevel))]
public class QuestLockedDoor : MonoBehaviour
{
    [Header("Required Quests")]
    [Tooltip("All of these quests must be completed before the exit unlocks")]
    [SerializeField] private QuestData[] requiredQuests = new QuestData[0];

    [Header("Doorway Control")]
    [Tooltip("Should the doorway require interaction, or auto-transition when unlocked?")]
    [SerializeField] private bool requireInteractionWhenUnlocked = true;

    [Header("Feedback")]
    [Tooltip("Message to show when player tries to use locked exit")]
    [SerializeField] private string lockedMessage = "Complete all objectives to proceed";

    [Tooltip("Message to show when exit unlocks")]
    [SerializeField] private string unlockedMessage = "Exit unlocked!";

    [Header("Visual Feedback")]
    [Tooltip("Optional visual to show when exit is locked")]
    [SerializeField] private GameObject lockedVisual;

    [Tooltip("Optional visual to show when exit is unlocked")]
    [SerializeField] private GameObject unlockedVisual;

    [Header("Audio")]
    [SerializeField] private AudioClip unlockSound;
    [SerializeField] private AudioClip lockedSound;

    [Header(" Handler Speech References")]
    [SerializeField] private HandlerSpeechData questsNotCompleteSpeech;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Components
    private DoorwayToOtherLevel doorway;
    private DoorToOtherLevelInteractable doorInteractable;

    // State
    private bool isUnlocked = false;
    private bool hasShownUnlockMessage = false;
    private bool hasSubscribedToQuestManager = false;


    private void Awake()
    {
        doorway = GetComponent<DoorwayToOtherLevel>();
        doorInteractable = GetComponent<DoorToOtherLevelInteractable>();

        if (doorway == null)
        {
            Debug.LogError("[LevelExitController] No Doorway component found!");
        }
    }

    private void Start()
    {
        // Subscribe to quest completion events
        SubscribeToQuestManager();

        // Check initial unlock state (syncs with QuestManager)
        CheckUnlockState();
    }

    private void OnEnable()
    {
        // Re-subscribe when enabled (in case we were disabled during scene transitions)
        SubscribeToQuestManager();
    }

    private void OnDisable()
    {
        UnsubscribeFromQuestManager();
    }

    private void OnDestroy()
    {
        UnsubscribeFromQuestManager();
    }

    /// <summary>
    /// Subscribe to QuestManager events
    /// </summary>
    private void SubscribeToQuestManager()
    {
        if (hasSubscribedToQuestManager)
            return;

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted -= OnQuestCompleted;
            QuestManager.Instance.OnQuestCompleted += OnQuestCompleted;
            hasSubscribedToQuestManager = true;
            DebugLog("Subscribed to QuestManager events");
        }
        else
        {
            DebugLog("QuestManager not found - will retry");
            // Try again next frame
            Invoke(nameof(SubscribeToQuestManager), 0.1f);
        }
    }

    /// <summary>
    /// Unsubscribe from QuestManager events
    /// </summary>
    private void UnsubscribeFromQuestManager()
    {
        if (!hasSubscribedToQuestManager)
            return;

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted -= OnQuestCompleted;
            DebugLog("Unsubscribed from QuestManager events");
        }
        hasSubscribedToQuestManager = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        // If unlocked and doesn't require interaction, auto-transition
        if (isUnlocked && !requireInteractionWhenUnlocked && doorway != null)
        {
            DebugLog("Auto-transitioning through unlocked exit");
            doorway.UseDoorway();
        }
        // If locked, show feedback
        else if (!isUnlocked)
        {
            ShowLockedFeedback();
        }
    }

    /// <summary>
    /// Called when any quest is completed - check if we should unlock
    /// </summary>
    private void OnQuestCompleted(string questID)
    {
        DebugLog($"Quest completed: {questID} - checking unlock state");
        CheckUnlockState();
    }

    /// <summary>
    /// Check if all required quests are complete and update unlock state.
    /// UPDATED: Now called after save/load to sync with QuestManager.
    /// </summary>
    public void CheckUnlockState()
    {
        bool wasUnlocked = isUnlocked;
        isUnlocked = AreAllQuestsComplete();

        DebugLog($"Unlock state checked - Unlocked: {isUnlocked} (was: {wasUnlocked})");

        // If just unlocked, trigger unlock effects
        if (isUnlocked && !wasUnlocked)
        {
            OnExitUnlocked();
        }
        // If state changed from unlocked to locked (shouldn't happen normally)
        else if (!isUnlocked && wasUnlocked)
        {
            DebugLog("Exit was unlocked but is now locked - refreshing visuals");
        }

        // Always update doorway and visuals to ensure consistency
        UpdateDoorwayState();
        UpdateVisuals();
    }

    /// <summary>
    /// Check if all required quests are complete by querying QuestManager
    /// </summary>
    private bool AreAllQuestsComplete()
    {
        if (requiredQuests == null || requiredQuests.Length == 0)
        {
            DebugLog("No required quests - exit is always unlocked");
            return true; // No quests required = always unlocked
        }

        if (QuestManager.Instance == null)
        {
            DebugLog("QuestManager not found - exit remains locked");
            return false;
        }

        // Check each required quest
        foreach (var quest in requiredQuests)
        {
            if (quest == null)
                continue;

            if (!QuestManager.Instance.IsQuestComplete(quest.questID))
            {
                DebugLog($"Quest not complete: {quest.questID} ({quest.questName})");
                return false;
            }
            else
            {
                DebugLog($"Quest complete: {quest.questID} ({quest.questName})");
            }
        }

        DebugLog("All required quests complete!");
        return true;
    }

    /// <summary>
    /// Called when the exit unlocks
    /// </summary>
    private void OnExitUnlocked()
    {
        DebugLog("Exit unlocked!");

        // Play unlock sound
        if (unlockSound != null)
        {
            AudioSource.PlayClipAtPoint(unlockSound, transform.position);
        }

        // Show unlock message (only once)
        if (!hasShownUnlockMessage && !string.IsNullOrEmpty(unlockedMessage))
        {
            Debug.Log($"[EXIT] {unlockedMessage}");
            hasShownUnlockMessage = true;

            // TODO: If you have a UI notification system, trigger it here
            // Example: UINotificationManager.Instance?.ShowNotification(unlockedMessage);
        }
    }

    /// <summary>
    /// Update the doorway component based on unlock state
    /// </summary>
    private void UpdateDoorwayState()
    {
        if (doorway == null)
            return;

        // Update whether the doorway requires interaction
        doorway.requiresInteraction = requireInteractionWhenUnlocked || !isUnlocked;

        // If we have a DoorInteractable, update it too
        if (doorInteractable != null)
        {
            doorInteractable.SetInteractable(isUnlocked);
        }

        DebugLog($"Doorway state updated - Requires Interaction: {doorway.requiresInteraction}, Can Interact: {isUnlocked}");
    }

    /// <summary>
    /// Show feedback when player tries to use locked exit
    /// </summary>
    private void ShowLockedFeedback()
    {
        DebugLog($"Player attempted locked exit: {lockedMessage}");

        // Play locked sound
        if (lockedSound != null)
        {
            AudioSource.PlayClipAtPoint(lockedSound, transform.position);
        }

        if (questsNotCompleteSpeech != null)
            HandlerSpeechManager.Instance.PlaySpeech(questsNotCompleteSpeech);

        // Show locked message
        if (!string.IsNullOrEmpty(lockedMessage))
        {
            Debug.Log($"[EXIT] {lockedMessage}");

            // TODO: If you have a UI notification system, trigger it here
            // Example: UINotificationManager.Instance?.ShowNotification(lockedMessage);
        }
    }

    /// <summary>
    /// Update visual feedback based on unlock state.
    /// UPDATED: Now properly shows/hides visuals based on current state.
    /// </summary>
    private void UpdateVisuals()
    {
        DebugLog($"Updating visuals - isUnlocked: {isUnlocked}");

        // Update locked visual
        if (lockedVisual != null)
        {
            lockedVisual.SetActive(!isUnlocked);
            DebugLog($"Locked visual set to: {!isUnlocked}");
        }

        // Update unlocked visual
        if (unlockedVisual != null)
        {
            unlockedVisual.SetActive(isUnlocked);
            DebugLog($"Unlocked visual set to: {isUnlocked}");
        }
    }

    #region Public Methods

    /// <summary>
    /// Check if the exit is currently unlocked
    /// </summary>
    public bool IsUnlocked => isUnlocked;

    /// <summary>
    /// Get list of incomplete quest names (for UI display)
    /// </summary>
    public string[] GetIncompleteQuestNames()
    {
        if (requiredQuests == null || QuestManager.Instance == null)
            return new string[0];

        var incompleteNames = new System.Collections.Generic.List<string>();

        foreach (var quest in requiredQuests)
        {
            if (quest == null)
                continue;

            if (!QuestManager.Instance.IsQuestComplete(quest.questID))
            {
                incompleteNames.Add(quest.questName);
            }
        }

        return incompleteNames.ToArray();
    }

    /// <summary>
    /// Get completion progress (e.g., "2/3 objectives complete")
    /// </summary>
    public string GetProgressString()
    {
        if (requiredQuests == null || requiredQuests.Length == 0)
            return "No objectives required";

        if (QuestManager.Instance == null)
            return "0/0";

        int completed = 0;
        int total = 0;

        foreach (var quest in requiredQuests)
        {
            if (quest == null)
                continue;

            total++;
            if (QuestManager.Instance.IsQuestComplete(quest.questID))
                completed++;
        }

        return $"{completed}/{total} objectives complete";
    }

    /// <summary>
    /// Manually unlock the exit (for debugging or special cases)
    /// </summary>
    public void ForceUnlock()
    {
        DebugLog("Exit force unlocked");
        isUnlocked = true;
        UpdateDoorwayState();
        UpdateVisuals();
        OnExitUnlocked();
    }

    /// <summary>
    /// Manually lock the exit (for debugging or special cases)
    /// </summary>
    public void ForceLock()
    {
        DebugLog("Exit force locked");
        isUnlocked = false;
        hasShownUnlockMessage = false;
        UpdateDoorwayState();
        UpdateVisuals();
    }

    /// <summary>
    /// Call this after save/load operations to refresh state from QuestManager.
    /// This ensures visuals are correct after loading a save file.
    /// </summary>
    public void RefreshFromQuestManager()
    {
        DebugLog("Refreshing from QuestManager after save/load");
        CheckUnlockState();
    }

    #endregion

    #region Editor Helpers

    [Button("Check Unlock State")]
    private void EditorCheckUnlockState()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Can only check unlock state during play mode");
            return;
        }

        CheckUnlockState();
        Debug.Log($"[LevelExitController] Unlock State: {isUnlocked}");
        Debug.Log($"[LevelExitController] Progress: {GetProgressString()}");

        if (!isUnlocked)
        {
            var incompleteQuests = GetIncompleteQuestNames();
            Debug.Log($"[LevelExitController] Incomplete quests: {string.Join(", ", incompleteQuests)}");
        }
    }

    [Button("Force Unlock")]
    private void EditorForceUnlock()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Can only unlock during play mode");
            return;
        }

        ForceUnlock();
    }

    [Button("Force Lock")]
    private void EditorForceLock()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Can only lock during play mode");
            return;
        }

        ForceLock();
    }

    [Button("Refresh From Quest Manager")]
    private void EditorRefreshFromQuestManager()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Can only refresh during play mode");
            return;
        }

        RefreshFromQuestManager();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[LevelExitController:{gameObject.name}] {message}");
        }
    }

    private void OnDrawGizmos()
    {
        // Visual representation in scene view
        Gizmos.color = isUnlocked ? Color.green : Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(2.5f, 3.5f, 0.5f));

        // Show lock icon
        if (!isUnlocked)
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 3f,
                "🔒 EXIT LOCKED"
            );
#endif
        }
        else
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 3f,
                "🔓 EXIT UNLOCKED"
            );
#endif
        }
    }

    #endregion
}