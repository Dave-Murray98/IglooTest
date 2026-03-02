using UnityEngine;

/// <summary>
/// Interface for quest requirements that must be met before a quest can be completed.
/// Simple and extensible - add new requirement types as needed.
/// </summary>
public interface IQuestRequirement
{
    /// <summary>
    /// Check if this requirement is currently met
    /// </summary>
    /// <param name="player">The player attempting the quest</param>
    /// <returns>True if requirement is satisfied</returns>
    bool IsMet(GameObject player);

    /// <summary>
    /// Get a description of why the requirement failed (for UI feedback)
    /// </summary>
    /// <returns>Failure message to show player</returns>
    string GetFailureMessage();
}