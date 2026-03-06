using System;
using Sirenix.OdinInspector;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.SceneManagement;

/// <summary>
/// Interface for centralized manager coordination.
/// All core managers should implement this for lifecycle management.
/// </summary>
public interface IManager
{
    /// <summary>
    /// Initialize the manager's core functionality and state.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Refresh component references after scene changes.
    /// </summary>
    void RefreshReferences();

    /// <summary>
    /// Clean up resources and unsubscribe from events.
    /// </summary>
    void Cleanup();
}

/// <summary>
/// UPDATED: Central coordinator for all game managers and core systems with operational state management.
/// Now handles transitions between Menu and Gameplay states to prevent null references and enable
/// clean MainMenu integration. Persistent managers can adapt their behavior based on current state.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [ShowInInspector, ReadOnly]
    private static string levelSceneName = "Terrain With Cave";
    [ShowInInspector, ReadOnly] private static string mainMenuSceneName = "MainMenuScene";
    [ShowInInspector, ReadOnly] private static string gameOverSceneName = "GameOverScene";
    [ShowInInspector, ReadOnly] private static string gameWinSceneName = "GameWinScene";

    [Header("Persistent Managers")]
    [ShowInInspector, ReadOnly] private AudioManager audioManagerReference;

    [Header("Game State")]
    public bool isPaused = false;

    // Events for manager system coordination
    public static event Action OnManagersInitialized;
    public static event Action OnManagersRefreshed;

    // Manager tracking
    private List<IManager> sceneBasedManagers = new List<IManager>();
    private List<IManager> persistentManagers = new List<IManager>();
    private List<IManager> allManagers = new List<IManager>();


    // Public accessors for persistent managers
    public AudioManager AudioManager => AudioManager.Instance;



    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        InitializeManagers();
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Handles scene loaded events with improved singleton manager handling
    /// </summary>
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        StartCoroutine(RefreshManagerReferencesCoroutine());
    }

    /// <summary>
    /// Enhanced manager initialization that handles both persistent and scene-based managers
    /// </summary>
    private void InitializeManagers()
    {
        DebugLog("Starting manager initialization");

        // STEP 1: Initialize or connect to persistent singleton managers
        InitializePersistentManagers();

        // STEP 2: Find and initialize scene-based managers
        FindAndRegisterSceneManagers();
        InitializeSceneBasedManagers();

        OnManagersInitialized?.Invoke();
        DebugLog("Manager initialization complete");
    }

    /// <summary>
    /// Handles persistent singleton managers (InputManager, PlayerStateManager, etc.)
    /// </summary>
    private void InitializePersistentManagers()
    {
        DebugLog("Initializing persistent managers");
        persistentManagers.Clear();

        InitializeAudioManager();

        DebugLog($"Initialized {persistentManagers.Count} persistent managers");
    }


    private void InitializeAudioManager()
    {
        if (AudioManager.Instance == null)
        {
            DebugLog("Creating AudioManager singleton");
            var audioManagerGO = FindFirstObjectByType<AudioManager>();
            if (audioManagerGO == null)
            {
                Debug.LogWarning("No AudioManager found in scene!");
                audioManagerReference = null;
                return;
            }
            else
            {
                audioManagerGO.Initialize();
            }
        }
        else
        {
            DebugLog("AudioManager singleton already exists - refreshing");
            AudioManager.Instance.RefreshReferences();
        }

        audioManagerReference = AudioManager.Instance;
        if (audioManagerReference != null)
        {
            if (!persistentManagers.Contains(audioManagerReference))
            {
                persistentManagers.Add(audioManagerReference);
            }
            DebugLog("AudioManager ready");
        }
    }

    /// <summary>
    /// Finds and registers only scene-based managers
    /// </summary>
    private void FindAndRegisterSceneManagers()
    {
        sceneBasedManagers.Clear();

        // Register scene-based managers that implement IManager

        DebugLog($"Found {sceneBasedManagers.Count} scene-based managers");

        // Update the combined manager list
        UpdateAllManagersList();
    }

    /// <summary>
    /// Combines persistent and scene-based managers
    /// </summary>
    private void UpdateAllManagersList()
    {
        allManagers.Clear();

        // Add persistent managers
        allManagers.AddRange(persistentManagers);

        // Add scene-based managers
        allManagers.AddRange(sceneBasedManagers);

        DebugLog($" Total managers tracked: {allManagers.Count}");
    }

    /// <summary>
    /// Initializes only scene-based managers (persistent ones are already initialized)
    /// </summary>
    private void InitializeSceneBasedManagers()
    {
        DebugLog(" Initializing scene-based managers");

        foreach (var manager in sceneBasedManagers)
        {
            try
            {
                manager.Initialize();
                DebugLog($" Initialized {manager.GetType().Name}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($" Failed to initialize {manager.GetType().Name}: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Enhanced reference refresh with proper singleton handling
    /// </summary>
    private IEnumerator RefreshManagerReferencesCoroutine()
    {
        yield return null;
        yield return new WaitForSecondsRealtime(0.1f);
        RefreshManagerReferences();
    }

    /// <summary>
    /// Refreshes all manager references with singleton awareness
    /// </summary>
    private void RefreshManagerReferences()
    {
        DebugLog("Refreshing manager references");

        // STEP 1: Handle persistent managers
        RefreshPersistentManagers();

        // STEP 2: Re-find scene-based managers (they may have changed)
        FindAndRegisterSceneManagers();

        // STEP 3: Refresh scene-based managers
        foreach (var manager in sceneBasedManagers)
        {
            try
            {
                manager.RefreshReferences();
                DebugLog($"Refreshed {manager.GetType().Name}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to refresh {manager.GetType().Name}: {e.Message}");
            }
        }

        OnManagersRefreshed?.Invoke();
        DebugLog("Manager refresh complete");
    }

    /// <summary>
    /// Handles refresh for persistent singleton managers
    /// </summary>
    private void RefreshPersistentManagers()
    {
        DebugLog("Refreshing persistent managers");

        // Refresh AudioManager
        if (AudioManager.Instance != null)
        {
            try
            {
                AudioManager.Instance.RefreshReferences();
                audioManagerReference = AudioManager.Instance;
                DebugLog("Refreshed AudioManager singleton");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to refresh AudioManager: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("AudioManager singleton is null during refresh!");
            audioManagerReference = null;
        }

        // Update persistent managers list
        persistentManagers.Clear();
        if (audioManagerReference != null) persistentManagers.Add(audioManagerReference);
    }

    /// <summary>
    /// Pauses the game by setting time scale to 0 and firing pause events.
    /// </summary>
    public void PauseGame()
    {
        if (!isPaused)
        {
            isPaused = true;
            Time.timeScale = 0f;
            GameEvents.TriggerGamePaused();
        }
    }

    /// <summary>
    /// Resumes the game by restoring time scale and firing resume events.
    /// </summary>
    public void ResumeGame()
    {
        if (isPaused)
        {
            isPaused = false;
            Time.timeScale = 1f;
            GameEvents.TriggerGameResumed();
        }
    }

    /// <summary>
    /// Quits the game application.
    /// </summary>
    public void QuitGame()
    {
        DebugLog("Quitting Game");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ReturnToMainMenu()
    {
        DebugLog("Returning to Main Menu");
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void RestartLevel()
    {
        SceneManager.LoadScene(levelSceneName);
    }

    public void OnPlayerDeath()
    {
        DebugLog("Player died!");
        SceneManager.LoadScene(gameOverSceneName);
    }

    [Button("Test Win")]
    public void OnPlayerWin()
    {
        DebugLog("Player won!");
        SceneManager.LoadScene(gameWinSceneName);
    }

    /// <summary>
    /// Manually triggers manager reference refresh with singleton support
    /// </summary>
    [Button]
    public void RefreshReferences()
    {
        RefreshManagerReferences();
    }

    /// <summary>
    /// Gets the InputManager instance (singleton)
    /// </summary>
    public InputManager GetInputManager()
    {
        return InputManager.Instance;
    }


    /// <summary>
    /// Checks if all critical managers are available and properly initialized
    /// </summary>
    public bool AreManagersReady()
    {
        bool inputManagerReady = InputManager.Instance != null && InputManager.Instance.IsProperlyInitialized;

        return inputManagerReady;
    }

    private void OnDestroy()
    {
        // Only cleanup scene-based managers
        // Persistent managers handle their own cleanup
        foreach (var manager in sceneBasedManagers)
        {
            try
            {
                manager.Cleanup();
            }
            catch (System.Exception e)
            {
                Debug.LogError($" Failed to cleanup {manager.GetType().Name}: {e.Message}");
            }
        }

        // Clear all lists
        sceneBasedManagers.Clear();
        persistentManagers.Clear();
        allManagers.Clear();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[GameManager] {message}");
        }
    }


}