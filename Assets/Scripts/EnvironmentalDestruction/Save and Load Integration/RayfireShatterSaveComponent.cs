using System;
using System.Collections.Generic;
using RayFire;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Enhanced version: Now includes unique ID management for integration with save system.
/// Used for pre-shattered Rayfire rigids with object type mesh root.
/// The ID is automatically assigned by SceneDestructionStateManager using deterministic ordering.
/// </summary>
public class RayfireShatterSaveComponent : MonoBehaviour
{
    [Header("Rayfire Reference")]
    public RayfireRigid rigid;

    [Header("Save System Integration")]
    [ShowInInspector, ReadOnly]
    [Tooltip("Unique ID assigned by SceneDestructionStateManager. Do not modify manually.")]
    private string destructionID;

    [Header("Debug Data")]
    [SerializeField] private bool enableDebugLogs = false;

    [ShowInInspector]
    public List<RayfireShatterFragData> shatterFragData = new List<RayfireShatterFragData>();

    /// <summary>
    /// Public property for accessing the destruction ID (read-only to external code)
    /// </summary>
    public string DestructionID => destructionID;

    /// <summary>
    /// Checks if this component has been assigned an ID by the manager
    /// </summary>
    public bool HasValidID => !string.IsNullOrEmpty(destructionID);

    /// <summary>
    /// Sets the destruction ID - should only be called by SceneDestructionStateManager
    /// </summary>
    /// <param name="id">The unique ID to assign</param>
    public void SetDestructionID(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning($"[RayfireShatterSaveComponent] Attempted to set null/empty ID on {gameObject.name}");
            return;
        }

        if (!string.IsNullOrEmpty(destructionID) && destructionID != id)
        {
            Debug.LogWarning($"[RayfireShatterSaveComponent] Overwriting existing ID '{destructionID}' with '{id}' on {gameObject.name}");
        }

        destructionID = id;
        DebugLog($"Assigned ID '{id}' to {gameObject.name}");
    }

    /// <summary>
    /// Collects current destruction state from all fragments
    /// </summary>
    /// <returns>List of fragment data representing current state</returns>
    public List<RayfireShatterFragData> SaveData()
    {
        if (rigid == null)
        {
            Debug.LogError($"[RayfireShatterSaveComponent] Cannot save data - RayfireRigid reference is null on {gameObject.name}");
            return new List<RayfireShatterFragData>();
        }

        if (rigid.fragments == null || rigid.fragments.Count == 0)
        {
            Debug.LogWarning($"[RayfireShatterSaveComponent] No fragments found on {gameObject.name} (ID: {destructionID})");
            return new List<RayfireShatterFragData>();
        }

        List<RayfireShatterFragData> data = new List<RayfireShatterFragData>();
        int childNumber = 0;

        foreach (RayfireRigid fragment in rigid.fragments)
        {
            if (fragment == null || fragment.tsf == null)
            {
                Debug.LogWarning($"[RayfireShatterSaveComponent] Skipping null fragment at index {childNumber} on {gameObject.name}");
                childNumber++;
                continue;
            }

            RayfireShatterFragData fragData = new RayfireShatterFragData
            {
                id = childNumber,
                position = fragment.tsf.position,
                rotation = fragment.tsf.rotation
            };

            // Check activation status
            RFActivation activation = fragment.act;
            fragData.isActive = activation != null && activation.activated;

            data.Add(fragData);
            childNumber++;
        }

        DebugLog($"Saved data for '{destructionID}': {data.Count} fragments, " +
                  $"{data.FindAll(f => f.isActive).Count} active");

        return data;
    }

    /// <summary>
    /// Restores destruction state from saved fragment data
    /// </summary>
    /// <param name="shatterData">Previously saved fragment data</param>
    public void RestoreData(List<RayfireShatterFragData> shatterData)
    {
        if (rigid == null)
        {
            Debug.LogError($"[RayfireShatterSaveComponent] Cannot restore data - RayfireRigid reference is null on {gameObject.name}");
            return;
        }

        if (shatterData == null || shatterData.Count == 0)
        {
            Debug.LogWarning($"[RayfireShatterSaveComponent] No data to restore for {gameObject.name} (ID: {destructionID})");
            return;
        }

        // Reset the rigid to default state before restoring
        rigid.ResetRigid();

        int restoredCount = 0;
        int skippedCount = 0;

        foreach (RayfireShatterFragData data in shatterData)
        {
            // Validate fragment ID is within bounds
            if (data.id < 0 || data.id >= rigid.transform.childCount)
            {
                Debug.LogWarning($"[RayfireShatterSaveComponent] Fragment ID {data.id} out of bounds on {gameObject.name}");
                skippedCount++;
                continue;
            }

            Transform fragmentTransform = rigid.transform.GetChild(data.id);
            if (fragmentTransform == null)
            {
                Debug.LogWarning($"[RayfireShatterSaveComponent] Fragment transform not found for ID {data.id} on {gameObject.name}");
                skippedCount++;
                continue;
            }

            RayfireRigid fragment = fragmentTransform.GetComponent<RayfireRigid>();
            if (fragment == null)
            {
                Debug.LogWarning($"[RayfireShatterSaveComponent] Fragment RayfireRigid not found by id: {data.id} on {gameObject.name}");
                skippedCount++;
                continue;
            }

            // Restore fragment state if it was active
            if (data.isActive)
            {
                fragment.Activate();
                fragment.tsf.position = data.position;
                fragment.tsf.rotation = data.rotation;
                restoredCount++;
            }
        }

        DebugLog($"Restored data for '{destructionID}': " +
                  $"{restoredCount} fragments restored, {skippedCount} skipped");
    }

    /// <summary>
    /// Clears stored debug data
    /// </summary>
    public void ClearData()
    {
        shatterFragData.Clear();
    }

    /// <summary>
    /// Returns information about this component for debugging
    /// </summary>
    public string GetDebugInfo()
    {
        if (rigid == null)
            return $"RayfireShatterSaveComponent[{destructionID ?? "NO_ID"}] - No rigid reference";

        int totalFragments = rigid.fragments?.Count ?? 0;
        int activeFragments = 0;

        if (rigid.fragments != null)
        {
            foreach (var frag in rigid.fragments)
            {
                if (frag?.act != null && frag.act.activated)
                    activeFragments++;
            }
        }

        return $"RayfireShatterSaveComponent[{destructionID ?? "NO_ID"}] on {gameObject.name}: " +
               $"{totalFragments} fragments ({activeFragments} active)";
    }

    #region Editor Testing Methods

    [Button("Test Save Data")]
    public void TestSaveData()
    {
        shatterFragData = SaveData();
        Debug.Log($"Test Save Complete: {shatterFragData.Count} fragments saved");
    }

    [Button("Test Restore Data")]
    public void TestRestoreData()
    {
        if (shatterFragData.Count == 0)
        {
            Debug.LogWarning("No data to restore - run Test Save Data first");
            return;
        }

        RestoreData(shatterFragData);
        Debug.Log($"Test Restore Complete: {shatterFragData.Count} fragments restored");
    }

    [Button("Clear Test Data")]
    public void ClearTestData()
    {
        ClearData();
        Debug.Log("Test data cleared");
    }

    [Button("Show Debug Info")]
    public void ShowDebugInfo()
    {
        Debug.Log(GetDebugInfo());
    }

    #endregion

    private void OnValidate()
    {
        // Auto-find RayfireRigid if not assigned
        if (rigid == null)
        {
            rigid = GetComponent<RayfireRigid>();
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log("RayfireShatterSaveComponent: " + message);
    }
}

public class RayfireShatterFragData
{
    public int id;
    public Vector3 position;
    public Quaternion rotation;
    public bool isActive;
}
