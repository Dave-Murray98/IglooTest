using Sirenix.OdinInspector;
using UnityEngine;

[System.Serializable]
public class ToolEnergySourceData
{
    [Header("Ammo Properties")]
    [Tooltip("Tools that can use this energy source type")]
    public ItemData[] compatibleTools;


    [Header("Ammo Stack Configuration")]
    [Tooltip("Maximum capacity count per stack/item instance")]
    [Range(1, 1000)]
    public int maxEnergyCapacity = 50;

}
