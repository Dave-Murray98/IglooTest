using UnityEngine;

public class QuestTriggerBackup : MonoBehaviour
{

    [SerializeField] private QuestTrigger questTrigger;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PilotController>() != null)
            if (questTrigger != null)
            {
                questTrigger.ForceCompleteQuest();
                this.gameObject.SetActive(false);
            }
    }
}
