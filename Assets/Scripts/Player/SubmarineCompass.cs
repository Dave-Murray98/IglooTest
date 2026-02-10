using UnityEngine;

public class SubmarineCompass : MonoBehaviour
{
    void Start()
    {
        // Set initial north-facing rotation
        SetCompassRotation();
    }

    void Update()
    {
        // Keep maintaining the north-facing rotation
        SetCompassRotation();
    }

    void SetCompassRotation()
    {
        // Get the world rotation and convert to euler angles
        Vector3 worldRotation = transform.rotation.eulerAngles;
        
        // Keep the compass pointing north (Y = 0) while maintaining its tilt/pitch
        // X and Z will naturally match the submarine's tilt because we're working in world space
        transform.rotation = Quaternion.Euler(worldRotation.x, 0f, worldRotation.z);
    }
}