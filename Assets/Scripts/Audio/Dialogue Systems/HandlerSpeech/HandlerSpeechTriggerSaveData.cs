using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Save data structure for handler speech trigger states
/// Tracks which triggers have been activated to prevent re-triggering after saves/loads
/// </summary>
[System.Serializable]
public class HandlerSpeechTriggerSaveData
{
    [Header("Trigger States")]
    public List<TriggerStateEntry> triggerStates = new List<TriggerStateEntry>();

    public HandlerSpeechTriggerSaveData()
    {
        triggerStates = new List<TriggerStateEntry>();
    }

    /// <summary>
    /// Adds or updates a trigger's state
    /// </summary>
    public void SetTriggerState(string triggerID, bool hasTriggered)
    {
        // Check if entry already exists
        var existingEntry = triggerStates.Find(t => t.triggerID == triggerID);

        if (existingEntry != null)
        {
            existingEntry.hasTriggered = hasTriggered;
        }
        else
        {
            triggerStates.Add(new TriggerStateEntry
            {
                triggerID = triggerID,
                hasTriggered = hasTriggered
            });
        }
    }

    /// <summary>
    /// Gets a trigger's state
    /// </summary>
    public bool GetTriggerState(string triggerID)
    {
        var entry = triggerStates.Find(t => t.triggerID == triggerID);
        return entry?.hasTriggered ?? false;
    }

    /// <summary>
    /// Checks if a trigger state exists
    /// </summary>
    public bool HasTriggerState(string triggerID)
    {
        return triggerStates.Exists(t => t.triggerID == triggerID);
    }

    /// <summary>
    /// Gets count of tracked triggers
    /// </summary>
    public int TriggerCount => triggerStates.Count;

    /// <summary>
    /// Gets count of triggered triggers
    /// </summary>
    public int TriggeredCount => triggerStates.FindAll(t => t.hasTriggered).Count;

    /// <summary>
    /// Validates the save data
    /// </summary>
    public bool IsValid()
    {
        if (triggerStates == null)
            return false;

        // Check for duplicate IDs
        var uniqueIds = new HashSet<string>();
        foreach (var entry in triggerStates)
        {
            if (string.IsNullOrEmpty(entry.triggerID))
                return false;

            if (!uniqueIds.Add(entry.triggerID))
                return false; // Duplicate found
        }

        return true;
    }

    /// <summary>
    /// Gets debug info about the save data
    /// </summary>
    public string GetDebugInfo()
    {
        return $"HandlerSpeechTriggerSaveData: Total={TriggerCount}, Triggered={TriggeredCount}";
    }
}

/// <summary>
/// Individual trigger state entry
/// </summary>
[System.Serializable]
public class TriggerStateEntry
{
    public string triggerID;
    public bool hasTriggered;

    public TriggerStateEntry()
    {
    }

    public TriggerStateEntry(string id, bool triggered)
    {
        triggerID = id;
        hasTriggered = triggered;
    }
}