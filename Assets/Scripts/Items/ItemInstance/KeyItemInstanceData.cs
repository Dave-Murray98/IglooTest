using System;

/// <summary>
/// Instance-specific data for key items.
/// Currently minimal but included for consistency.
/// </summary>
[Serializable]
public class KeyItemInstanceData
{
    // Key items typically don't have instance-specific state.
    // This class exists for consistency and future expansion.

    /// <summary>
    /// Create instance data from template with default values.
    /// </summary>
    public KeyItemInstanceData(KeyItemData template)
    {
        // Currently no instance-specific data for key items
    }

    /// <summary>
    /// Default constructor for deserialization.
    /// </summary>
    public KeyItemInstanceData()
    {
    }

    /// <summary>
    /// Create a copy of this instance data.
    /// </summary>
    public KeyItemInstanceData CreateCopy()
    {
        return new KeyItemInstanceData();
    }
}