using UnityEngine;
using System.Collections;
using UnityEngine.Splines;


public class FirstPhorcydEncounterQuest : QuestTriggerWithFX
{
    public SplineAnimate phorcydSpline;

    protected override void Awake()
    {
        base.Awake();

        if (phorcydSpline == null)
        {
            phorcydSpline = GetComponentInChildren<SplineAnimate>();
            phorcydSpline.Completed += OnSplineCompleted;
            phorcydSpline.gameObject.SetActive(false);
        }
    }

    protected override void Start()
    {
        base.Start();
    }

    /// <summary>
    /// Play the completion particle effect
    /// </summary>
    protected override void PlayCompletionFX()
    {
        // Only play if particles are assigned
        if (completionParticles != null)
        {
            completionParticles.Play();
            DebugLog("Playing completion particles");
        }

        // Play the sound effect
        if (completionSFX != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound(completionSFX, completionVisual.transform.position, AudioCategory.Ambience);
            DebugLog("Playing completion sound");
        }

        if (phorcydSpline != null)
        {
            phorcydSpline.gameObject.SetActive(true);
            phorcydSpline.Play();
        }
    }

    private void OnSplineCompleted()
    {
        DebugLog("Spline completed");
        if (phorcydSpline != null)
            phorcydSpline.gameObject.SetActive(false);
    }
}