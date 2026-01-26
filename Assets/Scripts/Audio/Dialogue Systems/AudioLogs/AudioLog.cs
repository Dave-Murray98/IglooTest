using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Represents a physical audio log object in the game world
/// Tracks state, handles destruction, and communicates with AudioLogManager
/// </summary>
public class AudioLog : MonoBehaviour
{
    [Header("Audio Log Data")]
    [SerializeField] private AudioLogData audioLogData;

    [Header("Save System Integration")]
    [ShowInInspector]
    [ReadOnly]
    [Tooltip("Unique ID assigned by AudioLogManager. Do not modify manually.")]
    private string audioLogID;

    [Header("State")]
    [SerializeField][ReadOnly] private bool isDestroyed = false;
    [SerializeField][ReadOnly] private bool isCurrentlyPlaying = false;

    [Header("Visual Components")]
    [SerializeField] private GameObject visualModel;
    [SerializeField] private Collider audioLogCollider;
    [SerializeField] private AudioLogInteractable interactable;

    [Header("Destruction Settings")]
    [SerializeField] private GameObject destructionEffect;
    [SerializeField] private AudioClip destructionSound;
    [SerializeField] private float destructionEffectDuration = 2f;
    [SerializeField] private float destructionSoundVolume = 5f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Public properties
    public string AudioLogID => audioLogID;
    public AudioLogData AudioLogData => audioLogData;
    public bool IsDestroyed => isDestroyed;
    public bool IsCurrentlyPlaying
    {
        get => isCurrentlyPlaying;
        set => isCurrentlyPlaying = value;
    }

    /// <summary>
    /// Checks if this audio log has been assigned an ID by the manager
    /// </summary>
    public bool HasValidID => !string.IsNullOrEmpty(audioLogID);

    /// <summary>
    /// Sets the audio log ID - should only be called by AudioLogManager
    /// </summary>
    public void SetAudioLogID(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning($"[AudioLog] Attempted to set null/empty ID on {gameObject.name}");
            return;
        }

        if (!string.IsNullOrEmpty(audioLogID) && audioLogID != id)
        {
            Debug.LogWarning($"[AudioLog] Overwriting existing ID '{audioLogID}' with '{id}' on {gameObject.name}");
        }

        audioLogID = id;
        DebugLog($"Assigned ID '{id}'");
    }

    private void Awake()
    {
        // Find components if not assigned
        if (visualModel == null)
        {
            visualModel = transform.GetChild(0)?.gameObject;
        }

        if (audioLogCollider == null)
        {
            audioLogCollider = GetComponentInChildren<Collider>();
        }

        if (interactable == null)
        {
            interactable = GetComponentInChildren<AudioLogInteractable>();
        }

        ValidateSetup();
    }

    private void Start()
    {
        // Registration happens in AudioLogManager's initialization
        // Manager will discover and assign IDs to all audio logs
        DebugLog($"AudioLog component ready for manager registration");
    }

    /// <summary>
    /// Validates that the audio log has all required components and data
    /// </summary>
    private void ValidateSetup()
    {
        if (audioLogData == null)
        {
            Debug.LogError($"[AudioLog:{audioLogID}] No AudioLogData assigned!");
        }
        else if (!audioLogData.IsValid())
        {
            Debug.LogError($"[AudioLog:{audioLogID}] AudioLogData is invalid (missing audio clip or title)!");
        }

        if (visualModel == null)
        {
            Debug.LogWarning($"[AudioLog:{audioLogID}] No visual model assigned - destruction visuals won't work!");
        }

        if (audioLogCollider == null)
        {
            Debug.LogWarning($"[AudioLog:{audioLogID}] No collider found - audio log may not be interactable or damageable!");
        }
    }

    /// <summary>
    /// Destroys this audio log (called when health depletes or through save system)
    /// </summary>
    public void DestroyAudioLog()
    {
        if (isDestroyed)
        {
            DebugLog("Already destroyed");
            return;
        }

        DebugLog("Audio log destroyed");

        // Mark as destroyed
        isDestroyed = true;

        // Stop playback if this log is currently playing
        if (isCurrentlyPlaying)
        {
            AudioLogManager.Instance?.StopCurrentAudioLog();
        }

        // Notify manager
        if (AudioLogManager.Instance != null)
        {
            AudioLogManager.Instance.OnAudioLogDestroyed(this);
        }

        // Play destruction sound
        if (destructionSound != null)
        {
            AudioManager.Instance.PlaySound(destructionSound, transform.position, AudioCategory.Ambience);
        }

        NoisePool.Instance.GetNoise(transform.position, destructionSoundVolume);

        // Hide visual and disable interaction
        HideAudioLog();

        // Schedule complete destruction after effect plays
        Destroy(gameObject, destructionEffectDuration);
    }

    /// <summary>
    /// Hides the audio log's visual representation and disables interaction
    /// </summary>
    private void HideAudioLog()
    {
        // Hide visual model
        if (visualModel != null)
        {
            visualModel.SetActive(false);
        }

        // Disable collider
        if (audioLogCollider != null)
        {
            audioLogCollider.enabled = false;
        }

        // Disable interactable
        if (interactable != null)
        {
            interactable.enabled = false;
        }
    }

    /// <summary>
    /// Restores audio log to destroyed state (called by save system)
    /// </summary>
    public void RestoreAsDestroyed()
    {
        DebugLog("Restoring as destroyed (from save data)");
        isDestroyed = true;
        HideAudioLog();

        // Destroy immediately without effects since this is restoration
        Destroy(gameObject, 0.1f);
    }

    /// <summary>
    /// Gets formatted display information for UI
    /// </summary>
    public string GetDisplayInfo()
    {
        if (audioLogData == null) return "Unknown Audio Log";

        return $"{audioLogData.LogTitle} ({FormatDuration(audioLogData.Duration)})";
    }

    /// <summary>
    /// Formats duration in mm:ss format
    /// </summary>
    private string FormatDuration(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        return $"{minutes:00}:{secs:00}";
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[AudioLog:{audioLogID}] {message}");
        }
    }

    private void OnDestroy()
    {
        // Unregister from manager if still registered
        if (AudioLogManager.Instance != null && !isDestroyed)
        {
            AudioLogManager.Instance.UnregisterAudioLog(this);
        }
    }
}