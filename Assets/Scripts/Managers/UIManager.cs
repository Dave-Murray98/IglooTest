using System;
using Sirenix.OdinInspector;
using TMPro;
using Unity.VisualScripting;
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
    public PlayerDeathMenu playerDeathMenu;

    [Header("Health UI")]
    public Slider healthBar;
    public TextMeshProUGUI healthText;

    [Header("Stamina UI")]
    public Slider staminaBar;
    public TextMeshProUGUI staminaText;

    [Header("Oxygen UI")]
    public Slider oxygenBar;
    public TextMeshProUGUI oxygenText;
    public GameObject oxygenWarningIcon;

    [Header("Low Oxygen")]
    [SerializeField] private AudioSource uiAudioSource;
    [Tooltip("If true, creates an AudioSource automatically if not assigned")]
    [SerializeField] private bool autoCreateAudioSource = true;

    [Header("Inventory UI References")]
    public GameObject inventoryPanel;
    public bool isInventoryOpen = false;

    [Header("Interaction UI")]
    public InteractionUIManager interactionUIManager;

    [Header("Visual Effects")]
    [SerializeField] private Color staminaNormalColor = Color.cyan;
    [SerializeField] private Color oxygenNormalColor = Color.blue;

    private bool isOxygenDepleted = false;
    private bool isLowOxygenWarningActive = true; // Track warning state

    [Header("Vignettes")]
    public PlayerVignetteManager vignetteManager;

    [Header("Inventory")]
    public InventoryItemVisualCellColorHelper inventoryItemVisualCellColorHelper;

    [Header("Audio")]
    [SerializeField] private AudioClip lowOxygenAlertSound;
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
        SetupAudioSource();
        RefreshReferences();
    }

    /// <summary>
    /// Sets up or creates the UI audio source for sound effects
    /// </summary>
    private void SetupAudioSource()
    {
        if (uiAudioSource == null && autoCreateAudioSource)
        {
            // Create a dedicated audio source for UI sounds
            GameObject audioSourceObj = new GameObject("UI_AudioSource");
            audioSourceObj.transform.SetParent(transform, false);
            uiAudioSource = audioSourceObj.AddComponent<AudioSource>();

            // Configure audio source for UI sounds
            uiAudioSource.playOnAwake = false;
            uiAudioSource.loop = false;
            uiAudioSource.spatialBlend = 0f; // 2D sound

            DebugLog("UI AudioSource created automatically");
        }
    }

    public void RefreshReferences()
    {
        if (!CanOperateInCurrentState())
            return;

        HideLowOxygenWarning();

        DebugLog("UIManager: Refreshing references");

        // Unsubscribe first to prevent duplicates
        UnsubscribeFromEvents();

        // Subscribe to events
        SubscribeToEvents();

        if (pauseMenu != null)
        {
            pauseMenuManager = pauseMenu.GetComponent<PauseMenuManager>();
            pauseMenuManager.Initialize();
            SetUpPauseMenuEvents();
        }

        if (playerDeathMenu != null)
        {
            playerDeathMenu.Initialize();
            SetupDeathMenuEvents();
        }

        if (interactionUIManager == null)
        {
            interactionUIManager = FindFirstObjectByType<InteractionUIManager>();

            // Create interaction UI manager if it doesn't exist
            if (interactionUIManager == null)
            {
                GameObject interactionUIObj = new GameObject("InteractionUIManager");
                interactionUIObj.transform.SetParent(transform, false);
                interactionUIManager = interactionUIObj.AddComponent<InteractionUIManager>();
            }
        }

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }

        if (vignetteManager == null)
        {
            vignetteManager = FindFirstObjectByType<PlayerVignetteManager>();
        }

        // Initialize bar colors
        InitializeBarColors();

        // Update UI with current values
        UpdateUIAfterSceneLoad();
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

    private void SetupDeathMenuEvents()
    {
        if (playerDeathMenu == null)
        {
            Debug.LogError("PlayerDeathMenu is null, cannot set up death menu events");
            return;
        }

        // Unsubscribe first to prevent duplicates
        playerDeathMenu.OnLoadButtonPressed -= OnLoadGameButtonClicked;
        playerDeathMenu.OnReturnToMenuButtonPressed -= OnReturnToMenuButtonClicked;
        playerDeathMenu.OnQuitButtonPressed -= OnQuitButtonClicked;

        // Subscribe to events
        playerDeathMenu.OnLoadButtonPressed += OnLoadGameButtonClicked;
        playerDeathMenu.OnReturnToMenuButtonPressed += OnReturnToMenuButtonClicked;
        playerDeathMenu.OnQuitButtonPressed += OnQuitButtonClicked;
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
        pauseMenuManager.OnSaveButtonPressed -= OnSaveGameButtonClicked;
        pauseMenuManager.OnLoadButtonPressed -= OnLoadGameButtonClicked;
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
        pauseMenuManager.OnSaveButtonPressed += OnSaveGameButtonClicked;
        pauseMenuManager.OnLoadButtonPressed += OnLoadGameButtonClicked;
        pauseMenuManager.OnReturnToMenuButtonPressed += OnReturnToMenuButtonClicked;
        pauseMenuManager.OnQuitButtonPressed += OnQuitButtonClicked;
    }

    /// <summary>
    /// Initialize UI bar colors to normal states
    /// </summary>
    private void InitializeBarColors()
    {
        // Initialize stamina bar color
        if (staminaBar != null)
        {
            var staminaFillImage = staminaBar.fillRect?.GetComponent<Image>();
            if (staminaFillImage != null)
            {
                staminaFillImage.color = staminaNormalColor;
            }
        }

        // Initialize oxygen bar color
        if (oxygenBar != null)
        {
            var oxygenFillImage = oxygenBar.fillRect?.GetComponent<Image>();
            if (oxygenFillImage != null)
            {
                oxygenFillImage.color = oxygenNormalColor;
            }
        }
    }

    /// <summary>
    /// Subscribe to all relevant game events
    /// </summary>
    private void SubscribeToEvents()
    {
        // Health events
        GameEvents.OnPlayerHealthChanged += UpdateHealthBar;

        // Stamina events
        GameEvents.OnPlayerStaminaChanged += UpdateStaminaBar;
        GameEvents.OnPlayerStaminaDepleted += OnStaminaDepleted;
        GameEvents.OnPlayerStaminaRecovered += OnStaminaRecovered;

        // Oxygen events
        GameEvents.OnPlayerOxygenChanged += UpdateOxygenBar;
        GameEvents.OnPlayerOxygenDepleted += OnOxygenDepleted;
        GameEvents.OnPlayerOxygenRecovered += OnOxygenRecovered;

        // NEW: Subscribe to threshold events from PlayerOxygen
        SubscribeToOxygenThresholdEvents();

        // Game state events
        GameEvents.OnGamePaused += ShowPauseMenu;
        GameEvents.OnGameResumed += HidePauseMenu;

        // UI events
        GameEvents.OnInventoryOpened += ShowInventoryPanel;
        GameEvents.OnInventoryClosed += HideInventoryPanel;
    }

    /// <summary>
    /// NEW: Subscribes to low oxygen threshold events from PlayerOxygen component
    /// </summary>
    private void SubscribeToOxygenThresholdEvents()
    {
        PlayerOxygen playerOxygen = GameManager.Instance?.playerManager?.oxygen;
        if (playerOxygen != null)
        {
            playerOxygen.OnOxygenPassedLowThreshold += ShowLowOxygenWarning;
            playerOxygen.OnOxygenReturnedToNormal += HideLowOxygenWarning;
            DebugLog("Subscribed to oxygen threshold events");
        }
        else
        {
            Debug.LogError("PlayerOxygen not found - cannot subscribe to threshold events");
        }
    }

    /// <summary>
    /// NEW: Unsubscribes from low oxygen threshold events
    /// </summary>
    private void UnsubscribeFromOxygenThresholdEvents()
    {
        var playerOxygen = GameManager.Instance?.playerManager?.oxygen;
        if (playerOxygen != null)
        {
            playerOxygen.OnOxygenPassedLowThreshold -= ShowLowOxygenWarning;
            playerOxygen.OnOxygenReturnedToNormal -= HideLowOxygenWarning;
            DebugLog("Unsubscribed from oxygen threshold events");
        }
    }

    /// <summary>
    /// Unsubscribe from all game events
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        // Health events
        GameEvents.OnPlayerHealthChanged -= UpdateHealthBar;

        // Stamina events
        GameEvents.OnPlayerStaminaChanged -= UpdateStaminaBar;
        GameEvents.OnPlayerStaminaDepleted -= OnStaminaDepleted;
        GameEvents.OnPlayerStaminaRecovered -= OnStaminaRecovered;

        // Oxygen events
        GameEvents.OnPlayerOxygenChanged -= UpdateOxygenBar;
        GameEvents.OnPlayerOxygenDepleted -= OnOxygenDepleted;
        GameEvents.OnPlayerOxygenRecovered -= OnOxygenRecovered;

        // NEW: Unsubscribe from threshold events
        UnsubscribeFromOxygenThresholdEvents();

        // Game state events
        GameEvents.OnGamePaused -= ShowPauseMenu;
        GameEvents.OnGameResumed -= HidePauseMenu;

        // UI events
        GameEvents.OnInventoryOpened -= ShowInventoryPanel;
        GameEvents.OnInventoryClosed -= HideInventoryPanel;
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

    public bool ShouldToggleMenuModeInput()
    {
        return IsAnyUiOpen();
    }

    public bool IsAnyUiOpen()
    {
        if (isInventoryOpen)
        {
            DebugLog("IsAnyUiOpen: Inventory is open");
            return true;
        }

        if (StorageContainerUI.Instance != null && StorageContainerUI.Instance.IsOpen)
        {
            DebugLog("IsAnyUiOpen: Storage container is open");
            return true;
        }

        if (PickupOverflowUI.Instance != null && PickupOverflowUI.Instance.IsOpen)
        {
            DebugLog("IsAnyUiOpen: Pickup overflow is open");
            return true;
        }

        DebugLog("IsAnyUiOpen: No UI is open");

        return false;
    }

    public void ToggleUIMenuModeInput(bool inMenuMode)
    {
        if (inMenuMode)
        {
            InputManager.Instance.DisableGameplayInput();
            SetMouseLocked(false);
            if (GameManager.Instance.playerManager.controller != null)
                GameManager.Instance.playerManager.controller.canLook = false;
        }
        else
        {
            InputManager.Instance.EnableGameplayInput();
            SetMouseLocked(true);
            if (GameManager.Instance.playerManager.controller != null)
                GameManager.Instance.playerManager.controller.canLook = true;
        }

        DebugLog($"ToggleUIMenuModeInput: {inMenuMode}");
    }

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

            if (!ShouldToggleMenuModeInput())
            {
                DebugLog("Re-enabling gameplay input after closing pause menu");
                SetMouseLocked(true);
            }

            if (closeMenuSound != null)
            {
                AudioManager.Instance.PlaySound2D(closeMenuSound, AudioCategory.UI);
            }
        }

        pauseMenuManager.CloseSettingsPanel();
    }

    public void ShowPlayerDeathMenu()
    {
        if (playerDeathMenu != null)
        {
            playerDeathMenu.gameObject.SetActive(true);
        }

        GameManager.Instance.PauseGame();
        InputManager.Instance.DisableUIInput();
        InputManager.Instance.DisableCoreGameplayInput();
    }

    public void HidePlayerDeathMenu()
    {
        if (playerDeathMenu != null)
        {
            playerDeathMenu.gameObject.SetActive(false);
        }
    }

    private void ShowInventoryPanel()
    {
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(true);
            isInventoryOpen = true;

            ToggleUIMenuModeInput(true);
            DebugLog("ShowingInventoryPanel, disabling gameplay input");

            if (openMenuSound != null)
            {
                AudioManager.Instance.PlaySound2D(openMenuSound, AudioCategory.UI);
            }
        }
    }

    public void HideInventoryPanel()
    {
        if (inventoryPanel != null)
        {
            DebugLog("HidingInventoryPanel");
            inventoryPanel.SetActive(false);
            isInventoryOpen = false;

            if (!ShouldToggleMenuModeInput())
            {
                DebugLog("Enabling gameplay input");
                ToggleUIMenuModeInput(false);
            }
            else
            {
                DebugLog("Not re-enabling gameplay input");
            }

            if (closeMenuSound != null)
            {
                AudioManager.Instance.PlaySound2D(closeMenuSound, AudioCategory.UI);
            }
        }
    }

    #endregion

    #region Health UI Updates

    /// <summary>
    /// Updates the health bar and text display
    /// </summary>
    private void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (healthBar != null)
        {
            healthBar.value = currentHealth / maxHealth;
        }
        if (healthText != null)
        {
            healthText.text = $"{currentHealth:F0}/{maxHealth:F0}";
        }
    }

    #endregion

    #region Stamina UI Updates

    /// <summary>
    /// Updates the stamina bar and text display
    /// </summary>
    private void UpdateStaminaBar(float currentStamina, float maxStamina)
    {
        if (staminaBar != null)
        {
            staminaBar.value = currentStamina / maxStamina;
        }
        if (staminaText != null)
        {
            staminaText.text = $"{currentStamina:F0}/{maxStamina:F0}";
        }
    }

    /// <summary>
    /// Handles visual feedback when stamina is depleted
    /// </summary>
    private void OnStaminaDepleted()
    {
        DebugLog("[UIManager] Stamina depleted - visual feedback applied");
    }

    /// <summary>
    /// Handles visual feedback when stamina recovers from depletion
    /// </summary>
    private void OnStaminaRecovered()
    {
        // Restore normal stamina bar color
        if (staminaBar != null)
        {
            var fillImage = staminaBar.fillRect?.GetComponent<Image>();
            if (fillImage != null)
            {
                fillImage.color = staminaNormalColor;
            }
        }

        DebugLog("[UIManager] Stamina recovered - normal visuals restored");
    }

    #endregion

    #region Oxygen UI Updates

    /// <summary>
    /// Updates the oxygen bar and text display
    /// </summary>
    public void UpdateOxygenBar(float currentOxygen, float maxOxygen)
    {
        if (oxygenBar != null)
        {
            float oxygenPercentage = currentOxygen / maxOxygen;
            oxygenBar.value = oxygenPercentage;
        }

        if (oxygenText != null)
        {
            oxygenText.text = $"{currentOxygen:F0}/{maxOxygen:F0}";
        }
    }

    /// <summary>
    /// Handles visual feedback when oxygen is depleted
    /// </summary>
    private void OnOxygenDepleted()
    {
        isOxygenDepleted = true;
        DebugLog("[UIManager] Oxygen depleted - CRITICAL visual feedback applied");
    }

    /// <summary>
    /// Handles visual feedback when oxygen recovers from depletion
    /// </summary>
    private void OnOxygenRecovered()
    {
        isOxygenDepleted = false;
        DebugLog("[UIManager] Oxygen recovered - normal visuals restored");
    }

    /// <summary>
    /// NEW: Shows low oxygen warning icon and plays alert sound.
    /// Called via event when oxygen crosses below threshold.
    /// </summary>
    private void ShowLowOxygenWarning()
    {
        Debug.Log("[UIManager] ShowLowOxygenWarning called");

        if (isLowOxygenWarningActive)
        {
            Debug.Log("[UIManager] Low oxygen warning icon already showing, returning");
            return; // Already showing
        }

        isLowOxygenWarningActive = true;

        // Show visual warning
        if (oxygenWarningIcon != null)
        {
            oxygenWarningIcon.SetActive(true);
            DebugLog("Low oxygen warning icon SHOWN");
        }

        // Play audio alert (non-looping)
        PlayLowOxygenAlert();

        // You can add additional effects here:
        // - Start pulsing animation on oxygen bar
        // - Show screen vignette effect
        // - Trigger character breathing sounds
        DebugLog("[UIManager] Low oxygen warning ACTIVATED");
    }

    /// <summary>
    /// NEW: Hides low oxygen warning icon.
    /// Called via event when oxygen crosses back above threshold.
    /// </summary>
    private void HideLowOxygenWarning()
    {
        if (!isLowOxygenWarningActive)
            return; // Already hidden

        isLowOxygenWarningActive = false;

        // Hide visual warning
        if (oxygenWarningIcon != null)
        {
            oxygenWarningIcon.SetActive(false);
            DebugLog("Low oxygen warning icon HIDDEN");
        }

        // Stop any ongoing effects (like animations or vignettes)
        // If you add pulsing animations, stop them here

        DebugLog("[UIManager] Low oxygen warning DEACTIVATED");
    }

    /// <summary>
    /// NEW: Plays the low oxygen alert sound effect.
    /// This is a one-shot sound, not looping.
    /// </summary>
    private void PlayLowOxygenAlert()
    {
        if (lowOxygenAlertSound != null)
            AudioManager.Instance.PlaySound2D(lowOxygenAlertSound, AudioCategory.UI);
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

    public void OnSaveGameButtonClicked()
    {
        if (buttonSound != null)
            AudioManager.Instance.PlaySound2D(buttonSound, AudioCategory.UI);

        SaveManager.Instance.SaveGame();
    }

    public void OnLoadGameButtonClicked()
    {
        if (buttonSound != null)
            AudioManager.Instance.PlaySound2D(buttonSound, AudioCategory.UI);

        StartCoroutine(LoadGameWithPauseHandling());
    }

    private System.Collections.IEnumerator LoadGameWithPauseHandling()
    {
        // Remember if we were paused
        bool wasPaused = GameManager.Instance.isPaused;

        // Temporarily unpause for the load operation
        if (wasPaused)
        {
            Time.timeScale = 1f; // Allow coroutines to run
        }

        // Start the load operation
        SaveManager.Instance.LoadGame();

        // Wait a frame to let the load start
        yield return null;
    }

    #endregion

    /// <summary>
    /// Updates all UI elements after a scene load
    /// </summary>
    public void UpdateUIAfterSceneLoad()
    {
        if (GameManager.Instance?.playerManager != null && GameManager.Instance?.playerData != null)
        {
            // Update health UI
            UpdateHealthBar(GameManager.Instance.playerManager.health.currentHealth, GameManager.Instance.playerData.maxHealth);

            // Update stamina UI
            UpdateStaminaBar(GameManager.Instance.playerManager.stamina.currentStamina, GameManager.Instance.playerData.maxStamina);

            // Update oxygen UI
            UpdateOxygenBar(GameManager.Instance.playerManager.oxygen.currentOxygen, GameManager.Instance.playerData.maxOxygen);

            if (GameManager.Instance.playerManager.health.IsDead)
            {
                ShowPlayerDeathMenu();
            }
            else
            {
                HidePlayerDeathMenu();
            }
        }

        SetMouseLocked(true);
    }

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