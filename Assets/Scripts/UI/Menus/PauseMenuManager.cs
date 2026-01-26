using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UPDATED: Pause menu manager with "Return to Main Menu" functionality.
/// Handles transitions from gameplay back to main menu properly.
/// </summary>
public class PauseMenuManager : MonoBehaviour
{
    [Header("Buttons")]
    public Button resumeButton;
    public Button settingsButton;
    public Button saveButton;
    public Button loadButton;
    public Button returnToMenuButton;  // NEW: Return to main menu button
    public Button quitButton;

    [Header("Settings Panel")]
    public GameObject settingsPanel;

    public AudioSettingsUI audioSettingsUI;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    //Button events
    public event Action OnResumeButtonPressed;
    public event Action OnSettingsButtonPressed;
    public event Action OnSaveButtonPressed;
    public event Action OnLoadButtonPressed;
    public event Action OnReturnToMenuButtonPressed;  // NEW: Return to menu event
    public event Action OnQuitButtonPressed;


    public void Initialize()
    {
        if (audioSettingsUI == null)
        {
            audioSettingsUI = GetComponentInChildren<AudioSettingsUI>();
        }

        DebugLog("Initializing PauseMenuManager");

        audioSettingsUI.Initialize();

        CloseSettingsPanel();

        this.gameObject.SetActive(false);
    }


    #region Button Events
    public void OnResumeButtonClicked() => OnResumeButtonPressed?.Invoke();

    public void OnSettingsButtonClicked() => OnSettingsButtonPressed?.Invoke();

    public void OnSaveButtonClicked() => OnSaveButtonPressed?.Invoke();

    public void OnLoadButtonClicked() => OnLoadButtonPressed?.Invoke();

    /// <summary>
    /// NEW: Handles Return to Main Menu button click
    /// </summary>
    public void OnReturnToMenuButtonClicked() => OnReturnToMenuButtonPressed?.Invoke();

    public void OnQuitButtonClicked() => OnQuitButtonPressed?.Invoke();

    #endregion

    #region Settings Panel Management

    public void OpenSettingsPanel()
    {
        settingsPanel.SetActive(true);

        DebugLog("Opening settings panel");
    }

    public void CloseSettingsPanel()
    {
        settingsPanel.SetActive(false);
        DebugLog("Closing settings panel");
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PauseMenuManager] {message}");
        }
    }
}