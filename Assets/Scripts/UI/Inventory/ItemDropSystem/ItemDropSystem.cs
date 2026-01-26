using UnityEngine;
using System.Collections.Generic;
using NWH.DWP2.WaterObjects;
using NGS.AdvancedCullingSystem.Dynamic;

/// <summary>
/// REFACTORED: ItemDropSystem with complete ItemInstance integration
/// Now preserves item state (ammo, durability, etc.) through drop/pickup cycles
/// Works cooperatively with InventoryManager's validation system
/// </summary>
public class ItemDropSystem : MonoBehaviour
{
    public static ItemDropSystem Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private ItemDropSettings dropSettings;
    [SerializeField] private GameObject pickupInteractionPrefab;

    [Header("Scene Organization")]
    [SerializeField] private Transform droppedItemsContainer;
    [SerializeField] private string droppedItemsContainerName = "Items";
    [SerializeField] private bool autoFindContainer = true;

    [Header("Components")]
    [SerializeField] private ItemDropValidator dropValidator;

    [Header("Object Pooling")]
    [SerializeField] private bool useObjectPooling = true;
    [SerializeField] private int poolSize = 20;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Object pool for pickup interaction components
    private Queue<GameObject> pickupPool = new Queue<GameObject>();
    private List<GameObject> activeItems = new List<GameObject>();

    public GameObject GetPickupPrefab() => pickupInteractionPrefab;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeSystem();
    }

    private void Start()
    {
        ValidateSetup();
        SetupDroppedItemsContainer();

        if (useObjectPooling)
        {
            InitializeObjectPool();
        }
    }

    #region Initialization

    private void InitializeSystem()
    {
        LoadDropSettings();
        SetupDropValidator();
    }

    private void LoadDropSettings()
    {
        if (dropSettings == null)
        {
            dropSettings = Resources.Load<ItemDropSettings>("Settings/ItemDropSettings");
            if (dropSettings == null)
            {
                DebugLog("No ItemDropSettings found in Resources/Settings/. Creating default settings.");
                CreateDefaultDropSettings();
            }
        }
    }

    private void SetupDropValidator()
    {
        if (dropValidator == null)
        {
            dropValidator = GetComponent<ItemDropValidator>();
            if (dropValidator == null)
            {
                dropValidator = gameObject.AddComponent<ItemDropValidator>();
            }
        }

        if (dropValidator != null)
        {
            dropValidator.DropSettings = dropSettings;
        }
    }

    private void SetupDroppedItemsContainer()
    {
        if (droppedItemsContainer == null && autoFindContainer)
        {
            GameObject containerGO = GameObject.Find(droppedItemsContainerName);
            if (containerGO != null)
            {
                droppedItemsContainer = containerGO.transform;
                DebugLog($"Found existing dropped items container: {droppedItemsContainerName}");
            }
            else
            {
                containerGO = new GameObject(droppedItemsContainerName);
                droppedItemsContainer = containerGO.transform;
                DebugLog($"Created new dropped items container: {droppedItemsContainerName}");
            }
        }
        else if (droppedItemsContainer != null)
        {
            DebugLog($"Using assigned dropped items container: {droppedItemsContainer.name}");
        }
        else
        {
            DebugLog("Warning: No dropped items container configured - items will spawn at world root");
        }
    }

    private void ValidateSetup()
    {
        if (pickupInteractionPrefab == null)
        {
            if (dropSettings != null && dropSettings.pickupInteractionPrefab != null)
            {
                pickupInteractionPrefab = dropSettings.pickupInteractionPrefab;
            }
            else
            {
                CreateDefaultPickupInteractionPrefab();
            }
        }

        ValidatePickupInteractionPrefab();
    }

    private void ValidatePickupInteractionPrefab()
    {
        if (pickupInteractionPrefab == null)
            return;

        bool hasCollider = pickupInteractionPrefab.GetComponent<Collider>() != null;
        bool hasPickupScript = pickupInteractionPrefab.GetComponent<ItemPickupInteractable>() != null;

        if (!hasCollider)
        {
            DebugLog("Warning: Pickup interaction prefab missing Collider component");
        }

        if (!hasPickupScript)
        {
            DebugLog("Warning: Pickup interaction prefab missing ItemPickupInteractable component");
        }
    }

    private void CreateDefaultDropSettings()
    {
        dropSettings = ScriptableObject.CreateInstance<ItemDropSettings>();
        DebugLog("Created default ItemDropSettings");
    }

    private void CreateDefaultPickupInteractionPrefab()
    {
        DebugLog("No pickup interaction prefab assigned - creating basic one");

        GameObject prefab = new GameObject("PickupInteraction");

        var collider = prefab.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 1f;

        var pickup = prefab.AddComponent<ItemPickupInteractable>();

        pickupInteractionPrefab = prefab;
        DebugLog("Created default pickup interaction prefab");
    }

    private void InitializeObjectPool()
    {
        if (pickupInteractionPrefab == null)
            return;

        pickupPool.Clear();

        for (int i = 0; i < poolSize; i++)
        {
            GameObject pooledPickup = Instantiate(pickupInteractionPrefab);

            if (droppedItemsContainer != null)
            {
                pooledPickup.transform.SetParent(droppedItemsContainer);
            }

            pooledPickup.SetActive(false);
            pickupPool.Enqueue(pooledPickup);
        }

        DebugLog($"Initialized pickup interaction pool with {poolSize} items");
    }

    #endregion

    #region REFACTORED: Enhanced Public API with ItemInstance

    /// <summary>
    /// REFACTORED: Drop item from inventory with ItemInstance preservation
    /// Uses InventoryManager's validation system to prevent item loss on failure
    /// </summary>
    public static bool DropItemFromInventory(string itemId, Vector3? customDropPosition = null)
    {
        if (Instance == null)
        {
            Debug.LogError("ItemDropSystem instance not found");
            return false;
        }

        var inventory = PlayerInventoryManager.Instance;
        if (inventory == null)
        {
            Debug.LogError("InventoryManager not found");
            return false;
        }

        Instance.DebugLog($"=== DropItemFromInventory called for: {itemId} ===");

        // Use InventoryManager's enhanced drop method
        var dropResult = inventory.TryDropItem(itemId, customDropPosition);

        if (dropResult.success)
        {
            Instance.DebugLog($"Successfully dropped item {itemId}: {dropResult.reason}");
            return true;
        }
        else
        {
            Instance.DebugLog($"Failed to drop item {itemId}: {dropResult.reason} (FailureType: {dropResult.failureReason})");
            Instance.ShowDropFailureMessage(dropResult);
            return false;
        }
    }

    /// <summary>
    /// REFACTORED: Validates if an item can be dropped (used by UI systems)
    /// </summary>
    public static bool CanDropItem(string itemId, Vector3? customDropPosition = null)
    {
        if (Instance == null) return false;

        var inventory = PlayerInventoryManager.Instance;
        if (inventory == null) return false;

        var item = inventory.InventoryGridData.GetItem(itemId);
        if (item?.ItemData == null) return false;

        ItemData itemData = item.ItemData;

        if (!itemData.CanDrop || !itemData.HasVisualPrefab)
            return false;

        if (inventory.IsDropValidationEnabled && Instance.dropValidator != null)
        {
            Vector3 targetPosition = customDropPosition ?? Instance.GetPlayerDropPosition();
            var validationResult = Instance.dropValidator.ValidateDropPosition(targetPosition, itemData, true);
            return validationResult.isValid;
        }

        return true;
    }

    /// <summary>
    /// Gets detailed failure reason for UI feedback
    /// </summary>
    public static string GetDropFailureReason(string itemId, Vector3? customDropPosition = null)
    {
        if (Instance == null) return "Drop system not available";

        var inventory = PlayerInventoryManager.Instance;
        if (inventory == null) return "Inventory system not available";

        var item = inventory.InventoryGridData.GetItem(itemId);
        if (item?.ItemData == null) return "Item not found in inventory";

        ItemData itemData = item.ItemData;

        if (!itemData.CanDrop)
            return $"{itemData.itemName} cannot be dropped (Key Item)";

        if (!itemData.HasVisualPrefab)
            return $"{itemData.itemName} has no visual representation for dropping";

        if (inventory.IsDropValidationEnabled && Instance.dropValidator != null)
        {
            Vector3 targetPosition = customDropPosition ?? Instance.GetPlayerDropPosition();
            var validationResult = Instance.dropValidator.ValidateDropPosition(targetPosition, itemData, true);
            if (!validationResult.isValid)
                return $"Cannot drop here: {validationResult.reason}";
        }

        return "Item can be dropped";
    }

    #endregion

    #region REFACTORED: Core Drop Implementation with ItemInstance

    /// <summary>
    /// REFACTORED: Core drop implementation using ItemInstance
    /// Called by InventoryManager after validation and removal
    /// Preserves complete item state (ammo, durability, etc.)
    /// </summary>
    public bool DropItem(ItemInstance itemInstance, Vector3? dropPosition = null)
    {
        if (itemInstance?.ItemData == null)
        {
            DebugLog("Cannot drop null ItemInstance");
            return false;
        }

        ItemData itemData = itemInstance.ItemData;

        if (!itemData.CanDrop)
        {
            DebugLog($"Item {itemData.itemName} cannot be dropped");
            return false;
        }

        if (!itemData.HasVisualPrefab)
        {
            Debug.LogError($"Item {itemData.itemName} has no visual prefab assigned! Cannot drop.");
            return false;
        }

        // Get drop position
        Vector3 targetPosition = dropPosition ?? GetPlayerDropPosition();

        // Validate drop position
        var validationResult = dropValidator.ValidateDropPosition(targetPosition, itemData, true);
        if (!validationResult.isValid)
        {
            DebugLog($"Cannot drop {itemData.itemName}: {validationResult.reason}");
            return false;
        }

        // Create the dropped item
        GameObject droppedItem = CreateDroppedItemObject(itemData, validationResult.position);
        if (droppedItem == null)
        {
            DebugLog($"Failed to create dropped item object for {itemData.itemName}");
            return false;
        }

        // Register with scene state manager (using ItemInstance)
        string droppedId = RegisterDroppedItemWithStateManager(itemInstance, validationResult.position);
        if (string.IsNullOrEmpty(droppedId))
        {
            DebugLog($"Failed to register {itemData.itemName} with state manager");
            Destroy(droppedItem);
            return false;
        }

        // Configure the dropped item with ItemInstance
        ConfigureDroppedItem(droppedItem, itemInstance, droppedId);

        // Play drop effects
        PlayDropEffects(validationResult.position);

        DebugLog($"Successfully dropped {itemData.itemName} (InstanceID: {itemInstance.InstanceID}) at {validationResult.position}");
        return true;
    }

    /// <summary>
    /// Creates a dropped item GameObject using the unified visual prefab system
    /// </summary>
    private GameObject CreateDroppedItemObject(ItemData itemData, Vector3 position)
    {
        GameObject visualRoot = Instantiate(itemData.visualPrefab, position, Quaternion.identity);

        if (visualRoot == null)
        {
            Debug.LogError($"Failed to instantiate visual prefab for {itemData.itemName}");
            return null;
        }

        if (droppedItemsContainer != null)
        {
            visualRoot.transform.SetParent(droppedItemsContainer);
        }

        Vector3 targetScale = itemData.GetVisualPrefabScale();
        if (targetScale != Vector3.one)
        {
            visualRoot.transform.localScale = targetScale;
        }

        SetUpAdvancedCulling(visualRoot);
        SetupItemPhysics(visualRoot, itemData);

        GameObject pickupChild = CreatePickupInteractionChild(visualRoot.transform);
        if (pickupChild == null)
        {
            Debug.LogError($"Failed to create pickup interaction child for {itemData.itemName}");
            Destroy(visualRoot);
            return null;
        }

        activeItems.Add(visualRoot);
        EnforceItemLimit();

        return visualRoot;
    }

    private void SetUpAdvancedCulling(GameObject visualRoot)
    {
        DC_SourceSettings dcSourceSettings = visualRoot.GetComponent<DC_SourceSettings>();
        DC_CullingTargetObserver cullingTargetObserver = visualRoot.GetComponent<DC_CullingTargetObserver>();
        if (dcSourceSettings == null && cullingTargetObserver == null)
        {
            visualRoot.AddComponent<DC_SourceSettings>();
        }
    }

    private void SetupItemPhysics(GameObject visualRoot, ItemData itemData)
    {
        if (!itemData.usePhysicsOnDrop)
            return;

        Rigidbody rb = visualRoot.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = visualRoot.AddComponent<Rigidbody>();
        }

        rb.mass = itemData.objectMass;

        if (dropSettings != null)
        {
            rb.linearDamping = dropSettings.itemDrag;
            rb.angularDamping = dropSettings.itemAngularDrag;
        }

        MeshCollider meshCollider = visualRoot.GetComponent<MeshCollider>();
        if (meshCollider == null)
            meshCollider = visualRoot.AddComponent<MeshCollider>();
        meshCollider.convex = true;

        if (itemData.shouldFloat)
        {
            WaterObject waterObject = visualRoot.AddComponent<WaterObject>();
            waterObject.targetTriangleCount = 20;
            waterObject.buoyantForceCoefficient = rb.mass * 0.1f;
            waterObject.GenerateSimMesh();
        }

        DebugLog($"Physics setup for {itemData.itemName} - Mass: {rb.mass}");
    }

    private GameObject CreatePickupInteractionChild(Transform parent)
    {
        GameObject pickupChild;

        if (useObjectPooling && pickupPool.Count > 0)
        {
            pickupChild = pickupPool.Dequeue();
            pickupChild.transform.SetParent(parent);
            pickupChild.transform.localPosition = Vector3.zero;
            pickupChild.transform.localRotation = Quaternion.identity;
            pickupChild.SetActive(true);
        }
        else
        {
            pickupChild = Instantiate(pickupInteractionPrefab, parent);
            pickupChild.transform.localPosition = Vector3.zero;
            pickupChild.transform.localRotation = Quaternion.identity;
        }

        return pickupChild;
    }

    /// <summary>
    /// REFACTORED: Configures a dropped item with ItemInstance state
    /// </summary>
    private void ConfigureDroppedItem(GameObject droppedItem, ItemInstance itemInstance, string droppedId)
    {
        var pickupComponent = droppedItem.GetComponentInChildren<ItemPickupInteractable>();
        if (pickupComponent != null)
        {
            // Set ItemData first
            pickupComponent.SetItemData(itemInstance.ItemData);
            pickupComponent.SetInteractableID(droppedId);
            pickupComponent.MarkAsDroppedItem();

            // NEW: Set complete ItemInstance state
            pickupComponent.SetItemInstance(itemInstance);

            pickupComponent.ConfigurePhysics(dropSettings);
            pickupComponent.SetupComponentReferences();

            DebugLog($"Configured pickup with InstanceID: {itemInstance.InstanceID}");
        }
        else
        {
            Debug.LogError($"No ItemPickupInteractable found in children of {itemInstance.ItemData.itemName}");
        }

        ConfigureInteractionCollider(droppedItem, itemInstance.ItemData);
    }

    private void ConfigureInteractionCollider(GameObject droppedItem, ItemData itemData)
    {
        var pickupComponent = droppedItem.GetComponentInChildren<ItemPickupInteractable>();
        if (pickupComponent == null) return;

        var collider = pickupComponent.GetComponent<Collider>();
        if (collider == null) return;

        if (itemData.interactionColliderSize != Vector3.zero)
        {
            if (collider is SphereCollider sphereCollider)
            {
                float radius = Mathf.Max(itemData.interactionColliderSize.x, itemData.interactionColliderSize.y, itemData.interactionColliderSize.z) * 0.5f;
                sphereCollider.radius = radius;
            }
            else if (collider is BoxCollider boxCollider)
            {
                boxCollider.size = itemData.interactionColliderSize;
            }
        }
        else
        {
            AutoConfigureColliderFromVisualBounds(droppedItem, collider);
        }
    }

    private void AutoConfigureColliderFromVisualBounds(GameObject droppedItem, Collider collider)
    {
        Renderer[] renderers = droppedItem.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combinedBounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 localSize = droppedItem.transform.InverseTransformVector(combinedBounds.size);
        float padding = 1.2f;

        if (collider is SphereCollider sphereCollider)
        {
            float radius = Mathf.Max(localSize.x, localSize.y, localSize.z) * 0.5f * padding;
            sphereCollider.radius = radius;
        }
        else if (collider is BoxCollider boxCollider)
        {
            boxCollider.size = localSize * padding;
        }
    }

    private void PlayDropEffects(Vector3 position)
    {
        if (dropSettings == null) return;

        if (dropSettings.dropEffect != null)
        {
            Instantiate(dropSettings.dropEffect, position, Quaternion.identity);
        }

        if (dropSettings.dropSound != null)
        {
            AudioSource.PlayClipAtPoint(dropSettings.dropSound, position);
        }
    }

    /// <summary>
    /// REFACTORED: Registers dropped item with ItemInstance
    /// </summary>
    private string RegisterDroppedItemWithStateManager(ItemInstance itemInstance, Vector3 position)
    {
        DebugLog($"Registering {itemInstance.ItemData.itemName} (InstanceID: {itemInstance.InstanceID}) with SceneItemStateManager");

        if (SceneItemStateManager.Instance == null)
        {
            DebugLog("SceneItemStateManager not found");
            return null;
        }

        return SceneItemStateManager.Instance.AddDroppedInventoryItem(itemInstance, position);
    }

    private Vector3 GetPlayerDropPosition()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null && dropValidator != null)
        {
            return dropValidator.GetIdealDropPosition(player.transform);
        }

        DebugLog("No player found - dropping at world origin");
        return Vector3.zero;
    }

    #endregion
    // ItemDropSystem Part 2 - Continued from Part 1

    #region User Feedback System

    private void ShowDropFailureMessage(DropAttemptResult dropResult)
    {
        string userMessage = GetUserFriendlyFailureMessage(dropResult.failureReason, dropResult.reason);
        Debug.LogWarning($"Drop Failed: {userMessage}");
        // TODO: Integrate with UI notification system
    }

    private string GetUserFriendlyFailureMessage(DropFailureReason failureReason, string technicalReason)
    {
        switch (failureReason)
        {
            case DropFailureReason.ItemNotFound:
                return "Item not found in inventory.";

            case DropFailureReason.ItemNotDroppable:
                return "This item cannot be dropped.";

            case DropFailureReason.NoVisualPrefab:
                return "This item cannot be visualized in the world.";

            case DropFailureReason.InvalidDropPosition:
                return "Cannot drop item here. Try moving to a clearer area.";

            case DropFailureReason.InventoryRemovalFailed:
                return "Failed to remove item from inventory.";

            case DropFailureReason.DropFailedButRestored:
                return "Drop failed, but item was safely returned to inventory.";

            case DropFailureReason.DropFailedItemLost:
                return "CRITICAL ERROR: Item was lost during drop attempt!";

            default:
                return technicalReason;
        }
    }

    #endregion

    #region REFACTORED: Spawn Methods with ItemInstance Support

    /// <summary>
    /// REFACTORED: Spawn a dropped item at a specific position using ItemInstance
    /// Used by SceneItemStateManager for save/load restoration
    /// </summary>
    public GameObject SpawnDroppedItem(ItemInstance itemInstance, Vector3 position, string itemId)
    {
        if (itemInstance?.ItemData == null)
        {
            Debug.LogError("Cannot spawn dropped item: ItemInstance or ItemData is null");
            return null;
        }

        GameObject droppedItem = CreateDroppedItemObject(itemInstance.ItemData, position);
        if (droppedItem != null)
        {
            ConfigureDroppedItem(droppedItem, itemInstance, itemId);
            DebugLog($"Spawned dropped item {itemInstance.ItemData.itemName} (InstanceID: {itemInstance.InstanceID}) at {position}");
        }

        return droppedItem;
    }

    /// <summary>
    /// REFACTORED: Spawn a dropped item with rotation using ItemInstance
    /// </summary>
    public GameObject SpawnDroppedItem(ItemInstance itemInstance, Vector3 position, Vector3 rotation, string itemId)
    {
        if (itemInstance?.ItemData == null)
        {
            Debug.LogError("Cannot spawn dropped item: ItemInstance or ItemData is null");
            return null;
        }

        GameObject droppedItem = CreateDroppedItemObject(itemInstance.ItemData, position);
        if (droppedItem != null)
        {
            droppedItem.transform.rotation = Quaternion.Euler(rotation);
            ConfigureDroppedItem(droppedItem, itemInstance, itemId);
            DebugLog($"Spawned dropped item {itemInstance.ItemData.itemName} (InstanceID: {itemInstance.InstanceID}) at {position} with rotation {rotation}");
        }

        return droppedItem;
    }

    /// <summary>
    /// Remove a dropped item from tracking (called when picked up)
    /// </summary>
    public void RemoveDroppedItem(GameObject item)
    {
        if (activeItems.Contains(item))
        {
            activeItems.Remove(item);
        }
    }

    #endregion

    #region Utility Methods

    private void EnforceItemLimit()
    {
        if (dropSettings == null) return;

        while (activeItems.Count > dropSettings.maxDroppedItems)
        {
            GameObject oldestItem = activeItems[0];
            activeItems.RemoveAt(0);

            if (oldestItem != null)
            {
                ReturnItemToPool(oldestItem);
            }
        }
    }

    private void ReturnItemToPool(GameObject item)
    {
        var pickupComponent = item.GetComponentInChildren<ItemPickupInteractable>();
        if (pickupComponent != null && useObjectPooling && pickupPool.Count < poolSize)
        {
            GameObject pickupChild = pickupComponent.gameObject;
            CleanupPickupForPooling(pickupChild);

            if (droppedItemsContainer != null)
            {
                pickupChild.transform.SetParent(droppedItemsContainer);
            }
            else
            {
                pickupChild.transform.SetParent(null);
            }

            pickupChild.SetActive(false);
            pickupPool.Enqueue(pickupChild);
        }

        Destroy(item);
    }

    private void CleanupPickupForPooling(GameObject pickup)
    {
        pickup.transform.localPosition = Vector3.zero;
        pickup.transform.localRotation = Quaternion.identity;
        pickup.transform.localScale = Vector3.one;

        var pickupComponent = pickup.GetComponent<ItemPickupInteractable>();
        if (pickupComponent != null)
        {
            pickupComponent.SetItemData(null);
            pickupComponent.SetInteractableID("");
        }
    }

    public ItemDropSettings GetDropSettings()
    {
        return dropSettings;
    }

    public void SetDropSettings(ItemDropSettings newSettings)
    {
        dropSettings = newSettings;
        if (dropValidator != null)
        {
            dropValidator.DropSettings = dropSettings;
        }
    }

    public (int activeItems, int pooledPickups, int totalCapacity) GetDropSystemStats()
    {
        return (activeItems.Count, pickupPool.Count, dropSettings?.maxDroppedItems ?? 0);
    }

    public Transform GetDroppedItemsContainer()
    {
        return droppedItemsContainer;
    }

    public void SetDroppedItemsContainer(Transform container)
    {
        droppedItemsContainer = container;
        DebugLog($"Dropped items container set to: {(container != null ? container.name : "null")}");
    }

    public void ClearAllDroppedItems()
    {
        foreach (var item in activeItems.ToArray())
        {
            if (item != null)
            {
                ReturnItemToPool(item);
            }
        }
        activeItems.Clear();
        DebugLog("Cleared all dropped items");
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs || (dropSettings != null && dropSettings.enableDebugLogs))
        {
            Debug.Log($"[ItemDropSystem] {message}");
        }
    }

    private void OnDestroy()
    {
        ClearAllDroppedItems();
    }
}