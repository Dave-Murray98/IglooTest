using UnityEngine;

public class CaveEntranceTrigger : EventTrigger
{
    [SerializeField] private Animator rockDebrisAnimator;
    [SerializeField] private string rockDebrisAnimationName;

    [SerializeField] private GameObject npcsParent;

    [SerializeField] private AudioClip rockDebrisSound;

    [SerializeField] private Transform rockDebrisTransform;

    protected override void TriggerEvent()
    {
        base.TriggerEvent();

        rockDebrisAnimator.SetTrigger(rockDebrisAnimationName);
        npcsParent.SetActive(false);
    }

    public void PlayRockDebrisSound()
    {
        if (rockDebrisSound != null)
            AudioManager.Instance.PlaySound(rockDebrisSound, rockDebrisTransform.position, AudioCategory.Ambience, layer: AudioLayer.Exterior);
    }


}
