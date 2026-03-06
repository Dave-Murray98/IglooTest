using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// Controls underwater ambience audio based on the submarine's water depth.
/// Plays different ambient loops for above water, surface, deep, and very deep environments.
/// All filtering/muffling is handled by the mixer groups, not here.
/// </summary>
public class UnderwaterAmbienceController : MonoBehaviour
{
    [Header("Ambience Audio Clips")]
    [SerializeField] private AudioClip aboveWaterAmbienceClip;
    [SerializeField] private AudioClip surfaceAmbienceClip;
    [SerializeField] private AudioClip deepAmbienceClip;
    [SerializeField] private AudioClip veryDeepAmbienceClip;

    [Header("Transition Sound Effects")]
    [SerializeField] private AudioClip submergeClip;
    [SerializeField] private AudioClip breachClip;
    [SerializeField][Range(0f, 1f)] private float transitionSFXVolume = 1f;

    [Header("Depth Thresholds")]
    [SerializeField] private float surfaceDepthThreshold = 1f;
    [SerializeField] private float deepDepthThreshold = 3f;
    [SerializeField] private float veryDeepDepthThreshold = 10f;
    [SerializeField] private float submarineSubmersionThreshold = 0.5f;

    [Header("Volume Settings")]
    [SerializeField] private float maxAboveWaterVolume = 0.5f;
    [SerializeField] private float maxSurfaceVolume = 0.6f;
    [SerializeField] private float maxDeepVolume = 0.8f;
    [SerializeField] private float maxVeryDeepVolume = 0.9f;

    [Header("Transition Settings")]
    [SerializeField] private float crossfadeDuration = 2f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private PlayerWaterDetector waterDetector;

    private AudioSource aboveWaterSource;
    private AudioSource surfaceSource;
    private AudioSource deepSource;
    private AudioSource veryDeepSource;
    private AudioSource transitionSFXSource;

    private Tweener aboveWaterTween;
    private Tweener surfaceTween;
    private Tweener deepTween;
    private Tweener veryDeepTween;

    private bool isSubmarineSubmerged = false;
    private float currentDepth = 0f;

    private enum AmbienceState
    {
        AboveWater,
        Surface,
        Transitioning,
        Deep,
        VeryDeep
    }

    private AmbienceState currentState = AmbienceState.AboveWater;
    private AmbienceState targetState = AmbienceState.AboveWater;

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        waterDetector = FindFirstObjectByType<PlayerWaterDetector>();
        if (waterDetector == null)
            Debug.LogWarning("[UnderwaterAmbience] PlayerWaterDetector not found.");

        CreateAudioSources();
        InitialiseState();
    }

    private void Update()
    {
        if (waterDetector == null) return;

        isSubmarineSubmerged = waterDetector.IsSubmarineUnderwater;
        currentDepth = waterDetector.SubmarineDepth;

        AmbienceState newTarget = DetermineTargetState();
        if (newTarget != targetState)
        {
            DebugLog($"State: {targetState} → {newTarget} (depth {currentDepth:F2}m)");
            targetState = newTarget;
            TransitionToState(targetState);
        }

        // Continuously update crossfade volumes while in transition zone
        if (currentState == AmbienceState.Transitioning)
            UpdateCrossfadeVolumes();
    }

    private void OnDestroy()
    {
        aboveWaterTween?.Kill();
        surfaceTween?.Kill();
        deepTween?.Kill();
        veryDeepTween?.Kill();
    }

    // -----------------------------------------------------------------------
    // Setup
    // -----------------------------------------------------------------------

    private void CreateAudioSources()
    {
        aboveWaterSource = CreateSource("AboveWater", aboveWaterAmbienceClip);
        surfaceSource = CreateSource("Surface", surfaceAmbienceClip);
        deepSource = CreateSource("Deep", deepAmbienceClip);
        veryDeepSource = CreateSource("VeryDeep", veryDeepAmbienceClip);
        transitionSFXSource = CreateSource("TransitionSFX", null, loop: false);
    }

    private AudioSource CreateSource(string label, AudioClip clip, bool loop = true)
    {
        var go = new GameObject($"Ambience_{label}");
        go.transform.SetParent(transform);

        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = loop;
        src.playOnAwake = false;
        src.volume = 0f;
        src.spatialBlend = 0f; // 2D — ambience fills the whole space

        // All ambience comes from outside the hull
        RouteToAudioManager(src, AudioCategory.Ambience, AudioLayer.Exterior);

        return src;
    }

    /// <summary>
    /// Asks AudioManager for the correct mixer group and assigns it directly.
    /// This keeps routing consistent with the rest of the audio system.
    /// </summary>
    private void RouteToAudioManager(AudioSource source, AudioCategory category, AudioLayer layer)
    {
        if (AudioManager.Instance == null) return;

        // AudioManager exposes its mixer groups so we can assign them here
        var group = layer == AudioLayer.Exterior
            ? AudioManager.Instance.exteriorMixerGroup
            : AudioManager.Instance.interiorMixerGroup;

        if (group != null)
            source.outputAudioMixerGroup = group;
        else
            Debug.LogWarning("[UnderwaterAmbience] Could not retrieve mixer group from AudioManager.");
    }

    private void InitialiseState()
    {
        // Start silent — first Update will determine and apply the correct state
        targetState = AmbienceState.AboveWater;
        currentState = AmbienceState.AboveWater;
    }

    // -----------------------------------------------------------------------
    // State Machine
    // -----------------------------------------------------------------------

    private AmbienceState DetermineTargetState()
    {
        if (!isSubmarineSubmerged || currentDepth < submarineSubmersionThreshold)
            return AmbienceState.AboveWater;

        if (currentDepth < surfaceDepthThreshold) return AmbienceState.Surface;
        if (currentDepth > veryDeepDepthThreshold) return AmbienceState.VeryDeep;
        if (currentDepth > deepDepthThreshold) return AmbienceState.Deep;

        return AmbienceState.Transitioning;
    }

    private void TransitionToState(AmbienceState newState)
    {
        currentState = newState;

        switch (newState)
        {
            case AmbienceState.AboveWater:
                // Only play breach SFX if we were underwater before
                if (currentState != AmbienceState.AboveWater)
                    PlayTransitionSFX(breachClip);

                FadeTo(aboveWaterSource, ref aboveWaterTween, maxAboveWaterVolume);
                FadeTo(surfaceSource, ref surfaceTween, 0f, stopOnZero: true);
                FadeTo(deepSource, ref deepTween, 0f, stopOnZero: true);
                FadeTo(veryDeepSource, ref veryDeepTween, 0f, stopOnZero: true);
                break;

            case AmbienceState.Surface:
                if (currentState == AmbienceState.AboveWater)
                    PlayTransitionSFX(submergeClip);

                FadeTo(surfaceSource, ref surfaceTween, maxSurfaceVolume);
                FadeTo(aboveWaterSource, ref aboveWaterTween, 0f, stopOnZero: true);
                FadeTo(deepSource, ref deepTween, 0f, stopOnZero: true);
                FadeTo(veryDeepSource, ref veryDeepTween, 0f, stopOnZero: true);
                break;

            case AmbienceState.Transitioning:
                EnsurePlaying(surfaceSource);
                EnsurePlaying(deepSource);
                FadeTo(aboveWaterSource, ref aboveWaterTween, 0f, stopOnZero: true);
                FadeTo(veryDeepSource, ref veryDeepTween, 0f, stopOnZero: true);
                // Actual crossfade volumes handled in UpdateCrossfadeVolumes()
                break;

            case AmbienceState.Deep:
                FadeTo(deepSource, ref deepTween, maxDeepVolume);
                FadeTo(aboveWaterSource, ref aboveWaterTween, 0f, stopOnZero: true);
                FadeTo(surfaceSource, ref surfaceTween, 0f, stopOnZero: true);
                FadeTo(veryDeepSource, ref veryDeepTween, 0f, stopOnZero: true);
                break;

            case AmbienceState.VeryDeep:
                FadeTo(veryDeepSource, ref veryDeepTween, maxVeryDeepVolume);
                FadeTo(aboveWaterSource, ref aboveWaterTween, 0f, stopOnZero: true);
                FadeTo(surfaceSource, ref surfaceTween, 0f, stopOnZero: true);
                FadeTo(deepSource, ref deepTween, 0f, stopOnZero: true);
                break;
        }
    }

    private void UpdateCrossfadeVolumes()
    {
        float depthRange = deepDepthThreshold - surfaceDepthThreshold;
        float t = Mathf.Clamp01((currentDepth - surfaceDepthThreshold) / depthRange);

        if (surfaceSource != null && surfaceSource.isPlaying)
            surfaceSource.volume = maxSurfaceVolume * (1f - t);

        if (deepSource != null && deepSource.isPlaying)
            deepSource.volume = maxDeepVolume * t;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void FadeTo(AudioSource source, ref Tweener tween, float targetVolume, bool stopOnZero = false)
    {
        if (source == null) return;

        tween?.Kill();

        EnsurePlaying(source);

        tween = source.DOFade(targetVolume, crossfadeDuration)
            .SetEase(Ease.OutQuart);

        if (stopOnZero && targetVolume <= 0f)
            tween.OnComplete(() => source.Stop());
    }

    private void EnsurePlaying(AudioSource source)
    {
        if (source != null && !source.isPlaying && source.clip != null)
            source.Play();
    }

    private void PlayTransitionSFX(AudioClip clip)
    {
        if (transitionSFXSource == null || clip == null) return;
        transitionSFXSource.PlayOneShot(clip, transitionSFXVolume);
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[UnderwaterAmbience] {message}");
    }

    // -----------------------------------------------------------------------
    // Debug
    // -----------------------------------------------------------------------

    [Button("Debug State")]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugAmbienceState()
    {
        Debug.Log($"[UnderwaterAmbience] State={currentState} | Depth={currentDepth:F2}m | Submerged={isSubmarineSubmerged}");
        Debug.Log($"  AboveWater: playing={aboveWaterSource?.isPlaying}, vol={aboveWaterSource?.volume:F2}");
        Debug.Log($"  Surface:    playing={surfaceSource?.isPlaying},    vol={surfaceSource?.volume:F2}");
        Debug.Log($"  Deep:       playing={deepSource?.isPlaying},       vol={deepSource?.volume:F2}");
        Debug.Log($"  VeryDeep:   playing={veryDeepSource?.isPlaying},   vol={veryDeepSource?.volume:F2}");
    }
}