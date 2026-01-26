using UnityEngine;
using Sirenix.OdinInspector;
using Unity.VisualScripting;

/// <summary>
/// Simple door interactable that opens/closes on player interaction.
/// Unlike QuestInteractable, this doesn't require quest completion - just direct interaction.
/// Works similarly to your quest door system but without the quest requirement.
/// Properly integrates with your save system to remember door state across scenes.
/// </summary>
public class SimpleDoorInteractable : InteractableBase, IConditionalInteractable
{
    [Header("Door Configuration")]
    [Tooltip("Visual object that shows when door is closed")]
    [SerializeField] private GameObject closedDoorVisual;

    [Tooltip("Visual object that shows when door is open (optional)")]
    [SerializeField] private GameObject openDoorVisual;

    [SerializeField] private DoorHandler doorHandler;

    [Tooltip("Can the door be closed again after opening?")]
    [SerializeField] private bool canToggle = true;

    [Tooltip("Should the door stay open permanently once opened?")]
    [SerializeField] private bool staysOpenPermanently = true;

    [Tooltip("Message to show when player doesn't have the required key")]
    [SerializeField] private string lockedMessage = "You need a key to open this door";

    [Header("Audio")]
    [SerializeField] private AudioClip doorOpenSound;
    [SerializeField] private AudioClip doorCloseSound;
    [SerializeField] private AudioClip lockedSound;

    // Current door state
    [ShowInInspector] private bool isOpen = false;
    [ShowInInspector] private bool isLocked = false;

    protected override void Awake()
    {
        base.Awake();

        if (doorHandler == null)
        {
            doorHandler = GetComponentInChildren<DoorHandler>();
        }

        // Set default interaction prompt if not set
        if (string.IsNullOrEmpty(interactionPrompt))
        {
            interactionPrompt = "open door";
        }

    }

    protected override void Start()
    {
        base.Start();

        // Set initial visual state
        RefreshVisualState();
    }

    #region IInteractable Implementation

    public override bool CanInteract
    {
        get
        {
            if (!base.CanInteract)
                return false;

            // If door stays open permanently and is already open, can't interact
            if (staysOpenPermanently && isOpen)
                return false;

            // If door can't toggle and is already open, can't interact
            if (!canToggle && isOpen)
                return false;

            return true;
        }
    }

    public override string GetInteractionPrompt()
    {
        if (!CanInteract)
            return "";

        // Show locked message if door is locked
        if (isLocked)
        {
            return lockedMessage;
        }

        // Show appropriate prompt based on door state
        if (isOpen)
        {
            return canToggle ? "close door" : "";
        }
        else
        {
            return "open door";
        }
    }

    protected override bool PerformInteraction(GameObject player)
    {
        // Check if player meets requirements (has key if needed)
        if (!MeetsInteractionRequirements(player))
        {
            DebugLog($"Cannot interact - requirements not met: {GetRequirementFailureMessage()}");
            PlayInteractionAudio(false);
            PlayLockedSound();
            return false;
        }

        // Toggle door state
        ToggleDoor();

        return true;
    }

    #endregion

    #region IConditionalInteractable Implementation

    public bool MeetsInteractionRequirements(GameObject player)
    {
        // Check if player is in vehicle (can't open doors while in vehicle)
        if (PlayerStateManager.Instance != null &&
            PlayerStateManager.Instance.CurrentStateType == PlayerStateType.Vehicle)
        {
            DebugLog("Cannot open door while in vehicle");
            return false;
        }

        return true;
    }

    public string GetRequirementFailureMessage()
    {
        return "";
    }

    #endregion

    #region Door Behavior

    /// <summary>
    /// Toggles the door between open and closed states
    /// </summary>
    private void ToggleDoor()
    {
        // If door was locked, unlock it permanently after first use
        if (isLocked)
        {
            isLocked = false;
            DebugLog("Door unlocked");
        }

        // Toggle door state
        isOpen = !isOpen;

        DebugLog($"Door {(isOpen ? "opened" : "closed")}");

        // Mark as used
        if (!hasBeenUsed)
        {
            hasBeenUsed = true;
        }

        // Play appropriate audio feedback
        PlayInteractionAudio(isOpen);

        // Play appropriate sound
        PlayDoorSound();

        // Update visuals
        RefreshVisualState();

        // If door stays open permanently, disable interaction
        if (staysOpenPermanently && isOpen)
        {
            canInteract = false;
            DebugLog("Door locked in open position (stays open permanently)");
        }
    }

    #endregion

    #region Audio

    private void PlayDoorSound()
    {
        AudioClip soundToPlay = isOpen ? doorOpenSound : doorCloseSound;

        if (soundToPlay != null)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(soundToPlay, doorHandler.transform.position, AudioCategory.PlayerSFX);
            }
        }
    }

    private void PlayLockedSound()
    {
        if (lockedSound != null)
        {
            AudioSource.PlayClipAtPoint(lockedSound, transform.position);
        }
    }

    #endregion

    #region Save/Load Implementation

    protected override object GetCustomSaveData()
    {
        return new SimpleDoorSaveData
        {
            isOpen = this.isOpen,
            isLocked = this.isLocked
        };
    }

    protected override void LoadCustomSaveData(object customData)
    {
        if (customData is SimpleDoorSaveData doorData)
        {
            DebugLog($"Loading door save data - isOpen: {doorData.isOpen}, isLocked: {doorData.isLocked}");

            isOpen = doorData.isOpen;
            isLocked = doorData.isLocked;

            // If door was open and stays open permanently, disable interaction
            if (staysOpenPermanently && isOpen)
            {
                canInteract = false;
            }
        }
    }

    protected override void RefreshVisualState()
    {
        DebugLog($"Refreshing door visuals - isOpen: {isOpen}");

        // Update door visuals based on state
        if (closedDoorVisual != null)
        {
            closedDoorVisual.SetActive(!isOpen);
        }

        if (openDoorVisual != null)
        {
            openDoorVisual.SetActive(isOpen);
        }

        if (doorHandler != null)
        {
            doorHandler.SetDoorState(isOpen);
            DebugLog($"Door state set to open: {isOpen}");
        }

        // Update interaction prompt
        if (isLocked)
        {
            interactionPrompt = lockedMessage;
        }
        else if (isOpen)
        {
            interactionPrompt = canToggle ? "close door" : "";
        }
        else
        {
            interactionPrompt = "open door";
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Manually set the door's open/closed state
    /// </summary>
    public void SetDoorState(bool open)
    {
        if (isOpen == open)
            return;

        isOpen = open;
        RefreshVisualState();
        DebugLog($"Door state manually set to: {(open ? "open" : "closed")}");
    }

    /// <summary>
    /// Manually lock or unlock the door
    /// </summary>
    public void SetLocked(bool locked)
    {
        isLocked = locked;
        RefreshVisualState();
        DebugLog($"Door {(locked ? "locked" : "unlocked")}");
    }

    /// <summary>
    /// Check if the door is currently open
    /// </summary>
    public bool IsOpen => isOpen;

    /// <summary>
    /// Check if the door is currently locked
    /// </summary>
    public bool IsLocked => isLocked;

    #endregion

    #region Editor Helpers

    [Button("Open Door")]
    private void EditorOpenDoor()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Can only open door during play mode");
            return;
        }

        if (!isOpen)
        {
            ToggleDoor();
        }
    }

    [Button("Close Door")]
    private void EditorCloseDoor()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Can only close door during play mode");
            return;
        }

        if (isOpen && canToggle)
        {
            ToggleDoor();
        }
    }

    [Button("Toggle Lock")]
    private void EditorToggleLock()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Can only toggle lock during play mode");
            return;
        }

        SetLocked(!isLocked);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // Draw door status in scene view
#if UNITY_EDITOR
        string statusText = Application.isPlaying
            ? (isOpen ? "🚪 OPEN" : "🚪 CLOSED") + (isLocked ? " 🔒 LOCKED" : "")
            : "DOOR";

        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 2f,
            statusText
        );
#endif
    }

    #endregion
}

/// <summary>
/// Save data structure for simple door state
/// </summary>
[System.Serializable]
public class SimpleDoorSaveData
{
    public bool isOpen;
    public bool isLocked;
}