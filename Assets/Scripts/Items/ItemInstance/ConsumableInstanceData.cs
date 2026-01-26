using UnityEngine;
using System;

/// <summary>
/// Instance-specific data for consumable items.
/// Stores mutable state like remaining uses for multi-use consumables.
/// </summary>

[Serializable]
public class ConsumableInstanceData
{

    /// <summary>
    /// Create instance data from template with default values.
    /// </summary>
    public ConsumableInstanceData(ConsumableData template)
    {
        if (template == null)
        {
            return;
        }
    }

    /// <summary>
    /// Default constructor for deserialization.
    /// </summary>
    public ConsumableInstanceData()
    {
    }

    /// <summary>
    /// Create a copy of this instance data.
    /// </summary>
    public ConsumableInstanceData CreateCopy()
    {
        return new ConsumableInstanceData
        {
        };
    }

}