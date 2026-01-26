using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Save data for equipment system with ItemInstance tracking
/// REFACTORED: Properly serializes instance IDs for validation
/// UPDATED: Now includes inventory item ID for proper save/load
/// </summary>
[System.Serializable]
public class EquipmentSaveData
{
    [Header("Hotkey Assignments")]
    public List<HotkeyBinding> hotkeyBindings = new List<HotkeyBinding>();

    [Header("Equipped Item")]
    public EquippedItemData equippedItem = new EquippedItemData();

    [Header("Current State Tracking")]
    public int currentActiveSlot = 1;
    public bool hasEquippedItem = false;

    public EquipmentSaveData()
    {
        hotkeyBindings = new List<HotkeyBinding>();
        for (int i = 1; i <= 10; i++)
        {
            hotkeyBindings.Add(new HotkeyBinding(i));
        }

        equippedItem = new EquippedItemData();
        currentActiveSlot = 1;
        hasEquippedItem = false;
    }

    public EquipmentSaveData(EquipmentSaveData other)
    {
        equippedItem = new EquippedItemData(other.equippedItem);

        hotkeyBindings = new List<HotkeyBinding>();
        foreach (var binding in other.hotkeyBindings)
        {
            hotkeyBindings.Add(new HotkeyBinding(binding));
        }

        currentActiveSlot = other.currentActiveSlot;
        hasEquippedItem = other.hasEquippedItem;

        var assignedCount = hotkeyBindings.FindAll(h => h.isAssigned).Count;

        if (currentActiveSlot >= 1 && currentActiveSlot <= 10)
        {
            var activeBinding = GetHotkeyBinding(currentActiveSlot);
            if (activeBinding?.isAssigned == true)
            {
                DebugLog($"Copy: slot {currentActiveSlot} = {activeBinding.itemDataName} (ID: {activeBinding.itemId}, InstanceID: {activeBinding.itemInstanceId})");
            }
            else
            {
                DebugLog($"Copy: slot {currentActiveSlot} empty");
            }
        }
    }

    public HotkeyBinding GetHotkeyBinding(int slotNumber)
    {
        return hotkeyBindings.Find(h => h.slotNumber == slotNumber);
    }

    public void UpdateCurrentState(int activeSlot, bool hasEquipped)
    {
        currentActiveSlot = activeSlot;
        hasEquippedItem = hasEquipped;
    }

    public bool IsValid()
    {
        bool basicValid = hotkeyBindings != null && hotkeyBindings.Count == 10;
        bool slotValid = currentActiveSlot >= 1 && currentActiveSlot <= 10;
        return basicValid && slotValid;
    }

    public void DebugLog(string message)
    {
        if (EquippedItemManager.Instance.enableDebugLogs)
            Debug.Log($"[EquipmentSaveData] {message}");
    }
}