using UnityEngine;

/// <summary>
/// Serializable data structure for storing audio volume settings.
/// This is what gets saved to disk and loaded back when restoring game state.
/// Each category has its own volume level (0.0 to 1.0).
/// </summary>
[System.Serializable]
public class AudioSettingsData
{
    [Header("Volume Levels (0.0 to 1.0)")]
    [Range(0f, 1f)] public float ambienceVolume = 0.7f;
    [Range(0f, 1f)] public float playerSFXVolume = 1.0f;
    [Range(0f, 1f)] public float enemySFXVolume = 1.0f;
    [Range(0f, 1f)] public float dialogueVolume = 1.0f;
    [Range(0f, 1f)] public float uiVolume = 1.0f;
    [Range(0f, 1f)] public float musicVolume = 0.8f;

    /// <summary>
    /// Master volume that affects all categories
    /// </summary>
    [Range(0f, 1f)] public float masterVolume = 1.0f;

    /// <summary>
    /// Creates default audio settings with balanced volume levels
    /// </summary>
    public AudioSettingsData()
    {
        // Defaults are set in field initializers above
    }

    /// <summary>
    /// Copy constructor for creating independent copies
    /// </summary>
    public AudioSettingsData(AudioSettingsData other)
    {
        if (other == null) return;

        ambienceVolume = other.ambienceVolume;
        playerSFXVolume = other.playerSFXVolume;
        enemySFXVolume = other.enemySFXVolume;
        dialogueVolume = other.dialogueVolume;
        uiVolume = other.uiVolume;
        musicVolume = other.musicVolume;
        masterVolume = other.masterVolume;
    }

    /// <summary>
    /// Gets the volume level for a specific audio category
    /// </summary>
    public float GetCategoryVolume(AudioCategory category)
    {
        return category switch
        {
            AudioCategory.Ambience => ambienceVolume,
            AudioCategory.PlayerSFX => playerSFXVolume,
            AudioCategory.EnemySFX => enemySFXVolume,
            AudioCategory.Dialogue => dialogueVolume,
            AudioCategory.UI => uiVolume,
            AudioCategory.Music => musicVolume,
            _ => 1.0f
        };
    }

    /// <summary>
    /// Sets the volume level for a specific audio category
    /// </summary>
    public void SetCategoryVolume(AudioCategory category, float volume)
    {
        volume = Mathf.Clamp01(volume); // Ensure 0-1 range

        switch (category)
        {
            case AudioCategory.Ambience:
                ambienceVolume = volume;
                break;
            case AudioCategory.PlayerSFX:
                playerSFXVolume = volume;
                break;
            case AudioCategory.EnemySFX:
                enemySFXVolume = volume;
                break;
            case AudioCategory.Dialogue:
                dialogueVolume = volume;
                break;
            case AudioCategory.UI:
                uiVolume = volume;
                break;
            case AudioCategory.Music:
                musicVolume = volume;
                break;
        }
    }

    /// <summary>
    /// Validates that all volume values are within acceptable range
    /// </summary>
    public bool IsValid()
    {
        return masterVolume >= 0f && masterVolume <= 1f &&
               ambienceVolume >= 0f && ambienceVolume <= 1f &&
               playerSFXVolume >= 0f && playerSFXVolume <= 1f &&
               enemySFXVolume >= 0f && enemySFXVolume <= 1f &&
               dialogueVolume >= 0f && dialogueVolume <= 1f &&
               uiVolume >= 0f && uiVolume <= 1f &&
               musicVolume >= 0f && musicVolume <= 1f;
    }

    /// <summary>
    /// Returns debug information about current volume settings
    /// </summary>
    public string GetDebugInfo()
    {
        return $"Master: {masterVolume:F2} | Ambience: {ambienceVolume:F2} | " +
               $"PlayerSFX: {playerSFXVolume:F2} | EnemySFX: {enemySFXVolume:F2} | " +
               $"Dialogue: {dialogueVolume:F2} | UI: {uiVolume:F2} | Music: {musicVolume:F2}";
    }
}