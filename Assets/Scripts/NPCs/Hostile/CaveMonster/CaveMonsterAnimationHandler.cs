using UnityEngine;

public class CaveMonsterAnimationHandler : MonoBehaviour
{
    [SerializeField] private Animator animator;


    [SerializeField] private string openMouthTriggerString = "OpenMouth";

    [SerializeField] private string biteTriggerString = "Bite";

    [SerializeField] private CaveMonster caveMonster;

    [SerializeField] private bool enableDebugLogs = false;

    private void Awake()
    {
        if (caveMonster == null)
            caveMonster = GetComponent<CaveMonster>();
    }

    public void StartOpenMouthLoopAnimation()
    {
        DebugLog("Starting open mouth loop animation!");
        animator.ResetTrigger(biteTriggerString);
        animator.SetTrigger(openMouthTriggerString);
    }

    public void StartBiteAnimation()
    {
        DebugLog("Starting bite animation!");
        animator.ResetTrigger(openMouthTriggerString);
        animator.SetTrigger(biteTriggerString);
    }

    public void OnBiteAnimationFinished()
    {
        DebugLog("Bite animation finished!");
        animator.ResetTrigger(biteTriggerString);
        caveMonster.OnBiteFinished();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log("[CaveMonsterAnimationHandler] " + message);
        }
    }
}
