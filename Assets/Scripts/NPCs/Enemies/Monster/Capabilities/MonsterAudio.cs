using UnityEngine;

public class MonsterAudio : MonoBehaviour
{

    [Header("References")]
    [SerializeField] private UnderwaterMonsterController controller;

    [Header("Audio Clips")]
    public AudioClip[] attackClips;
    public AudioClip[] takeHitClips;
    public AudioClip[] takeStunClips;
    public AudioClip[] echolocationClips;
    public AudioClip[] onHearNoiseClips;
    public AudioClip onDeathClip;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<UnderwaterMonsterController>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        controller.attack.OnPerformingAttack += PlayOnAttack;
        controller.health.OnMonsterTakeHit += PlayOnTakeHit;
        controller.health.OnMonsterStunned += PlayOnTakeStun;
        controller.OnPlayIdleSound += PlayEchoLocationSound;
        controller.hearing.OnNoiseHeard += PlayOnNoiseHeard;
        controller.health.OnDeath += PlayOnDeath;
    }

    private void PlayOnTakeHit()
    {
        if (takeHitClips.Length == 0) return;

        AudioClip randomClip = takeHitClips[Random.Range(0, takeHitClips.Length)];
        AudioManager.Instance.PlaySound(randomClip, transform.position, AudioCategory.EnemySFX);
    }

    private void PlayOnTakeStun()
    {
        if (takeStunClips.Length == 0) return;

        AudioClip randomClip = takeStunClips[Random.Range(0, takeStunClips.Length)];
        AudioManager.Instance.PlaySound(randomClip, transform.position, AudioCategory.EnemySFX);
    }

    private void PlayOnDeath()
    {
        if (onDeathClip == null) return;

        AudioManager.Instance.PlaySound(onDeathClip, transform.position, AudioCategory.EnemySFX);
    }

    private void PlayOnAttack()
    {
        if (attackClips.Length == 0) return;

        AudioClip randomClip = attackClips[Random.Range(0, attackClips.Length)];
        AudioManager.Instance.PlaySound(randomClip, transform.position, AudioCategory.EnemySFX);
    }

    private void PlayOnNoiseHeard(Vector3 vector)
    {
        if (onHearNoiseClips.Length == 0) return;

        AudioClip randomClip = onHearNoiseClips[Random.Range(0, onHearNoiseClips.Length)];
        AudioManager.Instance.PlaySound(randomClip, transform.position, AudioCategory.EnemySFX);
    }

    private void PlayEchoLocationSound()
    {
        if (echolocationClips.Length == 0) return;

        AudioClip randomClip = echolocationClips[Random.Range(0, echolocationClips.Length)];
        AudioManager.Instance.PlaySound(randomClip, transform.position, AudioCategory.EnemySFX);
    }

    private void OnDestroy()
    {
        controller.attack.OnPerformingAttack -= PlayOnAttack;
        controller.health.OnMonsterTakeHit -= PlayOnTakeHit;
        controller.health.OnMonsterStunned -= PlayOnTakeStun;
        controller.OnPlayIdleSound -= PlayEchoLocationSound;
        controller.hearing.OnNoiseHeard -= PlayOnNoiseHeard;
        controller.health.OnDeath -= PlayOnDeath;
    }
}
