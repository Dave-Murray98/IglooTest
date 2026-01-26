using System;

/// <summary>
/// Instance-specific data for throwable items.
/// Currently minimal but included for consistency.
/// </summary>
[Serializable]
public class ThrowableInstanceData
{
    // Throwables are typically single-use, so minimal instance data needed.
    // This class exists for consistency and future expansion.

    /// <summary>
    /// Create instance data from template with default values.
    /// </summary>
    public ThrowableInstanceData(ThrowableData template)
    {
        // Currently no instance-specific data for throwables
    }

    /// <summary>
    /// Default constructor for deserialization.
    /// </summary>
    public ThrowableInstanceData()
    {
    }

    /// <summary>
    /// Create a copy of this instance data.
    /// </summary>
    public ThrowableInstanceData CreateCopy()
    {
        return new ThrowableInstanceData();
    }

}