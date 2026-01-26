using UnityEngine;

public class DeadFakeMonsterAudio : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;

    [SerializeField] private AudioClip[] dyingSFXs;

    [SerializeField] private float minDelayBetweenSounds = 0.5f;
    [SerializeField] private float maxDelayBetweenSounds = 1.5f;

    private float nextSoundTime = 0f;
    private float volume = 1f;


    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        if (AudioManager.Instance != null)
        {
            volume = AudioManager.Instance.GetCategoryVolume(AudioCategory.EnemySFX);
        }
    }

    private void Update()
    {
        if (!isActiveAndEnabled) return;

        if (Time.time > nextSoundTime)
        {
            PlayDyingSound();
            nextSoundTime = Time.time + Random.Range(minDelayBetweenSounds, maxDelayBetweenSounds);
        }
    }

    private void PlayDyingSound()
    {
        audioSource.PlayOneShot(dyingSFXs[Random.Range(0, dyingSFXs.Length)], volume);
    }

}
