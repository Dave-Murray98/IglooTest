using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages player roles and controller assignments for local multiplayer.
/// Handles spawning PlayerInput components and assigning them to Pilot, Engineer, or Gunner roles.
/// PlayerInputManager uses Evoke Unity Events to send messages to this script.
/// UPDATED: Now assigns Engineer as 2nd player (for testing purposes)
/// </summary>
public class PlayerRoleManager : MonoBehaviour
{
    public static PlayerRoleManager Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private int maxGunners = 4;
    [SerializeField] private GameObject pilotInputPrefab;
    [SerializeField] private GameObject engineerInputPrefab;
    [SerializeField] private GameObject gunnerInputPrefab;

    [Header("References")]
    [SerializeField] private Transform inputHandlerParent;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // Role tracking
    private PilotInputHandler pilotHandler;
    private EngineerInputHandler engineerHandler;
    private List<GunnerInputHandler> gunnerHandlers = new List<GunnerInputHandler>();

    // Events
    public static event Action<PilotInputHandler> OnPilotAssigned;
    public static event Action<EngineerInputHandler> OnEngineerAssigned;
    public static event Action<GunnerInputHandler, int> OnGunnerAssigned; // handler, gunner number
    public static event Action<int> OnPlayerJoinedEvent; // player index - renamed to avoid conflict
    public static event Action<int> OnPlayerLeftEvent; // player index - renamed to avoid conflict

    // State
    public bool HasPilot => pilotHandler != null;
    public bool HasEngineer => engineerHandler != null;
    public int ConnectedGunnersCount => gunnerHandlers.Count;
    public int TotalPlayersConnected => (HasPilot ? 1 : 0) + (HasEngineer ? 1 : 0) + ConnectedGunnersCount;

    private void Awake()
    {
        Debug.Log("===== PlayerRoleManager Awake START =====");

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            DebugLog("PlayerRoleManager singleton created");
        }
        else
        {
            Debug.Log("[PlayerRoleManager] Duplicate instance destroyed");
            Destroy(gameObject);
            return;
        }

        // Ensure we have a parent for input handlers
        if (inputHandlerParent == null)
        {
            inputHandlerParent = transform;
        }

        // CRITICAL: Try to subscribe to events in Awake
        var inputManager = FindFirstObjectByType<PlayerInputManager>();
        if (inputManager != null)
        {
            Debug.Log($"[PlayerRoleManager] Found PlayerInputManager: {inputManager.name}");
            Debug.Log($"[PlayerRoleManager] Notification Behavior: {inputManager.notificationBehavior}");

            inputManager.onPlayerJoined += OnPlayerInputJoined;
            inputManager.onPlayerLeft += OnPlayerInputLeft;
            DebugLog("Subscribed to PlayerInputManager C# events");
        }
        else
        {
            Debug.LogError("[PlayerRoleManager] NO PlayerInputManager found in scene! Add one to the scene!");
        }

        Debug.Log("===== PlayerRoleManager Awake END =====");
    }

    private void Start()
    {
        // Check for any PlayerInput objects that might have spawned before we subscribed
        var existingPlayers = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        if (existingPlayers.Length > 0)
        {
            DebugLog($"Found {existingPlayers.Length} existing PlayerInput(s), assigning roles...");
            foreach (var player in existingPlayers)
            {
                OnPlayerInputJoined(player);
            }
        }
        else
        {
            DebugLog("No existing PlayerInputs found, waiting for controllers to join...");
        }
    }

    /// <summary>
    /// PUBLIC Unity Message - Called by PlayerInputManager via SendMessage when "Send Messages" is enabled
    /// This MUST be public for Unity's SendMessage to find it
    /// </summary>
    public void OnPlayerJoined(PlayerInput playerInput)
    {
        DebugLog($"===== OnPlayerJoined MESSAGE RECEIVED for Player {playerInput.playerIndex} =====");
        OnPlayerInputJoined(playerInput);
    }

    /// <summary>
    /// PUBLIC Unity Message - Called by PlayerInputManager via SendMessage when "Send Messages" is enabled
    /// This MUST be public for Unity's SendMessage to find it
    /// </summary>
    public void OnPlayerLeft(PlayerInput playerInput)
    {
        DebugLog($"===== OnPlayerLeft MESSAGE RECEIVED for Player {playerInput.playerIndex} =====");
        OnPlayerInputLeft(playerInput);
    }

    /// <summary>
    /// Called when a new player joins via PlayerInputManager
    /// UPDATED: Priority order for testing - Pilot -> Engineer -> Gunners
    /// </summary>
    private void OnPlayerInputJoined(PlayerInput playerInput)
    {
        DebugLog($"=== OnPlayerInputJoined CALLED ===");
        DebugLog($"Player {playerInput.playerIndex} joined with device: {playerInput.currentControlScheme}");
        DebugLog($"Current state - HasPilot: {HasPilot}, HasEngineer: {HasEngineer}, Gunners: {gunnerHandlers.Count}/{maxGunners}");

        // Assign role based on what's available (TESTING PRIORITY: Pilot -> Engineer -> Gunners)
        if (!HasPilot)
        {
            DebugLog("No pilot exists, assigning this player as pilot...");
            AssignAsPilot(playerInput);
        }
        else if (!HasEngineer)
        {
            DebugLog("Pilot exists but no engineer, assigning this player as engineer...");
            AssignAsEngineer(playerInput);
        }
        else if (gunnerHandlers.Count < maxGunners)
        {
            DebugLog($"Pilot and engineer exist, assigning this player as gunner {gunnerHandlers.Count + 1}...");
            AssignAsGunner(playerInput);
        }
        else
        {
            Debug.LogWarning($"[PlayerRoleManager] All roles filled! Cannot assign player {playerInput.playerIndex}");
            // Could destroy the PlayerInput here or show UI feedback
        }

        OnPlayerJoinedEvent?.Invoke(playerInput.playerIndex);
        DebugLog($"=== OnPlayerInputJoined COMPLETE ===");
    }

    /// <summary>
    /// Called when a player leaves
    /// </summary>
    private void OnPlayerInputLeft(PlayerInput playerInput)
    {
        DebugLog($"Player {playerInput.playerIndex} left");

        // Check if it was the pilot
        if (pilotHandler != null && pilotHandler.PlayerIndex == playerInput.playerIndex)
        {
            DebugLog("Pilot disconnected!");
            pilotHandler = null;
            // Could reassign roles here or pause game
        }

        // Check if it was the engineer
        if (engineerHandler != null && engineerHandler.PlayerIndex == playerInput.playerIndex)
        {
            DebugLog("Engineer disconnected!");
            engineerHandler = null;
            // Could reassign roles here or pause game
        }

        // Check if it was a gunner
        for (int i = gunnerHandlers.Count - 1; i >= 0; i--)
        {
            if (gunnerHandlers[i].PlayerIndex == playerInput.playerIndex)
            {
                DebugLog($"Gunner {i + 1} disconnected!");
                gunnerHandlers.RemoveAt(i);
                break;
            }
        }

        OnPlayerLeftEvent?.Invoke(playerInput.playerIndex);
    }

    /// <summary>
    /// Assigns a PlayerInput as the pilot
    /// </summary>
    private void AssignAsPilot(PlayerInput playerInput)
    {
        // Add or get PilotInputHandler component
        var handler = playerInput.gameObject.GetComponent<PilotInputHandler>();
        if (handler == null)
        {
            handler = playerInput.gameObject.AddComponent<PilotInputHandler>();
        }

        pilotHandler = handler;

        // Switch to Pilot action map
        playerInput.SwitchCurrentActionMap("Pilot");

        DebugLog($"Player {playerInput.playerIndex} assigned as PILOT");
        OnPilotAssigned?.Invoke(handler);
    }

    /// <summary>
    /// Assigns a PlayerInput as the engineer
    /// </summary>
    private void AssignAsEngineer(PlayerInput playerInput)
    {
        // Add or get EngineerInputHandler component
        var handler = playerInput.gameObject.GetComponent<EngineerInputHandler>();
        if (handler == null)
        {
            handler = playerInput.gameObject.AddComponent<EngineerInputHandler>();
        }

        engineerHandler = handler;

        // Switch to Engineer action map
        playerInput.SwitchCurrentActionMap("Engineer");

        DebugLog($"Player {playerInput.playerIndex} assigned as ENGINEER");
        OnEngineerAssigned?.Invoke(handler);
    }

    /// <summary>
    /// Assigns a PlayerInput as a gunner
    /// </summary>
    private void AssignAsGunner(PlayerInput playerInput)
    {
        // Add or get GunnerInputHandler component
        var handler = playerInput.gameObject.GetComponent<GunnerInputHandler>();
        if (handler == null)
        {
            handler = playerInput.gameObject.AddComponent<GunnerInputHandler>();
        }

        int gunnerNumber = gunnerHandlers.Count;
        handler.SetGunnerNumber(gunnerNumber);
        gunnerHandlers.Add(handler);

        // Switch to Gunner action map
        playerInput.SwitchCurrentActionMap("Gunner");

        DebugLog($"Player {playerInput.playerIndex} assigned as GUNNER {gunnerNumber + 1}");
        OnGunnerAssigned?.Invoke(handler, gunnerNumber);
    }

    /// <summary>
    /// Gets the pilot input handler
    /// </summary>
    public PilotInputHandler GetPilotHandler()
    {
        return pilotHandler;
    }

    /// <summary>
    /// Gets the engineer input handler
    /// </summary>
    public EngineerInputHandler GetEngineerHandler()
    {
        return engineerHandler;
    }

    /// <summary>
    /// Gets a specific gunner input handler by index
    /// </summary>
    public GunnerInputHandler GetGunnerHandler(int gunnerIndex)
    {
        if (gunnerIndex >= 0 && gunnerIndex < gunnerHandlers.Count)
        {
            return gunnerHandlers[gunnerIndex];
        }
        return null;
    }

    /// <summary>
    /// Gets all gunner input handlers
    /// </summary>
    public List<GunnerInputHandler> GetAllGunnerHandlers()
    {
        return new List<GunnerInputHandler>(gunnerHandlers);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        // Unsubscribe from events
        var inputManager = FindFirstObjectByType<PlayerInputManager>();
        if (inputManager != null)
        {
            inputManager.onPlayerJoined -= OnPlayerInputJoined;
            inputManager.onPlayerLeft -= OnPlayerInputLeft;
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerRoleManager] {message}");
        }
    }
}