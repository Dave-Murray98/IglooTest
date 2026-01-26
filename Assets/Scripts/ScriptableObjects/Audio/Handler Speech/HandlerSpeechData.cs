using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// ScriptableObject that stores handler speech data for the player's corporate handler.
/// Create instances via Assets > Create > Handler Speech > Handler Speech Data
/// </summary>
[CreateAssetMenu(fileName = "New Handler Speech", menuName = "Handler Speech/Handler Speech Data", order = 1)]
public class HandlerSpeechData : ScriptableObject
{
    [Header("Info")]
    [InfoBox("High Priority Alert:\nCan Interrupt: TRUE\nCan Be Interrupted: FALSE\nMustPlayIfCantInterrupt: TRUE\nPriority: 9")]
    [InfoBox("Low Priority Alert:\nCan Interrupt: FALSE\nCan Be Interrupted: TRUE\nMustPlayIfCantInterrupt: FALSE\nPriority: 3")]
    [InfoBox("Story-Critical Dialogue:\nCan Interrupt: TRUE\nCan Be Interrupted: FALSE\nMustPlayIfCantInterrupt: TRUE\nPriority: 10")]

    [Header("Speech Information")]
    [Tooltip("Unique identifier for this speech - used for save system")]
    [SerializeField] private string speechID = "";

    [Tooltip("Display name for UI and debugging")]
    [SerializeField] private string speechTitle = "Untitled Speech";

    [Tooltip("Description of when/why this speech plays")]
    [SerializeField][TextArea(3, 10)] private string speechDescription = "";

    [SerializeField] private AudioClip audioClip;

    [Header("Interruption Settings")]
    [Tooltip("Can this speech interrupt other handler speeches?")]
    [SerializeField] private bool canInterrupt = false;

    [Tooltip("Can this speech be interrupted by other handler speeches?")]
    [SerializeField] private bool canBeInterrupted = true;

    [Tooltip("If this speech can't interrupt, should it queue to play after current speech?")]
    [SerializeField] private bool mustPlayIfCantInterrupt = false;

    [Header("Priority")]
    [Tooltip("Priority for conflict resolution (0-10, higher = more important)")]
    [SerializeField][Range(0, 10)] private float priority = 5f;

    [Header("Visual Settings")]
    [SerializeField] private Sprite speechIcon;

    // Public properties
    public string SpeechID => speechID;
    public string SpeechTitle => speechTitle;
    public string SpeechDescription => speechDescription;
    public AudioClip AudioClip => audioClip;
    public bool CanInterrupt => canInterrupt;
    public bool CanBeInterrupted => canBeInterrupted;
    public bool MustPlayIfCantInterrupt => mustPlayIfCantInterrupt;
    public float Priority => priority;
    public Sprite SpeechIcon => speechIcon;

    /// <summary>
    /// Gets the duration of the audio clip in seconds
    /// </summary>
    public float Duration => audioClip != null ? audioClip.length : 0f;

    /// <summary>
    /// Validates that the handler speech has required data
    /// </summary>
    public bool IsValid()
    {
        if (audioClip == null)
        {
            Debug.LogError($"[HandlerSpeechData:{name}] No audio clip assigned!");
            return false;
        }

        if (string.IsNullOrEmpty(speechID))
        {
            Debug.LogError($"[HandlerSpeechData:{name}] No speech ID assigned!");
            return false;
        }

        if (string.IsNullOrEmpty(speechTitle))
        {
            Debug.LogWarning($"[HandlerSpeechData:{speechID}] No speech title assigned");
        }

        return true;
    }

    /// <summary>
    /// Gets debug information about this speech
    /// </summary>
    public string GetDebugInfo()
    {
        return $"Speech: {speechTitle} (ID: {speechID})\n" +
               $"Duration: {Duration:F2}s\n" +
               $"CanInterrupt: {canInterrupt}, CanBeInterrupted: {canBeInterrupted}\n" +
               $"MustPlayIfCantInterrupt: {mustPlayIfCantInterrupt}\n" +
               $"Priority: {priority}";
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Auto-generate speech ID from asset name if not set
        if (string.IsNullOrEmpty(speechID))
        {
            speechID = name.Replace(" ", "_");
        }

        // Automatically set title from clip name if not set
        if (string.IsNullOrEmpty(speechTitle) && audioClip != null)
        {
            speechTitle = audioClip.name;
        }

        // Validate settings
        IsValid();
    }
#endif
}