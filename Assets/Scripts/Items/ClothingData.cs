using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// REFACTORED: Pure template data for clothing items (READ ONLY).
/// All mutable state has been moved to ClothingInstanceData.
/// This ScriptableObject defines the rules and base stats - ItemInstance holds the current state.
/// </summary>
[System.Serializable]
public class ClothingData
{
    [Header("Clothing Type & Layers")]
    [Tooltip("What type of clothing this is (Head, Torso, etc.)")]
    public ClothingType clothingType;

    [Tooltip("Which layers this clothing can be equipped to")]
    public ClothingLayer[] validLayers;

    [Header("Defensive Stats (Template Values)")]
    [Tooltip("Physical damage reduction provided by this clothing at full condition")]
    [Range(0f, 100f)]
    public float defenseValue = 0f;

    [Tooltip("Cold weather protection provided by this clothing at full condition")]
    [Range(0f, 100f)]
    public float warmthValue = 0f;

    [Tooltip("Rain and water resistance provided by this clothing at full condition")]
    [Range(0f, 100f)]
    public float rainResistance = 0f;

    [Header("Condition System (Template Rules)")]
    [Tooltip("Maximum condition/durability of this clothing item")]
    [Range(0f, 100f)]
    public float maxCondition = 100f;

    [Tooltip("How much condition is lost when damaged")]
    [Range(0f, 10f)]
    public float damageRate = 1f;

    [Header("Repair Settings (Template Rules)")]
    [Tooltip("Can this clothing item be repaired?")]
    public bool canBeRepaired = true;

    [Tooltip("Minimum condition this item can be repaired to (wear and tear)")]
    [Range(0f, 100f)]
    public float minimumRepairCondition = 20f;

    /// <summary>
    /// Checks if this clothing can be equipped to the specified layer
    /// </summary>
    public bool CanEquipToLayer(ClothingLayer layer)
    {
        return System.Array.Exists(validLayers, l => l == layer);
    }

    /// <summary>
    /// Gets the effective defense value based on condition percentage.
    /// Template method - takes instance condition as parameter.
    /// </summary>
    public float GetEffectiveDefense(float conditionPercentage)
    {
        return defenseValue * conditionPercentage;
    }

    /// <summary>
    /// Gets the effective warmth value based on condition percentage.
    /// Template method - takes instance condition as parameter.
    /// </summary>
    public float GetEffectiveWarmth(float conditionPercentage)
    {
        return warmthValue * conditionPercentage;
    }

    /// <summary>
    /// Gets the effective rain resistance based on condition percentage.
    /// Template method - takes instance condition as parameter.
    /// </summary>
    public float GetEffectiveRainResistance(float conditionPercentage)
    {
        return rainResistance * conditionPercentage;
    }

    /// <summary>
    /// Gets a user-friendly condition description based on percentage.
    /// Template method - takes instance condition as parameter.
    /// </summary>
    public string GetConditionDescription(float conditionPercentage)
    {
        if (conditionPercentage >= 0.9f) return "Excellent";
        if (conditionPercentage >= 0.7f) return "Good";
        if (conditionPercentage >= 0.5f) return "Fair";
        if (conditionPercentage >= 0.3f) return "Poor";
        if (conditionPercentage >= 0.1f) return "Very Poor";
        return "Ruined";
    }
}

/// <summary>
/// Types of clothing based on body area
/// </summary>
public enum ClothingType
{
    Head,    // Hats, helmets, masks, scarves
    Torso,   // Shirts, jackets, vests, coats
    Hands,   // Gloves, mittens
    Legs,    // Pants, shorts, leggings
    Socks,   // Socks, stockings
    Shoes    // Boots, shoes, sandals
}

/// <summary>
/// Specific equipment layers for clothing items.
/// Allows for layering system (inner/outer layers).
/// </summary>
public enum ClothingLayer
{
    // Head layers (2 slots total)
    HeadUpper,   // Hat slot - hats, helmets, caps
    HeadLower,   // Scarf slot - scarves, face masks, neck warmers

    // Torso layers (2 slots total)  
    TorsoInner,  // Inner layer - t-shirts, tank tops, base layers
    TorsoOuter,  // Outer layer - jackets, coats, vests, hoodies

    // Leg layers (2 slots total)
    LegsInner,   // Inner layer - thermal underwear, leggings, base layers
    LegsOuter,   // Outer layer - pants, jeans, shorts, overalls

    // Single layers (1 slot each)
    Hands,       // Gloves, mittens
    Socks,       // Socks, stockings  
    Shoes        // Boots, shoes, sandals
}