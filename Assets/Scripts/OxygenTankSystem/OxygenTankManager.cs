using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Unity.VisualScripting;

/// <summary>
/// Core oxygen tank system manager handling equipment and oxygen consumption.
/// Simplified single-slot version based on ClothingManager pattern.
/// The equipped tank acts as the player's oxygen source.
/// </summary>
public class OxygenTankManager : MonoBehaviour, IManagerState
{
    public static OxygenTankManager Instance { get; private set; }

    [Header("Tank Slot Configuration")]
    [SerializeField] private OxygenTankSlot tankSlot;

    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLogs = true;

    // Events for UI synchronization
    public event Action<OxygenTankSlot, InventoryItemData> OnTankEquipped;
    public event Action<OxygenTankSlot, string> OnTankUnequipped;
    public event Action<string, float> OnTankOxygenChanged;
    public event Action OnTankDataChanged;

    // IManagerState implementation
    private ManagerOperationalState operationalState = ManagerOperationalState.Gameplay;
    public ManagerOperationalState CurrentOperationalState => operationalState;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeTankSlot();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Subscribe to inventory events to handle item removal
        if (PlayerInventoryManager.Instance != null)
        {
            PlayerInventoryManager.Instance.OnItemRemoved += OnInventoryItemRemoved;
        }
    }

    private void OnDestroy()
    {
        if (PlayerInventoryManager.Instance != null)
        {
            PlayerInventoryManager.Instance.OnItemRemoved -= OnInventoryItemRemoved;
        }
    }

    /// <summary>
    /// Initialize the tank slot
    /// </summary>
    private void InitializeTankSlot()
    {
        if (tankSlot == null)
        {
            DebugLog("Initializing default oxygen tank slot");
            tankSlot = new OxygenTankSlot();
        }

        DebugLog("Oxygen tank system initialized");
    }

    #region Equipment Methods

    /// <summary>
    /// Equips an oxygen tank from inventory to the tank slot.
    /// </summary>
    public bool EquipTank(string itemId)
    {
        if (PlayerInventoryManager.Instance == null)
        {
            DebugLog("Cannot equip tank - InventoryManager not found");
            return false;
        }

        // Get the item from inventory (includes full ItemInstance)
        var item = PlayerInventoryManager.Instance.InventoryGridData.GetItem(itemId);
        if (item == null)
        {
            DebugLog($"Cannot equip tank - item {itemId} not found in inventory");
            return false;
        }

        // Validate item type and data
        if (!ValidateItemForEquipping(item))
        {
            return false;
        }

        // Handle occupied slot (swap scenario)
        if (tankSlot.IsOccupied)
        {
            return EquipWithSwap(item);
        }
        else
        {
            return EquipToEmptySlot(item);
        }
    }

    /// <summary>
    /// Validates that an item can be equipped as an oxygen tank
    /// </summary>
    private bool ValidateItemForEquipping(InventoryItemData item)
    {
        if (item.ItemData?.itemType != ItemType.OxygenTank)
        {
            DebugLog($"Cannot equip item {item.ID} - not an oxygen tank");
            return false;
        }

        var tankData = item.ItemData.OxygenTankData;
        if (tankData == null)
        {
            DebugLog($"Cannot equip item {item.ID} - no oxygen tank data");
            return false;
        }

        if (!tankData.IsValid())
        {
            DebugLog($"Cannot equip {item.ItemData.itemName} - invalid tank data");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Equips tank to an empty slot (simple case).
    /// Transfers ItemInstance from inventory to tank slot.
    /// </summary>
    private bool EquipToEmptySlot(InventoryItemData item)
    {
        // CRITICAL: Get the ItemInstance BEFORE removing from inventory
        ItemInstance tankInstance = item.ItemInstance;

        if (tankInstance == null)
        {
            DebugLog($"Cannot equip tank {item.ID} - ItemInstance is null");
            return false;
        }

        // Remove from inventory first
        if (!PlayerInventoryManager.Instance.RemoveItem(item.ID))
        {
            DebugLog($"Failed to remove tank {item.ID} from inventory");
            return false;
        }

        // Equip to slot with complete ItemInstance
        tankSlot.EquipTank(item.ID, tankInstance);

        // Fire events
        OnTankEquipped?.Invoke(tankSlot, item);
        OnTankDataChanged?.Invoke();

        DebugLog($"Equipped {item.ItemData.itemName} to tank slot [Instance: {tankInstance.InstanceID}]");
        return true;
    }


    // INSTRUCTIONS: Replace the EquipWithSwap method in your OxygenTankManager.cs with this enhanced version

    /// <summary>
    /// Equips tank with swapping logic - maintains ItemInstance for both tanks.
    /// ENHANCED: Preserves position and rotation from inventory during swap.
    /// The displaced tank is placed at the exact position/rotation of the new tank.
    /// </summary>
    private bool EquipWithSwap(InventoryItemData newTank)
    {
        string currentTankId = tankSlot.equippedTankId;

        DebugLog($"Attempting to swap {newTank.ItemData.itemName} with currently equipped tank");

        // Get the currently equipped ItemInstance from the slot
        var currentTankInstance = tankSlot.GetEquippedTankInstance();
        if (currentTankInstance == null)
        {
            DebugLog($"Warning: Currently equipped tank {currentTankId} has no ItemInstance - treating as empty slot");
            return EquipToEmptySlot(newTank);
        }

        // Store new tank's ItemInstance BEFORE removing from inventory
        ItemInstance newTankInstance = newTank.ItemInstance;
        if (newTankInstance == null)
        {
            DebugLog("Cannot execute swap - new tank has no ItemInstance");
            return false;
        }

        // CRITICAL: Store the position and rotation of the new tank BEFORE removing it
        Vector2Int newTankPosition = newTank.GridPosition;
        int newTankRotation = newTank.currentRotation;

        DebugLog($"New tank position: {newTankPosition}, rotation: {newTankRotation}");

        // Remove new tank from inventory (frees up space for displaced tank)
        if (!PlayerInventoryManager.Instance.RemoveItem(newTank.ID))
        {
            DebugLog($"Swap failed: Could not remove {newTank.ID} from inventory");
            return false;
        }

        // Check if we have space for displaced tank at the position the new tank just vacated
        if (!PlayerInventoryManager.Instance.HasSpaceForItemAt(currentTankInstance.ItemData, newTankPosition, newTankRotation))
        {
            DebugLog($"Cannot swap - no space for {currentTankInstance.ItemData.itemName} at position {newTankPosition} with rotation {newTankRotation}");

            // Rollback: restore new tank to inventory at original position
            if (!PlayerInventoryManager.Instance.AddItem(newTank, newTankPosition, newTankRotation))
            {
                DebugLog($"CRITICAL: Failed to restore {newTank.ItemData.itemName} to inventory during rollback!");
            }

            return false;
        }

        // Unequip current tank from slot
        string unequippedTankId = tankSlot.UnequipTank();

        // Equip new tank to slot
        tankSlot.EquipTank(newTank.ID, newTankInstance);

        // Place displaced tank at the EXACT position and rotation of the new tank
        if (PlayerInventoryManager.Instance.AddItem(currentTankInstance, newTankPosition, newTankRotation))
        {
            // Success!
            OnTankEquipped?.Invoke(tankSlot, newTank);
            OnTankDataChanged?.Invoke();

            DebugLog($"Swap completed: {newTank.ItemData.itemName} equipped, {currentTankInstance.ItemData.itemName} placed at position {newTankPosition} with rotation {newTankRotation}");
            return true;
        }
        else
        {
            // Rollback: swap failed to place displaced tank in inventory
            DebugLog($"Swap rollback: Failed to place {currentTankInstance.ItemData.itemName} at position {newTankPosition}");

            // Restore original equipped state
            tankSlot.UnequipTank();
            tankSlot.EquipTank(currentTankId, currentTankInstance);

            // Return new tank to inventory at original position
            if (!PlayerInventoryManager.Instance.AddItem(newTank, newTankPosition, newTankRotation))
            {
                DebugLog($"CRITICAL: Failed to restore {newTank.ItemData.itemName} to inventory during rollback!");
            }

            return false;
        }
    }

    #endregion

    #region Unequip Methods

    /// <summary>
    /// Unequips the oxygen tank and returns it to inventory.
    /// Maintains ItemInstance throughout the operation.
    /// </summary>
    public bool UnequipTank()
    {
        if (tankSlot == null || tankSlot.IsEmpty)
        {
            DebugLog("Cannot unequip - tank slot empty or not found");
            return false;
        }

        string tankId = tankSlot.equippedTankId;

        // Get the complete equipped tank with ItemInstance
        var equippedTank = tankSlot.GetEquippedTank();

        if (equippedTank?.ItemInstance?.ItemData == null)
        {
            DebugLog($"Cannot unequip {tankId} - tank instance data not found");
            return false;
        }

        // Check if inventory has space
        if (!PlayerInventoryManager.Instance.HasSpaceForItem(equippedTank.ItemData))
        {
            DebugLog($"Cannot unequip {equippedTank.ItemData.itemName} - inventory full");
            return false;
        }

        // Unequip from slot
        tankSlot.UnequipTank();

        // Add to inventory with the SAME ItemInstance (preserves all state)
        if (PlayerInventoryManager.Instance.AddItem(equippedTank))
        {
            // Success
            OnTankUnequipped?.Invoke(tankSlot, tankId);
            OnTankDataChanged?.Invoke();

            DebugLog($"Unequipped {equippedTank.ItemData.itemName} and returned to inventory [Instance: {equippedTank.ItemInstance.InstanceID}]");
            return true;
        }
        else
        {
            // Failed to add to inventory - restore to slot with ItemInstance
            tankSlot.EquipTank(tankId, equippedTank.ItemInstance);

            DebugLog($"Failed to return {equippedTank.ItemData.itemName} to inventory - restored to slot");
            return false;
        }
    }

    #endregion

    #region Oxygen Consumption

    /// <summary>
    /// Consumes oxygen from the equipped tank.
    /// Returns true if oxygen was consumed, false if tank is empty or not equipped.
    /// </summary>
    public bool ConsumeTankOxygen(float amount)
    {
        if (tankSlot.IsEmpty)
            return false;

        var instanceData = tankSlot.GetEquippedTankInstanceData();
        if (instanceData == null)
            return false;

        if (instanceData.IsEmpty())
            return false;

        float oxygenBefore = instanceData.currentOxygen;
        instanceData.ConsumeOxygen(amount);

        if (instanceData.currentOxygen != oxygenBefore)
        {
            OnTankOxygenChanged?.Invoke(tankSlot.equippedTankId, instanceData.currentOxygen);

            if (instanceData.IsEmpty())
            {
                DebugLog($"Tank {tankSlot.equippedTankId} is now empty");
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Adds oxygen to the equipped tank (for refilling).
    /// </summary>
    public bool AddTankOxygen(float amount)
    {
        if (tankSlot.IsEmpty)
            return false;

        var templateData = tankSlot.GetEquippedTankData();
        var instanceData = tankSlot.GetEquippedTankInstanceData();

        if (templateData == null || instanceData == null)
            return false;

        bool success = instanceData.AddOxygen(amount, templateData);

        if (success)
        {
            OnTankOxygenChanged?.Invoke(tankSlot.equippedTankId, instanceData.currentOxygen);
            DebugLog($"Added {amount:F1} oxygen to tank - now at {instanceData.currentOxygen:F1}");
        }

        return success;
    }

    #endregion

    #region Accessors

    /// <summary>
    /// Gets the oxygen tank slot
    /// </summary>
    public OxygenTankSlot GetSlot()
    {
        return tankSlot;
    }

    /// <summary>
    /// Checks if a tank is currently equipped
    /// </summary>
    public bool HasTankEquipped()
    {
        return tankSlot != null && tankSlot.IsOccupied;
    }

    /// <summary>
    /// Gets the current oxygen amount from equipped tank
    /// </summary>
    public float GetCurrentOxygen()
    {
        return tankSlot?.GetCurrentOxygen() ?? 0f;
    }

    /// <summary>
    /// Gets the max capacity of the equipped tank
    /// </summary>
    public float GetMaxCapacity()
    {
        return tankSlot?.GetEquippedTankData()?.maxCapacity ?? 0f;
    }

    /// <summary>
    /// Gets the oxygen percentage (0-1) of the equipped tank
    /// </summary>
    public float GetOxygenPercentage()
    {
        return tankSlot?.GetOxygenPercentage() ?? 0f;
    }

    /// <summary>
    /// Checks if the equipped tank is empty
    /// </summary>
    public bool IsTankEmpty()
    {
        return tankSlot?.IsTankEmpty() ?? true;
    }

    #endregion

    #region Data Management

    /// <summary>
    /// Sets tank data directly (used by save system)
    /// </summary>
    public void SetTankData(OxygenTankSlot newSlot)
    {
        tankSlot = newSlot ?? new OxygenTankSlot();
        OnTankDataChanged?.Invoke();

        DebugLog($"Tank data set: {(tankSlot.IsOccupied ? "Tank equipped" : "No tank")}");
    }

    /// <summary>
    /// Clears the equipped tank
    /// </summary>
    public void ClearTank()
    {
        if (tankSlot.IsOccupied)
        {
            string tankId = tankSlot.UnequipTank();
            OnTankUnequipped?.Invoke(tankSlot, tankId);
        }

        OnTankDataChanged?.Invoke();
        DebugLog("Tank cleared");
    }

    /// <summary>
    /// Handles inventory item removal - unequip if currently equipped
    /// </summary>
    private void OnInventoryItemRemoved(string itemId)
    {
        if (tankSlot != null && tankSlot.equippedTankId == itemId)
        {
            DebugLog($"Tank {itemId} removed from inventory - unequipping");
            tankSlot.UnequipTank();
            OnTankUnequipped?.Invoke(tankSlot, itemId);
            OnTankDataChanged?.Invoke();
        }
    }

    #endregion

    #region  IManagerState Implementation

    public void SetOperationalState(ManagerOperationalState newState)
    {
        operationalState = newState;

        switch (newState)
        {
            case ManagerOperationalState.Menu:
                OnEnterMenuState();
                break;
            case ManagerOperationalState.Gameplay:
                OnEnterGameplayState();
                break;
            case ManagerOperationalState.Transition:
                OnEnterTransitionState();
                break;
        }
    }

    public void OnEnterMenuState()
    {
        // Disable Update if you have one
        this.enabled = false;
    }

    public void OnEnterGameplayState()
    {
        // Re-enable operations
        this.enabled = true;
    }

    public void OnEnterTransitionState()
    {
        // Minimal operations during transition
    }

    public bool CanOperateInCurrentState()
    {
        return operationalState == ManagerOperationalState.Gameplay;
    }

    #endregion

    #region Debug Methods

    [Button("Clear Tank")]
    private void DebugClearTank()
    {
        ClearTank();
    }

    [Button("Debug Tank Status")]
    private void DebugTankStatus()
    {
        if (tankSlot != null)
        {
            Debug.Log(tankSlot.GetDebugInfo());
        }
        else
        {
            Debug.Log("Tank slot is null");
        }
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[OxygenTankManager] {message}");
        }
    }

    #endregion
}