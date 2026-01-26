using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Credits : MonoBehaviour
{
    [SerializeField] private float transitionTime = 7f;

    private void Start()
    {
        StartCoroutine(TransitionBackToMainMenu());
    }

    private IEnumerator TransitionBackToMainMenu()
    {
        yield return new WaitForSeconds(transitionTime);
        SceneManager.LoadSceneAsync("MainMenu");
    }
}
