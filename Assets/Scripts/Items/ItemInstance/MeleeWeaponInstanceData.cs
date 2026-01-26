using System;

/// <summary>
/// Instance-specific data for melee weapons.
/// Currently minimal but included for future durability/sharpness systems.
/// </summary>
[Serializable]
public class MeleeWeaponInstanceData
{
    // Melee weapons currently don't have instance-specific state,
    // but this class exists for consistency and future expansion.
    // Future: could add durability, sharpness, modifications, etc.

    /// <summary>
    /// Create instance data from template with default values.
    /// </summary>
    public MeleeWeaponInstanceData(MeleeWeaponData template)
    {
        // Currently no instance-specific data for melee weapons
    }

    /// <summary>
    /// Default constructor for deserialization.
    /// </summary>
    public MeleeWeaponInstanceData()
    {
    }

    /// <summary>
    /// Create a copy of this instance data.
    /// </summary>
    public MeleeWeaponInstanceData CreateCopy()
    {
        return new MeleeWeaponInstanceData();
    }
}