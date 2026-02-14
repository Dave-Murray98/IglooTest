using System;
using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    public static event Action<string> OnQuestCompleted;

    private readonly HashSet<string> _completedQuests = new HashSet<string>();

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
    }

    public void CompleteQuest(string questID)
    {
        if (_completedQuests.Contains(questID))
            return;

        _completedQuests.Add(questID);
        OnQuestCompleted?.Invoke(questID);

        DebugLog($"Quest completed: {questID}");
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[QuestManager] {message}");
    }

}