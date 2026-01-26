using System;
using UnityEngine;

/// <summary>
/// Instance-specific data for bow items.
/// UPDATED: Now tracks if an arrow is nocked and ready to fire.
/// </summary>
[Serializable]
public class BowInstanceData
{
    [Header("Bow State")]
    [Tooltip("Whether an arrow is currently nocked and ready to fire")]
    public bool hasArrowNocked;

    /// <summary>
    /// Create instance data from template with default values.
    /// </summary>
    public BowInstanceData(BowData template)
    {
        // Start with no arrow nocked (will nock on equip if arrows available)
        hasArrowNocked = false;
    }

    /// <summary>
    /// Default constructor for deserialization.
    /// </summary>
    public BowInstanceData()
    {
        hasArrowNocked = false;
    }

    /// <summary>
    /// Create a copy of this instance data.
    /// </summary>
    public BowInstanceData CreateCopy()
    {
        return new BowInstanceData
        {
            hasArrowNocked = this.hasArrowNocked
        };
    }

    /// <summary>
    /// Nock an arrow, making the bow ready to fire.
    /// </summary>
    public void NockArrow()
    {
        hasArrowNocked = true;
    }

    /// <summary>
    /// Consume the nocked arrow (when firing).
    /// Returns true if an arrow was nocked and consumed.
    /// </summary>
    public bool ConsumeArrow()
    {
        if (!hasArrowNocked)
            return false;

        hasArrowNocked = false;
        return true;
    }

    /// <summary>
    /// Check if bow is ready to fire.
    /// </summary>
    public bool IsReadyToFire()
    {
        return hasArrowNocked;
    }

    /// <summary>
    /// Clear arrow state (when unequipping or out of ammo).
    /// </summary>
    public void ClearArrow()
    {
        hasArrowNocked = false;
    }
}