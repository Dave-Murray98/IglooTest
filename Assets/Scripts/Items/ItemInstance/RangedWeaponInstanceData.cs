using UnityEngine;
using System;

/// <summary>
/// Instance-specific data for ranged weapons.
/// Stores mutable state like current ammo loaded in the weapon's clip.
/// UPDATED: Proper clip management for ammo system integration.
/// </summary>
[Serializable]
public class RangedWeaponInstanceData
{
    [Header("Weapon Clip State")]
    [Tooltip("Current ammo loaded in weapon's clip/magazine")]
    public int currentAmmoInClip;

    /// <summary>
    /// Create instance data from template with default values.
    /// </summary>
    public RangedWeaponInstanceData(RangedWeaponData template)
    {
        if (template == null)
        {
            currentAmmoInClip = 0;
            return;
        }

        // Start with default starting ammo from template
        currentAmmoInClip = template.defaultStartingAmmo;
    }

    /// <summary>
    /// Default constructor for deserialization.
    /// </summary>
    public RangedWeaponInstanceData()
    {
        currentAmmoInClip = 0;
    }

    /// <summary>
    /// Create a copy of this instance data.
    /// </summary>
    public RangedWeaponInstanceData CreateCopy()
    {
        return new RangedWeaponInstanceData
        {
            currentAmmoInClip = this.currentAmmoInClip
        };
    }

    /// <summary>
    /// Check if weapon has ammo in clip to fire.
    /// </summary>
    public bool HasAmmoInClip => currentAmmoInClip > 0;

    /// <summary>
    /// Try to fire the weapon, consuming one ammo from clip.
    /// Returns true if shot was successful.
    /// </summary>
    public bool TryFire()
    {
        if (currentAmmoInClip > 0)
        {
            currentAmmoInClip--;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Load ammo into the clip from inventory ammo.
    /// Returns the amount of ammo actually loaded.
    /// </summary>
    public int LoadClip(int ammoAvailable, int maxClipSize)
    {
        // Calculate how much we can load
        int spaceInClip = maxClipSize - currentAmmoInClip;
        int ammoToLoad = Mathf.Min(ammoAvailable, spaceInClip);

        // Load the ammo
        currentAmmoInClip += ammoToLoad;

        return ammoToLoad; // Return how much we actually used
    }

    /// <summary>
    /// Check if clip is full.
    /// </summary>
    public bool IsClipFull(int maxClipSize)
    {
        return currentAmmoInClip >= maxClipSize;
    }

    /// <summary>
    /// Check if clip needs reloading (empty or not full).
    /// </summary>
    public bool NeedsReload(int maxClipSize)
    {
        return currentAmmoInClip < maxClipSize;
    }

    /// <summary>
    /// Get clip status as string for UI.
    /// </summary>
    public string GetClipStatus(int maxClipSize)
    {
        return $"{currentAmmoInClip}/{maxClipSize}";
    }
}