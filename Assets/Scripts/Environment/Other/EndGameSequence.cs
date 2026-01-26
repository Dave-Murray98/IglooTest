using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

public class EndGameSequence : MonoBehaviour
{
    [SerializeField] private GameObject ocean;

    [SerializeField] private GameObject enemies;

    [SerializeField] private GameObject fakeMonster;

    [SerializeField] private float delayBeforeLoweringOcean = 2f;
    [SerializeField] private float oceanLoweringSpeed = 2f;
    [SerializeField] private float oceanOriginalHeight = 0f;

    [SerializeField] private float oceanHeightJustBeforeLowering;

    [SerializeField] private float oceanHeightWhenToBreakEggs = -63.5F;

    [SerializeField] private float oceanLoweringTargetHeight = -100f;

    [SerializeField] private QuestData endGameQuest;

    private Egg[] eggsInScene;

    private bool hasAlreadyBeenTriggered = false;

    [SerializeField] private Rigidbody[] floatingCorpses;

    private void Start()
    {
        eggsInScene = FindObjectsByType<Egg>(FindObjectsSortMode.None);

        floatingCorpses = GetComponentsInChildren<Rigidbody>();

        ResetEndGameSequence();

        QuestManager.Instance.OnQuestCompleted += SyncFromQuestManager;
        QuestManager.Instance.OnQuestManagerFinishedLoading += SyncFromQuestManager;
        SyncFromQuestManager(string.Empty);
    }

    /// <summary>
    /// Syncs local state from QuestManager (the source of truth)
    /// </summary>
    private void SyncFromQuestManager(string questID = null)
    {
        if (QuestManager.Instance != null)
        {
            if (QuestManager.Instance.IsQuestComplete(endGameQuest.questID))
                StartEndGameSequence();
        }
    }

    public void ResetEndGameSequence()
    {
        if (ocean != null)
        {
            ocean.transform.position = new Vector3(ocean.transform.position.x, oceanOriginalHeight, ocean.transform.position.z);
        }

        if (fakeMonster != null)
        {
            fakeMonster.SetActive(false);
        }

        if (enemies != null)
        {
            enemies.SetActive(true);
        }

        if (floatingCorpses != null)
        {
            foreach (Rigidbody corpse in floatingCorpses)
            {
                corpse.useGravity = false;
            }
        }
    }

    [Button]
    public void StartEndGameSequence()
    {

        if (hasAlreadyBeenTriggered)
            return;

        QuestManager.Instance.CompleteQuest(endGameQuest.questID);

        hasAlreadyBeenTriggered = true;

        if (enemies != null)
        {
            enemies.SetActive(false);
        }

        if (fakeMonster != null)
        {
            fakeMonster.SetActive(true);
        }

        if (ocean != null)
        {
            StartCoroutine(WaitAndLowerOcean());
        }
    }

    private IEnumerator WaitAndLowerOcean()
    {
        ocean.transform.position = new Vector3(ocean.transform.position.x, oceanHeightJustBeforeLowering, ocean.transform.position.z);

        yield return new WaitForSeconds(delayBeforeLoweringOcean);

        if (floatingCorpses != null)
        {
            foreach (Rigidbody corpse in floatingCorpses)
            {
                corpse.useGravity = true;
            }
        }

        StartCoroutine(LowerOcean());
    }

    private IEnumerator LowerOcean()
    {
        bool eggsBroken = false;

        while (ocean.transform.position.y > oceanLoweringTargetHeight)
        {
            ocean.transform.position += Vector3.down * Time.deltaTime * oceanLoweringSpeed;
            yield return null;

            if (!eggsBroken)
                if (ocean.transform.position.y <= oceanHeightWhenToBreakEggs)
                {
                    foreach (Egg egg in eggsInScene)
                    {
                        egg.BreakEgg();
                    }

                    eggsBroken = true;
                }
        }
    }

    private void OnDestroy()
    {
        QuestManager.Instance.OnQuestCompleted -= SyncFromQuestManager;
        QuestManager.Instance.OnQuestManagerFinishedLoading -= SyncFromQuestManager;
    }
}
