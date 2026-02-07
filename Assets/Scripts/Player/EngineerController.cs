using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls the engineer's repair functionality.
/// Handles region selection based on input direction, outline flashing, and repair button mashing.
/// </summary>
public class EngineerController : MonoBehaviour
{

    [Header("Repair Settings")]
    [Tooltip("How much health is restored per repair button press")]
    [SerializeField] private float repairAmountPerPress = 5f;

    [Tooltip("Minimum time between repair presses (prevents super-fast mashing exploits)")]
    [SerializeField] private float repairCooldown = 0.1f;

    [Header("Outline Flash Settings")]
    [Tooltip("Maximum outline width during flash animation")]
    [SerializeField] private float maxOutlineWidth = 5f;

    [Tooltip("How fast the outline flashes (higher = faster pulsing)")]
    [SerializeField] private float flashSpeed = 2f;

    [Header("Region Selection Settings")]
    [Tooltip("Input threshold for selecting a direction (0-1)")]
    [SerializeField] private float directionThreshold = 0.6f;

    [Header("Rumble Settings")]
    [SerializeField] private float lowFrequency = 0.4f;
    [SerializeField] private float highFrequency = 0.4f;
    [SerializeField] private float rumbleDuration = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showDebugGizmos = true;

    // Input handler reference
    private EngineerInputHandler inputHandler;

    // Current state
    [ShowInInspector, ReadOnly] private SubmarineHealthRegion currentSelectedRegion;
    [ShowInInspector, ReadOnly] private string currentSelectedRegionName = "None";
    [ShowInInspector, ReadOnly] private bool isInSelectMode = false;

    // Repair cooldown tracking
    private float lastRepairTime = 0f;

    // Outline flashing
    private Coroutine flashCoroutine;

    // State
    [ShowInInspector, ReadOnly] private bool isAssigned = false;

    private Gamepad assignedGamepad;
    private void Start()
    {


        // Subscribe to engineer assignment
        PlayerRoleManager.OnEngineerAssigned += OnEngineerAssigned;

        // Check if engineer already exists
        if (PlayerRoleManager.Instance != null && PlayerRoleManager.Instance.HasEngineer)
        {
            inputHandler = PlayerRoleManager.Instance.GetEngineerHandler();
            AssignToEngineer(inputHandler);
        }
        else
        {
            DebugLog("Waiting for engineer to connect...");
        }

        SubmarineHealthManager.Instance.OnSubmarineTakenDamage += RumblePulse;

    }

    private void OnEngineerAssigned(EngineerInputHandler handler)
    {
        AssignToEngineer(handler);
    }

    private void AssignToEngineer(EngineerInputHandler handler)
    {
        inputHandler = handler;
        isAssigned = true;

        // Subscribe to input events
        inputHandler.OnSelectModeEntered += HandleSelectModeEntered;
        inputHandler.OnSelectModeExited += HandleSelectModeExited;
        inputHandler.OnRepairButtonPressed += HandleRepairButtonPressed;

        DebugLog($"Assigned to Engineer (Player {handler.PlayerIndex})");

        // Get assigned gamepad for rumble
        assignedGamepad = handler.GetAssignedGamepad();
    }

    private void Update()
    {
        // Only process if we have an assigned engineer
        if (!isAssigned || inputHandler == null || !inputHandler.IsActive)
        {
            return;
        }

        // Update region selection when in select mode
        if (isInSelectMode)
        {
            UpdateRegionSelection();
        }
    }

    #region Region Selection

    /// <summary>
    /// Updates the currently selected region based on input direction
    /// </summary>
    private void UpdateRegionSelection()
    {
        Vector2 input = inputHandler.RegionSelectionInput;
        SubmarineHealthRegion newRegion = DetermineRegionFromInput(input);

        // If the region changed, update selection
        if (newRegion != currentSelectedRegion)
        {
            SelectRegion(newRegion);
        }
    }

    /// <summary>
    /// Maps input direction to a health region
    /// </summary>
    private SubmarineHealthRegion DetermineRegionFromInput(Vector2 input)
    {
        // No input or very small input = Bottom region (default)
        if (input.magnitude < 0.1f)
        {
            return SubmarineHealthManager.Instance.GetRegionByName("Bottom");
        }

        // Check which direction has the strongest input
        float absX = Mathf.Abs(input.x);
        float absY = Mathf.Abs(input.y);

        // Vertical input is stronger
        if (absY > absX && absY > directionThreshold)
        {
            if (input.y > 0)
            {
                // Forward = Front region
                return SubmarineHealthManager.Instance.GetRegionByName("Front");
            }
            else
            {
                // Backward = Back region
                return SubmarineHealthManager.Instance.GetRegionByName("Back");
            }
        }
        // Horizontal input is stronger
        else if (absX > absY && absX > directionThreshold)
        {
            if (input.x > 0)
            {
                // Right = Right region
                return SubmarineHealthManager.Instance.GetRegionByName("Right");
            }
            else
            {
                // Left = Left region
                return SubmarineHealthManager.Instance.GetRegionByName("Left");
            }
        }

        // If input doesn't meet threshold in any direction, default to bottom
        return SubmarineHealthManager.Instance.GetRegionByName("Bottom");
    }

    /// <summary>
    /// Selects a region and starts the outline flash animation
    /// </summary>
    private void SelectRegion(SubmarineHealthRegion region)
    {
        // Deselect previous region
        if (currentSelectedRegion != null)
        {
            DeselectCurrentRegion();
        }

        // Select new region
        currentSelectedRegion = region;
        currentSelectedRegionName = region != null ? region.RegionName : "None";

        if (currentSelectedRegion != null)
        {
            // Start flashing outline
            StartOutlineFlash();

            DebugLog($"Selected region: {currentSelectedRegion.RegionName}");
        }
    }

    /// <summary>
    /// Deselects the current region and stops outline animation
    /// </summary>
    private void DeselectCurrentRegion()
    {
        if (currentSelectedRegion != null && currentSelectedRegion.repairOutline != null)
        {
            // Stop flashing
            StopOutlineFlash();

            // Set outline to 0
            currentSelectedRegion.repairOutline.OutlineWidth = 0f;

            DebugLog($"Deselected region: {currentSelectedRegion.RegionName}");
        }

        currentSelectedRegion = null;
        currentSelectedRegionName = "None";
    }

    #endregion

    #region Outline Flashing

    /// <summary>
    /// Starts the outline flashing animation
    /// </summary>
    private void StartOutlineFlash()
    {
        // Stop any existing flash coroutine
        StopOutlineFlash();

        // Start new flash coroutine
        if (currentSelectedRegion != null && currentSelectedRegion.repairOutline != null)
        {
            flashCoroutine = StartCoroutine(FlashOutlineCoroutine());
        }
    }

    /// <summary>
    /// Stops the outline flashing animation
    /// </summary>
    private void StopOutlineFlash()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }
    }

    /// <summary>
    /// Coroutine that animates the outline width in a pulsing pattern
    /// </summary>
    private IEnumerator FlashOutlineCoroutine()
    {
        while (currentSelectedRegion != null && currentSelectedRegion.repairOutline != null)
        {
            // Use PingPong to create a smooth 0 -> max -> 0 loop
            float width = Mathf.PingPong(Time.time * flashSpeed, maxOutlineWidth);
            currentSelectedRegion.repairOutline.OutlineWidth = width;

            yield return null;
        }
    }

    #endregion

    #region Input Event Handlers

    /// <summary>
    /// Called when engineer enters select mode (left trigger pressed)
    /// </summary>
    private void HandleSelectModeEntered()
    {
        isInSelectMode = true;
        DebugLog("Entered select mode");

        // Immediately select default region (bottom)
        UpdateRegionSelection();
    }

    /// <summary>
    /// Called when engineer exits select mode (left trigger released)
    /// </summary>
    private void HandleSelectModeExited()
    {
        isInSelectMode = false;
        DebugLog("Exited select mode");

        // Deselect current region
        DeselectCurrentRegion();
    }

    /// <summary>
    /// Called when engineer presses the repair button (A button)
    /// </summary>
    private void HandleRepairButtonPressed()
    {
        // Can only repair when in select mode and a region is selected
        if (!isInSelectMode || currentSelectedRegion == null)
        {
            return;
        }

        // Check cooldown to prevent super-fast mashing exploits
        if (Time.time - lastRepairTime < repairCooldown)
        {
            return;
        }

        // Perform repair
        RepairSelectedRegion();

        lastRepairTime = Time.time;
    }

    #endregion

    #region Repair Logic

    /// <summary>
    /// Repairs the currently selected region
    /// </summary>
    private void RepairSelectedRegion()
    {
        if (currentSelectedRegion == null) return;

        // Check if region is already at max health
        if (currentSelectedRegion.CurrentHealth >= currentSelectedRegion.MaxHealth)
        {
            DebugLog($"Region {currentSelectedRegion.RegionName} already at max health");
            return;
        }

        // Apply repair
        float healthBefore = currentSelectedRegion.CurrentHealth;
        currentSelectedRegion.RestoreHealth(repairAmountPerPress);
        float healthAfter = currentSelectedRegion.CurrentHealth;

        float actualRepair = healthAfter - healthBefore;

        DebugLog($"Repaired {currentSelectedRegion.RegionName} by {actualRepair:F1} " +
                 $"({currentSelectedRegion.CurrentHealth:F0}/{currentSelectedRegion.MaxHealth:F0})");


        // Rumble gamepad
        RumblePulse(lowFrequency, highFrequency, rumbleDuration);

        // The crack visuals will automatically update via SubmarineHealthRegion's UpdateCrackVisuals()
    }

    public void RumblePulse(float lowFrequency, float highFrequency, float duration)
    {
        if (assignedGamepad != null)
        {
            //start rumble 
            assignedGamepad.SetMotorSpeeds(lowFrequency, highFrequency);

            //stop rumble after duration
            StartCoroutine(StopRumbleAfterDuration(duration));
        }
    }

    private IEnumerator StopRumbleAfterDuration(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (assignedGamepad != null)
        {
            assignedGamepad.SetMotorSpeeds(0f, 0f);
        }
    }

    #endregion

    private void OnDestroy()
    {
        // Unsubscribe from events
        PlayerRoleManager.OnEngineerAssigned -= OnEngineerAssigned;

        if (inputHandler != null)
        {
            inputHandler.OnSelectModeEntered -= HandleSelectModeEntered;
            inputHandler.OnSelectModeExited -= HandleSelectModeExited;
            inputHandler.OnRepairButtonPressed -= HandleRepairButtonPressed;
        }

        // Clean up outline
        StopOutlineFlash();
        if (currentSelectedRegion != null && currentSelectedRegion.repairOutline != null)
        {
            currentSelectedRegion.repairOutline.OutlineWidth = 0f;
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[EngineerController] {message}");
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !isAssigned) return;

        // Draw a sphere at the currently selected region
        if (currentSelectedRegion != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentSelectedRegion.transform.position, 0.5f);
        }
    }

#if UNITY_EDITOR
    // Inspector buttons for testing
    [Button("Test Repair Current Region"), PropertyOrder(100)]
    private void TestRepair()
    {
        if (currentSelectedRegion != null)
        {
            RepairSelectedRegion();
        }
        else
        {
            Debug.Log("[EngineerController] No region selected for testing");
        }
    }

    [Button("Select Front Region"), PropertyOrder(101)]
    private void TestSelectFront()
    {
        if (SubmarineHealthManager.Instance != null)
        {
            SelectRegion(SubmarineHealthManager.Instance.GetRegionByName("Front"));
            isInSelectMode = true;
        }
    }

    [Button("Deselect Region"), PropertyOrder(102)]
    private void TestDeselect()
    {
        DeselectCurrentRegion();
        isInSelectMode = false;
    }
#endif
}