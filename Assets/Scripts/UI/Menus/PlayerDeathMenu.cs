using UnityEngine;

public class PlayerDeathMenu : MonoBehaviour
{
    public void ReturnToMainMenu()
    {
        GameManager.Instance.ReturnToMainMenu();
    }

    public void OpenSettings()
    {
        Debug.Log("Open settings clicked");
    }

    public void QuitGame()
    {
        GameManager.Instance.QuitGame();
    }

    public void RestartLevel()
    {
        GameManager.Instance.RestartLevel();
    }
}
