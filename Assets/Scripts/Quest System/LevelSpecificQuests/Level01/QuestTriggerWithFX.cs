using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Inherits from QuestTrigger but adds particle effects that only play on first trigger,
/// not when restoring from save data.
/// 
/// Visual behavior:
/// - Completion visual shows BOTH on first trigger AND when restored from save
/// - Completion particles play ONLY on first trigger (not on restore if the restored state is already completed)
/// </summary>
public class QuestTriggerWithFX : QuestTrigger
{
    [Header("Completion FX")]
    [Tooltip("Particle system to play when quest completes (only on first trigger)")]
    [SerializeField] protected ParticleSystem completionParticles;

    [SerializeField] protected AudioClip completionSFX;


    /// <summary>
    /// Override Awake to add particle validation
    /// </summary>
    protected override void Awake()
    {
        base.Awake();

        // Validate particle system
        if (completionParticles == null)
        {
            Debug.LogWarning($"[EnterLevel01QuestTrigger:{gameObject.name}] No completion particles assigned!");
        }
    }

    protected override void CompleteQuest()
    {
        base.CompleteQuest();
    }

    protected override void RefreshVisualState()
    {
        base.RefreshVisualState();

        if (shouldApplyQuestCompleteFX)
            PlayCompletionFX();
    }

    /// <summary>
    /// Play the completion particle effect
    /// </summary>
    protected virtual void PlayCompletionFX()
    {
        // Play the particle system
        completionParticles.Play();

        // Play the sound effect
        if (completionSFX != null)
        {
            AudioManager.Instance.PlaySound(completionSFX, completionVisual.transform.position, AudioCategory.Ambience);
        }
    }


}