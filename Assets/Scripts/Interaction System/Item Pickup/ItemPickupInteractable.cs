using System.Collections;
using UnityEngine;

/// <summary>
/// REFACTORED: Enhanced item pickup interactable with ItemInstance support and GUID-based ID system
/// FIXED: Now properly detects and regenerates duplicate IDs when items are duplicated or prefabs are instantiated
/// Works seamlessly with unified SceneItemStateManager and ItemInstance system
/// </summary>
public class ItemPickupInteractable : MonoBehaviour, IInteractable
{
    [Header("Item Settings")]
    [SerializeField] private ItemData itemData;
    [SerializeField] private int quantity = 1;
    [SerializeField] private string interactableID;
    [SerializeField] private bool autoGenerateID = true;

    [Header("Editor ID Generation")]
    [SerializeField] private string editorGUID = ""; // Persists across duplications and scene reloads
    [SerializeField] private bool isEditorPlaced = true; // Distinguishes editor-placed from runtime-dropped items

    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 2f;
    [SerializeField] private string interactionPrompt = "";
    [SerializeField] private bool showInteractionPrompt = true;
    public bool ShowInteractionPrompt => showInteractionPrompt;

    [Header("Component References")]
    [SerializeField] private Transform rootTransform;
    [SerializeField] private Rigidbody itemRigidbody;
    [SerializeField] private Collider itemCollider;

    [Header("Feedback")]
    [SerializeField] private GameObject pickupEffect;
    [SerializeField] private AudioClip pickupSound;

    [Header("Position Tracking")]
    [SerializeField] private bool enablePositionTracking = true;
    [SerializeField] private float trackingUpdateInterval = 2f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // Item type tracking
    private bool isDroppedInventoryItem = false;
    private bool isOriginalSceneItem = true;

    // REFACTORED: Instance state storage
    private string storedInstanceID;
    private int storedConsumableUses;
    private int storedWeaponAmmo;
    private int storedAmmo;
    private float storedClothingCondition;

    private int storedToolEnergy;
    private int storedToolEnergySourceEnergy;
    private float storedOxygen;

    // Position tracking for original items
    private Vector3 lastTrackedPosition;
    private Vector3 lastTrackedRotation;
    private float lastTrackingUpdate;

    // IInteractable implementation
    public string InteractableID => interactableID;
    public Transform Transform => transform;
    public bool CanInteract => enabled && gameObject.activeInHierarchy && itemData != null;
    public float InteractionRange => interactionRange;

    private void Awake()
    {
        SetupComponentReferences();

        if (autoGenerateID && string.IsNullOrEmpty(interactableID))
        {
            GenerateUniqueID();
        }

        if (string.IsNullOrEmpty(interactionPrompt) && itemData != null)
        {
            string quantityText = quantity > 1 ? $" ({quantity})" : "";
            interactionPrompt = $"pick up {itemData.itemName}{quantityText}";
        }

        // Initialize position tracking for original items
        InitializePositionTracking();
    }

    private void Start()
    {
        // Check if this original scene item was already collected
        if (isOriginalSceneItem && SceneItemStateManager.Instance != null)
        {
            if (SceneItemStateManager.Instance.IsItemCollected(interactableID))
            {
                DebugLog($"Original scene item {interactableID} was previously collected - destroying immediately");
                Destroy(rootTransform ? rootTransform.gameObject : gameObject);
                return;
            }
        }


        DebugLog($"Item pickup {interactableID} initialized (Original: {isOriginalSceneItem}, Dropped: {isDroppedInventoryItem})");
    }

    private void Update()
    {
        // Update position tracking for original items
        UpdatePositionTracking();
    }

    #region Position Tracking System

    private void InitializePositionTracking()
    {
        if (isOriginalSceneItem)
        {
            Transform root = rootTransform ?? transform;
            lastTrackedPosition = root.position;
            lastTrackedRotation = root.eulerAngles;
            DebugLog($"Initialized position tracking for original item {interactableID}");
        }
    }

    private void UpdatePositionTracking()
    {
        if (isOriginalSceneItem && enablePositionTracking && Time.time > lastTrackingUpdate + trackingUpdateInterval)
        {
            CheckForPositionChanges();
            lastTrackingUpdate = Time.time;
        }
    }

    private void CheckForPositionChanges()
    {
        Transform root = rootTransform ?? transform;

        bool positionChanged = Vector3.Distance(root.position, lastTrackedPosition) > 0.01f;
        bool rotationChanged = Vector3.Distance(root.eulerAngles, lastTrackedRotation) > 1f;

        if (positionChanged || rotationChanged)
        {
            DebugLog($"Item {interactableID} moved - notifying SceneItemStateManager");
            SceneItemStateManager.NotifyItemMoved(interactableID);

            lastTrackedPosition = root.position;
            lastTrackedRotation = root.eulerAngles;
        }
    }

    private void ForceTrackingUpdate()
    {
        if (isOriginalSceneItem)
        {
            DebugLog($"Force updating tracking for {interactableID}");
            SceneItemStateManager.NotifyItemMoved(interactableID);

            Transform root = rootTransform ?? transform;
            lastTrackedPosition = root.position;
            lastTrackedRotation = root.eulerAngles;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isOriginalSceneItem && enablePositionTracking)
        {
            if (collision.relativeVelocity.magnitude > 2f)
            {
                DebugLog($"Item {interactableID} hit by {collision.gameObject.name}");
                Invoke(nameof(ForceTrackingUpdate), 0.1f);
            }
        }
    }

    #endregion

    #region Component Setup

    public void SetupComponentReferences()
    {
        rootTransform = transform.parent;

        if (rootTransform == null)
        {
            rootTransform = transform;
        }

        if (itemRigidbody == null)
        {
            itemRigidbody = rootTransform.GetComponent<Rigidbody>();
        }

        if (itemCollider == null)
        {
            itemCollider = GetComponent<Collider>();
        }

        DebugLog($"Component references setup - Root: {rootTransform != null}, Rigidbody: {itemRigidbody != null}, Collider: {itemCollider != null}");
    }

    #endregion

    #region IInteractable Implementation

    public string GetInteractionPrompt()
    {
        if (!CanInteract) return "";
        return interactionPrompt;
    }

    public bool Interact(GameObject player)
    {
        DebugLog($"Interact called by {player.name} for item {itemData?.itemName ?? "null"}");

        if (!CanInteract)
        {
            DebugLog("Cannot interact - item disabled or no data");
            return false;
        }

        var inventory = PlayerInventoryManager.Instance;
        if (inventory == null)
        {
            DebugLog("No InventoryManager found");
            return false;
        }

        // REFACTORED: Create ItemInstance to add to inventory
        ItemInstance itemInstance = GetItemInstance();
        if (itemInstance == null)
        {
            DebugLog("Failed to create ItemInstance for pickup");
            return false;
        }

        // CRITICAL FIX: Wrap ItemInstance in InventoryItemData
        // This preserves the complete instance state through pickup
        string tempId = $"pickup_{System.Guid.NewGuid()}";
        var inventoryItemData = new InventoryItemData(tempId, itemInstance, Vector2Int.zero);

        // Try normal pickup first
        if (inventory.HasSpaceForItem(itemData))
        {
            DebugLog($"Attempting to add {itemData.itemName} to inventory...");

            // Use the AddItem overload that takes InventoryItemData (preserves ItemInstance)
            if (inventory.AddItem(inventoryItemData, null, null))
            {
                DebugLog($"Successfully added {itemData.itemName} to inventory");
                HandleSuccessfulPickup();
                return true;
            }
            else
            {
                DebugLog($"Failed to add {itemData.itemName} to inventory despite having space");
                return false;
            }
        }
        else
        {
            // Inventory is full - show pickup overflow UI
            DebugLog($"Inventory is full for {itemData.itemName} - showing pickup overflow UI");
            ShowPickupOverflowUI(itemInstance);
            return true;
        }
    }

    public void OnPlayerEnterRange(GameObject player)
    {
        DebugLog($"Player entered range of {interactableID}");
    }

    public void OnPlayerExitRange(GameObject player)
    {
        DebugLog($"Player exited range of {interactableID}");
    }

    #endregion

    #region REFACTORED: ItemInstance Management

    /// <summary>
    /// NEW: Get or create ItemInstance for this pickup
    /// </summary>
    public ItemInstance GetItemInstance()
    {
        if (itemData == null)
        {
            Debug.LogError("Cannot create ItemInstance: ItemData is null");
            return null;
        }

        // Create new ItemInstance from template
        ItemInstance itemInstance = new ItemInstance(itemData);

        // Restore stored instance state if this was a dropped item
        if (isDroppedInventoryItem && !string.IsNullOrEmpty(storedInstanceID))
        {
            RestoreInstanceState(itemInstance);
            DebugLog($"Restored instance state for dropped item: InstanceID={storedInstanceID}");
        }
        else
        {
            DebugLog($"Created new ItemInstance from template for original scene item");
        }

        return itemInstance;
    }

    /// <summary>
    /// NEW: Set ItemInstance state (called when dropping from inventory)
    /// </summary>
    public void SetItemInstance(ItemInstance itemInstance)
    {
        if (itemInstance?.ItemData == null)
        {
            Debug.LogError("Cannot set ItemInstance: Instance or ItemData is null");
            return;
        }

        // Store the item data reference
        itemData = itemInstance.ItemData;

        // Store instance ID
        storedInstanceID = itemInstance.InstanceID;

        // Store type-specific state
        StoreInstanceState(itemInstance);

        // Update interaction prompt
        string quantityText = quantity > 1 ? $" ({quantity})" : "";
        interactionPrompt = $"pick up {itemData.itemName}{quantityText}";

        DebugLog($"Set ItemInstance state - ID: {storedInstanceID}, Type: {itemData.itemType}");
    }

    /// <summary>
    /// NEW: Store instance state from ItemInstance
    /// </summary>
    private void StoreInstanceState(ItemInstance itemInstance)
    {
        switch (itemInstance.ItemData.itemType)
        {
            case ItemType.RangedWeapon:
                if (itemInstance.RangedWeaponInstanceData != null)
                {
                    storedWeaponAmmo = itemInstance.RangedWeaponInstanceData.currentAmmoInClip;
                    DebugLog($"Stored weapon ammo: {storedWeaponAmmo}");
                }
                break;

            case ItemType.Ammo:
                if (itemInstance.AmmoInstanceData != null)
                {
                    storedAmmo = itemInstance.AmmoInstanceData.currentAmmo;
                    DebugLog($"Stored ammo: {storedAmmo}");
                }
                break;

            case ItemType.Clothing:
                if (itemInstance.ClothingInstanceData != null)
                {
                    storedClothingCondition = itemInstance.ClothingInstanceData.currentCondition;
                    DebugLog($"Stored clothing condition: {storedClothingCondition}");
                }
                break;

            case ItemType.Tool:
                if (itemInstance.ToolInstanceData != null)
                {
                    storedToolEnergy = itemInstance.ToolInstanceData.equippedEnergySourceAmount;
                    DebugLog($"Stored tool energy: {storedToolEnergy}");
                }
                break;

            case ItemType.ToolEnergySource:
                if (itemInstance.ToolEnergySourceInstanceData != null)
                {
                    storedToolEnergySourceEnergy = itemInstance.ToolEnergySourceInstanceData.currentEnergy;
                    DebugLog($"Stored tool energy source: {storedToolEnergySourceEnergy}");
                }
                break;

            case ItemType.OxygenTank:
                if (itemInstance.OxygenTankInstanceData != null)
                {
                    storedOxygen = itemInstance.OxygenTankInstanceData.currentOxygen;
                    DebugLog($"Stored oxygen tank: {storedOxygen}");
                }
                break;

                // Other types have no mutable state yet
        }
    }

    /// <summary>
    /// NEW: Restore instance state to ItemInstance
    /// </summary>
    private void RestoreInstanceState(ItemInstance itemInstance)
    {
        switch (itemInstance.ItemData.itemType)
        {
            case ItemType.RangedWeapon:
                if (itemInstance.RangedWeaponInstanceData != null)
                {
                    itemInstance.RangedWeaponInstanceData.currentAmmoInClip = storedWeaponAmmo;
                    DebugLog($"Restored weapon ammo: {storedWeaponAmmo}");
                }
                break;

            case ItemType.Ammo:
                if (itemInstance.AmmoInstanceData != null)
                {
                    itemInstance.AmmoInstanceData.currentAmmo = storedAmmo;
                    DebugLog($"Restored ammo: {storedAmmo}");
                }
                break;

            case ItemType.Clothing:
                if (itemInstance.ClothingInstanceData != null)
                {
                    itemInstance.ClothingInstanceData.currentCondition = storedClothingCondition;
                    DebugLog($"Restored clothing condition: {storedClothingCondition}");
                }
                break;

            case ItemType.Tool:
                if (itemInstance.ToolInstanceData != null)
                {
                    itemInstance.ToolInstanceData.equippedEnergySourceAmount = storedToolEnergy;
                    DebugLog($"Restored tool energy: {storedToolEnergy}");
                }
                break;

            case ItemType.ToolEnergySource:
                if (itemInstance.ToolEnergySourceInstanceData != null)
                {
                    itemInstance.ToolEnergySourceInstanceData.currentEnergy = storedToolEnergySourceEnergy;
                    DebugLog($"Restored tool energy source: {storedToolEnergySourceEnergy}");
                }
                break;

            case ItemType.OxygenTank:
                if (itemInstance.OxygenTankInstanceData != null)
                {
                    itemInstance.OxygenTankInstanceData.currentOxygen = storedOxygen;
                    DebugLog($"Restored oxygen tank: {storedOxygen}");
                }
                break;


                // Other types have no mutable state yet
        }
    }

    #endregion

    #region Pickup Handling

    private void HandleSuccessfulPickup()
    {
        // CRITICAL: Trigger handler speech BEFORE destroying the object
        NotifyHandlerSpeechTrigger();

        PlayPickupEffects();
        SceneItemStateManager.OnItemPickedUp(interactableID);
        DebugLog($"Item {interactableID} picked up and reported to SceneItemStateManager");

        if (ItemDropSystem.Instance != null && isDroppedInventoryItem)
        {
            ItemDropSystem.Instance.RemoveDroppedItem(rootTransform ? rootTransform.gameObject : gameObject);
        }

        Destroy(rootTransform.gameObject);
    }

    /// <summary>
    /// NEW: Notifies any HandlerSpeechItemPickupTrigger component before item is destroyed
    /// This ensures the speech trigger can fire even though the item is about to be destroyed
    /// </summary>
    private void NotifyHandlerSpeechTrigger()
    {
        DebugLog("Notifying HandlerSpeechItemPickupTrigger");
        // Try to find trigger on this object
        HandlerSpeechItemPickupTrigger trigger = GetComponent<HandlerSpeechItemPickupTrigger>();

        if (trigger == null)
        {
            // Try parent
            trigger = GetComponentInParent<HandlerSpeechItemPickupTrigger>();
        }

        // if (trigger == null && rootTransform != null)
        // {
        //     // Try root transform if different
        //     trigger = rootTransform.GetComponent<HandlerSpeechItemPickupTrigger>();
        // }

        // if (trigger == null)
        // {
        //     // Try children
        //     trigger = GetComponentInChildren<HandlerSpeechItemPickupTrigger>();
        // }

        if (trigger != null)
        {
            DebugLog($"Notifying HandlerSpeechItemPickupTrigger on {trigger.gameObject.name}");
            trigger.OnItemPickedUp();
        }
    }

    private void PlayPickupEffects()
    {
        if (pickupEffect != null)
        {
            Vector3 effectPosition = rootTransform ? rootTransform.position : transform.position;
            Instantiate(pickupEffect, effectPosition, Quaternion.identity);
        }

        if (pickupSound != null)
        {
            Vector3 soundPosition = rootTransform ? rootTransform.position : transform.position;
            AudioSource.PlayClipAtPoint(pickupSound, soundPosition);
        }
    }

    private void ShowInventoryFullMessage()
    {
        DebugLog("Inventory is full!");

        if (itemData != null)
        {
            Debug.Log($"Cannot pick up {itemData.itemName} - Inventory is full! Make space and try again.");
        }
        else
        {
            Debug.Log("Cannot pick up item - Inventory is full!");
        }
    }

    #endregion

    #region Pickup Overflow UI

    private void ShowPickupOverflowUI(ItemInstance itemInstance)
    {
        if (itemInstance == null)
        {
            DebugLog("Cannot show pickup overflow - no item data");
            return;
        }

        if (PickupOverflowUI.Instance == null)
        {
            DebugLog("PickupOverflowUI not found - falling back to full inventory message");
            ShowInventoryFullMessage();
            return;
        }

        if (PickupOverflowUI.IsPickupOverflowOpen())
        {
            DebugLog("Pickup overflow UI already open - closing previous and opening new");
        }

        DebugLog($"Opening pickup overflow UI for {itemInstance.ItemData.itemName}");
        SubscribeToPickupOverflowEvents();
        PickupOverflowUI.Instance.ShowPickupOverflow(itemInstance);
    }

    private void SubscribeToPickupOverflowEvents()
    {
        if (PickupOverflowUI.Instance != null)
        {
            PickupOverflowUI.Instance.OnPickupItemTransferred += OnPickupOverflowTransferred;
            PickupOverflowUI.Instance.OnPickupOverflowClosed += OnPickupOverflowClosed;
        }
    }

    private void UnsubscribeFromPickupOverflowEvents()
    {
        if (PickupOverflowUI.Instance != null)
        {
            PickupOverflowUI.Instance.OnPickupItemTransferred -= OnPickupOverflowTransferred;
            PickupOverflowUI.Instance.OnPickupOverflowClosed -= OnPickupOverflowClosed;
        }
    }

    private void OnPickupOverflowTransferred(ItemData transferredItem)
    {
        if (transferredItem == itemData)
        {
            DebugLog($"Pickup overflow transfer completed for {itemData.itemName} - removing from world");
            UnsubscribeFromPickupOverflowEvents();
            HandleSuccessfulPickup();
        }
    }

    private void OnPickupOverflowClosed(ItemData closedItem, bool wasTransferred)
    {
        if (closedItem == itemData)
        {
            DebugLog($"Pickup overflow closed for {itemData.itemName} - transferred: {wasTransferred}");
            UnsubscribeFromPickupOverflowEvents();

            if (!wasTransferred)
            {
                DebugLog($"Item {itemData.itemName} was not transferred - remains in world");
            }
        }
    }

    #endregion

    #region IMPROVED: ID Generation System

    /// <summary>
    /// IMPROVED: Generates unique IDs with GUID-based system
    /// - Editor-placed items use persistent GUIDs that survive duplication
    /// - Runtime-dropped items use runtime GUIDs with "dropped_" prefix
    /// </summary>
    private void GenerateUniqueID()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string itemName = itemData != null ? itemData.itemName : "UnknownItem";

        if (isEditorPlaced)
        {
            // For editor-placed items, generate new GUID if needed
            if (string.IsNullOrEmpty(editorGUID))
            {
                editorGUID = System.Guid.NewGuid().ToString("N").Substring(0, 8);
                DebugLog($"Generated new editor GUID: {editorGUID}");
            }

            interactableID = $"Item_{sceneName}_{itemName}_{editorGUID}";
        }
        else
        {
            // For runtime-dropped items, use runtime GUID with prefix
            string runtimeGUID = System.Guid.NewGuid().ToString("N").Substring(0, 8);
            interactableID = $"dropped_{sceneName}_{itemName}_{runtimeGUID}";
        }

        DebugLog($"Generated ID: {interactableID} (EditorPlaced: {isEditorPlaced})");
    }

    #endregion

    #region Public Configuration Methods

    public void SetItemData(ItemData newItemData, int newQuantity = 1)
    {
        itemData = newItemData;
        quantity = newQuantity;

        if (itemData != null)
        {
            string quantityText = quantity > 1 ? $" ({quantity})" : "";
            interactionPrompt = $"pick up {itemData.itemName}{quantityText}";
            DebugLog($"Item data set to {itemData.itemName}");
        }
    }

    public void SetInteractableID(string newID)
    {
        interactableID = newID;
        autoGenerateID = false;
    }

    public void MarkAsDroppedItem()
    {
        isDroppedInventoryItem = true;
        isOriginalSceneItem = false;
        isEditorPlaced = false; // Dropped items are NOT editor-placed
        enablePositionTracking = false;
        DebugLog($"Item {interactableID} marked as dropped inventory item");
    }

    public void MarkAsOriginalSceneItem()
    {
        isOriginalSceneItem = true;
        isDroppedInventoryItem = false;
        isEditorPlaced = true; // Original items are editor-placed
        enablePositionTracking = true;
        InitializePositionTracking();
        DebugLog($"Item {interactableID} marked as original scene item");
    }

    public void ConfigurePhysics(ItemDropSettings settings)
    {
        if (itemRigidbody == null || settings == null)
            return;

        if (settings.usePhysicsSimulation && itemData.usePhysicsOnDrop)
        {
            itemRigidbody.isKinematic = false;
            itemRigidbody.mass = itemData.objectMass;
            itemRigidbody.linearDamping = settings.itemDrag;
            itemRigidbody.angularDamping = settings.itemAngularDrag;

            Vector3 dropForce = settings.GetRandomDropForce();
            if (dropForce.magnitude > 0f)
            {
                itemRigidbody.AddForce(dropForce, ForceMode.Impulse);
            }

            DebugLog($"Physics configured for {interactableID} with mass: {itemRigidbody.mass}");

            if (settings.enablePhysicsSettling)
            {
                Invoke(nameof(SettlePhysics), settings.physicsSettleTime);
            }
        }
        else
        {
            itemRigidbody.isKinematic = true;
            DebugLog($"Physics disabled for {interactableID}");
        }
    }

    private void SettlePhysics()
    {
        if (itemRigidbody != null)
        {
            itemRigidbody.isKinematic = true;
            itemRigidbody.linearVelocity = Vector3.zero;
            itemRigidbody.angularVelocity = Vector3.zero;
            DebugLog($"Physics settled for {interactableID}");
        }
    }

    public void FreezePhysics()
    {
        if (itemRigidbody != null)
        {
            itemRigidbody.isKinematic = true;
            itemRigidbody.linearVelocity = Vector3.zero;
            itemRigidbody.angularVelocity = Vector3.zero;
        }
    }

    #endregion

    #region Position Tracking Control

    public void SetPositionTracking(bool enabled)
    {
        enablePositionTracking = enabled;
        DebugLog($"Position tracking {(enabled ? "enabled" : "disabled")} for {interactableID}");
    }

    public void ResetPositionTracking()
    {
        if (isOriginalSceneItem)
        {
            Transform root = rootTransform ?? transform;
            lastTrackedPosition = root.position;
            lastTrackedRotation = root.eulerAngles;
            DebugLog($"Reset position tracking for {interactableID}");
        }
    }

    public bool HasMovedFromSpawn()
    {
        if (!isOriginalSceneItem) return false;

        Transform root = rootTransform ?? transform;
        return Vector3.Distance(root.position, lastTrackedPosition) > 0.01f;
    }

    #endregion

    #region Getters

    public ItemData GetItemData() => itemData;
    public bool IsDroppedInventoryItem => isDroppedInventoryItem;
    public bool IsOriginalSceneItem => isOriginalSceneItem;
    public Transform GetRootTransform() => rootTransform;

    #endregion

    #region Debug

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ItemPickupInteractable:{interactableID}] {message}");
        }
    }

    #endregion

    protected virtual void OnDestroy()
    {
        UnsubscribeFromPickupOverflowEvents();
    }

    #region Editor Support

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        SetupComponentReferences();

        // Generate ID if needed (empty ID or empty GUID for editor items)
        if (autoGenerateID && (string.IsNullOrEmpty(interactableID) || (isEditorPlaced && string.IsNullOrEmpty(editorGUID))))
        {
            GenerateUniqueID();
        }

        if (itemData != null)
        {
            string quantityText = quantity > 1 ? $" ({quantity})" : "";
            interactionPrompt = $"pick up {itemData.itemName}{quantityText}";
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        if (rootTransform != null && rootTransform != transform)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, rootTransform.position);
        }

        if (isOriginalSceneItem && enablePositionTracking)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.1f);
        }
    }
#endif

    #endregion
}