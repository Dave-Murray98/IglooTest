using Sirenix.OdinInspector;
using UnityEngine;

[System.Serializable]
public class ToolData
{
    /// <summary>
    /// Whether the tool's action is triggered by a hold input (like a blowtorch) or a tap input (like placing C4).
    /// </summary>
    public bool isActionHeld = false;

    [Header("Energy Source System")]
    public bool requiresEnergySource = false;

    [Tooltip("Energy Source type this tool uses (for tools that require an energy source)")]
    public ItemData requiredEnergySourceType;

    [Tooltip("Energy consumed per second while tool is actively being used")]
    [Range(0, 100)]
    public int energyConsumptionRate = 1;

    [Tooltip("Maximum capacity of energy sources for this tool")]
    public int maxEnergyCapacity = 100;

    [Header("FX Spawn Config")]
    public Vector3 fxSpawnPoint;
    public Vector3 fxSpawnRotation;

    [Header("Audio")]
    public AudioClip[] reloadClips;
    public AudioClip startUseClip;
    public AudioClip useLoopClip;
    public AudioClip stopUseClip;

}