using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Equipment save component with ItemInstance validation
/// REFACTORED: Validates instances exist after load, refreshes caches
/// </summary>
public class EquipmentSaveComponent : SaveComponentBase, IPlayerDependentSaveable
{
    [Header("Component References")]
    [SerializeField] private EquippedItemManager equippedItemManager;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindReferences = true;

    [Header("Restoration Settings")]
    [SerializeField] private float visualCreationDelay = 0.1f;
    [SerializeField] private float stateRestorationDelay = 0.2f;
    [SerializeField] private float uiRefreshDelay = 0.3f;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        base.Awake();
        saveID = "Equipment_Main";
        autoGenerateID = false;

        if (autoFindReferences)
            FindEquipmentReferences();
    }

    private void Start()
    {
        ValidateReferences();
    }

    private void FindEquipmentReferences()
    {
        if (equippedItemManager == null)
            equippedItemManager = GetComponent<EquippedItemManager>();

        if (equippedItemManager == null)
            equippedItemManager = EquippedItemManager.Instance;

        if (equippedItemManager == null)
            equippedItemManager = FindFirstObjectByType<EquippedItemManager>();

        DebugLog($"Auto-found: {equippedItemManager != null}");
    }

    private void ValidateReferences()
    {
        if (equippedItemManager == null)
        {
            Debug.LogError($"[{name}] EquippedItemManager missing!");
        }
        else
        {
            DebugLog($"Validated: {equippedItemManager.name}");
        }
    }

    public override object GetDataToSave()
    {
        DebugLog("=== EXTRACTING EQUIPMENT ===");

        if (equippedItemManager == null)
        {
            DebugLog("Cannot save - EquippedItemManager not found");
            return new EquipmentSaveData();
        }

        var saveData = equippedItemManager.GetEquipmentDataDirect();
        var assignedCount = saveData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
        DebugLog($"Extracted: equipped={saveData.hasEquippedItem}, hotkeys={assignedCount}, slot={saveData.currentActiveSlot}");

        // Validate instance IDs are present
        ValidateInstanceIDsInSaveData(saveData);

        return saveData;
    }

    /// <summary>
    /// Validate that instance IDs are properly set in save data
    /// </summary>
    private void ValidateInstanceIDsInSaveData(EquipmentSaveData saveData)
    {
        if (saveData == null) return;

        // Check equipped item
        if (saveData.equippedItem.isEquipped)
        {
            if (string.IsNullOrEmpty(saveData.equippedItem.equippedItemInstanceId))
            {
                Debug.LogWarning($"[EquipmentSaveComponent] Equipped item missing instance ID: {saveData.equippedItem.equippedItemDataName}");
            }
        }

        // Check hotkey bindings
        foreach (var binding in saveData.hotkeyBindings)
        {
            if (binding.isAssigned && string.IsNullOrEmpty(binding.itemInstanceId))
            {
                Debug.LogWarning($"[EquipmentSaveComponent] Hotkey slot {binding.slotNumber} missing instance ID: {binding.itemDataName}");
            }
        }
    }

    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog("Extracting equipment save data");

        if (saveContainer == null)
        {
            DebugLog("saveContainer null");
            return new EquipmentSaveData();
        }

        if (saveContainer is PlayerPersistentData persistentData)
        {
            var equipmentData = persistentData.GetComponentData<EquipmentSaveData>(SaveID);
            if (equipmentData != null)
            {
                var assignedCount = equipmentData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
                DebugLog($"Extracted from persistent: {assignedCount} assignments, equipped: {equipmentData.hasEquippedItem}, slot: {equipmentData.currentActiveSlot}");
                return equipmentData;
            }
            else
            {
                DebugLog("No equipment in persistent - returning empty");
                return new EquipmentSaveData();
            }
        }
        else if (saveContainer is PlayerSaveData playerSaveData)
        {
            if (playerSaveData.customStats.TryGetValue(SaveID, out object equipmentDataObj) &&
                equipmentDataObj is EquipmentSaveData equipmentSaveData)
            {
                var assignedCount = equipmentSaveData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
                DebugLog($"Extracted from PlayerSaveData: {assignedCount} assignments, equipped: {equipmentSaveData.hasEquippedItem}, slot: {equipmentSaveData.currentActiveSlot}");
                return equipmentSaveData;
            }

            DebugLog("No equipment in PlayerSaveData - returning empty");
            return new EquipmentSaveData();
        }
        else if (saveContainer is EquipmentSaveData equipmentSaveData)
        {
            var assignedCount = equipmentSaveData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
            DebugLog($"Extracted direct: {assignedCount} assignments, equipped: {equipmentSaveData.hasEquippedItem}, slot: {equipmentSaveData.currentActiveSlot}");
            return equipmentSaveData;
        }
        else
        {
            DebugLog($"Invalid save type: {saveContainer.GetType()}");
            return new EquipmentSaveData();
        }
    }

    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (!(data is EquipmentSaveData equipmentData))
        {
            DebugLog($"Invalid save data type: {data?.GetType()}");
            return;
        }

        DebugLog($"=== RESTORING EQUIPMENT (Context: {context}) ===");

        if (autoFindReferences)
            FindEquipmentReferences();

        if (equippedItemManager == null)
        {
            DebugLog("Cannot load - EquippedItemManager not found");
            return;
        }

        var assignedCount = equipmentData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
        DebugLog($"Loading: {assignedCount} assignments, equipped: {equipmentData.hasEquippedItem}, slot: {equipmentData.currentActiveSlot}");

        try
        {
            StartCoroutine(StagedEquipmentRestoration(equipmentData, context));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load equipment: {e.Message}");
        }
    }

    /// <summary>
    /// Staged restoration with instance validation
    /// </summary>
    private System.Collections.IEnumerator StagedEquipmentRestoration(EquipmentSaveData saveData, RestoreContext context)
    {
        DebugLog("Starting staged restoration...");

        yield return new WaitForEndOfFrame();
        yield return new WaitUntil(() => PlayerInventoryManager.Instance != null);

        DebugLog("Stage 1: Validating instances...");
        yield return new WaitForSecondsRealtime(visualCreationDelay);

        // CRITICAL: Validate all instance IDs exist in inventory
        ValidateAndCleanSaveData(saveData);

        if (autoFindReferences)
            FindEquipmentReferences();

        if (equippedItemManager == null)
        {
            Debug.LogError("EquippedItemManager lost during restoration!");
            yield break;
        }

        DebugLog("Stage 2: Restoring data...");
        equippedItemManager.SetEquipmentData(saveData);

        yield return new WaitForSecondsRealtime(stateRestorationDelay);

        DebugLog("Stage 3: Refreshing caches...");
        RefreshAllInstanceCaches(saveData);

        DebugLog("Stage 4: Refreshing UI...");
        yield return StartCoroutine(RefreshEquipmentUIAfterLoad());

        DebugLog("Restoration complete");
    }

    /// <summary>
    /// Validate instance IDs exist in inventory, clean invalid bindings
    /// </summary>
    private void ValidateAndCleanSaveData(EquipmentSaveData saveData)
    {
        if (saveData == null || PlayerInventoryManager.Instance == null)
            return;

        var inventory = PlayerInventoryManager.Instance.InventoryGridData;

        // Validate equipped item instance
        if (saveData.equippedItem.isEquipped)
        {
            var equippedItem = inventory.GetItem(saveData.equippedItem.equippedItemId);
            if (equippedItem?.ItemInstance == null)
            {
                Debug.LogWarning($"[EquipmentSaveComponent] Equipped item {saveData.equippedItem.equippedItemDataName} has no valid instance - clearing");
                saveData.equippedItem.Clear();
                saveData.hasEquippedItem = false;
            }
            else if (equippedItem.ItemInstance.InstanceID != saveData.equippedItem.equippedItemInstanceId)
            {
                Debug.LogWarning($"[EquipmentSaveComponent] Equipped item instance ID mismatch - updating");
                saveData.equippedItem.equippedItemInstanceId = equippedItem.ItemInstance.InstanceID;
            }
        }

        // Validate hotkey binding instances
        foreach (var binding in saveData.hotkeyBindings)
        {
            if (!binding.isAssigned) continue;

            var item = inventory.GetItem(binding.itemId);
            if (item?.ItemInstance == null)
            {
                Debug.LogWarning($"[EquipmentSaveComponent] Hotkey slot {binding.slotNumber} item {binding.itemDataName} has no valid instance - clearing");
                binding.ClearSlot();
            }
            else if (item.ItemInstance.InstanceID != binding.itemInstanceId)
            {
                Debug.LogWarning($"[EquipmentSaveComponent] Hotkey slot {binding.slotNumber} instance ID mismatch - updating");
                binding.itemInstanceId = item.ItemInstance.InstanceID;
            }

            // Validate stacked items
            var validStackedIds = new List<string>();
            foreach (var stackedId in binding.stackedItemIds)
            {
                var stackedItem = inventory.GetItem(stackedId);
                if (stackedItem?.ItemInstance != null)
                {
                    validStackedIds.Add(stackedId);
                }
                else
                {
                    Debug.LogWarning($"[EquipmentSaveComponent] Stacked item {stackedId} in slot {binding.slotNumber} has no valid instance - removing from stack");
                }
            }
            binding.stackedItemIds = validStackedIds;
        }
    }

    /// <summary>
    /// Refresh all instance caches after load
    /// </summary>
    private void RefreshAllInstanceCaches(EquipmentSaveData saveData)
    {
        if (saveData == null)
            return;

        // Refresh equipped item cache
        if (saveData.equippedItem.isEquipped)
        {
            bool refreshed = saveData.equippedItem.RefreshInstanceCache();
            if (refreshed)
            {
                DebugLog($"Refreshed equipped item cache: {saveData.equippedItem.equippedItemDataName}");
            }
            else
            {
                Debug.LogWarning($"[EquipmentSaveComponent] Failed to refresh equipped item cache");
            }
        }

        // Refresh hotkey binding caches
        foreach (var binding in saveData.hotkeyBindings)
        {
            if (binding.isAssigned)
            {
                bool refreshed = binding.RefreshInstanceCache();
                if (refreshed)
                {
                    DebugLog($"Refreshed slot {binding.slotNumber} cache: {binding.itemDataName}");
                }
                else
                {
                    Debug.LogWarning($"[EquipmentSaveComponent] Failed to refresh slot {binding.slotNumber} cache");
                }
            }
        }
    }

    private System.Collections.IEnumerator RefreshEquipmentUIAfterLoad()
    {
        yield return new WaitForSecondsRealtime(uiRefreshDelay);

        if (equippedItemManager != null)
        {
            DebugLog("Forcing UI refresh");

            var allBindings = equippedItemManager.GetAllHotkeyBindings();
            for (int i = 0; i < allBindings.Count; i++)
            {
                var binding = allBindings[i];
                if (binding.isAssigned)
                    equippedItemManager.OnHotkeyAssigned?.Invoke(binding.slotNumber, binding);
                else
                    equippedItemManager.OnHotkeyCleared?.Invoke(binding.slotNumber);
            }

            var currentSlot = equippedItemManager.GetCurrentActiveSlot();
            var currentBinding = equippedItemManager.GetHotkeyBinding(currentSlot);
            bool isUsable = equippedItemManager.IsCurrentEquipmentValid();

            DebugLog($"Firing slot selection: slot {currentSlot}, usable: {isUsable}");
            equippedItemManager.OnSlotSelected?.Invoke(currentSlot, currentBinding, isUsable);

            if (equippedItemManager.HasEquippedItem)
            {
                DebugLog("Firing equipped event");
                equippedItemManager.OnItemEquipped?.Invoke(equippedItemManager.CurrentEquippedItem);
            }
            else
            {
                DebugLog("Firing unequipped event");
                equippedItemManager.OnItemUnequipped?.Invoke();
                equippedItemManager.OnUnarmedActivated?.Invoke();
            }

            DebugLog("UI refresh complete");
        }
    }

    #region IPlayerDependentSaveable
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null) return null;

        DebugLog("Using modular extraction");

        var equipmentData = unifiedData.GetComponentData<EquipmentSaveData>(SaveID);
        if (equipmentData != null)
        {
            var assignedCount = equipmentData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
            DebugLog($"Extracted: {assignedCount} assignments, equipped: {equipmentData.hasEquippedItem}, slot: {equipmentData.currentActiveSlot}");
            return equipmentData;
        }
        else
        {
            DebugLog("No equipment in unified - returning empty");
            return new EquipmentSaveData();
        }
    }

    public object CreateDefaultData()
    {
        DebugLog("Creating default equipment");
        return new EquipmentSaveData();
    }

    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is EquipmentSaveData equipmentData && unifiedData != null)
        {
            var assignedCount = equipmentData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
            DebugLog($"Contributing: {assignedCount} assignments, equipped: {equipmentData.hasEquippedItem}, slot: {equipmentData.currentActiveSlot}");

            unifiedData.SetComponentData(SaveID, equipmentData);
            DebugLog("Contributed successfully");
        }
        else
        {
            DebugLog($"Invalid contribution data: {componentData?.GetType().Name ?? "null"}");
        }
    }
    #endregion

    #region Lifecycle
    public override void OnBeforeSave()
    {
        DebugLog("Preparing for save");

        if (autoFindReferences)
            FindEquipmentReferences();
    }

    public override void OnAfterLoad()
    {
        DebugLog("Load completed - final validation");
        StartCoroutine(FinalValidation());
    }

    private System.Collections.IEnumerator FinalValidation()
    {
        yield return new WaitForSecondsRealtime(0.5f);

        if (equippedItemManager != null)
        {
            equippedItemManager.ValidateEquipmentForCurrentState();

            var currentSlot = equippedItemManager.GetCurrentActiveSlot();
            var hasEquipped = equippedItemManager.HasEquippedItem;
            var equippedItemName = equippedItemManager.GetEquippedItemData()?.itemName ?? "None";
            var equippedInstance = equippedItemManager.GetEquippedItemInstance();

            DebugLog($"FINAL - Slot: {currentSlot}, Equipped: {hasEquipped}, Item: {equippedItemName}, Instance: {equippedInstance?.InstanceID ?? "None"}");
        }
    }
    #endregion

    #region Public API
    public void SetEquippedItemManager(EquippedItemManager manager)
    {
        equippedItemManager = manager;
        autoFindReferences = false;
        DebugLog("Manager manually set");
    }

    public string GetCurrentEquippedItemName()
    {
        if (equippedItemManager?.HasEquippedItem == true)
            return equippedItemManager.GetEquippedItemData()?.itemName ?? "Unknown";
        return "None";
    }

    public int GetAssignedHotkeyCount()
    {
        if (equippedItemManager == null) return 0;
        var bindings = equippedItemManager.GetAllHotkeyBindings();
        return bindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
    }

    public bool HasValidReference() => equippedItemManager != null;

    public void RefreshReference()
    {
        if (autoFindReferences)
        {
            FindEquipmentReferences();
            ValidateReferences();
        }
    }

    public bool HasEquippedItem() => equippedItemManager?.HasEquippedItem == true;

    public ItemType? GetEquippedItemType()
    {
        if (!HasEquippedItem()) return null;
        return equippedItemManager.GetEquippedItemData()?.itemType;
    }

    public string GetEquipmentDebugInfo()
    {
        if (equippedItemManager == null)
            return "EquippedItemManager: null";

        var equippedItemName = GetCurrentEquippedItemName();
        var hotkeyCount = GetAssignedHotkeyCount();
        var currentSlot = equippedItemManager.GetCurrentActiveSlot();

        return $"Equipment: {equippedItemName}, {hotkeyCount}/10 hotkeys, slot: {currentSlot}";
    }

    public bool IsHotkeySlotAssigned(int slotNumber)
    {
        if (equippedItemManager == null) return false;
        var binding = equippedItemManager.GetHotkeyBinding(slotNumber);
        return binding?.isAssigned == true;
    }

    public string GetHotkeySlotItemName(int slotNumber)
    {
        if (equippedItemManager == null) return null;
        var binding = equippedItemManager.GetHotkeyBinding(slotNumber);
        if (binding?.isAssigned == true)
            return binding.GetCurrentItemData()?.itemName;
        return null;
    }
    #endregion
}