using UnityEngine;
using System;

/// <summary>
/// Instance-specific data for oxygen tank items (MUTABLE STATE).
/// Stores the current oxygen level for this specific tank instance.
/// Each ItemInstance has its own OxygenTankInstanceData with independent state.
/// </summary>
[Serializable]
public class OxygenTankInstanceData
{
    [Header("Tank State")]
    [Tooltip("Current oxygen in this tank")]
    [Range(0f, 500f)]
    public float currentOxygen;

    /// <summary>
    /// Create instance data from template with full oxygen.
    /// </summary>
    public OxygenTankInstanceData(OxygenTankData template)
    {
        if (template == null)
        {
            currentOxygen = 100f;
            return;
        }

        // Start at max capacity
        currentOxygen = template.maxCapacity;
    }

    /// <summary>
    /// Default constructor for deserialization.
    /// </summary>
    public OxygenTankInstanceData()
    {
        currentOxygen = 100f;
    }

    /// <summary>
    /// Create a copy of this instance data.
    /// </summary>
    public OxygenTankInstanceData CreateCopy()
    {
        return new OxygenTankInstanceData
        {
            currentOxygen = this.currentOxygen
        };
    }

    /// <summary>
    /// Get oxygen as a percentage (0-1) using template max capacity.
    /// </summary>
    public float GetOxygenPercentage(OxygenTankData template)
    {
        if (template == null || template.maxCapacity <= 0)
            return 0f;

        return currentOxygen / template.maxCapacity;
    }

    /// <summary>
    /// Consume oxygen from this tank.
    /// </summary>
    public void ConsumeOxygen(float amount)
    {
        currentOxygen = Mathf.Max(0f, currentOxygen - amount);
    }

    /// <summary>
    /// Add oxygen to this tank (for refilling).
    /// </summary>
    public bool AddOxygen(float amount, OxygenTankData template)
    {
        if (template == null)
        {
            Debug.LogWarning("Cannot add oxygen - template is null");
            return false;
        }

        if (!template.canBeRefilled)
        {
            Debug.LogWarning("This tank cannot be refilled");
            return false;
        }

        float oxygenBefore = currentOxygen;
        currentOxygen = Mathf.Min(template.maxCapacity, currentOxygen + amount);

        // Return true if oxygen actually changed
        return currentOxygen > oxygenBefore;
    }

    /// <summary>
    /// Check if this tank is empty.
    /// </summary>
    public bool IsEmpty()
    {
        return currentOxygen <= 0f;
    }

    /// <summary>
    /// Check if this tank is full.
    /// </summary>
    public bool IsFull(OxygenTankData template)
    {
        if (template == null)
            return false;

        return currentOxygen >= template.maxCapacity;
    }

    /// <summary>
    /// Get the remaining capacity for this tank.
    /// </summary>
    public float GetRemainingCapacity(OxygenTankData template)
    {
        if (template == null)
            return 0f;

        return template.maxCapacity - currentOxygen;
    }
}