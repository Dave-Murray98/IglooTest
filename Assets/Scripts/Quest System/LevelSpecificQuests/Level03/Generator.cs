using UnityEngine;

public class Generator : MonoBehaviour
{
    public QuestData[] GeneratorPowerCellQuests;

    public QuestData GeneratorRestorePowerQuest;

    [SerializeField] private Animator animator;

    [SerializeField] private string questCompleteAnimationBool = "IsActive";

    [Header("Audio")]
    [SerializeField] private AudioClip generatorActiveLoopClip;
    [SerializeField] private AudioClip generatorStartUpClip;
    [SerializeField] private AudioSource activeLoopAudioSource;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private void Start()
    {
        QuestManager.Instance.OnQuestCompleted += SyncFromQuestManager;
        QuestManager.Instance.OnQuestManagerFinishedLoading += SyncFromQuestManager;
        SyncFromQuestManager(string.Empty);
    }

    /// <summary>
    /// Syncs local state from QuestManager (the source of truth)
    /// </summary>
    private void SyncFromQuestManager(string questID = null)
    {

        if (QuestManager.Instance != null && GeneratorRestorePowerQuest != null)
        {
            bool questComplete = QuestManager.Instance.IsQuestComplete(GeneratorRestorePowerQuest.questID);
            bool wasComplete = questComplete;

            if (questComplete != wasComplete)
            {
                DebugLog($"Quest completion state changed: {wasComplete} -> {questComplete}");

                if (wasComplete == false && questComplete == true)
                {
                    CompleteGeneratorQuest();
                }
                else if (wasComplete == true && questComplete == false)
                {
                    StopActiveLoopClip();
                }
            }
            else
            {
                if (questComplete)
                {
                    return;
                }

                int questCount = 0;
                int questCompleteCount = 0;

                foreach (QuestData questData in GeneratorPowerCellQuests)
                {
                    questCount++;

                    bool powerCellQuestComplete = QuestManager.Instance.IsQuestComplete(questData.questID);
                    if (powerCellQuestComplete)
                    {
                        questCompleteCount++;
                    }
                }

                if (questCompleteCount >= questCount)
                {
                    CompleteGeneratorQuest();
                }
                else
                {
                    animator.SetBool(questCompleteAnimationBool, false);
                }
            }
        }
    }

    private void CompleteGeneratorQuest()
    {
        if (GeneratorRestorePowerQuest != null)
        {
            QuestManager.Instance.CompleteQuest(GeneratorRestorePowerQuest.questID);
        }

        PlayStartUpClip();
        PlayActiveLoopClip();
        animator.SetBool(questCompleteAnimationBool, true);
    }

    private void PlayStartUpClip()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound(generatorStartUpClip, transform.position, AudioCategory.PlayerSFX);
        }
    }

    private void PlayActiveLoopClip()
    {
        if (activeLoopAudioSource != null)
        {
            activeLoopAudioSource.clip = generatorActiveLoopClip;
            activeLoopAudioSource.Play();
        }
    }

    private void StopActiveLoopClip()
    {
        if (activeLoopAudioSource != null)
        {
            activeLoopAudioSource.Stop();
        }
    }

    private void OnDestroy()
    {
        QuestManager.Instance.OnQuestCompleted -= SyncFromQuestManager;
        QuestManager.Instance.OnQuestManagerFinishedLoading -= SyncFromQuestManager;
    }


    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log("[Generator]" + message);
        }
    }

}