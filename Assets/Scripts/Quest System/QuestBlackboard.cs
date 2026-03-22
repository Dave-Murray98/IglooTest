using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class QuestBlackboard : MonoBehaviour
{
    public static QuestBlackboard Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI blackboardText;

    [Header("Compass Settings")]
    [Tooltip("The submarine's transform - used as the 'from' point for compass directions")]
    [SerializeField] private Transform submarineTransform;

    [Tooltip("How often (in seconds) the blackboard refreshes compass directions")]
    [SerializeField] private float compassRefreshInterval = 2f;

    // A lookup table built once at startup: questID -> its QuestTrigger in the scene
    private Dictionary<string, QuestTrigger> questTriggerMap = new Dictionary<string, QuestTrigger>();

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
        BuildQuestTriggerMap();

        QuestManager.OnQuestCompleted += OnQuestCompleted;
        QuestManager.OnChainQuestStarted += OnChainQuestStarted;

        RefreshBlackboard();
        StartCoroutine(CompassRefreshRoutine());
    }

    private void OnDestroy()
    {
        QuestManager.OnQuestCompleted -= OnQuestCompleted;
        QuestManager.OnChainQuestStarted -= OnChainQuestStarted;
    }

    // -------------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scans the scene once at startup and maps every questID to its QuestTrigger.
    /// This means we never have to search the scene again during gameplay.
    /// </summary>
    private void BuildQuestTriggerMap()
    {
        questTriggerMap.Clear();

        QuestTrigger[] allTriggers = FindObjectsByType<QuestTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (QuestTrigger trigger in allTriggers)
        {
            if (trigger.questData == null) continue;

            string id = trigger.questData.questID;
            if (!questTriggerMap.ContainsKey(id))
                questTriggerMap.Add(id, trigger);
        }
    }

    // -------------------------------------------------------------------------
    // Coroutine
    // -------------------------------------------------------------------------

    /// <summary>
    /// Waits N seconds then refreshes the blackboard, forever.
    /// This keeps compass directions up to date as the submarine moves.
    /// </summary>
    private IEnumerator CompassRefreshRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(compassRefreshInterval);
            RefreshBlackboard();
        }
    }

    // -------------------------------------------------------------------------
    // Quest Events
    // -------------------------------------------------------------------------

    private void OnQuestCompleted(string questID)
    {
        QuestData activeChain = QuestManager.Instance.GetActiveChainQuest();
        if (activeChain == null)
            RefreshBlackboard();
    }

    private void OnChainQuestStarted(QuestData chainQuest)
    {
        RefreshBlackboard();
    }

    // -------------------------------------------------------------------------
    // Display
    // -------------------------------------------------------------------------

    /// <summary>
    /// Rebuilds the blackboard text immediately.
    /// Called on events and by the compass refresh coroutine.
    /// </summary>
    private void RefreshBlackboard()
    {
        QuestData activeChain = QuestManager.Instance.GetActiveChainQuest();

        if (activeChain != null)
            blackboardText.text = BuildQuestLine(1, activeChain);
        else
            blackboardText.text = BuildBiomeQuestText();
    }

    /// <summary>
    /// Builds the full text for all incomplete biome quests.
    /// </summary>
    private string BuildBiomeQuestText()
    {
        string text = "";
        int questNumber = 1;

        foreach (QuestData quest in QuestManager.Instance.biomeQuests)
        {
            if (!QuestManager.Instance.completedQuests.Contains(quest.questID))
            {
                text += BuildQuestLine(questNumber, quest) + "\n\n";
                questNumber++;
            }
        }

        return text.TrimEnd();
    }

    /// <summary>
    /// Builds a single quest line: "1. Description [NW]" or "1. Description" if no trigger.
    /// </summary>
    private string BuildQuestLine(int number, QuestData quest)
    {
        string compassSuffix = GetCompassSuffix(quest.questID);
        return $"{number}. {quest.questDescription}{compassSuffix}";
    }

    // -------------------------------------------------------------------------
    // Compass
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns a formatted compass suffix like " [NW]" if a trigger exists,
    /// or an empty string if there's no trigger for this quest.
    /// </summary>
    private string GetCompassSuffix(string questID)
    {
        if (submarineTransform == null) return "";
        if (!questTriggerMap.TryGetValue(questID, out QuestTrigger trigger)) return "";

        string direction = GetCompassDirection(submarineTransform.position, trigger.transform.position);
        return $" [{direction}]";
    }

    /// <summary>
    /// Calculates one of 8 compass directions (N, NE, E, SE, S, SW, W, NW)
    /// from a 'from' position to a 'to' position, using only X and Z (ignoring depth).
    /// </summary>
    private string GetCompassDirection(Vector3 from, Vector3 to)
    {
        // Get the flat (horizontal) direction vector, ignoring Y/depth
        Vector3 delta = to - from;
        delta.y = 0f;

        // Atan2 gives us the angle in radians - we convert to degrees
        // Atan2(x, z) gives 0 = North, 90 = East, matching world-space convention
        float angle = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;

        // Normalise to 0-360
        if (angle < 0f) angle += 360f;

        // Divide the circle into 8 equal 45-degree segments, offset by 22.5 so
        // North is centred on 0 degrees (337.5 to 22.5)
        int index = Mathf.RoundToInt(angle / 45f) % 8;

        string[] directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        return directions[index];
    }
}