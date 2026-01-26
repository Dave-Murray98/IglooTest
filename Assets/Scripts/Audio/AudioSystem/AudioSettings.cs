using Sirenix.OdinInspector;
using UnityEngine;

public class AudioSettings : SaveComponentBase, IPlayerDependentSaveable
{
    [Header("Audio Settings")]
    [SerializeField] private AudioSettingsData currentSettings;

    [Header("Auto-Reference Settings")]
    [SerializeField] private bool autoFindAudioManager = true;

    // Track if we've loaded from save to prevent premature application
    private bool hasLoadedFromSave = false;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        saveID = "AudioSettings_Main";
        autoGenerateID = false;
        base.Awake();

        // Initialize with default settings if needed
        if (currentSettings == null)
        {
            currentSettings = new AudioSettingsData();
        }
    }

    private void Start()
    {
        // FIXED: Only apply settings in Start if we haven't loaded from save
        // The load process will call ApplySettingsToAudioManager itself
        if (!hasLoadedFromSave && autoFindAudioManager && AudioManager.Instance != null)
        {
            DebugLog("Start: No save loaded yet, applying current/default settings");
            ApplySettingsToAudioManager();
        }
        else
        {
            DebugLog("Start: Save data will be loaded, skipping default application");
        }
    }

    public override object GetDataToSave()
    {
        // Get current settings from AudioManager if available
        if (AudioManager.Instance != null)
        {
            currentSettings = AudioManager.Instance.GetCurrentSettings();
        }

        DebugLog($"Saving audio settings: {currentSettings.GetDebugInfo()}");
        return new AudioSettingsData(currentSettings);
    }

    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog($"Extracting audio settings from save container (Type: {saveContainer?.GetType().Name ?? "null"})");

        // // Case 1: Direct AudioSettingsData
        // if (saveContainer is AudioSettingsData audioData)
        // {
        //     DebugLog($"✓ Direct AudioSettingsData: {audioData.GetDebugInfo()}");
        //     return audioData;
        // }

        // // Case 2: PlayerPersistentData (modular storage)
        // else if (saveContainer is PlayerPersistentData persistentData)
        // {
        //     DebugLog("Extracting from PlayerPersistentData");
        //     AudioSettingsData extractedData = persistentData.GetComponentData<AudioSettingsData>(SaveID);
        //     if (extractedData != null)
        //     {
        //         DebugLog($"✓ Extracted from PlayerPersistentData: {extractedData.GetDebugInfo()}");
        //         return extractedData;
        //     }

        //     DebugLog("⚠ No audio settings found in PlayerPersistentData, using defaults");
        //     return new AudioSettingsData();
        // }

        // // Case 3: PlayerSaveData (contains customStats dictionary)
        if (saveContainer is PlayerSaveData playerSaveData)
        {
            DebugLog($"Extracting from PlayerSaveData (CustomStats count: {playerSaveData.CustomDataCount})");

            // Try to get audio settings from customStats dictionary
            var audioSettingsData = playerSaveData.GetCustomData<AudioSettingsData>(SaveID);
            if (audioSettingsData != null)
            {
                DebugLog($"✓ Found in PlayerSaveData.customStats: {audioSettingsData.GetDebugInfo()}");
                return audioSettingsData;
            }

            // Debug: List what IS in customStats
            DebugLog("Available keys in customStats:");
            foreach (var key in playerSaveData.GetCustomDataKeys())
            {
                var data = playerSaveData.GetCustomData<object>(key);
                DebugLog($"  - {key}: {data?.GetType().Name ?? "null"}");
            }

            DebugLog("⚠ No audio settings found in PlayerSaveData.customStats, using defaults");
            return new AudioSettingsData();
        }

        Debug.LogError($"[AudioSettings] Unexpected save data type: {saveContainer?.GetType().Name ?? "null"}");
        return new AudioSettingsData();
    }

    #region IPlayerDependentSaveable Implementation

    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null)
        {
            DebugLog("Unified data is null, returning default settings");
            return new AudioSettingsData();
        }

        DebugLog("Extracting from unified save using modular interface");

        var audioData = unifiedData.GetComponentData<AudioSettingsData>(SaveID);
        if (audioData != null)
        {
            DebugLog($"Found audio settings: {audioData.GetDebugInfo()}");
            return audioData;
        }

        DebugLog("No audio settings in unified save, using defaults");
        return new AudioSettingsData();
    }

    public object CreateDefaultData()
    {
        DebugLog("Creating default audio settings for new game");
        return new AudioSettingsData();
    }

    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is AudioSettingsData audioData && unifiedData != null)
        {
            DebugLog($"Contributing audio settings to unified save: {audioData.GetDebugInfo()}");
            unifiedData.SetComponentData(SaveID, audioData);
        }
        else
        {
            DebugLog($"Invalid data for contribution - expected AudioSettingsData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (data is not AudioSettingsData audioData)
        {
            DebugLog($"Invalid save data type - expected AudioSettingsData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        DebugLog($"=== AUDIO SETTINGS RESTORATION (Context: {context}) ===");
        DebugLog($"Loading settings: {audioData.GetDebugInfo()}");

        // Validate data
        if (!audioData.IsValid())
        {
            Debug.LogWarning("[AudioSettings] Invalid audio data detected, using defaults");
            audioData = new AudioSettingsData();
        }

        // FIXED: Store the settings AND mark that we've loaded from save
        currentSettings = new AudioSettingsData(audioData);
        hasLoadedFromSave = true;

        // FIXED: Force immediate application to AudioManager
        DebugLog("Forcing immediate application of loaded settings to AudioManager");
        ApplySettingsToAudioManager();

        DebugLog($"Audio settings restoration complete for context: {context}");
    }

    private void ApplySettingsToAudioManager()
    {
        if (AudioManager.Instance == null)
        {
            DebugLog("AudioManager not available, settings will be applied when it becomes available");
            return;
        }

        // FIXED: Add detailed logging to verify what's being applied
        DebugLog($"Applying settings to AudioManager: {currentSettings.GetDebugInfo()}");
        AudioManager.Instance.ApplySettings(currentSettings);

        // FIXED: Verify application worked by reading back
        var verifySettings = AudioManager.Instance.GetCurrentSettings();
        DebugLog($"Verification - AudioManager now has: {verifySettings.GetDebugInfo()}");
    }

    public override void OnBeforeSave()
    {
        DebugLog("Preparing audio settings for save");

        // Ensure we have the latest settings from AudioManager
        if (AudioManager.Instance != null)
        {
            currentSettings = AudioManager.Instance.GetCurrentSettings();
        }
    }

    public override void OnAfterLoad()
    {
        DebugLog("Audio settings load completed");

        // FIXED: Ensure settings are applied again after full load sequence
        ApplySettingsToAudioManager();
    }

    public AudioSettingsData GetCurrentSettings()
    {
        return new AudioSettingsData(currentSettings);
    }

    public void UpdateSettings(AudioSettingsData newSettings)
    {
        if (newSettings == null || !newSettings.IsValid())
        {
            Debug.LogWarning("[AudioSettings] Attempted to set invalid audio settings");
            return;
        }

        currentSettings = new AudioSettingsData(newSettings);
        ApplySettingsToAudioManager();
        DebugLog($"Settings updated: {currentSettings.GetDebugInfo()}");
    }

    #region Editor Helpers

    [Button("Apply Current Settings to AudioManager")]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void EditorApplySettings()
    {
        ApplySettingsToAudioManager();
        Debug.Log("[AudioSettings] Settings applied to AudioManager from editor");
    }

    [Button("Get Settings from AudioManager")]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void EditorGetSettings()
    {
        if (AudioManager.Instance != null)
        {
            currentSettings = AudioManager.Instance.GetCurrentSettings();
            Debug.Log($"[AudioSettings] Got settings from AudioManager: {currentSettings.GetDebugInfo()}");
        }
        else
        {
            Debug.LogWarning("[AudioSettings] AudioManager not found");
        }
    }

    [Button("Reset to Default Settings")]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void EditorResetSettings()
    {
        currentSettings = new AudioSettingsData();
        ApplySettingsToAudioManager();
        Debug.Log("[AudioSettings] Reset to default settings");
    }

    #endregion
}