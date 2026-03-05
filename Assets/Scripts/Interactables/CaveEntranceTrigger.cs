using UnityEngine;

public class CaveEntranceTrigger : EventTrigger
{
    [SerializeField] private Animator rockDebrisAnimator;
    [SerializeField] private string rockDebrisAnimationName;

    [SerializeField] private GameObject npcsParent;

    protected override void TriggerEvent()
    {
        base.TriggerEvent();

        rockDebrisAnimator.SetTrigger(rockDebrisAnimationName);
        npcsParent.SetActive(false);
    }


}
