using UnityEngine;

public class DebrisSFXTrigger : MonoBehaviour
{
    [SerializeField] private AudioClip debrisSFX;
    [SerializeField] private AudioSource audioSource;

    private void OnEnable()
    {
        audioSource.clip = debrisSFX;
        audioSource.Play();
    }
}
