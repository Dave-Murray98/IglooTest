using System;

/// <summary>
/// Instance-specific data for tool items.
/// UPDATED: Now tracks equipped energy source for tools that require them.
/// Similar to how weapons track equipped ammo in clip.
/// Tools that require energy start with a default source with 100 energy.
/// </summary>
[Serializable]
public class ToolInstanceData
{
    [UnityEngine.Header("Equipped Energy Source")]
    public string equippedEnergySourceInstanceId = "";
    public int equippedEnergySourceAmount = 0;

    /// <summary>
    /// Create instance data from template with default values.
    /// Tools that require energy start with a default source (100 energy).
    /// </summary>
    public ToolInstanceData(ToolData template)
    {
        if (template == null)
        {
            equippedEnergySourceInstanceId = "";
            equippedEnergySourceAmount = 0;
            return;
        }

        // If tool requires energy source, start with a default source
        if (template.requiresEnergySource && template.requiredEnergySourceType != null)
        {
            // Generate a unique ID for the default energy source
            equippedEnergySourceInstanceId = System.Guid.NewGuid().ToString();
            // Start with full energy (100)
            equippedEnergySourceAmount = template.maxEnergyCapacity;
        }
        else
        {
            // Tools that don't require energy start empty
            equippedEnergySourceInstanceId = "";
            equippedEnergySourceAmount = 0;
        }
    }

    /// <summary>
    /// Default constructor for deserialization.
    /// </summary>
    public ToolInstanceData()
    {
        equippedEnergySourceInstanceId = "";
        equippedEnergySourceAmount = 0;
    }

    /// <summary>
    /// Create a copy of this instance data.
    /// </summary>
    public ToolInstanceData CreateCopy()
    {
        return new ToolInstanceData
        {
            equippedEnergySourceInstanceId = this.equippedEnergySourceInstanceId,
            equippedEnergySourceAmount = this.equippedEnergySourceAmount
        };
    }

    /// <summary>
    /// Check if tool has an energy source equipped.
    /// </summary>
    public bool HasEnergySourceEquipped()
    {
        return !string.IsNullOrEmpty(equippedEnergySourceInstanceId);
    }

    /// <summary>
    /// Check if equipped energy source has energy available.
    /// </summary>
    public bool HasEnergyAvailable()
    {
        return HasEnergySourceEquipped() && equippedEnergySourceAmount > 0;
    }

    /// <summary>
    /// Try to consume energy from equipped source.
    /// Returns true if energy was consumed.
    /// </summary>
    public bool TryConsumeEnergy(int amount)
    {
        if (!HasEnergyAvailable() || amount <= 0)
            return false;

        if (equippedEnergySourceAmount < amount)
            return false;

        equippedEnergySourceAmount -= amount;
        return true;
    }

    /// <summary>
    /// Get energy status string for UI.
    /// </summary>
    public string GetEnergyStatus(int maxCapacity)
    {
        if (!HasEnergySourceEquipped())
            return "No Energy Source";

        return $"{equippedEnergySourceAmount}/{maxCapacity}";
    }
}