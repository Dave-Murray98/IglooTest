using UnityEngine;

[CreateAssetMenu(fileName = "NewQuest", menuName = "Quests/Quest Data")]
public class QuestData : ScriptableObject
{
    public string questID;
    public string displayName;

    public string questDescription;
}