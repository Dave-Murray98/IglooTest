using UnityEngine;

public class BasicFlashingLight : MonoBehaviour
{
    [SerializeField] private Light pointLight;

    [SerializeField] private float flashInterval = 2f;

    private void Start()
    {
        if (pointLight == null)
        {
            pointLight = GetComponent<Light>();
        }

        StartCoroutine(FlashingCoroutine());
    }

    private System.Collections.IEnumerator FlashingCoroutine()
    {
        while (true)
        {
            pointLight.enabled = !pointLight.enabled;
            yield return new WaitForSeconds(flashInterval);
        }
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}
