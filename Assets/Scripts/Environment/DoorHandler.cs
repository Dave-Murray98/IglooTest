using UnityEngine;

public class DoorHandler : MonoBehaviour
{
    [SerializeField] private Animator doorAnimator;

    [SerializeField]
    private string doorOpenAnimationBoolName = "IsOpen";

    private void Awake()
    {
        if (doorAnimator == null)
            doorAnimator = GetComponent<Animator>();
    }

    public void OpenDoor()
    {
        doorAnimator.SetBool(doorOpenAnimationBoolName, true);
    }

    public void CloseDoor()
    {
        doorAnimator.SetBool(doorOpenAnimationBoolName, false);
    }

    public void SetDoorState(bool isOpen)
    {
        doorAnimator.SetBool(doorOpenAnimationBoolName, isOpen);
    }
}