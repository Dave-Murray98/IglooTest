using UnityEngine;

public class NPCAudio : MonoBehaviour
{
    public AudioClip[] idleAudioClips;
    public AudioClip[] attackAudioClips;


    public void PlayRandomIdleAudioClip()
    {
        if (idleAudioClips != null && idleAudioClips.Length > 0)
            AudioManager.Instance.PlaySound(idleAudioClips[Random.Range(0, idleAudioClips.Length)], transform.position, AudioCategory.EnemySFX, layer: AudioLayer.Exterior);
    }

    public void PlayRandomAttackAudioClip()
    {
        if (attackAudioClips != null && attackAudioClips.Length > 0)
            AudioManager.Instance.PlaySound(attackAudioClips[Random.Range(0, attackAudioClips.Length)], transform.position, AudioCategory.EnemySFX, layer: AudioLayer.Exterior);
    }

}