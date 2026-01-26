using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Handles damage to audio logs, inheriting from MonsterHitBox
/// When health depletes, destroys the audio log
/// </summary>
public class AudioLogHitBox : MonsterHitBox
{
    [Title("Components")]
    [SerializeField] private AudioLog audioLog;

    [Title("Damage Feedback")]
    [SerializeField] private AudioClip hitSound;

    private void Awake()
    {
        // Find AudioLog component if not assigned
        if (audioLog == null)
        {
            audioLog = GetComponentInParent<AudioLog>();
            if (audioLog == null)
            {
                audioLog = GetComponent<AudioLog>();
            }
        }

        if (audioLog == null)
        {
            Debug.LogError($"[AudioLogHitBox] No AudioLog component found!");
        }
    }

    /// <summary>
    /// Apply damage to the audio log
    /// </summary>
    [Button]
    public override void TakeDamage(float damage, Vector3 hitPoint)
    {
        // Don't take damage if already destroyed
        if (audioLog != null && audioLog.IsDestroyed)
        {
            DebugLog("Audio log already destroyed - ignoring damage");
            return;
        }

        DebugLog("Health depleted - destroying audio log");
        DestroyAudioLog();

        ParticleFXPool.Instance.GetImpactFX(hitPoint, Quaternion.identity);
        ParticleFXPool.Instance.GetDestroyedAudioLogFX(hitPoint, Quaternion.identity);
    }

    /// <summary>
    /// Destroys the audio log (delegates to AudioLog component)
    /// </summary>
    private void DestroyAudioLog()
    {
        if (audioLog != null)
        {
            audioLog.DestroyAudioLog();
        }
        else
        {
            Debug.LogError($"[AudioLogHitBox] Cannot destroy - no AudioLog reference!");
        }
    }

    /// <summary>
    /// Optional: Stun functionality (currently does nothing for audio logs)
    /// Included for interface compatibility with MonsterHitBox pattern
    /// </summary>
    public override void StunMonster(float stunDuration)
    {
        // Audio logs don't get stunned, but method exists for compatibility
        DebugLog($"Stun called with duration {stunDuration} (audio logs don't implement stun)");
    }

    protected override void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[AudioLogHitBox:{audioLog?.AudioLogID ?? "Unknown"}] {message}");
        }
    }

}