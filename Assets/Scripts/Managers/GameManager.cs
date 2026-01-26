using System;
using Sirenix.OdinInspector;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;

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

    [SerializeField] private string startingSceneName = "Level00";


    [Header("Scene-Based Managers")]
    public UIManager uiManager;


    [Header("Persistent Managers")]
    [ShowInInspector, ReadOnly] private InputManager inputManagerReference;
    [ShowInInspector, ReadOnly] private AudioManager audioManagerReference;

    [Header("Game State")]
    public bool isPaused = false;

    [Header("Manager Tracking")]
    [ShowInInspector, ReadOnly] private int totalManagedManagers;
    [ShowInInspector, ReadOnly] private int persistentManagerCount;
    [ShowInInspector, ReadOnly] private int sceneBasedManagerCount;

    [Header("Operational State")]
    [ShowInInspector, ReadOnly] private ManagerOperationalState currentOperationalState = ManagerOperationalState.Gameplay;

    // Events for manager system coordination
    public static event Action OnManagersInitialized;
    public static event Action OnManagersRefreshed;
    public static event Action<ManagerOperationalState> OnOperationalStateChanged;

    // Manager tracking
    private List<IManager> sceneBasedManagers = new List<IManager>();
    private List<IManager> persistentManagers = new List<IManager>();
    private List<IManager> allManagers = new List<IManager>();

    // State-aware manager tracking (independent of IManager)
    [ShowInInspector] private List<IManagerState> stateAwareManagers = new List<IManagerState>();

    // Public accessors for persistent managers
    public InputManager InputManager => InputManager.Instance;
    public AudioManager AudioManager => AudioManager.Instance;

    // Public accessor for operational state
    public ManagerOperationalState CurrentOperationalState => currentOperationalState;

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

        // Determine initial state based on starting scene
        DetermineInitialOperationalState();
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
        // Determine if this is a menu scene or gameplay scene
        UpdateOperationalStateForScene(scene.name);

        StartCoroutine(RefreshManagerReferencesCoroutine());
    }

    #region Operational State Management

    /// <summary>
    /// Determines initial operational state based on the starting scene
    /// </summary>
    private void DetermineInitialOperationalState()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (IsMenuScene(currentScene))
        {
            SetOperationalState(ManagerOperationalState.Menu);
        }
        else
        {
            SetOperationalState(ManagerOperationalState.Gameplay);
        }
    }

    /// <summary>
    /// Discovers and registers all state-aware singletons in the scene.
    /// This finds ANY MonoBehaviour that implements IManagerState, regardless of IManager.
    /// </summary>
    private void DiscoverStateAwareManagers()
    {
        stateAwareManagers.Clear();

        DebugLog("=== DISCOVERING STATE-AWARE MANAGERS ===");

        // Find ALL MonoBehaviours that implement IManagerState
        var allMonoBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

        foreach (var mono in allMonoBehaviours)
        {
            if (mono is IManagerState stateAware)
            {
                // Check if it's a singleton (has DontDestroyOnLoad)
                if (mono.gameObject.scene.name == "DontDestroyOnLoad" ||
                    mono.gameObject.scene.buildIndex == -1)
                {
                    if (!stateAwareManagers.Contains(stateAware))
                    {
                        stateAwareManagers.Add(stateAware);
                        DebugLog($"Registered state-aware manager: {mono.GetType().Name}");
                    }
                }

                if (mono is UIManager uiMgr)
                {
                    if (!stateAwareManagers.Contains(stateAware))
                    {
                        stateAwareManagers.Add(stateAware);
                        DebugLog($"Registered state-aware manager: {mono.GetType().Name}");
                    }
                }
            }
        }

        DebugLog($"Total state-aware managers discovered: {stateAwareManagers.Count}");
    }

    /// <summary>
    /// Updates operational state when a new scene loads
    /// </summary>
    private void UpdateOperationalStateForScene(string sceneName)
    {
        ManagerOperationalState targetState = IsMenuScene(sceneName)
            ? ManagerOperationalState.Menu
            : ManagerOperationalState.Gameplay;

        if (targetState != currentOperationalState)
        {
            DebugLog($"Scene '{sceneName}' requires state change: {currentOperationalState} -> {targetState}");
            SetOperationalState(targetState);
        }
    }

    /// <summary>
    /// Checks if a scene name is a menu scene (non-gameplay)
    /// </summary>
    private bool IsMenuScene(string sceneName)
    {
        // Add any menu scene names here
        switch (sceneName)
        {
            case "MainMenu":
                return true;
            case "Credits":
                return true;
        }

        return false;
    }

    /// <summary>
    /// Sets the operational state for all state-aware managers.
    /// Transitions through Transition state for clean state changes.
    /// </summary>
    public void SetOperationalState(ManagerOperationalState newState)
    {
        if (newState == currentOperationalState)
        {
            DebugLog($"Already in {newState} state");
            return;
        }

        DebugLog($"=== OPERATIONAL STATE CHANGE: {currentOperationalState} -> {newState} ===");

        // Transition through Transition state if not already transitioning
        if (currentOperationalState != ManagerOperationalState.Transition && newState != ManagerOperationalState.Transition)
        {
            DebugLog("Entering Transition state first...");
            TransitionToState(ManagerOperationalState.Transition);
        }

        // Now transition to target state
        TransitionToState(newState);

        DebugLog($"Operational state change complete: Now in {currentOperationalState} state");
    }

    /// <summary>
    /// Performs the actual state transition for all state-aware managers
    /// </summary>
    private void TransitionToState(ManagerOperationalState newState)
    {
        ManagerOperationalState previousState = currentOperationalState;
        currentOperationalState = newState;

        // Notify all state-aware persistent managers
        NotifyManagersOfStateChange(newState);

        // Fire event for external systems
        OnOperationalStateChanged?.Invoke(newState);

        DebugLog($"Transitioned from {previousState} to {newState}");
    }

    /// <summary>
    /// Notifies all state-aware managers about the state change.
    /// Now discovers managers dynamically to catch ALL IManagerState implementations.
    /// </summary>
    private void NotifyManagersOfStateChange(ManagerOperationalState newState)
    {
        // Rediscover state-aware managers before notifying (catches any new singletons)
        DiscoverStateAwareManagers();

        int notifiedCount = 0;

        // Notify all state-aware managers
        foreach (var stateAwareManager in stateAwareManagers)
        {
            try
            {
                stateAwareManager.SetOperationalState(newState);
                notifiedCount++;

                // Get the actual MonoBehaviour for logging
                if (stateAwareManager is MonoBehaviour mono)
                {
                    DebugLog($"Notified {mono.GetType().Name} of state change to {newState}");
                }
            }
            catch (System.Exception e)
            {
                if (stateAwareManager is MonoBehaviour mono)
                {
                    Debug.LogError($"Failed to notify {mono.GetType().Name} of state change: {e.Message}");
                }
            }
        }

        DebugLog($"Notified {notifiedCount} state-aware managers of transition to {newState}");
    }

    /// <summary>
    /// Returns to main menu from gameplay
    /// </summary>
    [Button("Return to Main Menu")]
    public void ReturnToMainMenu()
    {
        DebugLog("Returning to main menu...");

        // Transition to menu state
        SetOperationalState(ManagerOperationalState.Transition);
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    #endregion

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

        // STEP 3: Update tracking
        UpdateManagerCounts();

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

        // InputManager initialization
        InitializeInputManager();

        InitializeAudioManager();

        DebugLog($"Initialized {persistentManagers.Count} persistent managers");
    }

    /// <summary>
    /// Initialize or refresh InputManager
    /// </summary>
    private void InitializeInputManager()
    {
        if (InputManager.Instance == null)
        {
            DebugLog("Creating InputManager singleton");
            var inputManagerGO = FindFirstObjectByType<InputManager>();
            if (inputManagerGO == null)
            {
                Debug.LogWarning("No InputManager found in scene! You need to add one.");
                inputManagerReference = null;
                return;
            }
            else
            {
                inputManagerGO.Initialize();
            }
        }
        else
        {
            DebugLog("InputManager singleton already exists - refreshing");
            InputManager.Instance.RefreshReferences();
        }

        // Update reference and add to persistent managers
        inputManagerReference = InputManager.Instance;
        if (inputManagerReference != null)
        {
            if (!persistentManagers.Contains(inputManagerReference))
            {
                persistentManagers.Add(inputManagerReference);
            }
            DebugLog("InputManager ready");
        }
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

        if (uiManager != null) sceneBasedManagers.Add(uiManager);

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

        // STEP 4: Update tracking
        UpdateManagerCounts();

        OnManagersRefreshed?.Invoke();
        DebugLog("Manager refresh complete");
    }

    /// <summary>
    /// Handles refresh for persistent singleton managers
    /// </summary>
    private void RefreshPersistentManagers()
    {
        DebugLog("Refreshing persistent managers");

        // Refresh InputManager
        if (InputManager.Instance != null)
        {
            try
            {
                InputManager.Instance.RefreshReferences();
                inputManagerReference = InputManager.Instance;
                DebugLog("Refreshed InputManager singleton");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to refresh InputManager: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("InputManager singleton is null during refresh!");
            inputManagerReference = null;
        }

        // Update persistent managers list
        persistentManagers.Clear();
        if (inputManagerReference != null) persistentManagers.Add(inputManagerReference);
    }

    /// <summary>
    /// Updates manager count tracking for inspector display
    /// </summary>
    private void UpdateManagerCounts()
    {
        persistentManagerCount = persistentManagers.Count;
        sceneBasedManagerCount = sceneBasedManagers.Count;
        totalManagedManagers = allManagers.Count;
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

    /// <summary>
    /// Manually triggers manager reference refresh with singleton support
    /// </summary>
    [Button]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
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

        // In menu state, PlayerManager and PlayerStateManager may not exist - this is OK
        if (currentOperationalState == ManagerOperationalState.Menu)
        {
            return inputManagerReady; // Only need input for menus
        }


        return inputManagerReady;
    }

    /// <summary>
    /// Returns detailed debug info about all manager states
    /// </summary>
    [Button]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugManagerStates()
    {
        DebugLog("=== GAMEMANAGER DEBUG INFO ===");
        DebugLog($"Operational State: {currentOperationalState}");
        DebugLog($"Total Managers: {totalManagedManagers}");
        DebugLog($"Persistent Managers: {persistentManagerCount}");
        DebugLog($"Scene-Based Managers: {sceneBasedManagerCount}");
        DebugLog($"State-Aware Managers: {stateAwareManagers.Count}");
        DebugLog("");

        // Persistent Managers
        DebugLog("=== PERSISTENT MANAGERS (IManager) ===");
        DebugLog($"InputManager: {(InputManager.Instance != null ? "Available" : "NULL")}");
        if (InputManager.Instance != null)
        {
            DebugLog($"  - Initialized: {InputManager.Instance.IsProperlyInitialized}");
            if (InputManager.Instance is IManagerState stateAware)
            {
                DebugLog($"  - Operational State: {stateAware.CurrentOperationalState}");
            }
        }

        // State-Aware Managers
        DebugLog("");
        DebugLog("=== STATE-AWARE MANAGERS (IManagerState) ===");
        foreach (var stateManager in stateAwareManagers)
        {
            if (stateManager is MonoBehaviour mono)
            {
                DebugLog($"{mono.GetType().Name}:");
                DebugLog($"  - Operational State: {stateManager.CurrentOperationalState}");
                DebugLog($"  - Can Operate: {stateManager.CanOperateInCurrentState()}");
            }
        }

        // Scene-Based Managers
        DebugLog("");
        DebugLog("=== SCENE-BASED MANAGERS ===");
        DebugLog($"UIManager: {(uiManager != null ? "Available" : "NULL")}");
        // DebugLog($"TimeManager: {(timeManager != null ? "Available" : "NULL")}");
        // DebugLog($"WeatherManager: {(weatherManager != null ? "Available" : "NULL")}");
        DebugLog("==============================");
    }

    /// <summary>
    /// Debug method to force refresh all persistent managers
    /// </summary>
    [Button]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugRefreshPersistentManagers()
    {
        RefreshPersistentManagers();
        DebugLog("Persistent managers manually refreshed");
    }

    /// <summary>
    /// Get a summary of manager readiness for external systems
    /// </summary>
    public string GetManagerReadinessSummary()
    {
        var summary = new System.Text.StringBuilder();
        summary.AppendLine($"Operational State: {currentOperationalState}");
        summary.AppendLine($"Total Managers: {totalManagedManagers}");
        summary.AppendLine($"All Managers Ready: {AreManagersReady()}");
        summary.AppendLine($"InputManager Ready: {InputManager.Instance != null && InputManager.Instance.IsProperlyInitialized}");

        return summary.ToString();
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