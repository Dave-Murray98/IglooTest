using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Simple utility class for finding and consuming ammo from player inventory.
/// Handles the interaction between weapons and inventory ammo stacks.
/// MINIMAL IMPLEMENTATION - No caching, no pooling, just direct inventory queries.
/// </summary>
public static class AmmoInventoryHelper
{
    /// <summary>
    /// Find all ammo items in inventory that are compatible with the specified ammo type.
    /// Returns list of InventoryItemData containing ammo ItemInstances.
    /// </summary>
    public static List<InventoryItemData> FindCompatibleAmmo(ItemData requiredAmmoType)
    {
        if (requiredAmmoType == null)
        {
            Debug.LogWarning("[AmmoInventoryHelper] No ammo type specified");
            return new List<InventoryItemData>();
        }

        if (PlayerInventoryManager.Instance == null)
        {
            Debug.LogWarning("[AmmoInventoryHelper] PlayerInventoryManager not available");
            return new List<InventoryItemData>();
        }

        var allItems = PlayerInventoryManager.Instance.InventoryGridData.GetAllItems();
        var compatibleAmmo = new List<InventoryItemData>();

        foreach (var item in allItems)
        {
            // Check if this is the required ammo type
            if (item.ItemData == requiredAmmoType && item.ItemData.itemType == ItemType.Ammo)
            {
                // Check if this ammo stack has any ammo left
                if (item.ItemInstance?.AmmoInstanceData != null &&
                    item.ItemInstance.AmmoInstanceData.HasAmmo())
                {
                    compatibleAmmo.Add(item);
                }
            }
        }

        return compatibleAmmo;
    }

    /// <summary>
    /// Get the total amount of compatible ammo available in inventory.
    /// </summary>
    public static int GetTotalAmmoCount(ItemData requiredAmmoType)
    {
        var ammoStacks = FindCompatibleAmmo(requiredAmmoType);
        int total = 0;

        foreach (var stack in ammoStacks)
        {
            if (stack.ItemInstance?.AmmoInstanceData != null)
            {
                total += stack.ItemInstance.AmmoInstanceData.currentAmmo;
            }
        }

        return total;
    }

    /// <summary>
    /// Try to reload weapon from inventory ammo.
    /// Returns the amount of ammo actually loaded into the weapon.
    /// Automatically removes empty ammo stacks from inventory.
    /// </summary>
    public static int ReloadFromInventory(ItemData requiredAmmoType, int ammoNeeded)
    {
        if (ammoNeeded <= 0)
            return 0;

        var ammoStacks = FindCompatibleAmmo(requiredAmmoType);
        if (ammoStacks.Count == 0)
        {
            Debug.Log($"[AmmoInventoryHelper] No compatible ammo found for {requiredAmmoType?.itemName}");
            return 0;
        }

        int totalAmmoLoaded = 0;
        var emptyStacks = new List<string>(); // Track stacks to remove

        // Take ammo from stacks until we have enough or run out
        foreach (var ammoStack in ammoStacks)
        {
            if (totalAmmoLoaded >= ammoNeeded)
                break;

            var ammoInstance = ammoStack.ItemInstance?.AmmoInstanceData;
            if (ammoInstance == null)
                continue;

            // Calculate how much we need from this stack
            int remainingNeeded = ammoNeeded - totalAmmoLoaded;
            int ammoTaken = ammoInstance.TakeAmmo(remainingNeeded);
            totalAmmoLoaded += ammoTaken;

            Debug.Log($"[AmmoInventoryHelper] Took {ammoTaken} ammo from stack {ammoStack.ID} (Remaining: {ammoInstance.currentAmmo})");

            // Mark empty stacks for removal
            if (ammoInstance.IsEmpty())
            {
                emptyStacks.Add(ammoStack.ID);
                Debug.Log($"[AmmoInventoryHelper] Stack {ammoStack.ID} is now empty, will be removed");
            }
        }

        // Remove empty stacks from inventory
        foreach (var stackId in emptyStacks)
        {
            PlayerInventoryManager.Instance.RemoveItem(stackId);
            Debug.Log($"[AmmoInventoryHelper] Removed empty ammo stack: {stackId}");
        }

        Debug.Log($"[AmmoInventoryHelper] Reload complete: Loaded {totalAmmoLoaded} ammo (Requested: {ammoNeeded})");
        return totalAmmoLoaded;
    }

    /// <summary>
    /// Try to take a single unit of ammo from inventory (for bows).
    /// Returns true if ammo was found and consumed.
    /// Automatically removes empty ammo stacks.
    /// </summary>
    public static bool TakeSingleAmmo(ItemData requiredAmmoType)
    {
        var ammoStacks = FindCompatibleAmmo(requiredAmmoType);
        if (ammoStacks.Count == 0)
        {
            Debug.Log($"[AmmoInventoryHelper] No ammo available for {requiredAmmoType?.itemName}");
            return false;
        }

        // Take from first available stack
        var firstStack = ammoStacks[0];
        var ammoInstance = firstStack.ItemInstance?.AmmoInstanceData;

        if (ammoInstance == null || !ammoInstance.ConsumeAmmo(1))
        {
            Debug.LogWarning($"[AmmoInventoryHelper] Failed to consume ammo from stack {firstStack.ID}");
            return false;
        }

        Debug.Log($"[AmmoInventoryHelper] Consumed 1 ammo from stack {firstStack.ID} (Remaining: {ammoInstance.currentAmmo})");

        // Remove if empty
        if (ammoInstance.IsEmpty())
        {
            PlayerInventoryManager.Instance.RemoveItem(firstStack.ID);
            Debug.Log($"[AmmoInventoryHelper] Removed empty ammo stack: {firstStack.ID}");
        }

        return true;
    }

    /// <summary>
    /// Check if any compatible ammo exists in inventory.
    /// </summary>
    public static bool HasCompatibleAmmo(ItemData requiredAmmoType)
    {
        return FindCompatibleAmmo(requiredAmmoType).Count > 0;
    }

    /// <summary>
    /// Get debug info about ammo availability.
    /// </summary>
    public static string GetAmmoDebugInfo(ItemData requiredAmmoType)
    {
        if (requiredAmmoType == null)
            return "No ammo type specified";

        var ammoStacks = FindCompatibleAmmo(requiredAmmoType);
        int totalAmmo = GetTotalAmmoCount(requiredAmmoType);

        return $"Ammo Type: {requiredAmmoType.itemName}, Stacks: {ammoStacks.Count}, Total: {totalAmmo}";
    }
}