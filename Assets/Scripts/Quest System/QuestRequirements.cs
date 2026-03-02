using UnityEngine;

/// <summary>
/// Requires another quest to be completed first
/// </summary>
[System.Serializable]
public class OtherQuestRequirement : IQuestRequirement
{
    [SerializeField] private string requiredQuestID;

    public bool IsMet(GameObject player)
    {
        if (string.IsNullOrEmpty(requiredQuestID))
            return true; // No quest required

        if (QuestManager.Instance == null)
            return false;

        return QuestManager.Instance.IsQuestComplete(requiredQuestID);
    }

    public string GetFailureMessage()
    {
        return string.IsNullOrEmpty(requiredQuestID)
            ? ""
            : $"Complete required objective first";
    }
}

