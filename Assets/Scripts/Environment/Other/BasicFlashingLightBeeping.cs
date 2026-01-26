using UnityEngine;

public class BasicFlashingLightBeeping : MonoBehaviour
{
    [SerializeField] private Light pointLight;

    [SerializeField] private float beepDuration = 0.5f;      // How long each flash stays on
    [SerializeField] private float beepInterval = 0.5f;      // Gap between the two beeps
    [SerializeField] private float pauseAfterBeeps = 2f;     // Long pause after the beep pattern

    private void Start()
    {
        if (pointLight == null)
        {
            pointLight = GetComponent<Light>();
        }

        StartCoroutine(BeepingCoroutine());
    }

    private System.Collections.IEnumerator BeepingCoroutine()
    {
        while (true)
        {
            // First beep: Turn ON
            pointLight.enabled = true;
            yield return new WaitForSeconds(beepDuration);

            // First beep: Turn OFF
            pointLight.enabled = false;
            yield return new WaitForSeconds(beepInterval);

            // Second beep: Turn ON
            pointLight.enabled = true;
            yield return new WaitForSeconds(beepDuration);

            // Second beep: Turn OFF
            pointLight.enabled = false;

            // Long pause before repeating the pattern
            yield return new WaitForSeconds(pauseAfterBeeps);
        }
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}