using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Centralized manager that handles continuous damage from all hazards.
/// This is much more efficient than having each hazard run its own Update loop.
/// Only one Update loop runs, and it only processes hazards the player is actually touching.
/// Handles scene transitions automatically - works whether you have one per scene or use DontDestroyOnLoad.
/// </summary>
public class HazardManager : MonoBehaviour
{
    [Header("Manager Settings")]
    [Tooltip("Should this manager persist between scene loads? If false, each scene should have its own manager.")]
    [SerializeField] private bool persistBetweenScenes = false;

    // Singleton pattern so any hazard can easily access this manager
    public static HazardManager Instance { get; private set; }

    // Tracks all continuous hazards currently affecting the player
    private Dictionary<ContinuousHazard, float> activeHazards = new Dictionary<ContinuousHazard, float>();

    private void Awake()
    {
        // Set up singleton
        if (Instance == null)
        {
            Instance = this;

            // If set to persist, make this manager survive scene loads
            if (persistBetweenScenes)
            {
                DontDestroyOnLoad(gameObject);
                // Subscribe to scene loading events for cleanup
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
        }
        else
        {
            // If there's already an instance and we're NOT persisting, destroy the old one (new scene's manager takes over)
            if (!persistBetweenScenes && Instance.persistBetweenScenes == false)
            {
                Debug.Log("New scene loaded with new ContinuousHazardManager. Replacing previous manager.");
                Destroy(Instance.gameObject);
                Instance = this;
            }
            else
            {
                // Otherwise, we have a duplicate (probably an error)
                Debug.LogWarning("Multiple ContinuousHazardManagers detected! Destroying duplicate.");
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// Called when a new scene is loaded. Cleans up any hazards from the previous scene.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Clear all hazards since they're from the old scene
        // Hazards in the new scene will register themselves when the player enters them
        if (activeHazards.Count > 0)
        {
            Debug.Log($"Scene changed to {scene.name}. Clearing {activeHazards.Count} hazards from previous scene.");
            activeHazards.Clear();
        }
    }

    /// <summary>
    /// Called every frame. Processes damage from all active hazards.
    /// This only runs if there are hazards affecting the player.
    /// </summary>
    private void Update()
    {
        // If no hazards are active, skip all processing (very efficient!)
        if (activeHazards.Count == 0)
            return;

        // Create lists to track changes (can't modify dictionary while iterating)
        List<ContinuousHazard> hazardsToRemove = new List<ContinuousHazard>();
        List<KeyValuePair<ContinuousHazard, float>> hazardsToUpdate = new List<KeyValuePair<ContinuousHazard, float>>();

        // Process each active hazard
        foreach (var kvp in activeHazards)
        {
            ContinuousHazard hazard = kvp.Key;
            float timer = kvp.Value;

            // Safety check: if hazard was destroyed, mark for removal
            if (hazard == null)
            {
                hazardsToRemove.Add(hazard);
                continue;
            }

            // Update the timer
            timer += Time.deltaTime;

            // Check if it's time to deal damage
            if (timer >= hazard.DamageTickRate)
            {
                // Calculate damage for this tick
                float damageMultiplier = timer / hazard.DamageTickRate;
                float damageThisTick = (hazard.DamagePerSecond * hazard.DamageTickRate) * damageMultiplier;

                // Deal the damage
                hazard.DealContinuousDamageInternal(damageThisTick);

                // Reset timer
                timer = 0f;
            }

            // Store the updated timer to apply after iteration
            hazardsToUpdate.Add(new KeyValuePair<ContinuousHazard, float>(hazard, timer));
        }

        // Now safely update all the timers
        foreach (var kvp in hazardsToUpdate)
        {
            activeHazards[kvp.Key] = kvp.Value;
        }

        // Clean up any hazards that were destroyed
        foreach (var hazard in hazardsToRemove)
        {
            activeHazards.Remove(hazard);
        }
    }

    /// <summary>
    /// Registers a hazard to start dealing continuous damage.
    /// Called by ContinuousHazard when the player enters it.
    /// </summary>
    public void RegisterHazard(ContinuousHazard hazard)
    {
        if (!activeHazards.ContainsKey(hazard))
        {
            // Start timer at tick rate so damage happens immediately
            activeHazards[hazard] = hazard.DamageTickRate;

            hazard.DebugLog($"Registered with ContinuousHazardManager. Total active hazards: {activeHazards.Count}");
        }
    }

    /// <summary>
    /// Unregisters a hazard to stop dealing continuous damage.
    /// Called by ContinuousHazard when the player exits it.
    /// </summary>
    public void UnregisterHazard(ContinuousHazard hazard)
    {
        if (activeHazards.ContainsKey(hazard))
        {
            activeHazards.Remove(hazard);
            hazard.DebugLog($"Unregistered from ContinuousHazardManager. Total active hazards: {activeHazards.Count}");
        }
    }

    /// <summary>
    /// Gets the number of hazards currently affecting the player.
    /// Useful for debugging or UI displays.
    /// </summary>
    public int GetActiveHazardCount()
    {
        return activeHazards.Count;
    }

    private void OnDestroy()
    {
        // Clear singleton reference when destroyed
        if (Instance == this)
        {
            Instance = null;
        }
    }
}