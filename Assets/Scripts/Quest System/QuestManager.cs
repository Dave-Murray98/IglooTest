using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    // Fired when any quest is completed, passing its questID
    public static event Action<string> OnQuestCompleted;

    // Fired when a new chain quest unlocks, passing the quest's data
    public static event Action<QuestData> OnChainQuestStarted;

    // Fired when all quests (including all chain quests) are done
    public static event Action OnGameCompleted;

    public readonly List<string> completedQuests = new List<string>();

    [Header("Biome Quests")]
    [Tooltip("The 3 biome quests - all available from the start")]
    public List<QuestData> biomeQuests;

    [Header("Chain Quests (Sequential)")]
    [Tooltip("Unlock one-by-one after biome quests are done. Order matters! 0=Descend, 1=Escape, 2=SafeZone")]
    public List<QuestData> chainQuests;

    // Tracks which chain quest we're on. -1 means none have started yet.
    private int currentChainQuestIndex = -1;

    [Header("Speech Handler Speeches")]
    [SerializeField] private HandlerSpeechData onBiomeQuestsCompleteSpeech;
    [SerializeField] private GameObject caveEntranceSpeechTrigger;

    [Header("Cave Entrance")]
    [SerializeField] private GameObject nonDestructableBlockingObject;
    [SerializeField] private GameObject destructableBlockingObject;

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

        if (biomeQuests == null || biomeQuests.Count == 0)
            FindAllQuests();
    }

    private void Start()
    {
        HandleCaveEntrance(false);
    }

    private void FindAllQuests()
    {
        biomeQuests = new List<QuestData>();

        // Build a set of chain quest IDs so we don't accidentally count them as biome quests
        HashSet<string> chainQuestIDs = new HashSet<string>();
        if (chainQuests != null)
            foreach (QuestData cq in chainQuests)
                if (cq != null) chainQuestIDs.Add(cq.questID);

        QuestTrigger[] triggers = FindObjectsByType<QuestTrigger>(FindObjectsSortMode.None);
        foreach (QuestTrigger trigger in triggers)
        {
            if (trigger.questData != null && !chainQuestIDs.Contains(trigger.questData.questID))
            {
                DebugLog($"Found biome quest: {trigger.questData.questID}");
                biomeQuests.Add(trigger.questData);
            }
        }
    }

    /// <summary>
    /// Called by QuestTrigger when a player enters a quest zone.
    /// Works for both biome quests and chain quests.
    /// </summary>
    public void CompleteQuest(string questID)
    {
        if (completedQuests.Contains(questID))
            return;

        completedQuests.Add(questID);
        OnQuestCompleted?.Invoke(questID);
        DebugLog($"Quest completed: {questID}");

        if (IsChainQuest(questID))
        {
            // A chain quest was just finished - move to the next one (or end the game)
            HandleChainQuestCompleted();
        }
        else
        {
            // A biome quest was finished - check if all biome quests are now done
            if (AllBiomeQuestsComplete())
            {
                HandleCaveEntrance(true);
                StartNextChainQuest();

                if (onBiomeQuestsCompleteSpeech != null)
                    HandlerSpeechController.Instance.Play(onBiomeQuestsCompleteSpeech);

                if (caveEntranceSpeechTrigger != null)
                    caveEntranceSpeechTrigger.SetActive(true);
            }
            else
                HandleCaveEntrance(false);
        }
    }

    private bool IsChainQuest(string questID)
    {
        if (chainQuests == null) return false;
        foreach (QuestData cq in chainQuests)
            if (cq != null && cq.questID == questID) return true;
        return false;
    }

    private bool AllBiomeQuestsComplete()
    {
        foreach (QuestData q in biomeQuests)
            if (!completedQuests.Contains(q.questID)) return false;
        return true;
    }

    private void HandleChainQuestCompleted()
    {
        int nextIndex = currentChainQuestIndex + 1;

        if (nextIndex < chainQuests.Count)
            StartNextChainQuest();
        else
        {
            DebugLog("All quests complete! Game finished.");
            Debug.Log("[QuestManager] GAME COMPLETE!");
            OnGameCompleted?.Invoke();
        }
    }

    private void StartNextChainQuest()
    {
        currentChainQuestIndex++;

        if (currentChainQuestIndex >= chainQuests.Count)
        {
            DebugLog("No more chain quests.");
            return;
        }

        QuestData nextQuest = chainQuests[currentChainQuestIndex];
        if (nextQuest == null)
        {
            DebugLog($"Chain quest at index {currentChainQuestIndex} is null!");
            return;
        }

        DebugLog($"Starting chain quest: {nextQuest.questID}");
        OnChainQuestStarted?.Invoke(nextQuest);
    }

    private void HandleCaveEntrance(bool isCaveEntranceOpen)
    {
        if (isCaveEntranceOpen)
        {
            if (nonDestructableBlockingObject != null)
                nonDestructableBlockingObject.SetActive(false);

            if (destructableBlockingObject != null)
                destructableBlockingObject.SetActive(true);
        }
        else
        {
            if (nonDestructableBlockingObject != null)
                nonDestructableBlockingObject.SetActive(true);

            if (destructableBlockingObject != null)
                destructableBlockingObject.SetActive(false);

            if (caveEntranceSpeechTrigger != null)
                caveEntranceSpeechTrigger.SetActive(false);
        }
    }

    /// <summary>
    /// Returns the currently active chain quest, or null if none has started yet.
    /// </summary>
    public QuestData GetActiveChainQuest()
    {
        if (currentChainQuestIndex < 0 || currentChainQuestIndex >= chainQuests.Count)
            return null;
        return chainQuests[currentChainQuestIndex];
    }

    public bool IsQuestComplete(string questID)
    {
        if (string.IsNullOrEmpty(questID)) return false;
        return completedQuests.Contains(questID);
    }

    [Button]
    public void CompleteBiomeQuests()
    {
        foreach (QuestData q in biomeQuests)
            CompleteQuest(q.questID);
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[QuestManager] {message}");
    }
}