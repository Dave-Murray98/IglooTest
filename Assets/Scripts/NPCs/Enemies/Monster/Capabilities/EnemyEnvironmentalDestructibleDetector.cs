using UnityEngine;

public class EnemyEnvironmentalDestructibleDetector : MonoBehaviour
{
    public bool detectedDestructible = false;


    private void OnTriggerEnter(Collider other)
    {
        detectedDestructible = true;
    }

    private void OnTriggerExit(Collider other)
    {
        detectedDestructible = false;
    }
}
