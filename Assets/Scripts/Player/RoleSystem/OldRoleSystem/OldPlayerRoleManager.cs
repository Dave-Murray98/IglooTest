using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages player roles and controller assignments for local multiplayer.
/// Handles spawning PlayerInput components and assigning them to Pilot, Engineer, or Gunner roles.
/// Scene-based (not persistent) so it is recreated fresh on every scene load,
/// avoiding stale reference issues when restarting the level.
/// </summary>
public class OldPlayerRoleManager : MonoBehaviour
{
    public static OldPlayerRoleManager Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private int maxGunners = 4;

    [Header("References")]
    [SerializeField] private Transform inputHandlerParent;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // Role tracking
    private OldPilotInputHandler pilotHandler;
    private OldEngineerInputHandler engineerHandler;
    private List<OldGunnerInputHandler> gunnerHandlers = new List<OldGunnerInputHandler>();

    // Events
    public static event Action<OldPilotInputHandler> OnPilotAssigned;
    public static event Action<OldEngineerInputHandler> OnEngineerAssigned;
    public static event Action<OldGunnerInputHandler, int> OnGunnerAssigned;
    public static event Action<int> OnPlayerJoinedEvent;
    public static event Action<int> OnPlayerLeftEvent;

    // State
    public bool HasPilot => pilotHandler != null;
    public bool HasEngineer => engineerHandler != null;
    public int ConnectedGunnersCount => gunnerHandlers.Count;
    public int TotalPlayersConnected => (HasPilot ? 1 : 0) + (HasEngineer ? 1 : 0) + ConnectedGunnersCount;

    private void Awake()
    {
        Debug.Log("===== OldPlayerRoleManager Awake START =====");

        // Simple singleton - no DontDestroyOnLoad so this is recreated fresh each scene
        if (Instance == null)
        {
            Instance = this;
            DebugLog("OldPlayerRoleManager instance created");
        }
        else
        {
            Debug.Log("[OldPlayerRoleManager] Duplicate instance destroyed");
            Destroy(gameObject);
            return;
        }

        if (inputHandlerParent == null)
        {
            inputHandlerParent = transform;
        }

        // Subscribe to PlayerInputManager events
        PlayerInputManager inputManager = FindFirstObjectByType<PlayerInputManager>();
        if (inputManager != null)
        {
            Debug.Log($"[OldPlayerRoleManager] Found PlayerInputManager: {inputManager.name}");
            Debug.Log($"[OldPlayerRoleManager] Notification Behavior: {inputManager.notificationBehavior}");

            inputManager.onPlayerJoined += OnPlayerInputJoined;
            inputManager.onPlayerLeft += OnPlayerInputLeft;
            DebugLog("Subscribed to PlayerInputManager C# events");
        }
        else
        {
            Debug.LogError("[OldPlayerRoleManager] NO PlayerInputManager found in scene! Add one to the scene!");
        }

        Debug.Log("===== OldPlayerRoleManager Awake END =====");
    }

    private void Start()
    {
        // Check for any PlayerInput objects that may have already spawned before we subscribed
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
    /// PUBLIC Unity Message - Called by PlayerInputManager via SendMessage when "Send Messages" is enabled.
    /// This MUST be public for Unity's SendMessage to find it.
    /// </summary>
    public void OnPlayerJoined(PlayerInput playerInput)
    {
        DebugLog($"===== OnPlayerJoined MESSAGE RECEIVED for Player {playerInput.playerIndex} =====");
        OnPlayerInputJoined(playerInput);
    }

    /// <summary>
    /// PUBLIC Unity Message - Called by PlayerInputManager via SendMessage when "Send Messages" is enabled.
    /// This MUST be public for Unity's SendMessage to find it.
    /// </summary>
    public void OnPlayerLeft(PlayerInput playerInput)
    {
        DebugLog($"===== OnPlayerLeft MESSAGE RECEIVED for Player {playerInput.playerIndex} =====");
        OnPlayerInputLeft(playerInput);
    }

    /// <summary>
    /// Called when a new player joins via PlayerInputManager.
    /// Priority order: Pilot -> Gunners -> Engineer
    /// </summary>
    private void OnPlayerInputJoined(PlayerInput playerInput)
    {
        DebugLog($"=== OnPlayerInputJoined CALLED ===");
        DebugLog($"Player {playerInput.playerIndex} joined with device: {playerInput.currentControlScheme}");
        DebugLog($"Current state - HasPilot: {HasPilot}, HasEngineer: {HasEngineer}, Gunners: {gunnerHandlers.Count}/{maxGunners}");

        if (!HasPilot)
        {
            DebugLog("No pilot exists, assigning this player as pilot...");
            AssignAsPilot(playerInput);
        }
        else if (!HasEngineer)
        {
            DebugLog("No engineer exists, assigning this player as engineer...");
            AssignAsEngineer(playerInput);
        }
        else if (gunnerHandlers.Count < maxGunners)
        {
            DebugLog($"Assigning this player as gunner {gunnerHandlers.Count + 1}...");
            AssignAsGunner(playerInput);
        }
        else
        {
            Debug.LogWarning($"[OldPlayerRoleManager] All roles filled! Cannot assign player {playerInput.playerIndex}");
        }

        OnPlayerJoinedEvent?.Invoke(playerInput.playerIndex);
        DebugLog($"=== OnPlayerInputJoined COMPLETE ===");
    }

    /// <summary>
    /// Called when a player leaves.
    /// </summary>
    private void OnPlayerInputLeft(PlayerInput playerInput)
    {
        DebugLog($"Player {playerInput.playerIndex} left");

        if (pilotHandler != null && pilotHandler.PlayerIndex == playerInput.playerIndex)
        {
            DebugLog("Pilot disconnected!");
            pilotHandler = null;
        }

        if (engineerHandler != null && engineerHandler.PlayerIndex == playerInput.playerIndex)
        {
            DebugLog("Engineer disconnected!");
            engineerHandler = null;
        }

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
    /// Assigns a PlayerInput as the pilot.
    /// </summary>
    private void AssignAsPilot(PlayerInput playerInput)
    {
        DebugLog($"Assigning Player {playerInput.playerIndex} as PILOT");
        var handler = playerInput.gameObject.GetComponent<OldPilotInputHandler>();
        if (handler == null)
        {
            handler = playerInput.gameObject.AddComponent<OldPilotInputHandler>();
        }

        pilotHandler = handler;
        playerInput.SwitchCurrentActionMap("Pilot");

        SetupUIActionMap(playerInput);

        DebugLog($"Player {playerInput.playerIndex} assigned as PILOT");
        OnPilotAssigned?.Invoke(handler);
    }

    private void SetupUIActionMap(PlayerInput playerInput)
    {
        var uiMap = playerInput.actions.FindActionMap("UI");
        if (uiMap != null)
        {
            uiMap.Enable();
            DebugLog($"Enabled UI action map for Player {playerInput.playerIndex}");
        }
        else
        {
            Debug.LogWarning($"[OldPlayerRoleManager] No UI action map found for Player {playerInput.playerIndex}");
        }
    }

    /// <summary>
    /// Assigns a PlayerInput as the engineer.
    /// </summary>
    private void AssignAsEngineer(PlayerInput playerInput)
    {
        var handler = playerInput.gameObject.GetComponent<OldEngineerInputHandler>();
        if (handler == null)
        {
            handler = playerInput.gameObject.AddComponent<OldEngineerInputHandler>();
        }

        engineerHandler = handler;
        playerInput.SwitchCurrentActionMap("Engineer");

        SetupUIActionMap(playerInput);

        DebugLog($"Player {playerInput.playerIndex} assigned as ENGINEER");
        OnEngineerAssigned?.Invoke(handler);
    }

    /// <summary>
    /// Assigns a PlayerInput as a gunner.
    /// </summary>
    private void AssignAsGunner(PlayerInput playerInput)
    {
        var handler = playerInput.gameObject.GetComponent<OldGunnerInputHandler>();
        if (handler == null)
        {
            handler = playerInput.gameObject.AddComponent<OldGunnerInputHandler>();
        }

        int gunnerNumber = gunnerHandlers.Count;
        handler.SetGunnerNumber(gunnerNumber);
        gunnerHandlers.Add(handler);
        playerInput.SwitchCurrentActionMap("Gunner");

        SetupUIActionMap(playerInput);

        DebugLog($"Player {playerInput.playerIndex} assigned as GUNNER {gunnerNumber + 1}");
        OnGunnerAssigned?.Invoke(handler, gunnerNumber);
    }

    /// <summary>
    /// Gets the pilot input handler.
    /// </summary>
    public OldPilotInputHandler GetPilotHandler() => pilotHandler;

    /// <summary>
    /// Gets the engineer input handler.
    /// </summary>
    public OldEngineerInputHandler GetEngineerHandler() => engineerHandler;

    /// <summary>
    /// Gets a specific gunner input handler by index.
    /// </summary>
    public OldGunnerInputHandler GetGunnerHandler(int gunnerIndex)
    {
        if (gunnerIndex >= 0 && gunnerIndex < gunnerHandlers.Count)
        {
            return gunnerHandlers[gunnerIndex];
        }
        return null;
    }

    /// <summary>
    /// Gets all gunner input handlers.
    /// </summary>
    public List<OldGunnerInputHandler> GetAllGunnerHandlers()
    {
        return new List<OldGunnerInputHandler>(gunnerHandlers);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        // Unsubscribe from PlayerInputManager
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
            Debug.Log($"[OldPlayerRoleManager] {message}");
        }
    }
}