using UnityEngine;
using Sirenix.OdinInspector;
using UnityEngine.Rendering;

/// <summary>
/// Main oxygen tank UI controller that manages the tank slot UI.
/// Positioned near the inventory UI and handles tank display and interaction.
/// Follows the same event-driven pattern as ClothingUI.
/// Pattern based on ClothingUI.cs but simplified for single slot.
/// </summary>
public class OxygenTankUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject tankPanel;
    [SerializeField] private Transform slotContainer;

    [Header("Tank Slot UI References")]
    [SerializeField] private OxygenTankSlotUI slotUI;

    [Header("Auto-Setup")]
    [SerializeField] private bool autoFindSlotUI = true;
    [SerializeField] private GameObject slotUIPrefab;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Reference to tank system
    private OxygenTankManager tankManager;

    private void Awake()
    {
        if (autoFindSlotUI)
        {
            FindOrCreateSlotUI();
        }
    }

    private void Start()
    {
        InitializeFromTankManager();
    }

    private void OnEnable()
    {
        SubscribeToTankEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromTankEvents();
    }

    /// <summary>
    /// Initialize from the tank manager and setup UI
    /// </summary>
    private void InitializeFromTankManager()
    {
        tankManager = OxygenTankManager.Instance;
        if (tankManager == null)
        {
            Debug.LogError("OxygenTankManager not found! Make sure it exists in the scene.");
            return;
        }

        SubscribeToTankEvents();
        RefreshSlotUI();
    }

    /// <summary>
    /// Subscribe to tank manager events for UI updates
    /// </summary>
    private void SubscribeToTankEvents()
    {
        if (tankManager != null)
        {
            tankManager.OnTankEquipped -= OnTankEquipped;
            tankManager.OnTankUnequipped -= OnTankUnequipped;
            tankManager.OnTankOxygenChanged -= OnTankOxygenChanged;
            tankManager.OnTankDataChanged -= OnTankDataChanged;

            tankManager.OnTankEquipped += OnTankEquipped;
            tankManager.OnTankUnequipped += OnTankUnequipped;
            tankManager.OnTankOxygenChanged += OnTankOxygenChanged;
            tankManager.OnTankDataChanged += OnTankDataChanged;
        }

        // Also subscribe to inventory events to handle UI visibility
        GameEvents.OnInventoryOpened += OnInventoryOpened;
        GameEvents.OnInventoryClosed += OnInventoryClosed;
    }

    /// <summary>
    /// Unsubscribe from tank manager events
    /// </summary>
    private void UnsubscribeFromTankEvents()
    {
        if (tankManager != null)
        {
            tankManager.OnTankEquipped -= OnTankEquipped;
            tankManager.OnTankUnequipped -= OnTankUnequipped;
            tankManager.OnTankOxygenChanged -= OnTankOxygenChanged;
            tankManager.OnTankDataChanged -= OnTankDataChanged;
        }

        GameEvents.OnInventoryOpened -= OnInventoryOpened;
        GameEvents.OnInventoryClosed -= OnInventoryClosed;
    }

    /// <summary>
    /// Find existing slot UI or create it if needed
    /// </summary>
    private void FindOrCreateSlotUI()
    {
        if (slotContainer == null)
        {
            Debug.LogWarning("No slot container assigned - cannot setup oxygen tank slot UI");
            return;
        }

        // Try to find existing slot UI first
        var existingSlotUI = slotContainer.GetComponentInChildren<OxygenTankSlotUI>();

        if (existingSlotUI != null)
        {
            slotUI = existingSlotUI;
            return;
        }

        // Create slot UI if none exists
        CreateSlotUI();
    }

    /// <summary>
    /// Create the oxygen tank slot UI
    /// </summary>
    private void CreateSlotUI()
    {
        GameObject slotObj;

        if (slotUIPrefab != null)
        {
            slotObj = Instantiate(slotUIPrefab, slotContainer);
        }
        else
        {
            slotObj = CreateDefaultSlotUI();
        }

        slotUI = slotObj.GetComponent<OxygenTankSlotUI>();
        if (slotUI == null)
        {
            slotUI = slotObj.AddComponent<OxygenTankSlotUI>();
        }

        slotObj.name = "OxygenTankSlot";

        DebugLog("Created oxygen tank slot UI");
    }

    /// <summary>
    /// Create a default slot UI when no prefab is provided
    /// </summary>
    private GameObject CreateDefaultSlotUI()
    {
        GameObject slotObj = new GameObject("OxygenTankSlot");
        slotObj.transform.SetParent(slotContainer, false);

        // Add RectTransform
        var rectTransform = slotObj.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(60f, 60f);

        // Add background image
        var backgroundImage = slotObj.AddComponent<UnityEngine.UI.Image>();
        backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        // Add outline
        var outline = slotObj.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(1, 1);

        return slotObj;
    }

    #region Event Handlers

    /// <summary>
    /// Handle tank equipped to slot
    /// </summary>
    private void OnTankEquipped(OxygenTankSlot slot, InventoryItemData tank)
    {
        if (slotUI != null)
        {
            slotUI.RefreshDisplay();
        }

        DebugLog($"Tank {tank.ItemData?.itemName} equipped");
    }

    /// <summary>
    /// Handle tank unequipped from slot
    /// </summary>
    private void OnTankUnequipped(OxygenTankSlot slot, string tankId)
    {
        if (slotUI != null)
        {
            slotUI.RefreshDisplay();
        }

        DebugLog($"Tank {tankId} unequipped");
    }

    /// <summary>
    /// Handle tank oxygen changes
    /// </summary>
    private void OnTankOxygenChanged(string tankId, float newOxygen)
    {
        // Slot UI will handle this through its own subscription
        // but we could add additional UI elements here if needed
    }

    /// <summary>
    /// Handle general tank data changes
    /// </summary>
    private void OnTankDataChanged()
    {
        RefreshSlotUI();
    }

    /// <summary>
    /// Handle inventory opened (show tank UI)
    /// </summary>
    private void OnInventoryOpened()
    {
        ShowTankUI();
    }

    /// <summary>
    /// Handle inventory closed (hide tank UI)
    /// </summary>
    private void OnInventoryClosed()
    {
        HideTankUI();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Show the tank UI panel
    /// </summary>
    public void ShowTankUI()
    {
        if (tankPanel != null)
        {
            tankPanel.SetActive(true);
            RefreshSlotUI();
        }
    }

    /// <summary>
    /// Hide the tank UI panel
    /// </summary>
    public void HideTankUI()
    {
        if (tankPanel != null)
        {
            tankPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Refresh the slot UI display
    /// </summary>
    public void RefreshSlotUI()
    {
        if (slotUI != null)
        {
            slotUI.RefreshDisplay();
        }
    }

    /// <summary>
    /// Get the slot UI
    /// </summary>
    public OxygenTankSlotUI GetSlotUI()
    {
        return slotUI;
    }

    /// <summary>
    /// Check if the tank UI is currently visible
    /// </summary>
    public bool IsVisible => tankPanel != null && tankPanel.activeInHierarchy;

    #endregion

    #region Debug Methods

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[OxygenTankUI] {message}");

    }

    #endregion
}