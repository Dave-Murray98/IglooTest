using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

/// <summary>
/// UI component for the oxygen tank slot.
/// Displays tank sprite, oxygen bar, and handles drag-drop interactions.
/// Pattern based on ClothingSlotUI but simplified for single tank slot.
/// </summary>
public class OxygenTankSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Components")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image tankImage;
    [SerializeField] private TextMeshProUGUI slotLabel;
    [SerializeField] private Image oxygenBar;
    [SerializeField] private GameObject oxygenBarContainer;

    [Header("Visual Settings")]
    [SerializeField] private Color emptySlotColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color occupiedSlotColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
    [SerializeField] private Color hoverColor = new Color(0.4f, 0.4f, 0.5f, 0.9f);
    [SerializeField] private Color validDropColor = new Color(0.2f, 0.8f, 0.2f, 0.7f);
    [SerializeField] private Color invalidDropColor = new Color(0.8f, 0.2f, 0.2f, 0.7f);

    [Header("Oxygen Bar Colors")]
    [SerializeField] private Color fullOxygenColor = Color.cyan;
    [SerializeField] private Color halfOxygenColor = Color.yellow;
    [SerializeField] private Color lowOxygenColor = Color.red;

    [Header("Animation Settings")]
    [SerializeField] private float hoverAnimationDuration = 0.2f;
    [SerializeField] private float equipAnimationDuration = 0.3f;
    [SerializeField] private float errorShakeDuration = 0.5f;
    [SerializeField] private float errorShakeStrength = 10f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // State
    private bool isInitialized = false;
    private bool isHovering = false;
    private bool isDragOver = false;

    // References
    private OxygenTankManager tankManager;
    private PlayerInventoryManager inventoryManager;

    // Animation
    private Tween currentAnimation;

    private void Awake()
    {
        SetupUIComponents();
    }

    private void Start()
    {
        tankManager = OxygenTankManager.Instance;
        inventoryManager = PlayerInventoryManager.Instance;

        isInitialized = true;
        SubscribeToTankEvents();
        RefreshDisplay();
    }

    private void OnDestroy()
    {
        UnsubscribeFromTankEvents();
    }

    /// <summary>
    /// Subscribe to tank manager events for automatic UI updates
    /// </summary>
    private void SubscribeToTankEvents()
    {
        if (tankManager != null)
        {
            tankManager.OnTankEquipped += OnTankEquipped;
            tankManager.OnTankUnequipped += OnTankUnequipped;
            tankManager.OnTankOxygenChanged += OnOxygenChanged;
        }
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
            tankManager.OnTankOxygenChanged -= OnOxygenChanged;
        }
    }

    /// <summary>
    /// Setup UI components if they're not assigned
    /// </summary>
    private void SetupUIComponents()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (tankImage == null)
        {
            var images = GetComponentsInChildren<Image>();
            if (images.Length > 1)
                tankImage = images[1];
        }

        if (slotLabel == null)
            slotLabel = GetComponentInChildren<TextMeshProUGUI>();

        if (oxygenBar == null)
        {
            var barObj = transform.Find("OxygenBar");
            if (barObj != null)
            {
                oxygenBar = barObj.GetComponent<Image>();
                oxygenBarContainer = barObj.gameObject;
            }
        }

        CreateMissingComponents();
        UpdateSlotLabel();
    }

    /// <summary>
    /// Create missing UI components
    /// </summary>
    private void CreateMissingComponents()
    {
        if (backgroundImage == null)
        {
            backgroundImage = gameObject.GetComponent<Image>();
            if (backgroundImage == null)
            {
                Debug.LogWarning($"OxygenTankSlotUI {name} is missing a background image! creating new one");
                backgroundImage = gameObject.AddComponent<Image>();
            }
        }

        if (tankImage == null)
        {
            GameObject tankImageObj = new GameObject("TankImage");
            tankImageObj.transform.SetParent(transform, false);

            var rectTransform = tankImageObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(5, 5);
            rectTransform.offsetMax = new Vector2(-5, -5);

            tankImage = tankImageObj.AddComponent<Image>();
            tankImage.raycastTarget = false;
            tankImage.preserveAspect = true;
            DebugLog($"{name} is missing a tank image! creating new one");
        }

        if (slotLabel == null)
        {
            GameObject labelObj = new GameObject("SlotLabel");
            labelObj.transform.SetParent(transform, false);

            var rectTransform = labelObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(1, 0.3f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            slotLabel = labelObj.AddComponent<TextMeshProUGUI>();
            slotLabel.text = "O2 Tank";
            slotLabel.fontSize = 8f;
            slotLabel.color = Color.white;
            slotLabel.alignment = TextAlignmentOptions.Center;
            slotLabel.raycastTarget = false;
            DebugLog($"{name} is missing a slot label! creating new one");
        }

        if (oxygenBar == null)
        {
            CreateOxygenBar();
            DebugLog($"{name} is missing an oxygen bar! creating new one");
        }
    }

    /// <summary>
    /// Create the oxygen bar UI
    /// </summary>
    private void CreateOxygenBar()
    {
        GameObject containerObj = new GameObject("OxygenBarContainer");
        containerObj.transform.SetParent(transform, false);

        var containerRect = containerObj.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 0.7f);
        containerRect.anchorMax = new Vector2(1, 0.8f);
        containerRect.offsetMin = new Vector2(2, 0);
        containerRect.offsetMax = new Vector2(-2, 0);

        oxygenBarContainer = containerObj;

        GameObject backgroundBarObj = new GameObject("OxygenBarBackground");
        backgroundBarObj.transform.SetParent(containerObj.transform, false);

        var backgroundRect = backgroundBarObj.AddComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        var backgroundBarImage = backgroundBarObj.AddComponent<Image>();
        backgroundBarImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        backgroundBarImage.raycastTarget = false;

        GameObject oxygenBarObj = new GameObject("OxygenBar");
        oxygenBarObj.transform.SetParent(containerObj.transform, false);

        var oxygenRect = oxygenBarObj.AddComponent<RectTransform>();
        oxygenRect.anchorMin = Vector2.zero;
        oxygenRect.anchorMax = Vector2.one;
        oxygenRect.offsetMin = Vector2.zero;
        oxygenRect.offsetMax = Vector2.zero;

        oxygenBar = oxygenBarObj.AddComponent<Image>();
        oxygenBar.color = fullOxygenColor;
        oxygenBar.type = Image.Type.Filled;
        oxygenBar.fillMethod = Image.FillMethod.Horizontal;
        oxygenBar.raycastTarget = false;
    }

    /// <summary>
    /// Update the slot label text
    /// </summary>
    private void UpdateSlotLabel()
    {
        if (slotLabel != null)
        {
            slotLabel.text = "O2 Tank";
        }
    }

    /// <summary>
    /// Refresh the slot's visual display based on current tank state.
    /// </summary>
    public void RefreshDisplay()
    {
        if (!isInitialized || tankManager == null) return;

        var slot = tankManager.GetSlot();
        if (slot == null) return;

        bool isEmpty = slot.IsEmpty;
        var equippedTank = slot.GetEquippedTank();

        // Update background color
        Color targetColor = isEmpty ? emptySlotColor : occupiedSlotColor;
        if (isHovering)
            targetColor = hoverColor;

        if (backgroundImage != null)
            backgroundImage.color = targetColor;

        // Update tank image
        if (tankImage != null)
        {
            if (isEmpty || equippedTank?.ItemData?.itemSprite == null)
            {
                tankImage.sprite = null;
                tankImage.color = Color.clear;
            }
            else
            {
                tankImage.sprite = equippedTank.ItemData.itemSprite;
                tankImage.color = Color.white;
            }
        }

        // Update oxygen bar
        UpdateOxygenBar(slot);
    }

    /// <summary>
    /// Update the oxygen bar based on tank oxygen level.
    /// </summary>
    private void UpdateOxygenBar(OxygenTankSlot slot)
    {
        if (oxygenBarContainer == null || oxygenBar == null) return;

        bool isEmpty = slot.IsEmpty;
        oxygenBarContainer.SetActive(!isEmpty);

        if (!isEmpty)
        {
            var templateData = slot.GetEquippedTankData();
            var instanceData = slot.GetEquippedTankInstanceData();

            if (templateData != null && instanceData != null)
            {
                float oxygenPercentage = instanceData.GetOxygenPercentage(templateData);
                oxygenBar.fillAmount = oxygenPercentage;

                // Color coding based on oxygen level
                if (oxygenPercentage >= 0.6f)
                    oxygenBar.color = fullOxygenColor;
                else if (oxygenPercentage >= 0.3f)
                    oxygenBar.color = halfOxygenColor;
                else
                    oxygenBar.color = lowOxygenColor;
            }
        }
    }

    #region Event Handlers

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            TryUnequipTank();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;

        if (backgroundImage != null)
        {
            StopCurrentAnimation();
            currentAnimation = backgroundImage.DOColor(hoverColor, hoverAnimationDuration);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;

        if (!isDragOver && backgroundImage != null)
        {
            Color targetColor = GetCurrentTargetColor();
            StopCurrentAnimation();
            currentAnimation = backgroundImage.DOColor(targetColor, hoverAnimationDuration);
        }
    }

    /// <summary>
    /// Drop handler with comprehensive validation and rejection feedback.
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        // Clear drag over state
        isDragOver = false;
        ClearDragOverVisualFeedback();

        // Get the dragged object
        var draggedObject = eventData.pointerDrag;
        if (draggedObject == null)
        {
            DebugLog("Drop failed: No dragged object");
            return;
        }

        // Get the drag handler
        var dragHandler = draggedObject.GetComponent<PlayerInventoryItemDragHandler>();
        if (dragHandler == null)
        {
            DebugLog("Drop failed: No drag handler found");
            return;
        }

        // Get the item data from the drag handler
        var itemData = GetItemDataFromDragHandler(dragHandler);
        if (itemData == null)
        {
            DebugLog("Drop failed: No item data found");
            NotifyDragHandlerOfInvalidDrop(dragHandler);
            return;
        }

        // Validate this is an oxygen tank
        var validationResult = ValidateTankDrop(itemData);

        if (validationResult.IsValid)
        {
            DebugLog($"Valid drop detected: {itemData.ItemData?.itemName}");

            // Attempt to equip using the OxygenTankManager
            bool success = tankManager.EquipTank(itemData.ID);

            if (success)
            {
                DebugLog($"Successfully equipped {itemData.ItemData?.itemName}");
                ShowSuccessFeedback();
            }
            else
            {
                DebugLog($"Failed to equip {itemData.ItemData?.itemName}");
                NotifyDragHandlerOfInvalidDrop(dragHandler);
            }
        }
        else
        {
            // Detailed rejection with specific feedback
            DebugLog($"Invalid drop rejected: {validationResult.Message}");
            ShowRejectionFeedback(validationResult.Message, itemData);

            // Signal the drag handler that this was an invalid drop so it can revert
            NotifyDragHandlerOfInvalidDrop(dragHandler);
        }
    }

    /// <summary>
    /// Comprehensive validation for tank drops with detailed error messages.
    /// </summary>
    private ValidationResult ValidateTankDrop(InventoryItemData itemData)
    {
        // Check if it's an oxygen tank at all
        if (itemData?.ItemData?.itemType != ItemType.OxygenTank)
        {
            if (itemData?.ItemData != null)
            {
                string itemTypeName = itemData.ItemData.itemType.ToString();
                return new ValidationResult(false, $"{itemData.ItemData.itemName} is {itemTypeName}, not an oxygen tank");
            }
            return new ValidationResult(false, "Not an oxygen tank");
        }

        var tankData = itemData.ItemData.OxygenTankData;
        if (tankData == null)
        {
            return new ValidationResult(false, $"{itemData.ItemData.itemName} has no oxygen tank data");
        }

        if (!tankData.IsValid())
        {
            return new ValidationResult(false, $"{itemData.ItemData.itemName} has invalid tank data");
        }

        return new ValidationResult(true, "Valid oxygen tank drop");
    }

    /// <summary>
    /// Notify the drag handler that the drop was invalid so it can revert.
    /// </summary>
    private void NotifyDragHandlerOfInvalidDrop(PlayerInventoryItemDragHandler dragHandler)
    {
        // Use reflection to call a method on the drag handler to indicate invalid drop
        var method = dragHandler.GetType().GetMethod("HandleInvalidClothingDrop",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (method != null)
        {
            method.Invoke(dragHandler, null);
        }
        else
        {
            // Fallback: try to access the revert method directly
            var revertMethod = dragHandler.GetType().GetMethod("RevertToOriginalState",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (revertMethod != null)
            {
                revertMethod.Invoke(dragHandler, null);
                DebugLog("Invoked drag handler revert method as fallback");
            }
            else
            {
                Debug.LogWarning("[OxygenTankSlotUI] Could not notify drag handler of invalid drop - methods not found");
            }
        }
    }

    #endregion

    #region Drag Detection (for visual feedback only)

    /// <summary>
    /// Static method to detect when inventory items are being dragged over the tank slot.
    /// This provides visual feedback without interfering with the actual drop handling.
    /// </summary>
    public static void HandleDragOverTankSlot(PointerEventData eventData, InventoryItemData itemData)
    {
        // Find tank slot under pointer
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        OxygenTankSlotUI tankSlot = null;
        foreach (var result in results)
        {
            tankSlot = result.gameObject.GetComponent<OxygenTankSlotUI>();
            if (tankSlot != null)
                break;

            var parentSlot = result.gameObject.GetComponentInParent<OxygenTankSlotUI>();
            if (parentSlot != null)
            {
                tankSlot = parentSlot;
                break;
            }
        }

        if (tankSlot != null)
        {
            // Check if this is a valid drop target
            bool isValidDrop = itemData?.ItemData?.itemType == ItemType.OxygenTank &&
                               itemData.ItemData.OxygenTankData != null;

            // Set visual feedback
            tankSlot.SetDragOverVisualFeedback(isValidDrop);
        }
    }

    /// <summary>
    /// Static method to clear drag feedback from tank slot.
    /// </summary>
    public static void ClearAllDragFeedback()
    {
        var tankSlot = FindFirstObjectByType<OxygenTankSlotUI>();
        if (tankSlot != null)
        {
            tankSlot.ClearDragOverVisualFeedback();
        }
    }

    /// <summary>
    /// Set visual feedback during drag-over operations.
    /// </summary>
    private void SetDragOverVisualFeedback(bool isValidDrop)
    {
        isDragOver = true;

        if (backgroundImage != null)
        {
            StopCurrentAnimation();
            Color feedbackColor = isValidDrop ? validDropColor : invalidDropColor;
            currentAnimation = backgroundImage.DOColor(feedbackColor, hoverAnimationDuration);
        }
    }

    /// <summary>
    /// Clear drag-over visual feedback.
    /// </summary>
    private void ClearDragOverVisualFeedback()
    {
        isDragOver = false;

        if (backgroundImage != null)
        {
            Color targetColor = GetCurrentTargetColor();
            StopCurrentAnimation();
            currentAnimation = backgroundImage.DOColor(targetColor, hoverAnimationDuration);
        }
    }

    #endregion

    /// <summary>
    /// Get item data from drag handler using reflection
    /// </summary>
    private InventoryItemData GetItemDataFromDragHandler(PlayerInventoryItemDragHandler dragHandler)
    {
        var field = dragHandler.GetType().GetField("inventoryItemData",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        return field?.GetValue(dragHandler) as InventoryItemData;
    }

    #region User Feedback Methods

    /// <summary>
    /// Show visual feedback for successful operations
    /// </summary>
    private void ShowSuccessFeedback()
    {
        if (backgroundImage != null)
        {
            StopCurrentAnimation();

            var originalColor = backgroundImage.color;
            backgroundImage.color = validDropColor;

            currentAnimation = backgroundImage.DOColor(originalColor, 0.3f).SetDelay(0.1f);
        }
    }

    /// <summary>
    /// Show enhanced rejection feedback with detailed messaging.
    /// </summary>
    private void ShowRejectionFeedback(string message, InventoryItemData itemData)
    {
        Debug.LogWarning($"OxygenTankSlotUI Rejection: {message}");

        if (backgroundImage != null)
        {
            StopCurrentAnimation();

            var originalColor = backgroundImage.color;
            var originalPosition = transform.localPosition;

            // Enhanced rejection animation
            backgroundImage.color = invalidDropColor;
            backgroundImage.DOColor(originalColor, 0.5f);

            // Shake for rejections
            currentAnimation = transform.DOShakePosition(errorShakeDuration * 1.5f, errorShakeStrength * 1.5f, 15, 90, false, true)
                .OnComplete(() => transform.localPosition = originalPosition);
        }

        if (itemData?.ItemData != null)
        {
            DebugLog($"Rejected {itemData.ItemData.itemName}: {message}");
        }
    }

    /// <summary>
    /// Stop any current animation to prevent conflicts
    /// </summary>
    private void StopCurrentAnimation()
    {
        if (currentAnimation != null && currentAnimation.IsActive())
        {
            currentAnimation.Kill();
            currentAnimation = null;
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Try to unequip tank with better error handling and user feedback
    /// </summary>
    private void TryUnequipTank()
    {
        if (!isInitialized || tankManager == null) return;

        var slot = tankManager.GetSlot();
        if (slot == null || slot.IsEmpty)
        {
            return;
        }

        var equippedTank = slot.GetEquippedTank();
        if (equippedTank?.ItemData != null && !inventoryManager.HasSpaceForItem(equippedTank.ItemData))
        {
            return;
        }

        bool success = tankManager.UnequipTank();
        if (success)
        {
            DebugLog("Unequipped oxygen tank");
            ShowSuccessFeedback();
            RefreshDisplay();
        }
    }

    /// <summary>
    /// Get the current target color for the background
    /// </summary>
    private Color GetCurrentTargetColor()
    {
        if (!isInitialized || tankManager == null) return emptySlotColor;

        var slot = tankManager.GetSlot();
        return (slot?.IsEmpty ?? true) ? emptySlotColor : occupiedSlotColor;
    }

    /// <summary>
    /// Animate the tank being equipped
    /// </summary>
    private void AnimateItemEquipped()
    {
        if (tankImage != null)
        {
            tankImage.transform.localScale = Vector3.zero;
            tankImage.transform.DOScale(Vector3.one, equipAnimationDuration)
                .SetEase(Ease.OutBack);
        }
    }

    #endregion

    #region Tank Event Handlers

    /// <summary>
    /// Handle tank equipped events
    /// </summary>
    private void OnTankEquipped(OxygenTankSlot slot, InventoryItemData tank)
    {
        RefreshDisplay();
        AnimateItemEquipped();
    }

    /// <summary>
    /// Handle tank unequipped events
    /// </summary>
    private void OnTankUnequipped(OxygenTankSlot slot, string tankId)
    {
        RefreshDisplay();
    }

    /// <summary>
    /// Handle oxygen changes for equipped tank.
    /// </summary>
    private void OnOxygenChanged(string tankId, float newOxygen)
    {
        var slot = tankManager?.GetSlot();
        if (slot != null && slot.equippedTankId == tankId)
        {
            UpdateOxygenBar(slot);
        }
    }

    #endregion

    /// <summary>
    /// Debug logging helper
    /// </summary>
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[OxygenTankSlotUI] {message}");
    }

    /// <summary>
    /// Get debug information about this slot
    /// </summary>
    public string GetDebugInfo()
    {
        if (!isInitialized) return $"Slot[{name}]: Not initialized";

        var slot = tankManager?.GetSlot();
        if (slot == null) return "Slot: No tank slot found";

        return slot.GetDebugInfo();
    }
}