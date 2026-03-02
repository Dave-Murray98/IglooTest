using System;
using System.Collections;
using UnityEngine;

public class CaveMonster : MonoBehaviour
{

    [Header("References")]
    [SerializeField] private CaveMonsterAnimationHandler animationHandler;
    [SerializeField] private CaveMonsterHurtBox hurtBox;

    [Header("Settings")]
    [SerializeField] private float climbSpeed = 1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private void Awake()
    {
        QuestManager.OnQuestCompleted += OnQuestCompleted;
    }

    private void OnQuestCompleted(string completedQuestID)
    {
        //if the completed quest is the descend cave quest
        if (completedQuestID == QuestManager.Instance.chainQuests[0].questID)
        {
            StartClimbing();
        }
    }

    private void StartClimbing()
    {
        DebugLog("Starting to climb!");
        OpenMouth();
        StartCoroutine(ClimbCoroutine());
    }

    private IEnumerator ClimbCoroutine()
    {
        while (transform.localPosition.z < 0)
        {
            yield return null;
            transform.localPosition += new Vector3(0, 0, climbSpeed) * Time.deltaTime;
        }
    }

    private void StopClimbing()
    {
        DebugLog("Stopping climbing!");
        StopCoroutine(ClimbCoroutine());
    }

    private void OpenMouth()
    {
        animationHandler.StartOpenMouthLoopAnimation();
        hurtBox.gameObject.SetActive(true);
    }

    public void Bite(SubmarineHealthManager submarineHealthManager)
    {
        DebugLog("Biting player!");

        animationHandler.StartBiteAnimation();

        StopClimbing();

        if (submarineHealthManager != null)
            submarineHealthManager.HandleSubmarineDestroyed();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log("[CaveMonster]" + message);
    }

}