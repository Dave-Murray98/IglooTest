using System;
using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// UPDATED: InputManager with IManagerState implementation for menu/gameplay state awareness.
/// Now properly adapts behavior based on operational state:
/// - Menu State: Only UI/menu navigation active
/// - Gameplay State: Full input functionality
/// - Transition State: Minimal operations during state changes
/// </summary>
public class InputManager : MonoBehaviour, IManager, IManagerState
{
    public static InputManager Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    #region Fields
    [Header("Input Actions")]
    public InputActionAsset inputActions;

    [Header("UI Actions")]
    private InputAction pauseAction;

    [Header("Core Movement Actions")]
    private InputAction moveAction;
    private InputAction lookAction;

    #endregion

    #region Public Properties
    public Vector2 MovementInput { get; private set; }
    public Vector2 LookInput { get; private set; }

    #endregion

    #region Events


    // Gameplay action events (primary, secondary, reload)
    public event Action OnPrimaryActionPressed;
    public event Action OnPrimaryActionReleased;
    public event Action OnSecondaryActionPressed;
    public event Action OnSecondaryActionReleased;
    public static event Action<InputManager> OnInputManagerReady;

    #endregion

    // Action maps
    private InputActionMap uiActionMap;
    private InputActionMap coreMovementActionMap;

    // State tracking
    private bool gameplayInputEnabled = true;
    private bool isCleanedUp = false;
    private bool isFullyInitialized = false;

    // IManagerState implementation
    [ShowInInspector] private ManagerOperationalState operationalState = ManagerOperationalState.Gameplay;
    [ShowInInspector, ReadOnly] public ManagerOperationalState CurrentOperationalState => operationalState;

    // Utility methods
    public bool IsMoving() => MovementInput.magnitude > 0.1f;
    public bool IsLooking() => LookInput.magnitude > 0.1f;
    public bool IsProperlyInitialized => isFullyInitialized && !isCleanedUp;

    #region Singleton Pattern

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // IMMEDIATE SETUP - Don't wait for Initialize()
            SetupInputActionsImmediate();

            DebugLog("[InputManager] Singleton created with immediate input setup");
        }
        else
        {
            DebugLog("[InputManager] Duplicate destroyed");
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Complete the initialization process
        CompleteInitialization();
    }

    #endregion

    #region UPDATED: State-Aware Setup

    /// <summary>
    ///  Sets up input actions immediately in Awake() so input works from frame 1
    /// </summary>
    private void SetupInputActionsImmediate()
    {
        if (inputActions == null)
        {
            Debug.LogError("[InputManager] InputActionAsset is not assigned! Input will not work!");
            return;
        }

        // Get action maps
        uiActionMap = inputActions.FindActionMap("UI");
        coreMovementActionMap = inputActions.FindActionMap("CoreMovement");

        // Validate critical action maps exist
        if (uiActionMap == null)
        {
            Debug.LogError("[InputManager] UI ActionMap not found! Pause won't work!");
            return;
        }

        if (coreMovementActionMap == null)
        {
            Debug.LogError("[InputManager] Core movement ActionMaps not found! Movement won't work!");
            return;
        }

        // Setup actions
        SetupUIInputActions();
        SetupCoreMovementInputActions();

        // Subscribe to events
        SubscribeToInputActions();

        // CRITICAL: Enable essential ActionMaps immediately
        EnableEssentialActionMapsImmediate();

        DebugLog("[InputManager] Immediate input setup complete - Input should work now!");
    }

    /// <summary>
    /// UPDATED: Enables ActionMaps based on current operational state
    /// </summary>
    private void EnableEssentialActionMapsImmediate()
    {
        // UI ActionMap - ALWAYS enabled (needed for pause/menu navigation)
        if (uiActionMap != null)
        {
            uiActionMap.Enable();
            DebugLog("[InputManager] UI ActionMap enabled");
        }

        // Gameplay ActionMaps - only enable if not in menu state
        if (operationalState != ManagerOperationalState.Menu)
        {
            EnableGameplayActionMaps();
        }
        else
        {
            DebugLog("[InputManager] Menu state detected - gameplay inputs disabled");
        }
    }

    /// <summary>
    /// Enables all gameplay-related action maps
    /// </summary>
    private void EnableGameplayActionMaps()
    {
        if (coreMovementActionMap != null)
        {
            coreMovementActionMap.Enable();
            DebugLog("[InputManager] Core Movement ActionMap enabled");
        }
    }

    /// <summary>
    /// Disables all gameplay-related action maps (for menu state)
    /// </summary>
    private void DisableGameplayActionMaps()
    {
        coreMovementActionMap?.Disable();
        DebugLog("[InputManager] All gameplay ActionMaps disabled");
    }

    /// <summary>
    /// Completes initialization after immediate setup
    /// </summary>
    private void CompleteInitialization()
    {
        // Subscribe to game events
        GameEvents.OnGamePaused += DisableCoreGameplayInput;
        GameEvents.OnGameResumed += EnableCoreGameplayInput;

        isFullyInitialized = true;

        DebugLog("[InputManager] Full initialization complete");

        // Notify other systems
        OnInputManagerReady?.Invoke(this);
    }

    #endregion

    #region IManagerState Implementation

    public void SetOperationalState(ManagerOperationalState newState)
    {
        if (newState == operationalState)
        {
            DebugLog($"Already in {newState} state");
            return;
        }

        DebugLog($"Transitioning from {operationalState} to {newState}");

        var previousState = operationalState;
        operationalState = newState;

        // Handle state transitions
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

        DebugLog($"State transition complete: {previousState} -> {newState}");
    }

    public void OnEnterMenuState()
    {
        DebugLog("=== ENTERING MENU STATE ===");

        // Disable all gameplay input
        DisableGameplayActionMaps();

        if (GameManager.Instance.uiManager != null)
            GameManager.Instance.uiManager.ToggleUIMenuModeInput(true);

        // Ensure UI ActionMap is disabled (so the player can't pause the game from the menu)
        if (uiActionMap != null && !uiActionMap.enabled)
        {
            uiActionMap.Disable();
        }

        // Clear any held input states
        ClearAllInputStates();

        gameplayInputEnabled = false;

        DebugLog("Menu state entered - only UI input active");
    }

    public void OnEnterGameplayState()
    {
        DebugLog("=== ENTERING GAMEPLAY STATE ===");

        // Enable all gameplay ActionMaps
        EnableGameplayActionMaps();

        // Ensure UI ActionMap is still enabled for pause functionality
        if (uiActionMap != null && !uiActionMap.enabled)
        {
            uiActionMap.Enable();
        }

        gameplayInputEnabled = true;

        DebugLog("Gameplay state entered - all input active");
    }

    public void OnEnterTransitionState()
    {
        DebugLog("=== ENTERING TRANSITION STATE ===");

        // During transitions, minimize input processing
        // Keep UI active for potential loading screens
        DisableGameplayActionMaps();

        // Clear input states
        ClearAllInputStates();

        DebugLog("Transition state entered - minimal input active");
    }

    public bool CanOperateInCurrentState()
    {
        // InputManager can always operate (menus need input too)
        // But gameplay inputs are restricted in menu state
        return isFullyInitialized && !isCleanedUp;
    }

    /// <summary>
    /// Clears all input state flags
    /// </summary>
    private void ClearAllInputStates()
    {
        MovementInput = Vector2.zero;
        LookInput = Vector2.zero;

        DebugLog("All input states cleared");
    }

    #endregion

    #region IManager Implementation

    public void Initialize()
    {
        if (isCleanedUp)
        {
            DebugLog("[InputManager] Reinitializing after cleanup");
            isCleanedUp = false;
            SetupInputActionsImmediate();
        }

        if (!isFullyInitialized)
        {
            CompleteInitialization();
        }

        DebugLog("[InputManager] Initialize called - already set up in Awake()");
    }

    public void RefreshReferences()
    {
        if (isCleanedUp || !isFullyInitialized)
        {
            DebugLog("[InputManager] Skipping RefreshReferences - not properly initialized");
            return;
        }

        DebugLog("[InputManager] RefreshReferences - ensuring ActionMaps are enabled");

        // Re-enable essential ActionMaps
        EnableEssentialActionMapsImmediate();

        // Notify systems that we're ready
        OnInputManagerReady?.Invoke(this);
    }

    public void Cleanup()
    {
        DebugLog("[InputManager] Starting cleanup");
        isCleanedUp = true;
        isFullyInitialized = false;

        // Clear events
        ClearAllEvents();

        // Unsubscribe from game events
        GameEvents.OnGamePaused -= DisableCoreGameplayInput;
        GameEvents.OnGameResumed -= EnableCoreGameplayInput;

        // Disable and clean up input actions
        DisableAllInputActions();
        UnsubscribeFromInputActions();
    }

    #endregion


    #region Input State Management

    // Disables everything, including movement, etc
    public void DisableCoreGameplayInput()
    {
        if (isCleanedUp) return;

        DebugLog("[InputManager] Disabling gameplay input (keeping UI enabled)");
        gameplayInputEnabled = false;

        // Disable gameplay ActionMaps but KEEP UI enabled
        coreMovementActionMap?.Disable();

        // UI ActionMap stays enabled for pause functionality
        DebugLog("[InputManager] Gameplay input disabled, UI remains active");
    }

    // Re-enables everything
    public void EnableCoreGameplayInput()
    {
        if (isCleanedUp) return;

        DebugLog("[InputManager] Enabling gameplay input");
        gameplayInputEnabled = true;

        // Re-enable all essential ActionMaps
        EnableEssentialActionMapsImmediate();
    }

    /// <summary>
    /// Disables UI input (only used for main menu)
    /// </summary>
    public bool DisableUIInput()
    {
        if (isCleanedUp) return false;

        DebugLog("[InputManager] Disabling UI input");
        uiActionMap?.Disable();
        return true;
    }

    private void DisableAllInputActions()
    {
        uiActionMap?.Disable();
        coreMovementActionMap?.Disable();
    }

    #endregion

    #region Setup Methods

    private void SetupUIInputActions()
    {
        pauseAction = uiActionMap.FindAction("Pause");
        if (pauseAction == null)
        {
            Debug.LogError("[InputManager] Pause action not found in UI ActionMap!");
        }

    }

    private void SetupCoreMovementInputActions()
    {
        moveAction = coreMovementActionMap.FindAction("Move");
        lookAction = coreMovementActionMap.FindAction("Look");
    }


    #endregion

    #region Event Management

    private void ClearAllEvents()
    {
        // FIXED: Clear all events properly
        OnPrimaryActionPressed = null;
        OnPrimaryActionReleased = null;
        OnSecondaryActionPressed = null;
        OnSecondaryActionReleased = null;
    }

    #endregion

    #region Event Subscription

    private void SubscribeToInputActions()
    {
        SubscribeToUIInputActions();
    }

    private void SubscribeToUIInputActions()
    {
        if (pauseAction != null)
        {
            pauseAction.performed += OnPausePerformed;
        }
    }


    private void UnsubscribeFromInputActions()
    {
        // UI actions
        if (pauseAction != null)
        {
            pauseAction.performed -= OnPausePerformed;
        }
    }

    #endregion

    #region Event Handlers

    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;

        DebugLog("[InputManager] Pause input detected!");

        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.isPaused)
                GameManager.Instance.ResumeGame();
            else
                GameManager.Instance.PauseGame();
        }
        else
        {
            Debug.LogWarning("[InputManager] GameManager.Instance is null - cannot handle pause");
        }
    }

    #endregion

    #region Update Loop

    private void Update()
    {
        if (isCleanedUp) return;

        // Update input values
        if (coreMovementActionMap?.enabled == true)
            UpdateCoreMovementInputValues();

        UpdateContextualInputValues();
    }

    private void UpdateCoreMovementInputValues()
    {
        MovementInput = moveAction?.ReadValue<Vector2>().normalized ?? Vector2.zero;
        LookInput = lookAction?.ReadValue<Vector2>().normalized ?? Vector2.zero;
    }

    private void UpdateContextualInputValues()
    {
        // Update gameplay action held states
    }

    #endregion

    private void OnDestroy()
    {
        if (Instance == this)
        {
            DebugLog("[InputManager] Singleton destroyed");
            Instance = null;
        }
        Cleanup();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[InputManager] {message}");
        }
    }
}