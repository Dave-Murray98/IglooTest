using UnityEngine;

public class NPCAnimationHandler : MonoBehaviour
{

    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Animator Parameters")]
    [SerializeField] private string attackTriggerName = "Attack";

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    public void PlayAttackAnimation()
    {
        if (animator != null)
        {
            animator.SetTrigger(attackTriggerName);
        }
    }
}