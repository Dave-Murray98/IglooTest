using UnityEngine;

public class SubmarineCompass : MonoBehaviour
{
    private Transform submarine;

    void Start()
    {
        // Get the submarine (the parent of this GameObject)
        submarine = transform.parent;
        SetCompassRotation();
    }

    void Update()
    {
        SetCompassRotation();
    }

    void SetCompassRotation()
    {
        // Get the submarine's world rotation
        Vector3 submarineRotation = submarine.eulerAngles;

        // Apply the submarine's pitch and roll to the compass
        // but force Y (yaw) to always be 0 (north)
        transform.rotation = Quaternion.Euler(submarineRotation.x, 0f, submarineRotation.z);
    }
}