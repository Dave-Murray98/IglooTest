using System.Collections;
using Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UploadTerminal : MonoBehaviour
{
    [SerializeField] private QuestData destroyUploadTerminalQuest;
    [SerializeField] private QuestData interactUploadTerminalQuest;

    [SerializeField] private UploadTerminalHitbox uploadTerminalHitbox;

    [SerializeField] private UploadTerminalInteractable uploadTerminalInteractable;

    [SerializeField] private QuestData generatorQuest;

    [SerializeField] private DoorHandler[] doors;

    [Header("Audio")]
    [SerializeField] private AudioClip destroyedUploadTerminalSound;
    [SerializeField] private AudioClip interactedUploadTerminalSound;

    [Header("End Game Transition")]
    [SerializeField] private float endGameTransitionDelay = 15f;
    [SerializeField] private bool isTransitioningToCredits = false;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;


    private void Awake()
    {
        if (doors == null || doors.Length == 0)
        {
            doors = GetComponentsInChildren<DoorHandler>();
        }
    }

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
        if (QuestManager.Instance != null)
        {
            if (QuestManager.Instance.IsQuestComplete(generatorQuest.questID))
                foreach (DoorHandler door in doors)
                    door.OpenDoor();
            else
                foreach (DoorHandler door in doors)
                    door.CloseDoor();

            if (IsUploadTerminalDestroyed())
                RestoreDestroyedUploadTerminalState();

            else if (IsUploadTerminalInteracted())
                RestoreInteractedUploadTerminalState();

            else
                ResetAll();
        }
    }

    private void RestoreDestroyedUploadTerminalState()
    {
        DebugLog("Restoring upload terminal state as DESTROYED");

        if (uploadTerminalHitbox != null)
        {
            uploadTerminalHitbox.gameObject.SetActive(true);
        }

        if (uploadTerminalInteractable != null)
        {
            uploadTerminalInteractable.gameObject.SetActive(false);
        }

        if (AudioManager.Instance != null && destroyedUploadTerminalSound != null)
            AudioManager.Instance.PlaySound(destroyedUploadTerminalSound, transform.position, AudioCategory.Ambience);

        StartCoroutine(TransitionToCredits());
    }

    private void RestoreInteractedUploadTerminalState()
    {
        DebugLog("Restoring upload terminal state as INTERACTED");

        if (uploadTerminalHitbox != null)
        {
            uploadTerminalHitbox.gameObject.SetActive(false);
        }

        if (uploadTerminalInteractable != null)
        {
            uploadTerminalInteractable.gameObject.SetActive(true);
        }

        if (AudioManager.Instance != null && interactedUploadTerminalSound != null)
            AudioManager.Instance.PlaySound(interactedUploadTerminalSound, transform.position, AudioCategory.Ambience);

        StartCoroutine(TransitionToCredits());
    }

    private IEnumerator TransitionToCredits()
    {
        if (isTransitioningToCredits)
        {
            // already transitioning
            yield break;
        }

        isTransitioningToCredits = true;
        yield return new WaitForSeconds(endGameTransitionDelay);
        SceneManager.LoadSceneAsync("Credits");
    }

    private bool IsUploadTerminalDestroyed()
    {
        return QuestManager.Instance.IsQuestComplete(destroyUploadTerminalQuest.questID);
    }

    private bool IsUploadTerminalInteracted()
    {
        return QuestManager.Instance.IsQuestComplete(interactUploadTerminalQuest.questID);
    }

    private void OnDestroy()
    {
        QuestManager.Instance.OnQuestCompleted -= SyncFromQuestManager;
        QuestManager.Instance.OnQuestManagerFinishedLoading -= SyncFromQuestManager;
    }

    private void ResetAll()
    {
        DebugLog("Resetting upload terminal state");

        if (uploadTerminalHitbox != null)
        {
            uploadTerminalHitbox.gameObject.SetActive(true);
        }

        if (uploadTerminalInteractable != null)
        {
            uploadTerminalInteractable.gameObject.SetActive(true);
        }

        isTransitioningToCredits = false;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log(message);
    }
}