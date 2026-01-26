using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data structure for saving destruction states of all Rayfire shattered objects in a scene.
/// Used by SceneDestructionStateManager to persist environmental destruction across
/// scene transitions and save/load operations.
/// </summary>
[System.Serializable]
public class DestructionStateSaveData
{
    [Header("Scene Info")]
    public string sceneName;
    public System.DateTime lastUpdated;

    [Header("Destruction States")]
    public Dictionary<string, List<RayfireShatterFragData>> destructionStates = new Dictionary<string, List<RayfireShatterFragData>>();

    public DestructionStateSaveData()
    {
        destructionStates = new Dictionary<string, List<RayfireShatterFragData>>();
    }

    /// <summary>
    /// Stores destruction state for a specific destructible object
    /// </summary>
    /// <param name="destructionID">Unique ID of the destructible object</param>
    /// <param name="fragmentData">List of fragment states</param>
    public void SetDestructionState(string destructionID, List<RayfireShatterFragData> fragmentData)
    {
        if (string.IsNullOrEmpty(destructionID))
        {
            Debug.LogWarning("[DestructionStateSaveData] Attempted to set destruction state with null/empty ID");
            return;
        }

        destructionStates[destructionID] = fragmentData;
    }

    /// <summary>
    /// Retrieves destruction state for a specific destructible object
    /// </summary>
    /// <param name="destructionID">Unique ID of the destructible object</param>
    /// <returns>Fragment data list, or null if not found</returns>
    public List<RayfireShatterFragData> GetDestructionState(string destructionID)
    {
        if (string.IsNullOrEmpty(destructionID))
            return null;

        if (destructionStates.TryGetValue(destructionID, out var data))
        {
            return data;
        }

        return null;
    }

    /// <summary>
    /// Checks if destruction state exists for a specific object
    /// </summary>
    public bool HasDestructionState(string destructionID)
    {
        if (string.IsNullOrEmpty(destructionID))
            return false;

        return destructionStates.ContainsKey(destructionID);
    }

    /// <summary>
    /// Removes destruction state for a specific object
    /// </summary>
    public bool RemoveDestructionState(string destructionID)
    {
        if (string.IsNullOrEmpty(destructionID))
            return false;

        return destructionStates.Remove(destructionID);
    }

    /// <summary>
    /// Gets all tracked destruction IDs
    /// </summary>
    public IEnumerable<string> GetAllDestructionIDs()
    {
        return destructionStates.Keys;
    }

    /// <summary>
    /// Clears all destruction states
    /// </summary>
    public void ClearAllStates()
    {
        destructionStates.Clear();
    }

    /// <summary>
    /// Gets count of tracked destruction objects
    /// </summary>
    public int Count => destructionStates.Count;

    /// <summary>
    /// Validates data integrity
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        if (destructionStates == null)
            return false;

        // Validate each destruction state
        foreach (var kvp in destructionStates)
        {
            if (string.IsNullOrEmpty(kvp.Key))
                return false;

            if (kvp.Value == null)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Returns debug information about stored data
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine($"=== Destruction State Save Data ===");
        info.AppendLine($"Scene: {sceneName}");
        info.AppendLine($"Last Updated: {lastUpdated}");
        info.AppendLine($"Tracked Objects: {destructionStates.Count}");

        foreach (var kvp in destructionStates)
        {
            int fragmentCount = kvp.Value?.Count ?? 0;
            int activeFragments = 0;
            if (kvp.Value != null)
            {
                foreach (var frag in kvp.Value)
                {
                    if (frag.isActive) activeFragments++;
                }
            }

            info.AppendLine($"  - {kvp.Key}: {fragmentCount} fragments ({activeFragments} active)");
        }

        return info.ToString();
    }

    /// <summary>
    /// Creates a deep copy of this save data
    /// </summary>
    public DestructionStateSaveData CreateCopy()
    {
        var copy = new DestructionStateSaveData
        {
            sceneName = sceneName,
            lastUpdated = lastUpdated
        };

        foreach (var kvp in destructionStates)
        {
            // Create new list with copies of fragment data
            var fragmentCopies = new List<RayfireShatterFragData>();
            foreach (var frag in kvp.Value)
            {
                fragmentCopies.Add(new RayfireShatterFragData
                {
                    id = frag.id,
                    position = frag.position,
                    rotation = frag.rotation,
                    isActive = frag.isActive
                });
            }

            copy.destructionStates[kvp.Key] = fragmentCopies;
        }

        return copy;
    }
}