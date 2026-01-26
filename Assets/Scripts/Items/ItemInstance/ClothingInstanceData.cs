using UnityEngine;
using System;

/// <summary>
/// REFACTORED: Instance-specific data for clothing items (MUTABLE STATE).
/// Stores per-item condition/durability that can change during gameplay.
/// Each ItemInstance has its own ClothingInstanceData with independent state.
/// </summary>
[Serializable]
public class ClothingInstanceData
{
    [Header("Clothing State")]
    [Tooltip("Current condition of this clothing item")]
    [Range(0f, 100f)]
    public float currentCondition;

    /// <summary>
    /// Create instance data from template with default values.
    /// </summary>
    public ClothingInstanceData(ClothingData template)
    {
        if (template == null)
        {
            currentCondition = 100f;
            return;
        }

        // Start at max condition from template
        currentCondition = template.maxCondition;
    }

    /// <summary>
    /// Default constructor for deserialization.
    /// </summary>
    public ClothingInstanceData()
    {
        currentCondition = 100f;
    }

    /// <summary>
    /// Create a copy of this instance data.
    /// </summary>
    public ClothingInstanceData CreateCopy()
    {
        return new ClothingInstanceData
        {
            currentCondition = this.currentCondition
        };
    }

    /// <summary>
    /// REFACTORED: Get condition as a percentage (0-1) using template max condition.
    /// </summary>
    public float GetConditionPercentage(ClothingData template)
    {
        if (template == null || template.maxCondition <= 0)
            return 0f;

        return currentCondition / template.maxCondition;
    }

    /// <summary>
    /// REFACTORED: Damage this clothing item using template damage rate.
    /// </summary>
    public void TakeDamage(ClothingData template)
    {
        if (template == null)
        {
            Debug.LogWarning("Cannot damage clothing - template is null");
            return;
        }

        currentCondition = Mathf.Max(0f, currentCondition - template.damageRate);
    }

    /// <summary>
    /// REFACTORED: Damage this clothing item by a specific amount.
    /// </summary>
    public void TakeDamage(float damageAmount)
    {
        currentCondition = Mathf.Max(0f, currentCondition - damageAmount);
    }

    /// <summary>
    /// REFACTORED: Repair this clothing item using template repair rules.
    /// Returns true if repair was successful (within limits).
    /// </summary>
    public bool Repair(float repairAmount, ClothingData template)
    {
        if (template == null)
        {
            Debug.LogWarning("Cannot repair clothing - template is null");
            return false;
        }

        if (!template.canBeRepaired)
        {
            Debug.LogWarning("This clothing item cannot be repaired");
            return false;
        }

        // Calculate the maximum condition this item can be repaired to
        // Some items degrade permanently and can't be repaired to full condition
        float maxRepairTo = Mathf.Max(template.minimumRepairCondition, template.maxCondition);

        float conditionBefore = currentCondition;
        currentCondition = Mathf.Min(maxRepairTo, currentCondition + repairAmount);

        // Return true if condition actually changed
        return currentCondition > conditionBefore;
    }

    /// <summary>
    /// REFACTORED: Check if this item can be repaired using template rules.
    /// </summary>
    public bool CanRepair(ClothingData template)
    {
        if (template == null || !template.canBeRepaired)
            return false;

        // Can only repair if not at maximum repairable condition
        float maxRepairTo = Mathf.Max(template.minimumRepairCondition, template.maxCondition);
        return currentCondition < maxRepairTo;
    }

    /// <summary>
    /// REFACTORED: Check if this item is damaged (below certain threshold).
    /// </summary>
    public bool IsDamaged(ClothingData template)
    {
        if (template == null)
            return false;

        float percentage = GetConditionPercentage(template);
        return percentage < 0.5f; // Below 50% is considered damaged
    }

    /// <summary>
    /// REFACTORED: Get the effective defense value for this instance.
    /// </summary>
    public float GetEffectiveDefense(ClothingData template)
    {
        if (template == null)
            return 0f;

        float percentage = GetConditionPercentage(template);
        return template.GetEffectiveDefense(percentage);
    }

    /// <summary>
    /// REFACTORED: Get the effective warmth value for this instance.
    /// </summary>
    public float GetEffectiveWarmth(ClothingData template)
    {
        if (template == null)
            return 0f;

        float percentage = GetConditionPercentage(template);
        return template.GetEffectiveWarmth(percentage);
    }

    /// <summary>
    /// REFACTORED: Get the effective rain resistance for this instance.
    /// </summary>
    public float GetEffectiveRainResistance(ClothingData template)
    {
        if (template == null)
            return 0f;

        float percentage = GetConditionPercentage(template);
        return template.GetEffectiveRainResistance(percentage);
    }

    /// <summary>
    /// REFACTORED: Get condition description for this instance.
    /// </summary>
    public string GetConditionDescription(ClothingData template)
    {
        if (template == null)
            return "Unknown";

        float percentage = GetConditionPercentage(template);
        return template.GetConditionDescription(percentage);
    }
}