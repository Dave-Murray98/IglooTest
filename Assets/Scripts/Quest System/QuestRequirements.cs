using System.Linq;
using UnityEngine;

/// <summary>
/// Collection of common quest requirement implementations.
/// Each is a simple, serializable class that can be configured in the Inspector.
/// </summary>

/// <summary>
/// Requires the player to have a specific item in their inventory
/// </summary>
[System.Serializable]
public class ItemRequirement : IQuestRequirement
{
    public ItemData requiredItem;
    [SerializeField] private int requiredQuantity = 1;

    public bool IsMet(GameObject player)
    {
        if (requiredItem == null)
            return true; // No item required

        if (PlayerInventoryManager.Instance == null)
            return false;

        return PlayerInventoryManager.Instance.HasItem(requiredItem, requiredQuantity);
    }

    public string GetFailureMessage()
    {
        if (requiredItem == null)
            return "";

        return requiredQuantity > 1
            ? $"Requires {requiredQuantity}x {requiredItem.itemName}"
            : $"Requires {requiredItem.itemName}";
    }
}

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

/// <summary>
/// Requires the player to NOT be in a specific state
/// </summary>
[System.Serializable]
public class PlayerStateRequirement : IQuestRequirement
{
    [SerializeField] private PlayerStateType forbiddenState = PlayerStateType.Vehicle;
    [SerializeField] private string failureMessage = "Cannot do this while in a vehicle";

    public bool IsMet(GameObject player)
    {
        if (PlayerStateManager.Instance == null)
            return true;

        return PlayerStateManager.Instance.CurrentStateType != forbiddenState;
    }

    public string GetFailureMessage()
    {
        return failureMessage;
    }
}


/// <summary>
/// Quest requirement that checks if enough audio logs have been destroyed.
/// Simply queries AudioLogManager which already tracks everything.
/// </summary>
[System.Serializable]
public class DestroyAudioLogQuestRequirement : IQuestRequirement
{
    [SerializeField]
    [Tooltip("Number of audio logs that must be destroyed")]
    private int requiredDestroyedCount = 6;

    public bool IsMet(GameObject player)
    {
        if (AudioLogManager.Instance == null)
            return false;

        // Count destroyed logs in current scene
        int destroyedCount = CountDestroyedInScene();
        return destroyedCount >= requiredDestroyedCount;
    }

    public string GetFailureMessage()
    {
        if (AudioLogManager.Instance == null)
            return $"Destroy {requiredDestroyedCount} audio logs";

        int current = CountDestroyedInScene();
        return $"Destroy audio logs: {current}/{requiredDestroyedCount}";
    }

    private int CountDestroyedInScene()
    {
        if (AudioLogManager.Instance == null)
            return 0;

        return AudioLogManager.Instance.DestroyedAudioLogsCount;
    }
}