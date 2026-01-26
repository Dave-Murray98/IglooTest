using Sirenix.OdinInspector;
using UnityEngine;

public class AudioSettings : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioSettingsData currentSettings;

    [Header("Auto-Reference Settings")]
    [SerializeField] private bool autoFindAudioManager = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;


    private void Awake()
    {
        // Initialize with default settings if needed
        if (currentSettings == null)
        {
            currentSettings = new AudioSettingsData();
        }
    }

    private void Start()
    {
        ApplySettingsToAudioManager();
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

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[AudioSettings] {message}");
        }
    }
}