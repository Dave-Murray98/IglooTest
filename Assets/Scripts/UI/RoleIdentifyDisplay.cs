using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// While a player holds the IdentifyRole button (mapped in the Pilot action map),
/// their corresponding UI text element appears and displays their current role(s).
/// Releasing the button hides their text again.
///
/// Setup:
///   - Add "IdentifyRole" to the Pilot action map, bound to whichever button you prefer.
///   - Assign the six TextMeshProUGUI fields in the Inspector (one per player slot).
///   - The text objects can be children of any canvas — they are shown/hidden independently.
/// </summary>
public class RoleIdentifyDisplay : MonoBehaviour
{
    [Header("Player Role Text Elements")]
    [Tooltip("Text element for player slot 1 (the first player to join).")]
    [SerializeField] private TextMeshProUGUI player1RoleText;

    [Tooltip("Text element for player slot 2.")]
    [SerializeField] private TextMeshProUGUI player2RoleText;

    [Tooltip("Text element for player slot 3.")]
    [SerializeField] private TextMeshProUGUI player3RoleText;

    [Tooltip("Text element for player slot 4.")]
    [SerializeField] private TextMeshProUGUI player4RoleText;

    [Tooltip("Text element for player slot 5.")]
    [SerializeField] private TextMeshProUGUI player5RoleText;

    [Tooltip("Text element for player slot 6.")]
    [SerializeField] private TextMeshProUGUI player6RoleText;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Maps each connected PlayerInput to its IdentifyRole action
    // so we can unsubscribe cleanly when that player leaves.
    private readonly Dictionary<PlayerInput, InputAction> identifyActions
        = new Dictionary<PlayerInput, InputAction>();

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        HideAllTexts();
    }

    private void OnEnable()
    {
        PlayerRoleManager.OnPlayerJoinedEvent += HandlePlayerJoined;
        PlayerRoleManager.OnPlayerLeftEvent += HandlePlayerLeft;
        PlayerRoleManager.OnConfigurationApplied += HandleConfigurationApplied;

        // Register any players already connected
        foreach (var pi in FindObjectsByType<PlayerInput>(FindObjectsSortMode.None))
            RegisterPlayer(pi);
    }

    private void OnDisable()
    {
        PlayerRoleManager.OnPlayerJoinedEvent -= HandlePlayerJoined;
        PlayerRoleManager.OnPlayerLeftEvent -= HandlePlayerLeft;
        PlayerRoleManager.OnConfigurationApplied -= HandleConfigurationApplied;

        foreach (var kvp in identifyActions)
        {
            kvp.Value.performed -= OnIdentifyPressed;
            kvp.Value.canceled -= OnIdentifyReleased;
        }

        identifyActions.Clear();
        HideAllTexts();
    }

    // -------------------------------------------------------------------------
    // Player join / leave
    // -------------------------------------------------------------------------

    private void HandlePlayerJoined(int playerIndex)
    {
        foreach (var pi in FindObjectsByType<PlayerInput>(FindObjectsSortMode.None))
        {
            if (pi.playerIndex == playerIndex)
            {
                RegisterPlayer(pi);
                return;
            }
        }
    }

    private void HandlePlayerLeft(int playerIndex)
    {
        PlayerInput leaving = null;
        foreach (var kvp in identifyActions)
        {
            if (kvp.Key.playerIndex == playerIndex)
            {
                leaving = kvp.Key;
                break;
            }
        }

        if (leaving == null) return;

        identifyActions[leaving].performed -= OnIdentifyPressed;
        identifyActions[leaving].canceled -= OnIdentifyReleased;
        identifyActions.Remove(leaving);

        // Hide and clear this player's text slot
        var text = GetTextForPlayerInput(leaving);
        if (text != null)
        {
            text.text = string.Empty;
            text.gameObject.SetActive(false);
        }

        DebugLog($"Player {playerIndex} unregistered.");
    }

    /// <summary>
    /// When the configuration changes (i.e. a player joins or leaves and roles are
    /// reassigned), any text currently visible needs its content refreshed so it
    /// doesn't show stale role information.
    /// </summary>
    private void HandleConfigurationApplied(int playerCount)
    {
        // Re-read and refresh any texts that are currently visible
        foreach (var kvp in identifyActions)
        {
            var text = GetTextForPlayerInput(kvp.Key);
            if (text != null && text.gameObject.activeSelf)
                text.text = BuildRoleText(kvp.Key);
        }
    }

    private void RegisterPlayer(PlayerInput playerInput)
    {
        if (identifyActions.ContainsKey(playerInput)) return;

        var pilotMap = playerInput.actions.FindActionMap("Pilot");
        if (pilotMap == null)
        {
            Debug.LogWarning($"[RoleIdentifyDisplay] Player {playerInput.playerIndex} has no 'Pilot' action map.");
            return;
        }

        var action = pilotMap.FindAction("IdentifyRole");
        if (action == null)
        {
            Debug.LogWarning($"[RoleIdentifyDisplay] No 'IdentifyRole' action found in Pilot map " +
                             $"for Player {playerInput.playerIndex}.");
            return;
        }

        action.performed += OnIdentifyPressed;
        action.canceled += OnIdentifyReleased;
        identifyActions[playerInput] = action;

        DebugLog($"Player {playerInput.playerIndex} registered.");
    }

    // -------------------------------------------------------------------------
    // Input callbacks
    // -------------------------------------------------------------------------

    private void OnIdentifyPressed(InputAction.CallbackContext ctx)
    {
        // Find which PlayerInput triggered this action
        var playerInput = FindPlayerInputForContext(ctx);
        if (playerInput == null) return;

        var text = GetTextForPlayerInput(playerInput);
        if (text == null) return;

        text.text = BuildRoleText(playerInput);
        text.gameObject.SetActive(true);

        DebugLog($"Player {playerInput.playerIndex} showing role text.");
    }

    private void OnIdentifyReleased(InputAction.CallbackContext ctx)
    {
        var playerInput = FindPlayerInputForContext(ctx);
        if (playerInput == null) return;

        var text = GetTextForPlayerInput(playerInput);
        if (text == null) return;

        text.gameObject.SetActive(false);

        DebugLog($"Player {playerInput.playerIndex} hiding role text.");
    }

    // -------------------------------------------------------------------------
    // Role text construction
    // -------------------------------------------------------------------------

    /// <summary>
    /// Asks PlayerRoleManager for this player's current assignment and builds
    /// a human-readable string describing their role(s).
    /// </summary>
    private string BuildRoleText(PlayerInput playerInput)
    {
        if (PlayerRoleManager.Instance == null) return "Unknown";

        // Work out which slot index this PlayerInput occupies
        int slotIndex = GetSlotIndexForPlayerInput(playerInput);
        if (slotIndex < 0) return "Unknown";

        var assignment = PlayerRoleManager.Instance.GetCurrentAssignment(slotIndex);
        if (assignment == null) return "No Role";

        // Build a list of role label strings, then join them
        var roleLines = new List<string>();

        if (assignment.HasRole(PlayerRole.Pilot)) roleLines.Add("Pilot");
        if (assignment.HasRole(PlayerRole.Engineer)) roleLines.Add("Engineer");
        if (assignment.HasRole(PlayerRole.Gunner)) roleLines.Add("Gunner");

        // Player number header, then roles slash-separated on the line beneath
        string header = $"Player {slotIndex + 1}";
        string roles = roleLines.Count > 0
            ? string.Join(" / ", roleLines)
            : "No Role";

        return $"{header}\n{roles}";
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the TextMeshProUGUI assigned to the slot this PlayerInput occupies.
    /// Slot is determined by position in PlayerRoleManager's connected player list.
    /// </summary>
    private TextMeshProUGUI GetTextForPlayerInput(PlayerInput playerInput)
    {
        int slot = GetSlotIndexForPlayerInput(playerInput);
        return slot switch
        {
            0 => player1RoleText,
            1 => player2RoleText,
            2 => player3RoleText,
            3 => player4RoleText,
            4 => player5RoleText,
            5 => player6RoleText,
            _ => null
        };
    }

    /// <summary>
    /// Finds the slot index for a given PlayerInput by asking PlayerRoleManager
    /// for the handler at each slot and comparing.
    /// Returns -1 if not found.
    /// </summary>
    private int GetSlotIndexForPlayerInput(PlayerInput playerInput)
    {
        if (PlayerRoleManager.Instance == null) return -1;

        for (int i = 0; i < 6; i++)
        {
            var handler = PlayerRoleManager.Instance.GetHandlerForSlot(i);
            if (handler != null && handler.gameObject == playerInput.gameObject)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Identifies which PlayerInput fired a given InputAction callback by checking
    /// which registered entry's action matches the one in the context.
    /// </summary>
    private PlayerInput FindPlayerInputForContext(InputAction.CallbackContext ctx)
    {
        foreach (var kvp in identifyActions)
        {
            if (kvp.Value == ctx.action)
                return kvp.Key;
        }
        return null;
    }

    private void HideAllTexts()
    {
        foreach (var text in new[]
        {
            player1RoleText, player2RoleText, player3RoleText,
            player4RoleText, player5RoleText, player6RoleText
        })
        {
            if (text != null)
            {
                text.text = string.Empty;
                text.gameObject.SetActive(false);
            }
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[RoleIdentifyDisplay] {message}");
    }
}