using Unity.VisualScripting;
using UnityEngine;

public class OnQuestCompletedHandlerSpeechCompleted : QuestTrigger
{
    [SerializeField] protected HandlerSpeechData handlerSpeechData;

    protected override void Start()
    {
        base.Start();

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted += SyncFromQuestManagerWithQuestID;
        }
    }

    /// <summary>
    /// Syncs local state from QuestManager (the source of truth)
    /// </summary>
    protected virtual void SyncFromQuestManagerWithQuestID(string completedQuestID)
    {
        if (QuestManager.Instance != null && questData != null)
        {
            bool wasComplete = questComplete;
            questComplete = QuestManager.Instance.IsQuestComplete(questData.questID);


            if (questComplete != wasComplete)
            {
                DebugLog($"Quest completion state changed: {wasComplete} -> {questComplete}");

                if (wasComplete == false && questComplete == true)
                {
                    DebugLog("Quest completed - triggering speech");
                    TriggerSpeech();
                }
                else
                {
                    DebugLog($"not triggering speech because wasComplete={wasComplete} and questComplete={questComplete}");
                }
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
    /// Called when any quest is completed in QuestManager
    /// </summary>
    protected override void OnQuestManagerQuestCompleted(string completedQuestID)
    {
        if (questData != null && completedQuestID == questData.questID)
        {
            DebugLog($"QuestManager reported quest completed: {completedQuestID}");
            SyncFromQuestManagerWithQuestID(completedQuestID);
        }
    }

    protected virtual void TriggerSpeech()
    {
        DebugLog("Triggering speech");

        if (HandlerSpeechManager.Instance != null && handlerSpeechData != null)
        {
            HandlerSpeechManager.Instance.PlaySpeech(handlerSpeechData);
        }
    }

    protected override void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[OnQuestCompletedHandlerSpeechCompleted] {message}");
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted -= SyncFromQuestManagerWithQuestID;
        }
    }
}
