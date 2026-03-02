using TMPro;
using UnityEngine;

public class QuestBlackboard : MonoBehaviour
{
    public static QuestBlackboard Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI blackboardText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        // Listen for any quest completing (biome or chain)
        QuestManager.OnQuestCompleted += OnQuestCompleted;

        // Listen for a new chain quest unlocking
        QuestManager.OnChainQuestStarted += OnChainQuestStarted;

        // Show all biome quests at the start
        blackboardText.text = GetBiomeQuestText();
    }

    private void OnDestroy()
    {
        QuestManager.OnQuestCompleted -= OnQuestCompleted;
        QuestManager.OnChainQuestStarted -= OnChainQuestStarted;
    }

    private void OnQuestCompleted(string questID)
    {
        // If we're still in the biome quest phase, refresh the biome list
        // (the completed quest will be missing from the new text, so it disappears)
        QuestData activeChain = QuestManager.Instance.GetActiveChainQuest();
        if (activeChain == null)
        {
            // No chain quest has started yet - we're still on biome quests
            blackboardText.text = GetBiomeQuestText();
        }
        // If a chain quest IS active, the board was already updated by OnChainQuestStarted
        // and completing it will be handled by the next OnChainQuestStarted call
    }

    private void OnChainQuestStarted(QuestData chainQuest)
    {
        // Replace the blackboard with just this one new quest
        blackboardText.text = $"1. {chainQuest.questDescription}";
    }

    private string GetBiomeQuestText()
    {
        string text = "";
        int questNumber = 1;

        foreach (QuestData quest in QuestManager.Instance.biomeQuests)
        {
            if (!QuestManager.Instance.completedQuests.Contains(quest.questID))
            {
                text += $"{questNumber}. {quest.questDescription}\n\n";
                questNumber++;
            }
        }

        return text;
    }
}