using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages all player role assignments for the session.
///
/// Workflow:
///   1. PlayerInputManager detects a controller connect or disconnect.
///   2. This manager updates its ordered list of connected players (slots).
///   3. It looks up the RoleConfiguration for the new player count.
///   4. It applies that configuration — removing stale role components,
///      adding correct ones, and wiring up their handler references.
///   5. TurretManager is told which turrets to activate.
///
/// Player slots are collapsed on disconnect: if Player 2 leaves in a 3-player game,
/// the old Player 3 becomes the new Player 2 and roles are fully reapplied.
/// </summary>
public class PlayerRoleManager : MonoBehaviour
{
    public static PlayerRoleManager Instance { get; private set; }

    [Header("Scene References")]
    [Tooltip("The PilotController in the scene (on the submarine).")]
    [SerializeField] private PilotController pilotController;

    [Tooltip("The EngineerController in the scene (on the submarine).")]
    [SerializeField] private EngineerController engineerController;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // -------------------------------------------------------------------------
    // Events — broadcast after each configuration is applied
    // -------------------------------------------------------------------------

    /// <summary>Fired after any role reconfiguration. Passes the new player count.</summary>
    public static event Action<int> OnConfigurationApplied;

    /// <summary>Fired when a player joins (before reconfiguration).</summary>
    public static event Action<int> OnPlayerJoinedEvent;

    /// <summary>Fired when a player leaves (before reconfiguration).</summary>
    public static event Action<int> OnPlayerLeftEvent;

    // -------------------------------------------------------------------------
    // Internal state
    // -------------------------------------------------------------------------

    /// <summary>
    /// Ordered list of connected players. Index 0 = first player to join.
    /// This list is collapsed when a player leaves — there are never gaps.
    /// </summary>
    private readonly List<PlayerInput> connectedPlayers = new List<PlayerInput>();

    public int PlayerCount => connectedPlayers.Count;
    private const int MaxPlayers = 6;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("[PlayerRoleManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }

        // Subscribe to PlayerInputManager events
        var inputManager = FindFirstObjectByType<PlayerInputManager>();
        if (inputManager != null)
        {
            inputManager.onPlayerJoined += HandlePlayerJoined;
            inputManager.onPlayerLeft += HandlePlayerLeft;
            DebugLog("Subscribed to PlayerInputManager events.");
        }
        else
        {
            Debug.LogError("[PlayerRoleManager] No PlayerInputManager found in scene!");
        }
    }

    private void Start()
    {
        // Catch any PlayerInputs that joined before we subscribed (e.g. from previous scene)
        var existing = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        foreach (var pi in existing)
        {
            if (!connectedPlayers.Contains(pi))
            {
                DebugLog($"Found pre-existing PlayerInput (Player {pi.playerIndex}), adding.");
                connectedPlayers.Add(pi);
            }
        }

        if (connectedPlayers.Count > 0)
            ApplyCurrentConfiguration();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        var inputManager = FindFirstObjectByType<PlayerInputManager>();
        if (inputManager != null)
        {
            inputManager.onPlayerJoined -= HandlePlayerJoined;
            inputManager.onPlayerLeft -= HandlePlayerLeft;
        }
    }

    // -------------------------------------------------------------------------
    // Join / Leave handlers
    // -------------------------------------------------------------------------

    // Unity's PlayerInputManager can call these via SendMessage if the notification
    // behaviour is set to "Send Messages". Making them public satisfies that requirement.
    public void OnPlayerJoined(PlayerInput playerInput) => HandlePlayerJoined(playerInput);
    public void OnPlayerLeft(PlayerInput playerInput) => HandlePlayerLeft(playerInput);

    private void HandlePlayerJoined(PlayerInput playerInput)
    {
        if (connectedPlayers.Count >= MaxPlayers)
        {
            Debug.LogWarning($"[PlayerRoleManager] Max players ({MaxPlayers}) reached. Ignoring new join.");
            return;
        }

        if (connectedPlayers.Contains(playerInput))
        {
            DebugLog($"PlayerInput for Player {playerInput.playerIndex} already registered, skipping.");
            return;
        }

        connectedPlayers.Add(playerInput);
        DebugLog($"Player joined — slot {connectedPlayers.Count - 1} (PlayerInput index {playerInput.playerIndex}). Total: {connectedPlayers.Count}");

        OnPlayerJoinedEvent?.Invoke(playerInput.playerIndex);
        ApplyCurrentConfiguration();
    }

    private void HandlePlayerLeft(PlayerInput playerInput)
    {
        bool removed = connectedPlayers.Remove(playerInput);
        if (!removed)
        {
            Debug.LogWarning($"[PlayerRoleManager] Tried to remove unknown PlayerInput (index {playerInput.playerIndex}).");
            return;
        }

        DebugLog($"Player left — Total now: {connectedPlayers.Count}");
        OnPlayerJoinedEvent?.Invoke(playerInput.playerIndex);

        if (connectedPlayers.Count > 0)
            ApplyCurrentConfiguration();
    }

    // -------------------------------------------------------------------------
    // Configuration application
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reads the current player count, fetches the matching RoleConfiguration,
    /// and applies it to every connected player.
    /// </summary>
    private void ApplyCurrentConfiguration()
    {
        if (connectedPlayers.Count == 0) return;

        var config = RoleConfiguration.GetForPlayerCount(connectedPlayers.Count);
        DebugLog($"Applying {connectedPlayers.Count}-player configuration...");

        // Apply turret layout first so turrets are active/inactive before controllers wire up
        if (TurretManager.Instance != null)
            TurretManager.Instance.ApplyTurretLayout(config.ActiveTurretNames);
        else
            Debug.LogWarning("[PlayerRoleManager] TurretManager not found — turrets won't update.");

        // Clear existing role components on all players before reassigning
        foreach (var playerInput in connectedPlayers)
            ClearRoleComponents(playerInput);

        // Apply each slot's assignment
        for (int i = 0; i < connectedPlayers.Count; i++)
        {
            var playerInput = connectedPlayers[i];
            var assignment = config.GetAssignment(i);

            if (assignment == null)
            {
                Debug.LogWarning($"[PlayerRoleManager] No assignment found for slot {i} in {connectedPlayers.Count}-player config.");
                continue;
            }

            ApplyAssignment(playerInput, assignment);
        }

        OnConfigurationApplied?.Invoke(connectedPlayers.Count);
        DebugLog($"Configuration applied for {connectedPlayers.Count} player(s).");
    }

    /// <summary>
    /// Removes all role-related components from a player's GameObject so they
    /// can be cleanly reassigned. The MultiRoleInputHandler itself is kept —
    /// only the bridge component (which routes input between roles) is removed.
    /// </summary>
    private void ClearRoleComponents(PlayerInput playerInput)
    {
        var bridge = playerInput.GetComponent<SoloPilotEngineerBridge>();
        if (bridge != null)
            Destroy(bridge);

        // Detach handler from controllers (controllers will re-receive a handler below)
        var handler = playerInput.GetComponent<MultiRoleInputHandler>();
        if (handler != null)
        {
            pilotController?.DetachHandler();
            engineerController?.DetachHandler();
        }
    }

    /// <summary>
    /// Applies a single PlayerRoleAssignment to a PlayerInput's GameObject.
    /// Adds a MultiRoleInputHandler if not present, then wires it up to the
    /// appropriate controllers based on the role flags.
    /// </summary>
    private void ApplyAssignment(PlayerInput playerInput, PlayerRoleAssignment assignment)
    {
        DebugLog($"  Slot {assignment.SlotIndex}: {assignment.Roles}" +
                 (string.IsNullOrEmpty(assignment.AssignedTurretName) ? "" : $" → {assignment.AssignedTurretName}"));

        // Ensure there is a MultiRoleInputHandler on this player
        var handler = playerInput.GetComponent<MultiRoleInputHandler>();
        if (handler == null)
            handler = playerInput.gameObject.AddComponent<MultiRoleInputHandler>();

        // All players use the Pilot action map (it contains all bindings)
        playerInput.SwitchCurrentActionMap("Pilot");

        // --- Pilot role ---
        if (assignment.HasRole(PlayerRole.Pilot))
        {
            pilotController?.AssignHandler(handler);
        }

        // --- Engineer role ---
        if (assignment.HasRole(PlayerRole.Engineer))
        {
            engineerController?.AssignHandler(handler);

            // If this player is ALSO a pilot, add the bridge that routes
            // left-stick input to the engineer when select mode is held
            if (assignment.HasRole(PlayerRole.Pilot))
            {
                var bridge = playerInput.gameObject.AddComponent<SoloPilotEngineerBridge>();
                bridge.Initialise(handler, pilotController, engineerController);
            }
        }

        // --- Gunner role ---
        if (assignment.HasRole(PlayerRole.Gunner) && !string.IsNullOrEmpty(assignment.AssignedTurretName))
        {
            var turretController = TurretManager.Instance?.GetTurretController(assignment.AssignedTurretName);
            if (turretController != null)
                turretController.AssignHandler(handler);
            else
                Debug.LogWarning($"[PlayerRoleManager] Could not find TurretController for '{assignment.AssignedTurretName}'.");
        }
    }

    // -------------------------------------------------------------------------
    // Public accessors (for UI, debug, or other systems that need role info)
    // -------------------------------------------------------------------------

    /// <summary>Returns the MultiRoleInputHandler for a given slot index, or null.</summary>
    public MultiRoleInputHandler GetHandlerForSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= connectedPlayers.Count) return null;
        return connectedPlayers[slotIndex].GetComponent<MultiRoleInputHandler>();
    }

    /// <summary>Returns the assignment for a given slot in the current configuration.</summary>
    public PlayerRoleAssignment GetCurrentAssignment(int slotIndex)
    {
        var config = RoleConfiguration.GetForPlayerCount(connectedPlayers.Count);
        return config?.GetAssignment(slotIndex);
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PlayerRoleManager] {message}");
    }
}