using System.Collections;
using UnityEngine;

public class ViewControlsPrompt : MonoBehaviour
{
    [SerializeField] private float displayDuration = 5f; // Duration to show the prompt after game starts

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Start a coroutine to hide the prompt after the specified duration
        StartCoroutine(HidePromptAfterDelay());
    }

    private IEnumerator HidePromptAfterDelay()
    {
        // Wait for the specified duration
        yield return new WaitForSeconds(displayDuration);

        // Hide the prompt GameObject
        gameObject.SetActive(false);
    }
}
