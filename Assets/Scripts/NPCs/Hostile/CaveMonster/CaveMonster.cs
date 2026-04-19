using System;
using System.Collections;
using Unity.VisualScripting.Dependencies.NCalc;
using UnityEngine;

public class CaveMonster : MonoBehaviour
{

    [Header("References")]
    [SerializeField] private CaveMonsterAnimationHandler animationHandler;
    [SerializeField] private CaveMonsterHurtBox hurtBox;

    [Header("Settings")]
    [SerializeField] private float climbSpeed = 1f;

    [Header("Audio")]
    [SerializeField] private AudioClip appearVoiceClip;
    [SerializeField] private AudioClip appearNoiseClip;
    [SerializeField] private AudioClip biteClip;
    [SerializeField] private AudioClip[] climbingVoiceClips;
    [SerializeField] private float minDelayBetweenClimbVoiceSounds = 8f;
    [SerializeField] private float maxDelayBetweenClimbVoiceSounds = 9f;
    [SerializeField] private AudioClip[] climbingNoiseClips;
    [SerializeField] private float minDelayBetweenClimbNoiseSounds = 1f;
    [SerializeField] private float maxDelayBetweenClimbNoiseSounds = 3f;

    [SerializeField] private AudioSource loopingRumbleSource;
    [SerializeField] private AudioClip rumbleClip;

    [Header("Logic")]
    public bool isBiting = false;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    [HideInInspector] public SubmarineHealthManager playerHealth; // this will be set (and cleared) by the hurtbox when the player enters/exits it


    private void Awake()
    {
        if (animationHandler == null)
            animationHandler = GetComponent<CaveMonsterAnimationHandler>();

        if (hurtBox == null)
            hurtBox = GetComponent<CaveMonsterHurtBox>();

        if (loopingRumbleSource == null)
        {
            loopingRumbleSource = GetComponentInChildren<AudioSource>();
        }

        loopingRumbleSource.clip = rumbleClip;
        loopingRumbleSource.loop = true;

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
        if (appearVoiceClip != null)
        {
            AudioManager.Instance.PlaySound(appearVoiceClip, Camera.main.transform.position, AudioCategory.EnemySFX, layer: AudioLayer.Exterior);
        }
        if (appearNoiseClip != null)
        {
            AudioManager.Instance.PlaySound(appearNoiseClip, Camera.main.transform.position, AudioCategory.EnemySFX, layer: AudioLayer.Exterior);
        }

        OpenMouth();
        StartCoroutine(ClimbCoroutine());
        StartCoroutine(ClimbVoiceCoroutine());
        StartCoroutine(ClimbNoiseCoroutine());
    }

    private IEnumerator ClimbCoroutine()
    {
        while (transform.localPosition.z < 0)
        {
            yield return null;
            transform.localPosition += new Vector3(0, 0, climbSpeed) * Time.deltaTime;
        }

        Bite();
    }

    private IEnumerator ClimbVoiceCoroutine()
    {
        while (transform.localPosition.z < 0)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(minDelayBetweenClimbVoiceSounds, maxDelayBetweenClimbVoiceSounds));
            if (climbingVoiceClips != null && climbingVoiceClips.Length > 0)
                AudioManager.Instance.PlaySound(climbingVoiceClips[UnityEngine.Random.Range(0, climbingVoiceClips.Length)], Camera.main.transform.position, AudioCategory.EnemySFX, layer: AudioLayer.Exterior);
        }
    }

    private IEnumerator ClimbNoiseCoroutine()
    {
        loopingRumbleSource.Play();
        while (transform.localPosition.z < 0)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(minDelayBetweenClimbNoiseSounds, maxDelayBetweenClimbNoiseSounds));
            if (climbingNoiseClips != null && climbingNoiseClips.Length > 0)
                AudioManager.Instance.PlaySound(climbingNoiseClips[UnityEngine.Random.Range(0, climbingNoiseClips.Length)], Camera.main.transform.position, AudioCategory.EnemySFX, layer: AudioLayer.Exterior);
        }
    }

    private void StopClimbing()
    {
        DebugLog("Stopping climbing!");
        loopingRumbleSource.Stop();
        StopAllCoroutines();

        if (!isBiting && transform.localPosition.z < -0.3f)
            StartClimbing();
    }

    private void OpenMouth()
    {
        DebugLog("Opening mouth!");
        animationHandler.StartOpenMouthLoopAnimation();
        hurtBox.gameObject.SetActive(true);
    }

    public void Bite()
    {
        isBiting = true;
        DebugLog("Bite triggered! Setting isBiting to true.");

        if (biteClip != null)
            AudioManager.Instance.PlaySound(biteClip, Camera.main.transform.position, AudioCategory.EnemySFX, layer: AudioLayer.Exterior);

        animationHandler.StartBiteAnimation();

        StopClimbing();
    }

    public void OnBiteFinished()
    {
        if (playerHealth != null)
            playerHealth.HandleSubmarineDestroyed();

        isBiting = false;
        DebugLog("Bite finished! Setting isBiting to false.");

        if (transform.localPosition.z < -0.3f)
        {
            StartClimbing();
        }
    }

    public void OnPlayerExitBiteRange()
    {
        isBiting = false;
        DebugLog("Player exited bite range! Setting isBiting to false.");
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log("[CaveMonster]" + message);
    }

}