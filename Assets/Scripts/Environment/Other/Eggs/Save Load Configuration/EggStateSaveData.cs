using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data structure for saving the broken states of all eggs in a scene.
/// Used by SceneEggStateManager to persist egg states across
/// scene transitions and save/load operations.
/// This follows the same pattern as DestructionStateSaveData.
/// </summary>
[System.Serializable]
public class EggStateSaveData
{
    [Header("Scene Info")]
    public string sceneName;
    public System.DateTime lastUpdated;

    [Header("Egg States")]
    public Dictionary<string, EggSaveData> eggStates = new Dictionary<string, EggSaveData>();

    public EggStateSaveData()
    {
        eggStates = new Dictionary<string, EggSaveData>();
    }

    /// <summary>
    /// Stores the state for a specific egg
    /// </summary>
    /// <param name="eggID">Unique ID of the egg</param>
    /// <param name="eggData">The egg's save data</param>
    public void SetEggState(string eggID, EggSaveData eggData)
    {
        if (string.IsNullOrEmpty(eggID))
        {
            Debug.LogWarning("[EggStateSaveData] Attempted to set egg state with null/empty ID");
            return;
        }

        eggStates[eggID] = eggData;
    }

    /// <summary>
    /// Retrieves the state for a specific egg
    /// </summary>
    /// <param name="eggID">Unique ID of the egg</param>
    /// <returns>Egg save data, or null if not found</returns>
    public EggSaveData GetEggState(string eggID)
    {
        if (string.IsNullOrEmpty(eggID))
            return null;

        if (eggStates.TryGetValue(eggID, out var data))
        {
            return data;
        }

        return null;
    }

    /// <summary>
    /// Checks if state exists for a specific egg
    /// </summary>
    public bool HasEggState(string eggID)
    {
        if (string.IsNullOrEmpty(eggID))
            return false;

        return eggStates.ContainsKey(eggID);
    }

    /// <summary>
    /// Removes state for a specific egg
    /// </summary>
    public bool RemoveEggState(string eggID)
    {
        if (string.IsNullOrEmpty(eggID))
            return false;

        return eggStates.Remove(eggID);
    }

    /// <summary>
    /// Gets all tracked egg IDs
    /// </summary>
    public IEnumerable<string> GetAllEggIDs()
    {
        return eggStates.Keys;
    }

    /// <summary>
    /// Clears all egg states
    /// </summary>
    public void ClearAllStates()
    {
        eggStates.Clear();
    }

    /// <summary>
    /// Gets count of tracked eggs
    /// </summary>
    public int Count => eggStates.Count;

    /// <summary>
    /// Validates data integrity
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        if (eggStates == null)
            return false;

        // Validate each egg state
        foreach (var kvp in eggStates)
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
        info.AppendLine($"=== Egg State Save Data ===");
        info.AppendLine($"Scene: {sceneName}");
        info.AppendLine($"Last Updated: {lastUpdated}");
        info.AppendLine($"Tracked Eggs: {eggStates.Count}");

        int brokenCount = 0;
        foreach (var kvp in eggStates)
        {
            if (kvp.Value.isBroken) brokenCount++;
            info.AppendLine($"  - {kvp.Key}: {(kvp.Value.isBroken ? "BROKEN" : "INTACT")}");
        }

        info.AppendLine($"Summary: {brokenCount} broken, {eggStates.Count - brokenCount} intact");

        return info.ToString();
    }

    /// <summary>
    /// Creates a deep copy of this save data
    /// </summary>
    public EggStateSaveData CreateCopy()
    {
        var copy = new EggStateSaveData
        {
            sceneName = sceneName,
            lastUpdated = lastUpdated
        };

        foreach (var kvp in eggStates)
        {
            copy.eggStates[kvp.Key] = new EggSaveData
            {
                isBroken = kvp.Value.isBroken
            };
        }

        return copy;
    }
}