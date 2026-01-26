using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Template data for oxygen tank items (ScriptableObject - immutable configuration).
/// Defines the maximum oxygen capacity for this type of tank.
/// </summary>
[System.Serializable]
public class OxygenTankData
{
    [Header("Tank Capacity")]
    [Tooltip("Maximum oxygen this tank can hold")]
    [Range(0f, 500f)]
    public float maxCapacity = 100f;

    [Header("Refill Settings")]
    [Tooltip("Can this tank be refilled?")]
    public bool canBeRefilled = true;

    [Tooltip("Cost to refill tank (if applicable)")]
    public float refillCost = 10f;

    /// <summary>
    /// Validates that the oxygen tank data is properly configured.
    /// </summary>
    public bool IsValid()
    {
        return maxCapacity > 0f;
    }

    /// <summary>
    /// Gets a user-friendly description of this tank's capacity.
    /// </summary>
    public string GetCapacityDescription()
    {
        return $"{maxCapacity:F0} oxygen capacity";
    }

}