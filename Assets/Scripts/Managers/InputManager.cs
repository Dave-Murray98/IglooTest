using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour, IManager
{
    public static InputManager Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    #region Fields
    [Header("Input Actions")]
    public InputActionAsset inputActions;

    [Header("UI Actions")]
    private InputAction pauseAction;

    [Header("Pilot Actions")]
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction surfaceAction;
    private InputAction diveAction;
    #endregion

    #region Public Properties
    public Vector2 MovementInput { get; private set; }
    public Vector2 LookInput { get; private set; }

    public bool SurfacePressed { get; private set; }
    [ShowInInspector, ReadOnly] public bool SurfaceHeld { get; private set; }
    public bool DivePressed { get; private set; }
    [ShowInInspector, ReadOnly] public bool DiveHeld { get; private set; }

    #endregion

    #region Events 
    public static event Action<InputManager> OnInputManagerReady;

    public event Action OnSurfacePressed;
    public event Action OnSurfaceReleased;
    public event Action OnDivePressed;
    public event Action OnDiveReleased;

    #endregion

    // Action maps
    private InputActionMap uiActionMap;
    private InputActionMap pilotActionMap;

    // State tracking
    [ShowInInspector, ReadOnly] private bool isCleanedUp = false;
    private bool isFullyInitialized = false;

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
        pilotActionMap = inputActions.FindActionMap("Pilot");

        // Validate critical action maps exist
        if (uiActionMap == null)
        {
            Debug.LogError("[InputManager] UI ActionMap not found! Pause won't work!");
            return;
        }

        if (pilotActionMap == null)
        {
            Debug.LogError("[InputManager] Core movement ActionMaps not found! Movement won't work!");
            return;
        }

        // Setup actions
        SetupUIInputActions();
        SetupPilotInputActions();

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
        EnableGameplayActionMaps();
    }

    /// <summary>
    /// Enables all gameplay-related action maps
    /// </summary>
    private void EnableGameplayActionMaps()
    {
        if (pilotActionMap != null)
        {
            pilotActionMap.Enable();
            DebugLog("[InputManager] Core Movement ActionMap enabled");
        }
    }

    /// <summary>
    /// Disables all gameplay-related action maps (for menu state)
    /// </summary>
    private void DisableGameplayActionMaps()
    {
        pilotActionMap?.Disable();
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
        UnsubscribeFromPilotInputActions();
    }

    #endregion


    #region Input State Management

    // Disables everything, including movement, etc
    public void DisableCoreGameplayInput()
    {
        if (isCleanedUp) return;

        DebugLog("[InputManager] Disabling gameplay input (keeping UI enabled)");

        // Disable gameplay ActionMaps but KEEP UI enabled
        pilotActionMap?.Disable();

        // UI ActionMap stays enabled for pause functionality
        DebugLog("[InputManager] Gameplay input disabled, UI remains active");
    }

    // Re-enables everything
    public void EnableCoreGameplayInput()
    {
        if (isCleanedUp) return;

        DebugLog("[InputManager] Enabling gameplay input");

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
        pilotActionMap?.Disable();
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

    private void SetupPilotInputActions()
    {
        moveAction = pilotActionMap.FindAction("Move");
        lookAction = pilotActionMap.FindAction("Look");
        surfaceAction = pilotActionMap.FindAction("Surface");
        diveAction = pilotActionMap.FindAction("Dive");
    }


    #endregion

    #region Event Management

    private void ClearAllEvents()
    {
        // Clear all events properly
        OnSurfacePressed = null;
        OnSurfaceReleased = null;
        OnDivePressed = null;
        OnDiveReleased = null;
    }

    #endregion

    #region Event Subscription

    private void SubscribeToInputActions()
    {
        SubscribeToUIInputActions();
        SubscribeToPilotInputActions();
    }

    private void SubscribeToPilotInputActions()
    {
        if (surfaceAction != null)
        {
            surfaceAction.performed += OnSurfacePerformed;
            surfaceAction.canceled += OnSurfaceCanceled;
        }
        if (diveAction != null)
        {
            diveAction.performed += OnDivePerformed;
            diveAction.canceled += OnDiveCanceled;
        }
    }

    private void UnsubscribeFromPilotInputActions()
    {
        if (surfaceAction != null)
        {
            surfaceAction.performed -= OnSurfacePerformed;
            surfaceAction.canceled -= OnSurfaceCanceled;
        }
        if (diveAction != null)
        {
            diveAction.performed -= OnDivePerformed;
            diveAction.canceled -= OnDiveCanceled;
        }
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

    private void OnSurfacePerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        SurfacePressed = true;
        OnSurfacePressed?.Invoke();
        DebugLog("[InputManager] Surface input detected!");
    }

    private void OnSurfaceCanceled(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnSurfaceReleased?.Invoke();
    }

    private void OnDivePerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        DivePressed = true;
        OnDivePressed?.Invoke();
        DebugLog("[InputManager] Dive input detected!");
    }

    private void OnDiveCanceled(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnDiveReleased?.Invoke();
    }
    #endregion

    #region Update Loop

    private void Update()
    {
        if (isCleanedUp) return;

        // Update input values
        if (pilotActionMap?.enabled == true)
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
        SurfaceHeld = surfaceAction?.IsPressed() ?? false;
        DiveHeld = diveAction?.IsPressed() ?? false;
        //DebugLog("[InputManager] Updating contextual input values, SurfaceHeld: " + SurfaceHeld + ", DiveHeld: " + DiveHeld);

        // Reset pressed states after they've been read
        if (SurfacePressed) SurfacePressed = false;
        if (DivePressed) DivePressed = false;
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