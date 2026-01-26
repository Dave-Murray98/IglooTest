using UnityEngine;

/// <summary>
/// Complete oxygen tank system state for saving/loading with ItemInstance support.
/// Stores the equipped tank with its ItemInstance data for accurate restoration.
/// Pattern based on ClothingSaveData.cs but simplified for single slot.
/// </summary>
[System.Serializable]
public class OxygenTankSaveData
{
    [Header("Tank Slot")]
    public OxygenTankSlotSaveData slot;

    public OxygenTankSaveData()
    {
        slot = new OxygenTankSlotSaveData();
    }

    /// <summary>
    /// Check if a tank is equipped
    /// </summary>
    public bool HasTankEquipped()
    {
        return slot != null && slot.IsOccupied;
    }

    /// <summary>
    /// Get the equipped tank ID
    /// </summary>
    public string GetEquippedTankId()
    {
        return slot?.equippedTankId ?? "";
    }

    /// <summary>
    /// Validate that the save data is consistent
    /// </summary>
    public bool IsValid()
    {
        return slot != null && slot.IsValid();
    }

    /// <summary>
    /// Gets debug information about the tank save data including ItemInstance info.
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== Oxygen Tank Save Data Debug Info ===");

        if (slot == null)
        {
            info.AppendLine("Slot: NULL");
        }
        else if (slot.IsEmpty)
        {
            info.AppendLine("Slot: Empty");
        }
        else
        {
            info.AppendLine(slot.ToString());
        }

        return info.ToString();
    }
}

