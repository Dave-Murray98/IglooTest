using UnityEngine;

/// <summary>
/// IMPROVED: Triggers handler speech when a specific item is picked up.
/// Now uses direct event-driven approach instead of inefficient polling.
/// Integrates with ItemPickupInteractable system via direct method call.
/// </summary>
public class HandlerSpeechItemPickupTrigger : HandlerSpeechTriggerBase
{
    [Header("Item Settings")]
    [Tooltip("Monitor a specific item pickup (leave null to monitor any item of specified type)")]
    [SerializeField] private ItemPickupInteractable specificItemPickup;

    [Tooltip("Monitor any pickup of this item type (leave null to monitor the specific pickup only)")]
    [SerializeField] private ItemData itemTypeToMonitor;

    [Tooltip("Should we monitor the specific pickup object or the item type globally?")]
    [SerializeField] private MonitorMode monitorMode = MonitorMode.SpecificPickup;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindItemPickup = true;

    public enum MonitorMode
    {
        SpecificPickup,     // Monitor a specific ItemPickupInteractable object
        ItemType,           // Monitor any pickup of a specific ItemData type
        Either              // Trigger on either the specific pickup OR any pickup of the type
    }

    protected override void Awake()
    {
        base.Awake();

        // Auto-find item pickup if needed
        if (autoFindItemPickup && specificItemPickup == null && monitorMode != MonitorMode.ItemType)
        {
            specificItemPickup = GetComponent<ItemPickupInteractable>();

            if (specificItemPickup == null)
            {
                specificItemPickup = GetComponentInChildren<ItemPickupInteractable>();
            }
        }

        // Validate setup
        ValidateItemSetup();
    }

    protected override void Start()
    {
        base.Start();

        // Subscribe to inventory events for item type monitoring
        if ((monitorMode == MonitorMode.ItemType || monitorMode == MonitorMode.Either) && itemTypeToMonitor != null)
        {
            SubscribeToInventoryEvents();
        }

        // NOTE: We no longer monitor specific pickup via coroutine or Update
        // Instead, ItemPickupInteractable will call OnItemPickedUp() directly before destruction
    }

    private void ValidateItemSetup()
    {
        switch (monitorMode)
        {
            case MonitorMode.SpecificPickup:
                if (specificItemPickup == null)
                {
                    Debug.LogError($"[HandlerSpeechItemPickupTrigger:{gameObject.name}] MonitorMode is SpecificPickup but no ItemPickupInteractable assigned!");
                }
                break;

            case MonitorMode.ItemType:
                if (itemTypeToMonitor == null)
                {
                    Debug.LogError($"[HandlerSpeechItemPickupTrigger:{gameObject.name}] MonitorMode is ItemType but no ItemData assigned!");
                }
                break;

            case MonitorMode.Either:
                if (specificItemPickup == null && itemTypeToMonitor == null)
                {
                    Debug.LogError($"[HandlerSpeechItemPickupTrigger:{gameObject.name}] MonitorMode is Either but neither ItemPickupInteractable nor ItemData assigned!");
                }
                break;
        }
    }

    private void SubscribeToInventoryEvents()
    {
        if (PlayerInventoryManager.Instance != null)
        {
            PlayerInventoryManager.Instance.OnItemAdded += HandleItemAdded;
            DebugLog("Subscribed to inventory item added events");
        }
        else
        {
            // Try subscribing later
            StartCoroutine(DelayedInventorySubscription());
        }
    }

    private System.Collections.IEnumerator DelayedInventorySubscription()
    {
        float waitTime = 0f;
        float maxWaitTime = 5f;

        while (PlayerInventoryManager.Instance == null && waitTime < maxWaitTime)
        {
            yield return new WaitForSeconds(0.5f);
            waitTime += 0.5f;
        }

        if (PlayerInventoryManager.Instance != null)
        {
            PlayerInventoryManager.Instance.OnItemAdded += HandleItemAdded;
            DebugLog("Subscribed to inventory item added events (delayed)");
        }
        else
        {
            Debug.LogWarning($"[HandlerSpeechItemPickupTrigger:{gameObject.name}] PlayerInventoryManager not found - cannot monitor item pickups");
        }
    }

    private void HandleItemAdded(InventoryItemData inventoryItemData)
    {
        if (inventoryItemData == null || itemTypeToMonitor == null)
            return;

        // Check if this is the item type we're monitoring
        if (inventoryItemData.ItemData == itemTypeToMonitor)
        {
            DebugLog($"Monitored item type picked up: {inventoryItemData.ItemData.itemName} - triggering speech");
            TriggerSpeech();
        }
    }

    /// <summary>
    /// NEW: Called directly by ItemPickupInteractable before it's destroyed
    /// This is the event-driven approach that replaces the inefficient polling
    /// </summary>
    public void OnItemPickedUp()
    {
        if (!CanTrigger())
        {
            DebugLog("Cannot trigger - conditions not met");
            return;
        }

        DebugLog("Specific item picked up - triggering handler speech");
        TriggerSpeech();
    }

    protected override void OnSpeechTriggered()
    {
        base.OnSpeechTriggered();
        DebugLog("Handler speech triggered for item pickup");
    }

    protected override void OnDestroy()
    {
        //base.OnDestroy();

        // Unsubscribe from events
        if (PlayerInventoryManager.Instance != null)
        {
            PlayerInventoryManager.Instance.OnItemAdded -= HandleItemAdded;
        }

    }

#if UNITY_EDITOR
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // Draw connection line to specific item pickup
        if (specificItemPickup != null && (monitorMode == MonitorMode.SpecificPickup || monitorMode == MonitorMode.Either))
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, specificItemPickup.transform.position);

            // Draw icon at item location
            Gizmos.DrawWireSphere(specificItemPickup.transform.position, 0.3f);
        }

        // Show monitor mode and item type in inspector
        if (handlerSpeechData != null)
        {
            string itemInfo = itemTypeToMonitor != null ? itemTypeToMonitor.itemName : "Any";
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2.5f,
                $"Mode: {monitorMode}\nItem: {itemInfo}"
            );
        }
    }
#endif
}