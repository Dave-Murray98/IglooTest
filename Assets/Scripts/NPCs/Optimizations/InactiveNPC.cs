using UnityEngine;

/// <summary>
/// Lightweight placeholder that represents a disabled NPC.
/// When this enters the player's proximity zone, it triggers the real NPC to reactivate.
/// </summary>
public class InactiveNPC : MonoBehaviour
{
    private NPC assignedNPC;

    public NPC AssignedNPC => assignedNPC;

    /// <summary>
    /// Assign which real NPC this placeholder represents.
    /// </summary>
    public void AssignNPC(NPC npc)
    {
        assignedNPC = npc;
    }

    /// <summary>
    /// Move this placeholder to a specific position.
    /// </summary>
    public void MoveTo(Vector3 position)
    {
        transform.position = position;
    }
}