using RayFire;
using UnityEngine;

public class EnvironmentalDestructibleAudio : MonoBehaviour
{
    [SerializeField] private RayfireRigid rigid;
    [SerializeField] private AudioClip[] destructionClips;

    [Header("Audio Settings")]
    [SerializeField] private float volume = 0.8f;
    [SerializeField] private float minTimeBetweenSounds = 0.3f; // Prevents sound spam

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    private float lastSoundTime;
    private bool hasSubscribedToFragments = false;

    private void Awake()
    {
        if (rigid == null) rigid = GetComponent<RayfireRigid>();

        if (rigid == null)
        {
            Debug.LogError("[EnvironmentalDestructibleAudio] No RayfireRigid found!");
            return;
        }
    }

    private void Start()
    {
        // Subscribe to the MAIN rigid's activation event
        // This fires when the object is first activated and fragments are created
        if (rigid == null)
        {
            Debug.LogError($"[EnvironmentalDestructibleAudio] Cannot subscribe to activation event - RayfireRigid reference is null on {gameObject.name}");
        }

        if (rigid.fragments == null || rigid.fragments.Count == 0)
        {
            Debug.LogWarning($"[EnvironmentalDestructibleAudio] No fragments found on {gameObject.name})");
        }

        int childNumber = 0;

        foreach (RayfireRigid fragment in rigid.fragments)
        {
            if (fragment == null)
            {
                Debug.LogWarning($"[EnvironmentalDestructibleAudio] Skipping null fragment at index {childNumber} on {gameObject.name}");
                childNumber++;
                continue;
            }

            fragment.activationEvent.LocalEvent += OnFragmentActivated;
            childNumber++;
            DebugLog($"Subscribed to main rigid demolition event on {gameObject.name}");
        }
    }

    /// <summary>
    /// Called when the rigid's fragments are activated
    /// </summary>
    private void OnActivation(RayfireRigid demolishedRigid)
    {
        DebugLog($"Demolition event triggered on {demolishedRigid.name}");

        // Play initial destruction sound
        PlayDestructionClip(demolishedRigid.transform.position);

        // Now subscribe to each fragment's activation event
        SubscribeToFragments();
    }

    /// <summary>
    /// Subscribes to activation events for all created fragments
    /// </summary>
    private void SubscribeToFragments()
    {
        if (hasSubscribedToFragments)
        {
            DebugLog("Already subscribed to fragments");
            return;
        }

        if (rigid.fragments == null || rigid.fragments.Count == 0)
        {
            DebugLog("No fragments available yet");
            return;
        }

        DebugLog($"Subscribing to {rigid.fragments.Count} fragments");

        foreach (RayfireRigid fragment in rigid.fragments)
        {
            if (fragment != null)
            {
                // Subscribe to the fragment's activation event
                fragment.activationEvent.LocalEvent += OnFragmentActivated;

                DebugLog($"Subscribed to fragment: {fragment.name}");
            }
        }

        hasSubscribedToFragments = true;
    }

    /// <summary>
    /// Called when an individual fragment is activated
    /// </summary>
    private void OnFragmentActivated(RayfireRigid activatedFragment)
    {
        DebugLog($"Fragment activated: {activatedFragment.name}");

        // Play sound at the fragment's position
        PlayDestructionClip(activatedFragment.transform.position);
    }

    /// <summary>
    /// Plays a destruction sound with cooldown to prevent spam
    /// </summary>
    private void PlayDestructionClip(Vector3 position)
    {
        // Cooldown check to prevent too many sounds at once
        if (Time.time - lastSoundTime < minTimeBetweenSounds)
        {
            DebugLog("Sound on cooldown, skipping");
            return;
        }

        if (destructionClips == null || destructionClips.Length == 0)
        {
            Debug.LogWarning("[EnvironmentalDestructibleAudio] No destruction clips assigned!");
            return;
        }

        // Pick a random clip
        AudioClip randomClip = destructionClips[Random.Range(0, destructionClips.Length)];

        // Play the sound at the destruction position
        AudioManager.Instance.PlaySound(randomClip, position, AudioCategory.Ambience, volume: volume);

        lastSoundTime = Time.time;

        DebugLog($"Played destruction clip: {randomClip.name} at position {position}");
    }

    private void OnDestroy()
    {
        // Clean up event subscriptions to prevent memory leaks
        if (rigid != null)
        {
            rigid.demolitionEvent.LocalEvent -= OnActivation;

            if (rigid.fragments != null)
            {
                foreach (RayfireRigid fragment in rigid.fragments)
                {
                    if (fragment != null)
                    {
                        fragment.activationEvent.LocalEvent -= OnFragmentActivated;
                    }
                }
            }
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[{gameObject.name} Audio] {message}");
        }
    }
}