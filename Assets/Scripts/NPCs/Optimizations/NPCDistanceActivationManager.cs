using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the activation and deactivation of NPCs based on proximity to the player.
/// Creates InactiveNPC placeholders for each NPC and swaps between active/inactive states.
/// </summary>
public class NPCDistanceActivationManager : MonoBehaviour
{
    public static NPCDistanceActivationManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Transform inactiveNPCParent;
    [SerializeField] private GameObject inactiveNPCPrefab;

    private Dictionary<NPC, InactiveNPC> npcToInactiveMap = new Dictionary<NPC, InactiveNPC>();

    public static event System.Action<NPC> OnNPCActivated;
    public static event System.Action<NPC> OnNPCDeactivated;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// Create InactiveNPC placeholders for all registered NPCs and set initial states.
    /// </summary>
    public void InitializeInactiveNPCs(List<NPC> registeredNPCs)
    {
        foreach (NPC npc in registeredNPCs)
        {
            // Create an InactiveNPC for this NPC
            GameObject inactiveObj = Instantiate(inactiveNPCPrefab, inactiveNPCParent);
            InactiveNPC inactiveNPC = inactiveObj.GetComponent<InactiveNPC>();

            if (inactiveNPC == null)
            {
                Debug.LogError("InactiveNPC prefab is missing the InactiveNPC component!");
                continue;
            }

            inactiveNPC.AssignNPC(npc);
            npcToInactiveMap[npc] = inactiveNPC;

            DeactivateNPC(npc);
        }
    }

    /// <summary>
    /// Activate an NPC (called when InactiveNPC enters player range).
    /// </summary>
    public void ActivateNPC(NPC npc)
    {
        if (npc == null || !npcToInactiveMap.ContainsKey(npc)) return;

        InactiveNPC inactiveNPC = npcToInactiveMap[npc];

        // Activate real NPC
        npc.gameObject.SetActive(true);

        // Deactivate placeholder
        inactiveNPC.gameObject.SetActive(false);

        OnNPCActivated?.Invoke(npc);
    }

    /// <summary>
    /// Deactivate an NPC (called when real NPC exits player range).
    /// </summary>
    public void DeactivateNPC(NPC npc)
    {
        if (npc == null || !npcToInactiveMap.ContainsKey(npc)) return;

        InactiveNPC inactiveNPC = npcToInactiveMap[npc];

        // Move placeholder to NPC's position
        inactiveNPC.MoveTo(npc.transform.position);

        // Deactivate real NPC
        npc.gameObject.SetActive(false);

        // Activate placeholder
        inactiveNPC.gameObject.SetActive(true);

        OnNPCDeactivated?.Invoke(npc);
    }


}