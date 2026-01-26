using UnityEngine;

/// <summary>
/// Handles player interaction with audio logs
/// Integrates with existing interaction system and AudioLogManager
/// NOTE: Does NOT save state - AudioLogManager is the single source of truth for audio log persistence
/// </summary>
public class AudioLogInteractable : MonoBehaviour, IInteractable
{
    [Header("Audio Log Reference")]
    [SerializeField] private AudioLog audioLog;

    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 2f;

    [Header("Interaction Prompts")]
    [SerializeField] private string playPrompt = "play audio log";
    [SerializeField] private string stopPrompt = "stop audio log";

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // IInteractable implementation
    public string InteractableID => audioLog?.AudioLogID ?? "Unknown";
    public Transform Transform => transform;
    public float InteractionRange => interactionRange;

    [SerializeField] protected bool showInteractionPrompt = false;
    public bool ShowInteractionPrompt => showInteractionPrompt;

    private void Awake()
    {
        // Find AudioLog component if not assigned
        if (audioLog == null)
        {
            audioLog = GetComponentInParent<AudioLog>();
            if (audioLog == null)
            {
                audioLog = GetComponent<AudioLog>();
            }
        }

        if (audioLog == null)
        {
            Debug.LogError($"[AudioLogInteractable] No AudioLog component found on {gameObject.name}!");
        }

        // Set default interaction range if not set
        if (interactionRange == 0f)
        {
            interactionRange = 2f;
        }
    }

    #region IInteractable Implementation

    public bool CanInteract
    {
        get
        {
            // Can't interact if audio log is destroyed or missing
            return enabled &&
                   gameObject.activeInHierarchy &&
                   audioLog != null &&
                   !audioLog.IsDestroyed &&
                   AudioLogManager.Instance != null;
        }
    }
    public string GetInteractionPrompt()
    {
        if (!CanInteract)
            return "";

        // Check if this audio log is currently playing
        bool isPlaying = audioLog.IsCurrentlyPlaying;

        // Use appropriate prompt
        string prompt = isPlaying ? stopPrompt : playPrompt;

        // Add audio log title if available
        if (audioLog.AudioLogData != null)
        {
            return $"{prompt} - {audioLog.AudioLogData.LogTitle}";
        }

        return prompt;
    }

    public bool Interact(GameObject player)
    {
        if (!CanInteract)
        {
            DebugLog("Cannot interact - conditions not met");
            return false;
        }

        if (audioLog == null)
        {
            DebugLog("No AudioLog reference - cannot interact");
            return false;
        }

        if (audioLog.IsDestroyed)
        {
            DebugLog("Audio log is destroyed - cannot interact");
            return false;
        }

        if (AudioLogManager.Instance == null)
        {
            DebugLog("AudioLogManager not found - cannot play audio log");
            return false;
        }

        // Toggle audio log playback
        if (audioLog.IsCurrentlyPlaying)
        {
            // Stop this audio log
            DebugLog("Stopping audio log playback");
            AudioLogManager.Instance.StopCurrentAudioLog();
        }
        else
        {
            // Start playing this audio log
            DebugLog($"Starting audio log playback: {audioLog.AudioLogData?.LogTitle ?? "Unknown"}");
            AudioLogManager.Instance.PlayAudioLog(audioLog);
        }

        return true;
    }

    public void OnPlayerEnterRange(GameObject player)
    {
        DebugLog($"Player entered range of audio log: {audioLog?.AudioLogData?.LogTitle ?? "Unknown"}");
    }

    public void OnPlayerExitRange(GameObject player)
    {
        DebugLog($"Player exited range of audio log: {audioLog?.AudioLogData?.LogTitle ?? "Unknown"}");
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[AudioLogInteractable:{audioLog?.AudioLogID ?? "Unknown"}] {message}");
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Auto-find AudioLog if not assigned
        if (audioLog == null && Application.isPlaying == false)
        {
            audioLog = GetComponentInParent<AudioLog>();
            if (audioLog == null)
            {
                audioLog = GetComponent<AudioLog>();
            }
        }
    }
#endif
}