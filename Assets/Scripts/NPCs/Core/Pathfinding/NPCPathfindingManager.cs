using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages pathfinding updates for all NPCs by spreading them across multiple frames.
/// This prevents performance spikes when many NPCs try to update paths simultaneously.
/// Automatically integrates with the NPC activation/deactivation system.
/// </summary>
public class NPCPathfindingManager : MonoBehaviour
{
    public static NPCPathfindingManager Instance { get; private set; }

    [Header("Update Settings")]
    [SerializeField]
    [Tooltip("Number of frames to spread path updates across. Higher = more spread out updates, lower CPU spikes.")]
    private int updateFrameSpread = 5;

    [Header("Debug Info")]
    [SerializeField]
    [Tooltip("Enable to see debug logs about NPC registration and updates.")]
    private bool showDebugInfo = false;

    // Stores which NPCs should update on which frame
    private List<List<NPCManagedUnderwaterMovement>> updateGroups;

    // Tracks which group should update this frame
    private int currentGroupIndex = 0;

    // Quick lookup to find which group an NPC is in
    private Dictionary<NPCManagedUnderwaterMovement, int> npcToGroupIndex = new Dictionary<NPCManagedUnderwaterMovement, int>();

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
        updateGroups = new List<List<NPCManagedUnderwaterMovement>>();

        for (int i = 0; i < updateFrameSpread; i++)
        {
            updateGroups.Add(new List<NPCManagedUnderwaterMovement>());
        }

        if (showDebugInfo)
        {
            Debug.Log($"NPCPathfindingManager: Initialized {updateFrameSpread} update groups.");
        }
    }

    private void Update()
    {
        // Update all NPCs in the current group
        UpdateCurrentGroup();

        // Move to the next group for next frame
        currentGroupIndex = (currentGroupIndex + 1) % updateFrameSpread;
    }

    /// <summary>
    /// Tell all NPCs in the current group to check if they need a path update.
    /// </summary>
    private void UpdateCurrentGroup()
    {
        List<NPCManagedUnderwaterMovement> currentGroup = updateGroups[currentGroupIndex];

        foreach (NPCManagedUnderwaterMovement npc in currentGroup)
        {
            if (npc != null && npc.gameObject.activeInHierarchy)
            {
                npc.TryUpdatePath();
            }
        }
    }

    /// <summary>
    /// Register an NPC to receive staggered path updates.
    /// Assigns it to the group with the fewest NPCs for balanced distribution.
    /// </summary>
    public void RegisterNPC(NPCManagedUnderwaterMovement npc)
    {
        if (npc == null) return;

        // Don't register if already registered
        if (npcToGroupIndex.ContainsKey(npc))
        {
            if (showDebugInfo)
            {
                Debug.LogWarning($"NPCPathfindingManager: {npc.name} is already registered.");
            }
            return;
        }

        // Find the group with the fewest NPCs for balanced distribution
        int targetGroupIndex = GetSmallestGroupIndex();

        // Add to the group
        updateGroups[targetGroupIndex].Add(npc);
        npcToGroupIndex[npc] = targetGroupIndex;

        if (showDebugInfo)
        {
            Debug.Log($"NPCPathfindingManager: Registered {npc.name} to group {targetGroupIndex}. Total NPCs: {GetTotalRegisteredCount()}");
        }
    }

    /// <summary>
    /// Unregister an NPC from receiving path updates (called when NPC is deactivated).
    /// </summary>
    public void UnregisterNPC(NPCManagedUnderwaterMovement npc)
    {
        if (npc == null) return;

        // Check if this NPC is registered
        if (!npcToGroupIndex.TryGetValue(npc, out int groupIndex))
        {
            return; // Not registered, nothing to do
        }

        // Remove from the group
        updateGroups[groupIndex].Remove(npc);
        npcToGroupIndex.Remove(npc);

        if (showDebugInfo)
        {
            Debug.Log($"NPCPathfindingManager: Unregistered {npc.name} from group {groupIndex}. Total NPCs: {GetTotalRegisteredCount()}");
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
    /// Get the total number of registered NPCs across all groups.
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

        // Get the managed movement component
        NPCManagedUnderwaterMovement managedMovement = npc.GetComponent<NPCManagedUnderwaterMovement>();

        if (managedMovement != null)
        {
            RegisterNPC(managedMovement);
        }
    }

    /// <summary>
    /// Called when an NPC is deactivated by the distance manager.
    /// </summary>
    private void OnNPCDeactivated(NPC npc)
    {
        if (npc == null) return;

        // Get the managed movement component
        NPCManagedUnderwaterMovement managedMovement = npc.GetComponent<NPCManagedUnderwaterMovement>();

        if (managedMovement != null)
        {
            UnregisterNPC(managedMovement);
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
        sb.AppendLine($"Current Group Index: {currentGroupIndex}");
        sb.AppendLine("\nNPCs per group:");

        for (int i = 0; i < updateGroups.Count; i++)
        {
            sb.AppendLine($"  Group {i}: {updateGroups[i].Count} NPCs");
        }

        return sb.ToString();
    }
}