using UnityEngine;

/// <summary>
/// Detects when NPCs (real or inactive) enter and exit the player's proximity zone.
/// Attach this to a child GameObject of the player with a sphere trigger collider.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class PlayerProximityZone : MonoBehaviour
{

    private SphereCollider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<SphereCollider>();
        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if it's an InactiveNPC entering
        InactiveNPC inactiveNPC = other.GetComponent<InactiveNPC>();
        if (inactiveNPC != null && inactiveNPC.AssignedNPC != null)
        {
            NPCDistanceActivationManager.Instance?.ActivateNPC(inactiveNPC.AssignedNPC);
            return;
        }

        // Check if it's a real NPC entering (shouldn't happen, but just in case)
        NPC npc = other.GetComponent<NPC>();
        if (npc != null)
        {
            // Real NPC entering means it's already active, so do nothing
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Only real NPCs can exit (InactiveNPCs don't move)
        NPC npc = other.GetComponent<NPC>();
        if (npc != null)
        {
            NPCDistanceActivationManager.Instance?.DeactivateNPC(npc);
        }
    }
}