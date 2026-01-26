using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Utility class for finding and managing tool energy sources in player inventory.
/// Handles swapping energy sources between tools and inventory.
/// Similar to AmmoInventoryHelper but for tool energy management.
/// </summary>
public static class ToolEnergyInventoryHelper
{
    /// <summary>
    /// Find all energy source items in inventory that are compatible with the specified energy type.
    /// Returns list of InventoryItemData containing energy source ItemInstances.
    /// </summary>
    public static List<InventoryItemData> FindCompatibleEnergySources(ItemData requiredEnergyType)
    {
        if (requiredEnergyType == null)
        {
            Debug.LogWarning("[ToolEnergyInventoryHelper] No energy type specified");
            return new List<InventoryItemData>();
        }

        if (PlayerInventoryManager.Instance == null)
        {
            Debug.LogWarning("[ToolEnergyInventoryHelper] PlayerInventoryManager not available");
            return new List<InventoryItemData>();
        }

        var allItems = PlayerInventoryManager.Instance.InventoryGridData.GetAllItems();
        var compatibleSources = new List<InventoryItemData>();

        foreach (var item in allItems)
        {
            // Check if this is the required energy source type
            if (item.ItemData == requiredEnergyType && item.ItemData.itemType == ItemType.ToolEnergySource)
            {
                // Include all energy sources (even empty ones for swapping)
                if (item.ItemInstance?.ToolEnergySourceInstanceData != null)
                {
                    compatibleSources.Add(item);
                }
            }
        }

        return compatibleSources;
    }

    /// <summary>
    /// Get the total amount of compatible energy available in inventory.
    /// </summary>
    public static int GetTotalEnergyCount(ItemData requiredEnergyType)
    {
        var energySources = FindCompatibleEnergySources(requiredEnergyType);
        int total = 0;

        foreach (var source in energySources)
        {
            if (source.ItemInstance?.ToolEnergySourceInstanceData != null)
            {
                total += source.ItemInstance.ToolEnergySourceInstanceData.currentEnergy;
            }
        }

        return total;
    }

    /// <summary>
    /// Get the next available energy source for swapping, excluding the currently equipped one.
    /// Prioritizes sources with the most energy first.
    /// </summary>
    public static InventoryItemData GetNextEnergySource(ItemData requiredEnergyType, string excludeInstanceId)
    {
        var sources = FindCompatibleEnergySources(requiredEnergyType);

        // Remove the currently equipped source from options
        if (!string.IsNullOrEmpty(excludeInstanceId))
        {
            sources = sources.Where(s => s.ItemInstance.InstanceID != excludeInstanceId).ToList();
        }

        if (sources.Count == 0)
        {
            Debug.Log($"[ToolEnergyInventoryHelper] No alternative energy sources found for {requiredEnergyType?.itemName}");
            return null;
        }

        // Sort by energy amount (highest first) to prioritize fuller sources
        sources.Sort((a, b) =>
        {
            int energyA = a.ItemInstance?.ToolEnergySourceInstanceData?.currentEnergy ?? 0;
            int energyB = b.ItemInstance?.ToolEnergySourceInstanceData?.currentEnergy ?? 0;
            return energyB.CompareTo(energyA);
        });

        return sources[0];
    }

    /// <summary>
    /// Swap the currently equipped energy source in a tool with one from inventory.
    /// Returns true if swap was successful.
    /// This is the main reload operation for tools.
    /// Order: Remove new source from inventory FIRST, then add old source back.
    /// ALL old sources are returned to inventory, including default starting sources.
    /// </summary>
    public static bool SwapEnergySource(ToolInstanceData toolInstance, ItemData requiredEnergyType)
    {
        if (toolInstance == null || requiredEnergyType == null)
        {
            Debug.LogWarning("[ToolEnergyInventoryHelper] Cannot swap - null parameters");
            return false;
        }

        if (PlayerInventoryManager.Instance == null)
        {
            Debug.LogWarning("[ToolEnergyInventoryHelper] PlayerInventoryManager not available");
            return false;
        }

        // Get currently equipped source instance ID
        string currentSourceId = toolInstance.equippedEnergySourceInstanceId;
        int currentSourceEnergy = toolInstance.equippedEnergySourceAmount;

        // Find next available source (excluding current)
        var nextSource = GetNextEnergySource(requiredEnergyType, currentSourceId);
        if (nextSource == null)
        {
            Debug.Log($"[ToolEnergyInventoryHelper] No energy sources available for swap");
            return false;
        }

        Debug.Log($"[ToolEnergyInventoryHelper] Swapping energy source - Current: {currentSourceId} ({currentSourceEnergy} energy), Next: {nextSource.ItemInstance.InstanceID}");

        // CRITICAL: Store the new source's data before removing from inventory
        var newSourceInstance = nextSource.ItemInstance;
        var newSourceInventoryId = nextSource.ID;
        int newSourceEnergy = newSourceInstance.ToolEnergySourceInstanceData?.currentEnergy ?? 0;

        // STEP 1: Remove the new source from inventory FIRST (ensures space for old source)
        bool removeSuccess = PlayerInventoryManager.Instance.RemoveItem(newSourceInventoryId);

        if (!removeSuccess)
        {
            Debug.LogError($"[ToolEnergyInventoryHelper] Failed to remove new energy source from inventory");
            return false;
        }

        Debug.Log($"[ToolEnergyInventoryHelper] Removed new energy source from inventory");

        // STEP 2: Return the old source to inventory (including default starting sources)
        if (!string.IsNullOrEmpty(currentSourceId) && currentSourceEnergy >= 0)
        {
            // Create an ItemInstance for the old source to return to inventory
            var oldSourceInstance = new ItemInstance(requiredEnergyType);

            // Restore its energy amount
            if (oldSourceInstance.ToolEnergySourceInstanceData != null)
            {
                oldSourceInstance.ToolEnergySourceInstanceData.currentEnergy = currentSourceEnergy;
            }

            // Add it back to inventory (space guaranteed since we removed new source first)
            bool addSuccess = PlayerInventoryManager.Instance.AddItem(oldSourceInstance);

            if (!addSuccess)
            {
                Debug.LogWarning($"[ToolEnergyInventoryHelper] Failed to add old energy source back to inventory with {currentSourceEnergy} energy");
                // We continue anyway - the swap must complete
            }
            else
            {
                Debug.Log($"[ToolEnergyInventoryHelper] Returned old energy source to inventory with {currentSourceEnergy} energy");
            }
        }

        // STEP 3: Equip the new source in the tool
        toolInstance.equippedEnergySourceInstanceId = newSourceInstance.InstanceID;
        toolInstance.equippedEnergySourceAmount = newSourceEnergy;

        Debug.Log($"[ToolEnergyInventoryHelper] Energy source swapped successfully - New source has {toolInstance.equippedEnergySourceAmount} energy");

        return true;
    }

    /// <summary>
    /// Check if any compatible energy sources exist in inventory (excluding currently equipped).
    /// </summary>
    public static bool HasAvailableEnergySources(ItemData requiredEnergyType, string excludeInstanceId)
    {
        var nextSource = GetNextEnergySource(requiredEnergyType, excludeInstanceId);
        return nextSource != null;
    }

    /// <summary>
    /// Check if a specific tool has any energy remaining in its equipped source.
    /// </summary>
    public static bool HasEnergyInEquippedSource(ToolInstanceData toolInstance)
    {
        if (toolInstance == null)
            return false;

        return toolInstance.equippedEnergySourceAmount > 0;
    }

    /// <summary>
    /// Try to consume energy from the equipped source.
    /// Returns true if energy was consumed, false if insufficient energy.
    /// </summary>
    public static bool TryConsumeEnergy(ToolInstanceData toolInstance, int amount)
    {
        if (toolInstance == null || amount <= 0)
            return false;

        if (toolInstance.equippedEnergySourceAmount < amount)
            return false;

        toolInstance.equippedEnergySourceAmount -= amount;
        return true;
    }

    /// <summary>
    /// Get debug info about energy source availability.
    /// </summary>
    public static string GetEnergyDebugInfo(ItemData requiredEnergyType, string currentSourceId)
    {
        if (requiredEnergyType == null)
            return "No energy type specified";

        var sources = FindCompatibleEnergySources(requiredEnergyType);
        int totalEnergy = GetTotalEnergyCount(requiredEnergyType);
        int availableSources = sources.Count(s => s.ItemInstance.InstanceID != currentSourceId);

        return $"Energy Type: {requiredEnergyType.itemName}, Available Sources: {availableSources}/{sources.Count}, Total Energy: {totalEnergy}";
    }
}