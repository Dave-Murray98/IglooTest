using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UPDATED: Pause menu manager with "Return to Main Menu" functionality.
/// Handles transitions from gameplay back to main menu properly.
/// </summary>
public class PlayerDeathMenu : MonoBehaviour
{
    [Header("Buttons")]
    public Button loadButton;
    public Button returnToMenuButton;  // NEW: Return to main menu button
    public Button quitButton;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    //Button events
    public event Action OnLoadButtonPressed;
    public event Action OnReturnToMenuButtonPressed;  // NEW: Return to menu event
    public event Action OnQuitButtonPressed;


    public void Initialize()
    {
        DebugLog("Initializing PlayerDeathMenu");

        this.gameObject.SetActive(false);
    }

    #region Button Events

    public void OnLoadButtonClicked() => OnLoadButtonPressed?.Invoke();

    public void OnReturnToMenuButtonClicked() => OnReturnToMenuButtonPressed?.Invoke();

    public void OnQuitButtonClicked() => OnQuitButtonPressed?.Invoke();

    #endregion

    #region Settings Panel Management

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PauseMenuManager] {message}");
        }
    }
}