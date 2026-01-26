using UnityEngine;

public class CompleteQuestInteractable : InteractableBase, IConditionalInteractable
{
    [SerializeField] protected QuestData questToComplete;

    public string GetRequirementFailureMessage()
    {
        return "";
    }

    public bool MeetsInteractionRequirements(GameObject player)
    {
        return true;
    }

    protected override bool PerformInteraction(GameObject player)
    {
        QuestManager.Instance.CompleteQuest(questToComplete.questID);

        return true;
    }
}
