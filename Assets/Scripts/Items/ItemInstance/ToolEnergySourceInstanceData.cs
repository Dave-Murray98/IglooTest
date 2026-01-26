using System;
using UnityEngine;

/// <summary>
/// Instance-specific data for Tool energy source items.
/// Now tracks current ammo count for consumption by tools.
/// </summary>
[Serializable]
public class ToolEnergySourceInstanceData
{
    public int currentEnergy = 0;

    /// <summary>
    /// Create instance data from template with default values.
    /// </summary>
    public ToolEnergySourceInstanceData(ToolEnergySourceData template)
    {
        if (template == null)
        {
            currentEnergy = 0;
            return;
        }

        // Start with full stack
        currentEnergy = template.maxEnergyCapacity;
    }

    /// <summary>
    /// Default constructor for deserialization.
    /// </summary>
    public ToolEnergySourceInstanceData()
    {
        currentEnergy = 0;
    }

    /// <summary>
    /// Create a copy of this instance data.
    /// </summary>
    public ToolEnergySourceInstanceData CreateCopy()
    {
        return new ToolEnergySourceInstanceData
        {
            currentEnergy = this.currentEnergy
        };
    }

    /// <summary>
    /// Try to consume energy from this stack.
    /// Returns true if ebergy was consumed, false if stack is empty.
    /// </summary>
    public bool ConsumeEnergy(int amount = 1)
    {
        if (currentEnergy < amount)
            return false;

        currentEnergy -= amount;
        return true;
    }

    /// <summary>
    /// Try to take energy from this stack (for reloading).
    /// Returns the amount actually taken (may be less than requested).
    /// </summary>
    public int TakeEnergy(int requestedAmount)
    {
        int energyToTake = Mathf.Min(requestedAmount, currentEnergy);
        currentEnergy -= energyToTake;
        return energyToTake;
    }

    /// <summary>
    /// Check if this energy stack is empty and should be removed.
    /// </summary>
    public bool IsEmpty()
    {
        return currentEnergy <= 0;
    }

    /// <summary>
    /// Check if this stack has any energy available.
    /// </summary>
    public bool HasEnergy()
    {
        return currentEnergy > 0;
    }
}
