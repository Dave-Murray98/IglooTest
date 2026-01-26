using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// ScriptableObject that stores audio log data including subtitles and speaker information.
/// Create instances via Assets > Create > Audio Logs > Audio Log Data
/// </summary>
[CreateAssetMenu(fileName = "New Audio Log", menuName = "Audio Logs/Audio Log Data", order = 1)]
public class AudioLogData : ScriptableObject
{
    [Header("Audio Log Information")]
    [SerializeField] private string logTitle = "Untitled Log";
    [SerializeField][TextArea(3, 10)] private string logDescription = "";
    [SerializeField] private AudioClip audioClip;

    [Header("Speaker Information")]
    [Tooltip("Name of the speaker (e.g., 'Dr. Sarah Chen')")]
    [SerializeField] private string speakerName = "";

    [Tooltip("Date of the recording (e.g., '2185.03.15')")]
    [SerializeField] private string recordingDate = "";

    [Tooltip("Portrait image of the speaker")]
    [SerializeField] private Sprite speakerPortrait;

    [Header("Visual Settings")]
    [SerializeField] private Sprite logIcon;

    [Header("Subtitles")]
    [Tooltip("Enable subtitles for this audio log")]
    [SerializeField] private bool hasSubtitles = false;

    [ShowIf("hasSubtitles")]
    [Tooltip("List of subtitle entries with timestamps")]
    [SerializeField]
    [ListDrawerSettings(ShowFoldout = true, DraggableItems = false)]
    private List<SubtitleEntry> subtitles = new List<SubtitleEntry>();

    // Public properties
    public string LogTitle => logTitle;
    public string LogDescription => logDescription;
    public AudioClip AudioClip => audioClip;
    public Sprite LogIcon => logIcon;

    // New speaker properties
    public string SpeakerName => speakerName;
    public string RecordingDate => recordingDate;
    public Sprite SpeakerPortrait => speakerPortrait;

    // Subtitle properties
    public bool HasSubtitles => hasSubtitles && subtitles != null && subtitles.Count > 0;
    public List<SubtitleEntry> Subtitles => subtitles;
    public int SubtitleCount => subtitles?.Count ?? 0;

    /// <summary>
    /// Gets the duration of the audio clip in seconds
    /// </summary>
    public float Duration => audioClip != null ? audioClip.length : 0f;

    /// <summary>
    /// Validates that the audio log has required data
    /// </summary>
    public bool IsValid()
    {
        return audioClip != null && !string.IsNullOrEmpty(logTitle);
    }

    /// <summary>
    /// Gets the subtitle that should be displayed at the given timestamp
    /// Returns null if no subtitle should be shown
    /// </summary>
    public SubtitleEntry GetSubtitleAtTime(float currentTime)
    {
        if (!HasSubtitles)
            return null;

        // Find the appropriate subtitle for the current time
        for (int i = 0; i < subtitles.Count; i++)
        {
            SubtitleEntry current = subtitles[i];

            // Get the next subtitle's timestamp (or use max value if this is the last one)
            float nextTimestamp = (i + 1 < subtitles.Count)
                ? subtitles[i + 1].Timestamp
                : float.MaxValue;

            // Check if this subtitle is active
            if (current.IsActiveAt(currentTime, nextTimestamp))
            {
                return current;
            }

            // If we've passed this subtitle and haven't found an active one, keep looking
            if (currentTime < current.Timestamp)
            {
                // We haven't reached any subtitle yet
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Validates all subtitle entries
    /// </summary>
    public bool ValidateSubtitles()
    {
        if (!hasSubtitles || subtitles == null)
            return true;

        foreach (var subtitle in subtitles)
        {
            if (!subtitle.IsValid())
            {
                Debug.LogWarning($"[AudioLogData:{logTitle}] Invalid subtitle entry found");
                return false;
            }
        }

        // Check if subtitles are in chronological order
        for (int i = 1; i < subtitles.Count; i++)
        {
            if (subtitles[i].Timestamp < subtitles[i - 1].Timestamp)
            {
                Debug.LogWarning($"[AudioLogData:{logTitle}] Subtitles are not in chronological order");
                return false;
            }
        }

        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Automatically set title from clip name if not set
        if (string.IsNullOrEmpty(logTitle) && audioClip != null)
        {
            logTitle = audioClip.name;
        }

        // Validate subtitles
        if (hasSubtitles && subtitles != null && subtitles.Count > 0)
        {
            ValidateSubtitles();
        }
    }

    [Button("Sort Subtitles by Timestamp")]
    [ShowIf("hasSubtitles")]
    private void SortSubtitles()
    {
        if (subtitles == null || subtitles.Count == 0)
            return;

        subtitles.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
        Debug.Log($"[AudioLogData:{logTitle}] Subtitles sorted by timestamp");
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [Button("Validate All Subtitles")]
    [ShowIf("hasSubtitles")]
    private void ValidateAllSubtitles()
    {
        if (ValidateSubtitles())
        {
            Debug.Log($"[AudioLogData:{logTitle}] All subtitles are valid!");
        }
    }

    [Button("Add Subtitle Entry")]
    [ShowIf("hasSubtitles")]
    private void AddSubtitleEntry()
    {
        if (subtitles == null)
            subtitles = new List<SubtitleEntry>();

        // Default timestamp is after the last subtitle (or 0 if none exist)
        float defaultTimestamp = subtitles.Count > 0
            ? subtitles[subtitles.Count - 1].Timestamp + 2f
            : 0f;

        subtitles.Add(new SubtitleEntry(defaultTimestamp, "New subtitle text"));
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [Button("Preview Subtitle Info")]
    [ShowIf("hasSubtitles")]
    private void PreviewSubtitleInfo()
    {
        if (!HasSubtitles)
        {
            Debug.Log($"[AudioLogData:{logTitle}] No subtitles configured");
            return;
        }

        Debug.Log($"=== SUBTITLE PREVIEW: {logTitle} ===");
        Debug.Log($"Speaker: {speakerName}");
        Debug.Log($"Date: {recordingDate}");
        Debug.Log($"Subtitle Count: {subtitles.Count}");
        Debug.Log($"Audio Duration: {Duration:F2}s");
        Debug.Log($"---");

        foreach (var subtitle in subtitles)
        {
            Debug.Log(subtitle.GetEditorPreview());
        }
    }
#endif
}