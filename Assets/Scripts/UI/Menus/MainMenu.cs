using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UPDATED: Main menu controller with operational state awareness.
/// Ensures GameManager is in Menu state and handles transitions to gameplay properly.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [SerializeField] private GameObject settingsMenu;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private string startingSceneName = "Level00";

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private void Awake()
    {
        if (settingsMenu != null)
        {
            settingsMenu.SetActive(false);
        }
    }

    private void Start()
    {
        InitializeMainMenu();
    }

    /// <summary>
    /// Initializes the main menu and ensures proper operational state
    /// </summary>
    private void InitializeMainMenu()
    {
        DebugLog("Initializing Main Menu");

        // Ensure GameManager is in Menu state
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.CurrentOperationalState != ManagerOperationalState.Menu)
            {
                DebugLog("Setting GameManager to Menu state");
                GameManager.Instance.SetOperationalState(ManagerOperationalState.Menu);
            }
        }
        else
        {
            Debug.LogWarning("[MainMenu] GameManager not found!");
        }

        // Update UI based on save file existence
        UpdateLoadButtonState();

        // Ensure InputManager is in correct state (UI only)
        if (InputManager.Instance != null)
        {
            DebugLog("InputManager found and ready for menu input");
        }
        else
        {
            Debug.LogWarning("[MainMenu] InputManager not found!");
        }

        DebugLog("Main Menu initialization complete");
    }

    /// <summary>
    /// Updates the load button based on whether a save file exists
    /// </summary>
    private void UpdateLoadButtonState()
    {
        if (loadGameButton != null && SaveManager.Instance != null)
        {
            bool saveExists = SaveManager.Instance.SaveExists();
            loadGameButton.interactable = saveExists;
            DebugLog($"Load button state: {(saveExists ? "Enabled" : "Disabled")}");
        }
    }

    /// <summary>
    /// Handles Load Game button press - transitions to gameplay state
    /// </summary>
    public void OnLoadGameButtonPressed()
    {
        DebugLog("Load Game button pressed");

        if (GameManager.Instance.uiManager != null)
            AudioManager.Instance.PlaySound2D(GameManager.Instance.uiManager.buttonSound, AudioCategory.UI);

        if (SaveManager.Instance != null)
        {
            // SaveManager will handle the scene transition and state restoration
            SaveManager.Instance.LoadGame();
        }
        else
        {
            Debug.LogError("[MainMenu] SaveManager not found - cannot load game!");
        }
    }

    /// <summary>
    /// Handles New Game button press - transitions to gameplay state with fresh data
    /// </summary>
    public void OnNewGameButtonPressed()
    {
        DebugLog("New Game button pressed");

        if (GameManager.Instance.uiManager != null)
            AudioManager.Instance.PlaySound2D(GameManager.Instance.uiManager.buttonSound, AudioCategory.UI);

        if (SceneTransitionManager.Instance != null)
        {
            // SceneTransitionManager will handle the transition and state setup
            SceneTransitionManager.Instance.StartNewGame(startingSceneName);
        }
        else
        {
            Debug.LogError("[MainMenu] SceneTransitionManager not found - cannot start new game!");
        }
    }

    /// <summary>
    /// Handles Settings button press
    /// </summary>
    public void OnSettingsButtonPressed()
    {
        DebugLog("Settings button pressed");

        if (GameManager.Instance.uiManager != null)
            AudioManager.Instance.PlaySound2D(GameManager.Instance.uiManager.buttonSound, AudioCategory.UI);

        if (settingsMenu != null)
        {
            settingsMenu.SetActive(true);
        }
    }

    /// <summary>
    /// Handles Quit Game button press
    /// </summary>
    public void OnQuitGameButtonPressed()
    {
        if (GameManager.Instance.uiManager != null)
            AudioManager.Instance.PlaySound2D(GameManager.Instance.uiManager.buttonSound, AudioCategory.UI);

        DebugLog("Quit Game button pressed");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Called when returning from settings menu
    /// </summary>
    public void OnSettingsMenuClosed()
    {
        if (settingsMenu != null)
        {
            settingsMenu.SetActive(false);
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[MainMenu] {message}");
        }
    }
}