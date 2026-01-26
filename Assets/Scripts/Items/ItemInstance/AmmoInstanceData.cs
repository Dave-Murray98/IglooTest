using System;
using UnityEngine;

/// <summary>
/// Instance-specific data for ammo items.
/// Now tracks current ammo count for consumption by weapons.
/// </summary>
[Serializable]
public class AmmoInstanceData
{
    [Header("Ammo State")]
    [Tooltip("Current ammo count in this stack")]
    public int currentAmmo;

    /// <summary>
    /// Create instance data from template with default values.
    /// </summary>
    public AmmoInstanceData(AmmoData template)
    {
        if (template == null)
        {
            currentAmmo = 0;
            return;
        }

        // Start with full stack
        currentAmmo = template.maxAmmoPerStack;
    }

    /// <summary>
    /// Default constructor for deserialization.
    /// </summary>
    public AmmoInstanceData()
    {
        currentAmmo = 0;
    }

    /// <summary>
    /// Create a copy of this instance data.
    /// </summary>
    public AmmoInstanceData CreateCopy()
    {
        return new AmmoInstanceData
        {
            currentAmmo = this.currentAmmo
        };
    }

    /// <summary>
    /// Try to consume ammo from this stack.
    /// Returns true if ammo was consumed, false if stack is empty.
    /// </summary>
    public bool ConsumeAmmo(int amount = 1)
    {
        if (currentAmmo < amount)
            return false;

        currentAmmo -= amount;
        return true;
    }

    /// <summary>
    /// Try to take ammo from this stack (for reloading).
    /// Returns the amount actually taken (may be less than requested).
    /// </summary>
    public int TakeAmmo(int requestedAmount)
    {
        int ammoToTake = Mathf.Min(requestedAmount, currentAmmo);
        currentAmmo -= ammoToTake;
        return ammoToTake;
    }

    /// <summary>
    /// Check if this ammo stack is empty and should be removed.
    /// </summary>
    public bool IsEmpty()
    {
        return currentAmmo <= 0;
    }

    /// <summary>
    /// Check if this stack has any ammo available.
    /// </summary>
    public bool HasAmmo()
    {
        return currentAmmo > 0;
    }
}