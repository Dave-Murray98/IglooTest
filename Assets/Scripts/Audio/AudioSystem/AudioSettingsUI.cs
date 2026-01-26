using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// UI controller for the audio settings menu. Connects UI sliders to the AudioManager
/// and provides real-time feedback. Automatically updates when settings are loaded from save files.
/// 
/// Setup Instructions:
/// 1. Create sliders for each audio category in your UI
/// 2. Assign the sliders to the corresponding fields in the Inspector
/// 3. Optionally assign text labels to show percentage values
/// 4. The controller will automatically sync with AudioManager on start
/// </summary>
public class AudioSettingsUI : MonoBehaviour
{
    [Header("Volume Sliders")]
    [Tooltip("Master volume slider (affects all categories)")]
    [SerializeField] private Slider masterVolumeSlider;

    [Tooltip("Ambience category volume slider")]
    [SerializeField] private Slider ambienceVolumeSlider;

    [Tooltip("Player SFX category volume slider")]
    [SerializeField] private Slider playerSFXVolumeSlider;

    [Tooltip("Enemy SFX category volume slider")]
    [SerializeField] private Slider enemySFXVolumeSlider;

    [Tooltip("Dialogue category volume slider")]
    [SerializeField] private Slider dialogueVolumeSlider;

    [Tooltip("UI category volume slider")]
    [SerializeField] private Slider uiVolumeSlider;

    [Tooltip("Music category volume slider")]
    [SerializeField] private Slider musicVolumeSlider;

    [Header("Volume Labels (Optional)")]
    [Tooltip("Optional text label for master volume percentage")]
    [SerializeField] private TextMeshProUGUI masterVolumeLabel;

    [SerializeField] private TextMeshProUGUI ambienceVolumeLabel;
    [SerializeField] private TextMeshProUGUI playerSFXVolumeLabel;
    [SerializeField] private TextMeshProUGUI enemySFXVolumeLabel;
    [SerializeField] private TextMeshProUGUI dialogueVolumeLabel;
    [SerializeField] private TextMeshProUGUI uiVolumeLabel;
    [SerializeField] private TextMeshProUGUI musicVolumeLabel;

    [Header("Back Button")]
    public Button backButton;

    [Header("Test Sounds (Optional)")]
    [Tooltip("Sound to play when testing player SFX slider")]
    [SerializeField] private AudioClip testPlayerSFXClip;

    [Tooltip("Sound to play when testing enemy SFX slider")]
    [SerializeField] private AudioClip testEnemySFXClip;

    [Tooltip("Sound to play when testing UI slider")]
    [SerializeField] private AudioClip testUIClip;

    [Header("Settings")]
    [SerializeField] private bool playTestSoundOnSliderChange = false;
    [SerializeField] private float testSoundCooldown = 1f;

    private float lastTestSoundTime;
    private bool isInitializing = false;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    public void Initialize()
    {
        InitializeUI();
        SubscribeToEvents();
    }

    private void OnEnable()
    {
        // Refresh UI when panel is enabled (in case settings changed)
        if (AudioManager.Instance != null)
        {
            RefreshAllSliders();
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    /// <summary>
    /// Initializes all sliders with current AudioManager settings
    /// </summary>
    private void InitializeUI()
    {
        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("[AudioSettingsUI] AudioManager not found!");
            return;
        }

        DebugLog("Initializing Audio Settings UI...");

        isInitializing = true;

        // Setup slider listeners
        SetupSliderListener(masterVolumeSlider, OnMasterVolumeChanged);
        SetupSliderListener(ambienceVolumeSlider, OnAmbienceVolumeChanged);
        SetupSliderListener(playerSFXVolumeSlider, OnPlayerSFXVolumeChanged);
        SetupSliderListener(enemySFXVolumeSlider, OnEnemySFXVolumeChanged);
        SetupSliderListener(dialogueVolumeSlider, OnDialogueVolumeChanged);
        SetupSliderListener(uiVolumeSlider, OnUIVolumeChanged);
        SetupSliderListener(musicVolumeSlider, OnMusicVolumeChanged);

        // Load current values from AudioManager
        RefreshAllSliders();

        isInitializing = false;

        DebugLog("Finished initializing Audio Settings UI.");
    }

    /// <summary>
    /// Sets up a slider with a listener and ensures it has proper range
    /// </summary>
    private void SetupSliderListener(Slider slider, Action<float> callback)
    {
        if (slider == null) return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;

        slider.onValueChanged.AddListener(value => callback?.Invoke(value));
    }

    /// <summary>
    /// Subscribes to AudioManager events for automatic UI updates
    /// </summary>
    private void SubscribeToEvents()
    {
        if (AudioManager.Instance == null) return;

        AudioManager.Instance.OnMasterVolumeChanged += OnAudioManagerMasterVolumeChanged;
        AudioManager.Instance.OnCategoryVolumeChanged += OnAudioManagerCategoryVolumeChanged;
    }

    /// <summary>
    /// Unsubscribes from AudioManager events
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        if (AudioManager.Instance == null) return;

        AudioManager.Instance.OnMasterVolumeChanged -= OnAudioManagerMasterVolumeChanged;
        AudioManager.Instance.OnCategoryVolumeChanged -= OnAudioManagerCategoryVolumeChanged;
    }

    /// <summary>
    /// Refreshes all slider values to match current AudioManager settings
    /// </summary>
    private void RefreshAllSliders()
    {
        isInitializing = true;

        if (AudioManager.Instance != null)
        {
            UpdateSliderValue(masterVolumeSlider, AudioManager.Instance.GetMasterVolume(), masterVolumeLabel);
            UpdateSliderValue(ambienceVolumeSlider, AudioManager.Instance.GetCategoryVolume(AudioCategory.Ambience), ambienceVolumeLabel);
            UpdateSliderValue(playerSFXVolumeSlider, AudioManager.Instance.GetCategoryVolume(AudioCategory.PlayerSFX), playerSFXVolumeLabel);
            UpdateSliderValue(enemySFXVolumeSlider, AudioManager.Instance.GetCategoryVolume(AudioCategory.EnemySFX), enemySFXVolumeLabel);
            UpdateSliderValue(dialogueVolumeSlider, AudioManager.Instance.GetCategoryVolume(AudioCategory.Dialogue), dialogueVolumeLabel);
            UpdateSliderValue(uiVolumeSlider, AudioManager.Instance.GetCategoryVolume(AudioCategory.UI), uiVolumeLabel);
            UpdateSliderValue(musicVolumeSlider, AudioManager.Instance.GetCategoryVolume(AudioCategory.Music), musicVolumeLabel);
        }

        isInitializing = false;
    }

    /// <summary>
    /// Updates a slider value and its associated label
    /// </summary>
    private void UpdateSliderValue(Slider slider, float value, TextMeshProUGUI label)
    {
        if (slider != null)
        {
            slider.value = value;
        }

        UpdateVolumeLabel(label, value);
    }

    /// <summary>
    /// Updates a volume label to show percentage
    /// </summary>
    private void UpdateVolumeLabel(TextMeshProUGUI label, float volume)
    {
        if (label != null)
        {
            label.text = $"{Mathf.RoundToInt(volume * 100)}%";
        }
    }

    #region Slider Callbacks

    private void OnMasterVolumeChanged(float value)
    {
        if (isInitializing) return;

        AudioManager.Instance?.SetMasterVolume(value);
        UpdateVolumeLabel(masterVolumeLabel, value);
    }

    private void OnAmbienceVolumeChanged(float value)
    {
        if (isInitializing) return;

        AudioManager.Instance?.SetCategoryVolume(AudioCategory.Ambience, value);
        UpdateVolumeLabel(ambienceVolumeLabel, value);
    }

    private void OnPlayerSFXVolumeChanged(float value)
    {
        if (isInitializing) return;

        AudioManager.Instance?.SetCategoryVolume(AudioCategory.PlayerSFX, value);
        UpdateVolumeLabel(playerSFXVolumeLabel, value);

        PlayTestSound(testPlayerSFXClip, AudioCategory.PlayerSFX);
    }

    private void OnEnemySFXVolumeChanged(float value)
    {
        if (isInitializing) return;

        AudioManager.Instance?.SetCategoryVolume(AudioCategory.EnemySFX, value);
        UpdateVolumeLabel(enemySFXVolumeLabel, value);

        PlayTestSound(testEnemySFXClip, AudioCategory.EnemySFX);
    }

    private void OnDialogueVolumeChanged(float value)
    {
        if (isInitializing) return;

        AudioManager.Instance?.SetCategoryVolume(AudioCategory.Dialogue, value);
        UpdateVolumeLabel(dialogueVolumeLabel, value);
    }

    private void OnUIVolumeChanged(float value)
    {
        if (isInitializing) return;

        AudioManager.Instance?.SetCategoryVolume(AudioCategory.UI, value);
        UpdateVolumeLabel(uiVolumeLabel, value);

        PlayTestSound(testUIClip, AudioCategory.UI);
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (isInitializing) return;

        AudioManager.Instance?.SetCategoryVolume(AudioCategory.Music, value);
        UpdateVolumeLabel(musicVolumeLabel, value);
    }

    #endregion

    #region AudioManager Event Handlers

    private void OnAudioManagerMasterVolumeChanged(float volume)
    {
        // Update UI if volume was changed from code
        if (masterVolumeSlider != null && !isInitializing && Mathf.Abs(masterVolumeSlider.value - volume) > 0.001f)
        {
            isInitializing = true;
            UpdateSliderValue(masterVolumeSlider, volume, masterVolumeLabel);
            isInitializing = false;
        }
    }

    private void OnAudioManagerCategoryVolumeChanged(AudioCategory category, float volume)
    {
        // Update UI if volume was changed from code
        if (isInitializing) return;

        isInitializing = true;

        Slider slider = category switch
        {
            AudioCategory.Ambience => ambienceVolumeSlider,
            AudioCategory.PlayerSFX => playerSFXVolumeSlider,
            AudioCategory.EnemySFX => enemySFXVolumeSlider,
            AudioCategory.Dialogue => dialogueVolumeSlider,
            AudioCategory.UI => uiVolumeSlider,
            AudioCategory.Music => musicVolumeSlider,
            _ => null
        };

        TextMeshProUGUI label = category switch
        {
            AudioCategory.Ambience => ambienceVolumeLabel,
            AudioCategory.PlayerSFX => playerSFXVolumeLabel,
            AudioCategory.EnemySFX => enemySFXVolumeLabel,
            AudioCategory.Dialogue => dialogueVolumeLabel,
            AudioCategory.UI => uiVolumeLabel,
            AudioCategory.Music => musicVolumeLabel,
            _ => null
        };

        if (slider != null && Mathf.Abs(slider.value - volume) > 0.001f)
        {
            UpdateSliderValue(slider, volume, label);
        }

        isInitializing = false;
    }

    #endregion

    #region Test Sounds

    /// <summary>
    /// Plays a test sound when adjusting volume sliders
    /// </summary>
    private void PlayTestSound(AudioClip clip, AudioCategory category)
    {
        if (!playTestSoundOnSliderChange || clip == null || AudioManager.Instance == null) return;

        // Cooldown to prevent too many test sounds
        if (Time.unscaledTime - lastTestSoundTime < testSoundCooldown) return;

        AudioManager.Instance.PlaySound2D(clip, category);
        lastTestSoundTime = Time.unscaledTime;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Resets all audio settings to defaults (button callback)
    /// </summary>
    public void ResetToDefaults()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.ResetToDefaultSettings();
            RefreshAllSliders();
        }
    }

    /// <summary>
    /// Mutes all audio (button callback)
    /// </summary>
    public void MuteAll()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMasterVolume(0f);
        }
    }

    /// <summary>
    /// Unmutes all audio (button callback)
    /// </summary>
    public void UnmuteAll()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMasterVolume(1f);
        }
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[AudioSettingsUI] {message}");
    }
}