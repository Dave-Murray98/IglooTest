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
        QuestManager.OnQuestCompleted += UpdateBlackboard;

        // initialize the blackboard text with all incomplete quests
        blackboardText.text = GetBlackboardText();
    }

    private void UpdateBlackboard(string questID)
    {
        blackboardText.text = GetBlackboardText();
    }

    // we take all the quests in the quest manager, and if they are not completed,
    // we add their description to the blackboard text, separated by by two newlines and with a numbered list
    private string GetBlackboardText()
    {
        string text = "";
        int questNumber = 1;

        foreach (QuestData quest in QuestManager.Instance.allQuests)
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