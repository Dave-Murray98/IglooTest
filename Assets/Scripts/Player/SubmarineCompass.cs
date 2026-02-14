using UnityEngine;

public class SubmarineCompass : MonoBehaviour
{
    private Transform submarine;

    void Start()
    {
        submarine = transform.parent;
        SetCompassRotation();
    }

    void Update()
    {
        SetCompassRotation();
    }

    void SetCompassRotation()
    {
        // Counteract the submarine's Y rotation in local space
        // If the submarine faces east (90 degrees), we rotate the compass -90 degrees locally
        // so that it ends up pointing north in world space
        transform.localEulerAngles = new Vector3(0f, -submarine.eulerAngles.y, 0f);
    }
}