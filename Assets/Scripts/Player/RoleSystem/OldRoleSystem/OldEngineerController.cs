using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls the engineer's repair functionality.
/// Handles region selection based on input direction, outline flashing, and repair button mashing.
/// </summary>
public class OldEngineerController : MonoBehaviour
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
    private OldEngineerInputHandler inputHandler;

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
        OldPlayerRoleManager.OnEngineerAssigned += OnEngineerAssigned;

        // Check if engineer already exists
        if (OldPlayerRoleManager.Instance != null && OldPlayerRoleManager.Instance.HasEngineer)
        {
            inputHandler = OldPlayerRoleManager.Instance.GetEngineerHandler();
            AssignToEngineer(inputHandler);
        }
        else
        {
            DebugLog("Waiting for engineer to connect...");
        }

        SubmarineHealthManager.Instance.OnSubmarineTakenDamage += HandleRumble;
    }

    private void HandleRumble(float lowFrequency, float highFrequency, float duration)
    {
        RumbleManager.Instance.RumblePulse(assignedGamepad, lowFrequency, highFrequency, duration);
    }

    private void OnEngineerAssigned(OldEngineerInputHandler handler)
    {
        AssignToEngineer(handler);
    }

    private void AssignToEngineer(OldEngineerInputHandler handler)
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
        if (!isAssigned || inputHandler == null || !inputHandler.IsActive)
            return;

        if (isInSelectMode)
            UpdateRegionSelection();
    }

    #region Region Selection

    private void UpdateRegionSelection()
    {
        Vector2 input = inputHandler.RegionSelectionInput;
        SubmarineHealthRegion newRegion = DetermineRegionFromInput(input);

        if (newRegion != currentSelectedRegion)
            SelectRegion(newRegion);
    }

    private SubmarineHealthRegion DetermineRegionFromInput(Vector2 input)
    {
        if (input.magnitude < 0.1f)
            return SubmarineHealthManager.Instance.GetRegionByName("Bottom");

        float absX = Mathf.Abs(input.x);
        float absY = Mathf.Abs(input.y);

        if (absY > absX && absY > directionThreshold)
            return SubmarineHealthManager.Instance.GetRegionByName(input.y > 0 ? "Front" : "Back");

        if (absX > absY && absX > directionThreshold)
            return SubmarineHealthManager.Instance.GetRegionByName(input.x > 0 ? "Right" : "Left");

        return SubmarineHealthManager.Instance.GetRegionByName("Bottom");
    }

    private void SelectRegion(SubmarineHealthRegion region)
    {
        if (currentSelectedRegion != null)
            DeselectCurrentRegion();

        currentSelectedRegion = region;
        currentSelectedRegionName = region != null ? region.RegionName : "None";

        if (currentSelectedRegion != null)
        {
            StartOutlineFlash();
            DebugLog($"Selected region: {currentSelectedRegion.RegionName}");
        }
    }

    private void DeselectCurrentRegion()
    {
        if (currentSelectedRegion != null && currentSelectedRegion.repairOutline != null)
        {
            StopOutlineFlash();
            currentSelectedRegion.repairOutline.OutlineWidth = 0f;
            DebugLog($"Deselected region: {currentSelectedRegion.RegionName}");
        }

        currentSelectedRegion = null;
        currentSelectedRegionName = "None";
    }

    #endregion

    #region Outline Flashing

    private void StartOutlineFlash()
    {
        StopOutlineFlash();

        if (currentSelectedRegion != null && currentSelectedRegion.repairOutline != null)
            flashCoroutine = StartCoroutine(FlashOutlineCoroutine());
    }

    private void StopOutlineFlash()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }
    }

    private IEnumerator FlashOutlineCoroutine()
    {
        while (currentSelectedRegion != null && currentSelectedRegion.repairOutline != null)
        {
            float width = Mathf.PingPong(Time.time * flashSpeed, maxOutlineWidth);
            currentSelectedRegion.repairOutline.OutlineWidth = width;
            yield return null;
        }
    }

    #endregion

    #region Input Event Handlers

    private void HandleSelectModeEntered()
    {
        isInSelectMode = true;
        DebugLog("Entered select mode");
        UpdateRegionSelection();
    }

    private void HandleSelectModeExited()
    {
        isInSelectMode = false;
        DebugLog("Exited select mode");
        DeselectCurrentRegion();
    }

    private void HandleRepairButtonPressed()
    {
        if (!isInSelectMode || currentSelectedRegion == null) return;
        if (Time.time - lastRepairTime < repairCooldown) return;

        RepairSelectedRegion();
        lastRepairTime = Time.time;
    }

    #endregion

    #region Repair Logic

    private void RepairSelectedRegion()
    {
        if (currentSelectedRegion == null) return;

        if (currentSelectedRegion.CurrentHealth >= currentSelectedRegion.MaxHealth)
        {
            DebugLog($"Region {currentSelectedRegion.RegionName} already at max health");
            return;
        }

        float healthBefore = currentSelectedRegion.CurrentHealth;
        currentSelectedRegion.RestoreHealth(repairAmountPerPress);
        float actualRepair = currentSelectedRegion.CurrentHealth - healthBefore;

        DebugLog($"Repaired {currentSelectedRegion.RegionName} by {actualRepair:F1} " +
                 $"({currentSelectedRegion.CurrentHealth:F0}/{currentSelectedRegion.MaxHealth:F0})");

        HandleRumble(lowFrequency, highFrequency, rumbleDuration);
    }

    #endregion

    private void OnDestroy()
    {
        OldPlayerRoleManager.OnEngineerAssigned -= OnEngineerAssigned;

        if (inputHandler != null)
        {
            inputHandler.OnSelectModeEntered -= HandleSelectModeEntered;
            inputHandler.OnSelectModeExited -= HandleSelectModeExited;
            inputHandler.OnRepairButtonPressed -= HandleRepairButtonPressed;
        }

        StopOutlineFlash();
        if (currentSelectedRegion != null && currentSelectedRegion.repairOutline != null)
            currentSelectedRegion.repairOutline.OutlineWidth = 0f;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[OldEngineerController] {message}");
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !isAssigned) return;

        if (currentSelectedRegion != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentSelectedRegion.transform.position, 0.5f);
        }
    }

#if UNITY_EDITOR
    [Button("Test Repair Current Region"), PropertyOrder(100)]
    private void TestRepair()
    {
        if (currentSelectedRegion != null) RepairSelectedRegion();
        else Debug.Log("[OldEngineerController] No region selected for testing");
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