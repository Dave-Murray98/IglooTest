using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages all NPCs in the scene. Provides central registration and player reference.
/// </summary>
public class NPCManager : MonoBehaviour
{
    public static NPCManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Transform playerTransform;

    public Transform PlayerTransform => playerTransform;

    public List<NPC> registeredNPCs = new List<NPC>();


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        foreach (NPC npc in GetComponentsInChildren<NPC>())
        {
            RegisterNPC(npc);
        }

    }

    private void Start()
    {
        NPCDistanceActivationManager.Instance.InitializeInactiveNPCs(registeredNPCs);
    }

    /// <summary>
    /// Register an NPC with the manager.
    /// </summary>
    public void RegisterNPC(NPC npc)
    {
        registeredNPCs.Add(npc);
    }

    /// <summary>
    /// Unregister an NPC from the manager.
    /// </summary>
    public void UnregisterNPC(NPC npc)
    {
        registeredNPCs.Remove(npc);
    }

    /// <summary>
    /// Get count of registered NPCs (useful for debugging).
    /// </summary>
    public int GetNPCCount()
    {
        return registeredNPCs.Count;
    }
}