using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Saves and restores the broken state of an individual egg.
/// Works with SceneEggStateManager to persist egg states across scene transitions and save/load.
/// Attach this to the same GameObject as the Egg script.
/// </summary>
public class EggSaveComponent : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    [Header("Egg Reference")]
    [Tooltip("Reference to the Egg script on this GameObject")]
    public Egg egg;

    [Header("Save System Integration")]
    [ShowInInspector, ReadOnly]
    [Tooltip("Unique ID assigned by SceneEggStateManager. Do not modify manually.")]
    private string eggID;

    /// <summary>
    /// Public property for accessing the egg ID (read-only to external code)
    /// </summary>
    public string EggID => eggID;

    /// <summary>
    /// Checks if this component has been assigned an ID by the manager
    /// </summary>
    public bool HasValidID => !string.IsNullOrEmpty(eggID);

    /// <summary>
    /// Sets the egg ID - should only be called by SceneEggStateManager
    /// </summary>
    /// <param name="id">The unique ID to assign</param>
    public void SetEggID(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning($"[EggSaveComponent] Attempted to set null/empty ID on {gameObject.name}");
            return;
        }

        if (!string.IsNullOrEmpty(eggID) && eggID != id)
        {
            Debug.LogWarning($"[EggSaveComponent] Overwriting existing ID '{eggID}' with '{id}' on {gameObject.name}");
        }

        eggID = id;
        DebugLog($"[EggSaveComponent] Assigned ID '{id}' to {gameObject.name}");
    }

    /// <summary>
    /// Collects the current broken state of this egg for saving
    /// </summary>
    /// <returns>Data representing whether the egg is currently broken</returns>
    public EggSaveData SaveData()
    {
        if (egg == null)
        {
            Debug.LogError($"[EggSaveComponent] Cannot save data - Egg reference is null on {gameObject.name}");
            return new EggSaveData { isBroken = false };
        }

        // Get the current broken state from the egg
        EggSaveData data = new EggSaveData
        {
            isBroken = egg.IsBroken
        };

        DebugLog($"[EggSaveComponent] Saved data for '{eggID}': isBroken = {data.isBroken}");

        return data;
    }

    /// <summary>
    /// Restores the egg's broken state from saved data
    /// </summary>
    /// <param name="data">Previously saved egg state data</param>
    public void RestoreData(EggSaveData data)
    {
        if (egg == null)
        {
            Debug.LogError($"[EggSaveComponent] Cannot restore data - Egg reference is null on {gameObject.name}");
            return;
        }

        if (data == null)
        {
            Debug.LogWarning($"[EggSaveComponent] No data to restore for {gameObject.name} (ID: {eggID})");
            return;
        }

        // Apply the saved state to the egg
        if (data.isBroken)
        {
            egg.BreakEgg();
            DebugLog($"[EggSaveComponent] Restored '{eggID}' as BROKEN");
        }
        else
        {
            egg.ResetEgg();
            DebugLog($"[EggSaveComponent] Restored '{eggID}' as INTACT");
        }
    }

    /// <summary>
    /// Returns information about this component for debugging
    /// </summary>
    public string GetDebugInfo()
    {
        if (egg == null)
            return $"EggSaveComponent[{eggID ?? "NO_ID"}] - No egg reference";

        return $"EggSaveComponent[{eggID ?? "NO_ID"}] on {gameObject.name}: " +
               $"isBroken = {egg.IsBroken}";
    }

    #region Editor Testing Methods

    [Button("Test Save Data")]
    public void TestSaveData()
    {
        EggSaveData data = SaveData();
        DebugLog($"Test Save Complete: isBroken = {data.isBroken}");
    }

    [Button("Show Debug Info")]
    public void ShowDebugInfo()
    {
        DebugLog(GetDebugInfo());
    }

    #endregion

    private void OnValidate()
    {
        // Auto-find Egg component if not assigned
        if (egg == null)
        {
            egg = GetComponent<Egg>();
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[EggSaveComponent] {message}");
        }
    }
}

/// <summary>
/// Simple data structure that stores whether an egg is broken or not
/// </summary>
[System.Serializable]
public class EggSaveData
{
    public bool isBroken;
}