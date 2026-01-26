using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// ScriptableObject defining a quest's properties and requirements.
/// Create these in the Project window to define your level objectives.
/// </summary>
[CreateAssetMenu(fileName = "New Quest", menuName = "Quest System/Quest Data")]
public class QuestData : ScriptableObject
{
    [Header("Quest Identity")]
    [Tooltip("Unique ID for this quest - used for saving and quest requirements")]
    public string questID;

    [Header("Quest Info")]
    public string questName;
    [TextArea(2, 4)]
    public string questDescription;

    [Header("Requirements")]
    [Tooltip("All requirements must be met before this quest can be completed")]
    [SerializeReference] // This allows for polymorphic serialization
    public IQuestRequirement[] requirements = new IQuestRequirement[0];

    [Header("Settings")]
    [Tooltip("Can this quest be completed multiple times?")]
    public bool isRepeatable = false;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    /// <summary>
    /// Check if all requirements for this quest are currently met
    /// </summary>
    public bool AreRequirementsMet(GameObject player)
    {
        if (requirements == null || requirements.Length == 0)
            return true; // No requirements = always available

        foreach (var requirement in requirements)
        {
            if (requirement == null)
                continue;

            if (!requirement.IsMet(player))
            {
                if (showDebugInfo)
                    Debug.Log($"[Quest:{questID}] Requirement failed: {requirement.GetFailureMessage()}");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Get the first requirement failure message (for UI feedback)
    /// </summary>
    public string GetFirstFailureMessage(GameObject player)
    {
        if (requirements == null || requirements.Length == 0)
            return "";

        foreach (var requirement in requirements)
        {
            if (requirement == null)
                continue;

            if (!requirement.IsMet(player))
                return requirement.GetFailureMessage();
        }

        return "";
    }

    private void OnValidate()
    {
        // Auto-generate quest ID if empty
        if (string.IsNullOrEmpty(questID))
        {
            questID = $"Quest_{name}";
        }

        // Ensure quest ID is valid (no spaces, special characters)
        questID = questID.Replace(" ", "_");
    }

    [Button("Test Requirements")]
    private void TestRequirements()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Can only test requirements during play mode");
            return;
        }

        var player = FindFirstObjectByType<PlayerController>();
        if (player == null)
        {
            Debug.LogWarning("No player found in scene");
            return;
        }

        bool allMet = AreRequirementsMet(player.gameObject);
        Debug.Log($"[Quest:{questID}] Requirements Met: {allMet}");

        if (!allMet)
        {
            Debug.Log($"Failure Reason: {GetFirstFailureMessage(player.gameObject)}");
        }
    }

    public ItemData GetRequiredItem()
    {
        ItemData requiredItem = null;
        foreach (var requirement in requirements)
        {
            if (requirement is ItemRequirement itemRequirement)
            {
                requiredItem = itemRequirement.requiredItem;
            }
        }
        return requiredItem;
    }
}