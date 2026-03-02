using UnityEngine;

public class CaveMonsterAnimationHandler : MonoBehaviour
{
    [SerializeField] private Animator animator;


    [SerializeField] private string openMouthTriggerString = "OpenMouth";

    [SerializeField] private string biteTriggerString = "Bite";

    public void StartOpenMouthLoopAnimation()
    {
        animator.ResetTrigger(biteTriggerString);
        animator.SetTrigger(openMouthTriggerString);
    }

    public void StartBiteAnimation()
    {
        animator.ResetTrigger(openMouthTriggerString);
        animator.SetTrigger(biteTriggerString);
    }

    public void OnBiteAnimationFinished()
    {
        animator.ResetTrigger(biteTriggerString);
    }
}
