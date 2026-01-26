using UnityEngine;

/// <summary>
/// Represents a single subtitle entry with timing information.
/// Used by AudioLogData to store subtitle sequences.
/// </summary>
[System.Serializable]
public class SubtitleEntry
{
    [Tooltip("When this subtitle should appear (in seconds from audio start)")]
    [SerializeField] private float timestamp;

    [Tooltip("The subtitle text to display")]
    [SerializeField][TextArea(2, 4)] private string text;

    [Tooltip("Optional: How long to display this subtitle (0 = until next subtitle)")]
    private float duration = 0f;

    // Public properties
    public float Timestamp => timestamp;
    public string Text => text;
    public float Duration => duration;

    /// <summary>
    /// Constructor for creating subtitle entries in code
    /// </summary>
    public SubtitleEntry(float timestamp, string text, float duration = 0f)
    {
        this.timestamp = timestamp;
        this.text = text;
        this.duration = duration;
    }

    /// <summary>
    /// Checks if this subtitle should be displayed at the given time
    /// </summary>
    public bool IsActiveAt(float currentTime, float nextSubtitleTime = float.MaxValue)
    {
        // If we have a duration, use it
        if (duration > 0f)
        {
            return currentTime >= timestamp && currentTime < (timestamp + duration);
        }

        // Otherwise, display until the next subtitle
        return currentTime >= timestamp && currentTime < nextSubtitleTime;
    }

    /// <summary>
    /// Validates that the subtitle entry has required data
    /// </summary>
    public bool IsValid()
    {
        return timestamp >= 0f && !string.IsNullOrEmpty(text);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Gets a preview string for editor display
    /// </summary>
    public string GetEditorPreview()
    {
        string preview = text;
        if (preview.Length > 40)
        {
            preview = preview.Substring(0, 37) + "...";
        }
        return $"[{timestamp:F1}s] {preview}";
    }
#endif
}