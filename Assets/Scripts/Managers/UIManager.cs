using System;
using Infohazard.Core;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UPDATED: UIManager with complete low oxygen warning system including audio
/// </summary>
public class UIManager : MonoBehaviour, IManager, IManagerState
{
    [Header("UI References")]
    public GameObject pauseMenu;
    public PauseMenuManager pauseMenuManager;

    [Header("Audio")]
    public AudioClip buttonSound;
    public AudioClip openMenuSound;
    public AudioClip closeMenuSound;


    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // IManagerState implementation
    [ShowInInspector] private ManagerOperationalState operationalState = ManagerOperationalState.Gameplay;
    [ShowInInspector, ReadOnly] public ManagerOperationalState CurrentOperationalState => operationalState;


    public void Initialize()
    {
        DebugLog("UIManager Initialized");
        RefreshReferences();

        HidePauseMenu();
    }


    public void RefreshReferences()
    {
        if (!CanOperateInCurrentState())
            return;

        DebugLog("UIManager: Refreshing references");

        // Unsubscribe first to prevent duplicates
        UnsubscribeFromEvents();

        // Subscribe to events
        SubscribeToEvents();
        SetUpPauseMenuEvents();
    }

    private void SetUpPauseMenuEvents()
    {
        if (pauseMenuManager == null)
        {
            Debug.LogError("PauseMenuManager is null, cannot set up pause menu events");
            return;
        }

        UnsubscribeFromPauseMenuEvents();
        SubscribeToPauseMenuEvents();
    }


    private void UnsubscribeFromPauseMenuEvents()
    {
        if (pauseMenuManager == null)
        {
            Debug.LogError("PauseMenuManager is null, cannot unsubscribe from pause menu events");
            return;
        }

        pauseMenuManager.OnResumeButtonPressed -= OnResumeButtonClicked;
        pauseMenuManager.OnSettingsButtonPressed -= OnSettingsButtonClicked;
        pauseMenuManager.OnReturnToMenuButtonPressed -= OnReturnToMenuButtonClicked;
        pauseMenuManager.OnQuitButtonPressed -= OnQuitButtonClicked;
    }

    private void SubscribeToPauseMenuEvents()
    {
        if (pauseMenuManager == null)
        {
            Debug.LogError("PauseMenuManager is null, cannot subscribe to pause menu events");
            return;
        }

        pauseMenuManager.OnResumeButtonPressed += OnResumeButtonClicked;
        pauseMenuManager.OnSettingsButtonPressed += OnSettingsButtonClicked;
        pauseMenuManager.OnReturnToMenuButtonPressed += OnReturnToMenuButtonClicked;
        pauseMenuManager.OnQuitButtonPressed += OnQuitButtonClicked;
    }

    /// <summary>
    /// Subscribe to all relevant game events
    /// </summary>
    private void SubscribeToEvents()
    {
        // Game state events
        GameEvents.OnGamePaused += ShowPauseMenu;
        GameEvents.OnGameResumed += HidePauseMenu;
    }



    /// <summary>
    /// Unsubscribe from all game events
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        // Game state events
        GameEvents.OnGamePaused -= ShowPauseMenu;
        GameEvents.OnGameResumed -= HidePauseMenu;

    }

    public void Cleanup()
    {
        UnsubscribeFromEvents();
    }

    /// <summary>
    /// Show a temporary message to the player
    /// </summary>
    public void ShowMessage(string message, float duration = 3f)
    {
        // This is a placeholder - you can implement a proper message system
        Debug.Log($"[UI Message] {message}");
    }

    #region Mouse Management

    public void ToggleUIMenuModeInput(bool enabled)
    {
        if (enabled)
        {
            SetMouseLocked(false);
        }
        else
        {
            SetMouseLocked(true);
        }
    }

    private void SetMouseLocked(bool locked)
    {
        if (locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    #endregion

    #region Menu Management
    private void ShowPauseMenu()
    {
        DebugLog("UIManager: ShowPauseMenu called");
        if (pauseMenu != null)
        {
            pauseMenu.SetActive(true);
            SetMouseLocked(false);

            if (openMenuSound != null)
            {
                AudioManager.Instance.PlaySound2D(openMenuSound, AudioCategory.UI);
            }
        }
    }

    private void HidePauseMenu()
    {
        DebugLog("UIManager: HidePauseMenu called");
        if (pauseMenu != null)
        {
            pauseMenu.SetActive(false);

            DebugLog("Re-enabling gameplay input after closing pause menu");
            SetMouseLocked(true);

            if (closeMenuSound != null)
            {
                AudioManager.Instance.PlaySound2D(closeMenuSound, AudioCategory.UI);
            }
        }

        pauseMenuManager.CloseSettingsPanel();
    }

    #endregion

    #region  IManagerState Implementation

    public void SetOperationalState(ManagerOperationalState newState)
    {
        operationalState = newState;

        switch (newState)
        {
            case ManagerOperationalState.Menu:
                OnEnterMenuState();
                break;
            case ManagerOperationalState.Gameplay:
                OnEnterGameplayState();
                break;
            case ManagerOperationalState.Transition:
                OnEnterTransitionState();
                break;
        }
    }

    public void OnEnterMenuState()
    {
        // Disable Update if you have one
        this.enabled = false;
    }

    public void OnEnterGameplayState()
    {
        // Re-enable operations
        this.enabled = true;
    }

    public void OnEnterTransitionState()
    {
        // Minimal operations during transition
    }

    public bool CanOperateInCurrentState()
    {
        return operationalState == ManagerOperationalState.Gameplay;
    }

    #endregion

    #region Button Handlers

    /// <summary>
    /// Handles Return to Main Menu button click
    /// </summary>
    private void OnReturnToMenuButtonClicked()
    {
        DebugLog("Return to Main Menu button clicked");

        if (buttonSound != null)
            AudioManager.Instance.PlaySound2D(buttonSound, AudioCategory.UI);

        // Close pause menu first
        if (GameManager.Instance != null && GameManager.Instance.isPaused)
        {
            GameManager.Instance.ResumeGame();
        }

        // Return to main menu via GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReturnToMainMenu();
        }
        else
        {
            Debug.LogError("[UIManager] GameManager not found - cannot return to menu!");
        }
    }

    public void OnResumeButtonClicked()
    {
        if (buttonSound != null)
            AudioManager.Instance.PlaySound2D(buttonSound, AudioCategory.UI);

        GameManager.Instance.ResumeGame();
    }

    public void OnSettingsButtonClicked()
    {
        if (buttonSound != null)
            AudioManager.Instance.PlaySound2D(buttonSound, AudioCategory.UI);

        pauseMenuManager.OpenSettingsPanel();
    }

    public void OnQuitButtonClicked()
    {
        if (buttonSound != null)
            AudioManager.Instance.PlaySound2D(buttonSound, AudioCategory.UI);

        GameManager.Instance.QuitGame();
    }


    #endregion


    private void OnDestroy()
    {
        Cleanup();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[UIManager] {message}");
    }
}