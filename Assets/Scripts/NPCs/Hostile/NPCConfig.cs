using UnityEngine;

/// <summary>
/// Configuration for different NPC types.
/// Defines whether an NPC is passive or hostile, and what behaviors it should have.
/// </summary>
[CreateAssetMenu(fileName = "New NPC Config", menuName = "NPC/NPC Config")]
public class NPCConfig : ScriptableObject
{
    [Header("NPC Type")]
    [Tooltip("Is this NPC hostile to the player?")]
    public bool isHostile = false;

    [Header("Combat Settings")]
    [Tooltip("Distance at which hostile NPCs enter combat state")]
    [SerializeField] private float combatEngageDistance = 10f;

    [Tooltip("Distance at which hostile NPCs exit combat state (should be larger than engage distance to prevent flickering)")]
    [SerializeField] private float combatDisengageDistance = 15f;

    public float CombatEngageDistance => combatEngageDistance;
    public float CombatDisengageDistance => combatDisengageDistance;
}