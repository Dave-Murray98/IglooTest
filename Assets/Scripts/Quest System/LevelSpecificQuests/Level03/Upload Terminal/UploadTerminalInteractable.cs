using UnityEngine;

public class UploadTerminalInteractable : CompleteQuestInteractable
{
    [SerializeField] private UploadTerminalHitbox uploadTerminalHitbox;

    [SerializeField] protected override bool showInteractionPrompt => false;

    protected override bool PerformInteraction(GameObject player)
    {
        QuestManager.Instance.CompleteQuest(questToComplete.questID);

        if (uploadTerminalHitbox != null)
        {
            uploadTerminalHitbox.gameObject.SetActive(false);
        }

        return true;
    }
}
