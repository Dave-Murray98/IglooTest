using System.Collections;
using UnityEngine;

/// <summary>
/// Base hazard script that deals instant damage when the player touches it.
/// Perfect for spikes, electric fences (one-time shock), etc.
/// </summary>
public class Hazard : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("How much damage this hazard deals")]
    [SerializeField] protected float damage = 10;
    public float Damage { get => damage; set => damage = value; }

    [Header("Debug Settings")]
    [SerializeField] protected bool enableDebugLogs = false;

    [Header("Audio")]
    public AudioClip[] damageAudioClips;

    /// <summary>
    /// Called when something enters the hazard's trigger.
    /// Virtual so child classes can override this behavior.
    /// </summary>
    protected virtual void OnTriggerEnter(Collider other)
    {
        DebugLog($"{other.name} entered hazard");

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();

        if (playerHealth != null)
        {
            DebugLog($"Dealing {damage} damage to {other.name}");
            DealDamage(playerHealth);
        }
        else
        {
            DebugLog($"No PlayerHealth component found on {other.name}");
        }
    }

    /// <summary>
    /// Deals damage to the player.
    /// Virtual so child classes can override how damage is dealt.
    /// </summary>
    protected virtual void DealDamage(PlayerHealth playerHealth)
    {
        playerHealth.TakeDamage(damage);

        if (damageAudioClips.Length > 0)
        {
            AudioClip randomClip = damageAudioClips[Random.Range(0, damageAudioClips.Length)];
            AudioManager.Instance.PlaySound2D(randomClip, AudioCategory.PlayerSFX);
        }

    }

    /// <summary>
    /// Helper method for debug logging that respects the enableDebugLogs setting.
    /// </summary>
    public virtual void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[Hazard - {gameObject.name}] {message}");
        }
    }
}