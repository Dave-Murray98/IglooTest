using NUnit.Framework;
using Sirenix.OdinInspector;
using UnityEngine;

public class Egg : MonoBehaviour
{

    [Header("References")]
    [SerializeField] private float onBreakEggNoiseVolume = 10f;

    [Header("Models")]
    [SerializeField] private GameObject normalEggModel;
    [SerializeField] private GameObject brokenEggModel;

    [Header("Particles")]
    [SerializeField] private ParticleSystem chemicalParticles;

    [Header("Hazard Collider")]
    [SerializeField]
    private GameObject hazardCollider;

    [ShowInInspector] private bool isBroken = false;

    // [Header("Audio")]
    // [SerializeField] private AudioClip eggActiveLoopingSound;
    // [SerializeField] private bool loopingAudioPlaying = false;
    // [SerializeField] private int eggActiveLoopingSoundID = -1;

    /// <summary>
    /// Public property to check if the egg is broken (for save system)
    /// </summary>
    public bool IsBroken => isBroken;

    // private void Start()
    // {
    //     if (AudioManager.Instance.IsProperlyInitialized)
    //     {
    //         if (!isBroken)
    //             PlayLoopingAudio();
    //         return;
    //     }

    //     AudioManager.Instance.OnAudioManagerInitialized += PlayLoopingAudio;
    // }

    // private void PlayLoopingAudio()
    // {
    //     if (loopingAudioPlaying) return;

    //     if (isBroken)
    //     {
    //         AudioManager.Instance.StopLoopingSound(eggActiveLoopingSoundID, AudioCategory.Ambience);
    //         return;
    //     }

    //     eggActiveLoopingSoundID = AudioManager.Instance.PlaySoundTracked(eggActiveLoopingSound, transform.position, AudioCategory.Ambience, loop: true);
    //     loopingAudioPlaying = true;
    // }

    [Button]
    public void ResetEgg()
    {
        normalEggModel.SetActive(true);
        brokenEggModel.SetActive(false);

        hazardCollider.SetActive(true);

        chemicalParticles.Play();

        // loopingAudioPlaying = false;
        // PlayLoopingAudio();

        isBroken = false;
    }


    [Button]
    public void BreakEgg()
    {
        if (isBroken) return;

        normalEggModel.SetActive(false);
        brokenEggModel.SetActive(true);

        hazardCollider.SetActive(false);

        chemicalParticles.Stop();

        NoisePool.Instance.GetNoise(transform.position, onBreakEggNoiseVolume);

        ParticleFXPool.Instance.GetBloodSplat(transform.position, Quaternion.identity);
        ParticleFXPool.Instance.GetBreakEggFX(transform.position, Quaternion.identity);

        // loopingAudioPlaying = false;
        // AudioManager.Instance.StopLoopingSound(eggActiveLoopingSoundID, AudioCategory.Ambience);

        isBroken = true;
    }
}