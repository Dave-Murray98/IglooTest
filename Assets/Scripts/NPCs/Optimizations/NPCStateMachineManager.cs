using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages behavior tree updates for all NPCs by spreading them across multiple frames.
/// This prevents performance spikes when many NPCs try to update their behavior trees simultaneously.
/// Automatically integrates with the NPC activation/deactivation system.
/// </summary>
public class NPCStateMachineManager : MonoBehaviour
{
    public static NPCStateMachineManager Instance { get; private set; }

    [Header("Update Settings")]
    [SerializeField]
    [Tooltip("Number of frames to spread behavior tree updates across. Higher = more spread out updates, lower CPU spikes.")]
    private int updateFrameSpread = 5;

    [SerializeField]
    [Tooltip("How many frames to wait between behavior tree ticks (1 = every frame, 2 = every other frame, etc.)")]
    private int updateInterval = 1;

    [Header("Debug Info")]
    [SerializeField]
    [Tooltip("Enable to see debug logs about NPC registration and updates.")]
    private bool showDebugInfo = false;

    // Stores which NPCs should update on which frame
    private List<List<NPCStateMachine>> updateGroups;

    // Tracks which group should update this frame
    private int currentGroupIndex = 0;

    // Frame counter for update interval
    private int frameCounter = 0;

    // Quick lookup to find which group an NPC is in
    private Dictionary<NPCStateMachine, int> stateMachineToGroupIndex = new Dictionary<NPCStateMachine, int>();

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Initialize the update groups
        InitializeUpdateGroups();
    }

    private void OnEnable()
    {
        // Subscribe to NPC activation/deactivation events
        NPCDistanceActivationManager.OnNPCActivated += OnNPCActivated;
        NPCDistanceActivationManager.OnNPCDeactivated += OnNPCDeactivated;
    }

    private void OnDisable()
    {
        // Unsubscribe from events
        NPCDistanceActivationManager.OnNPCActivated -= OnNPCActivated;
        NPCDistanceActivationManager.OnNPCDeactivated -= OnNPCDeactivated;
    }

    /// <summary>
    /// Create the update groups based on the frame spread setting.
    /// </summary>
    private void InitializeUpdateGroups()
    {
        updateGroups = new List<List<NPCStateMachine>>();

        for (int i = 0; i < updateFrameSpread; i++)
        {
            updateGroups.Add(new List<NPCStateMachine>());
        }

        if (showDebugInfo)
        {
            Debug.Log($"[NPCStateMachineManager] Initialized {updateFrameSpread} update groups with interval of {updateInterval} frames.");
        }
    }

    private void Update()
    {
        // Check if we should update this frame based on the interval
        frameCounter++;
        if (frameCounter >= updateInterval)
        {
            frameCounter = 0;

            // Update all NPCs in the current group
            UpdateCurrentGroup();

            // Move to the next group for next update
            currentGroupIndex = (currentGroupIndex + 1) % updateFrameSpread;
        }
    }

    /// <summary>
    /// Tell all state machines in the current group to update their behavior trees.
    /// </summary>
    private void UpdateCurrentGroup()
    {
        List<NPCStateMachine> currentGroup = updateGroups[currentGroupIndex];

        foreach (NPCStateMachine stateMachine in currentGroup)
        {
            if (stateMachine != null && stateMachine.gameObject.activeInHierarchy)
            {
                stateMachine.UpdateCurrentBehaviourTree();
            }
        }
    }

    /// <summary>
    /// Register an NPC state machine to receive staggered behavior tree updates.
    /// Assigns it to the group with the fewest NPCs for balanced distribution.
    /// </summary>
    public void RegisterStateMachine(NPCStateMachine stateMachine)
    {
        if (stateMachine == null) return;

        // Don't register if already registered
        if (stateMachineToGroupIndex.ContainsKey(stateMachine))
        {
            if (showDebugInfo)
            {
                Debug.LogWarning($"[NPCStateMachineManager] {stateMachine.name} is already registered.");
            }
            return;
        }

        // Find the group with the fewest NPCs for balanced distribution
        int targetGroupIndex = GetSmallestGroupIndex();

        // Add to the group
        updateGroups[targetGroupIndex].Add(stateMachine);
        stateMachineToGroupIndex[stateMachine] = targetGroupIndex;

        if (showDebugInfo)
        {
            Debug.Log($"[NPCStateMachineManager] Registered {stateMachine.name} to group {targetGroupIndex}. Total NPCs: {GetTotalRegisteredCount()}");
        }
    }

    /// <summary>
    /// Unregister an NPC state machine from receiving behavior tree updates (called when NPC is deactivated).
    /// </summary>
    public void UnregisterStateMachine(NPCStateMachine stateMachine)
    {
        if (stateMachine == null) return;

        // Check if this state machine is registered
        if (!stateMachineToGroupIndex.TryGetValue(stateMachine, out int groupIndex))
        {
            return; // Not registered, nothing to do
        }

        // Remove from the group
        updateGroups[groupIndex].Remove(stateMachine);
        stateMachineToGroupIndex.Remove(stateMachine);

        if (showDebugInfo)
        {
            Debug.Log($"[NPCStateMachineManager] Unregistered {stateMachine.name} from group {groupIndex}. Total NPCs: {GetTotalRegisteredCount()}");
        }
    }

    /// <summary>
    /// Find which group currently has the fewest NPCs.
    /// This ensures balanced distribution across update groups.
    /// </summary>
    private int GetSmallestGroupIndex()
    {
        int smallestIndex = 0;
        int smallestCount = updateGroups[0].Count;

        for (int i = 1; i < updateGroups.Count; i++)
        {
            if (updateGroups[i].Count < smallestCount)
            {
                smallestCount = updateGroups[i].Count;
                smallestIndex = i;
            }
        }

        return smallestIndex;
    }

    /// <summary>
    /// Get the total number of registered state machines across all groups.
    /// </summary>
    private int GetTotalRegisteredCount()
    {
        int total = 0;
        foreach (var group in updateGroups)
        {
            total += group.Count;
        }
        return total;
    }

    /// <summary>
    /// Called when an NPC is activated by the distance manager.
    /// </summary>
    private void OnNPCActivated(NPC npc)
    {
        if (npc == null) return;

        // Get the state machine component
        NPCStateMachine stateMachine = npc.stateMachine;

        if (stateMachine != null)
        {
            RegisterStateMachine(stateMachine);
        }
    }

    /// <summary>
    /// Called when an NPC is deactivated by the distance manager.
    /// </summary>
    private void OnNPCDeactivated(NPC npc)
    {
        if (npc == null) return;

        // Get the state machine component
        NPCStateMachine stateMachine = npc.stateMachine;

        if (stateMachine != null)
        {
            UnregisterStateMachine(stateMachine);
        }
    }

    /// <summary>
    /// Get debug information about the current state of the manager.
    /// Useful for monitoring in the inspector or debugging.
    /// </summary>
    public string GetDebugInfo()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"Total Registered NPCs: {GetTotalRegisteredCount()}");
        sb.AppendLine($"Update Frame Spread: {updateFrameSpread}");
        sb.AppendLine($"Update Interval: {updateInterval} frame(s)");
        sb.AppendLine($"Current Group Index: {currentGroupIndex}");
        sb.AppendLine($"Frame Counter: {frameCounter}");
        sb.AppendLine("\nNPCs per group:");

        for (int i = 0; i < updateGroups.Count; i++)
        {
            sb.AppendLine($"  Group {i}: {updateGroups[i].Count} NPCs");
        }

        return sb.ToString();
    }
}