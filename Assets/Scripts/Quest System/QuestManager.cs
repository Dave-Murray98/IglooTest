using System;
using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    public static event Action<string> OnQuestCompleted;

    public static event Action OnFinalQuestStarted;

    public readonly List<string> completedQuests = new List<string>();

    [Header("Quests")]
    public List<QuestData> allQuests;

    public QuestData finalQuest;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (allQuests == null || allQuests.Count == 0)
            FindAllQuests();
    }

    private void FindAllQuests()
    {
        QuestTrigger[] triggers = FindObjectsByType<QuestTrigger>(FindObjectsSortMode.None);
        foreach (QuestTrigger trigger in triggers)
        {
            if (trigger.questData != null)
            {
                if (trigger.questData.questID != finalQuest?.questID) // make sure we don't add the final quest to the list of all quests
                {
                    DebugLog($"Found quest: {trigger.questData.questID}");
                    allQuests.Add(trigger.questData);
                }
            }
        }
    }

    public void CompleteQuest(string questID)
    {
        if (completedQuests.Contains(questID))
            return;

        completedQuests.Add(questID);
        OnQuestCompleted?.Invoke(questID);

        if (completedQuests.Count == allQuests.Count)
        {
            StartFinalQuest();
        }

        DebugLog($"Quest completed: {questID}");
    }

    private void StartFinalQuest()
    {
        if (finalQuest == null)
        {
            DebugLog("No final quest set.");
            return;
        }

        DebugLog($"Starting final quest: {finalQuest.questID}");

        OnFinalQuestStarted?.Invoke();
        // You can implement logic here to activate the final quest in the game world
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[QuestManager] {message}");
    }

}