using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Controls the engineer's repair functionality.
/// Uses MultiRoleInputHandler.
///
/// Rumble fix: assignedGamepad is no longer cached at AssignHandler() time.
/// Instead, inputHandler.GetAssignedGamepad() is called at the moment rumble is needed.
/// </summary>
public class EngineerController : MonoBehaviour
{
    [Header("Repair Settings")]
    [Tooltip("How much health is restored per repair button press")]
    [SerializeField] private float repairAmountPerPress = 5f;

    [Tooltip("Minimum time between repair presses (prevents super-fast mashing)")]
    [SerializeField] private float repairCooldown = 0.1f;

    [Header("Outline Flash Settings")]
    [Tooltip("Maximum outline width during flash animation")]
    [SerializeField] private float maxOutlineWidth = 5f;

    [Tooltip("How fast the outline flashes (higher = faster pulsing)")]
    [SerializeField] private float flashSpeed = 2f;

    [Header("Region Selection Settings")]
    [Tooltip("Input threshold for selecting a direction (0–1)")]
    [SerializeField] private float directionThreshold = 0.6f;

    [Header("Rumble Settings")]
    [SerializeField] private float repairRumbleLow = 0.4f;
    [SerializeField] private float repairRumbleHigh = 0.4f;
    [SerializeField] private float repairRumbleDuration = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showDebugGizmos = true;

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    private MultiRoleInputHandler inputHandler;

    [ShowInInspector, ReadOnly] private SubmarineHealthRegion currentSelectedRegion;
    [ShowInInspector, ReadOnly] private string currentSelectedRegionName = "None";
    [ShowInInspector, ReadOnly] private bool isInSelectMode = false;

    private float lastRepairTime = 0f;
    private Coroutine flashCoroutine;

    // No cached assignedGamepad — always fetched fresh from inputHandler at call time.

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        SubmarineHealthManager.Instance.OnSubmarineTakenDamage += HandleDamageRumble;
    }

    private void Update()
    {
        if (inputHandler == null || !inputHandler.IsActive) return;
        if (isInSelectMode) UpdateRegionSelection();
    }

    private void OnDestroy()
    {
        if (SubmarineHealthManager.Instance != null)
            SubmarineHealthManager.Instance.OnSubmarineTakenDamage -= HandleDamageRumble;

        DetachHandler();
        StopOutlineFlash();

        if (currentSelectedRegion?.repairOutline != null)
            currentSelectedRegion.repairOutline.OutlineWidth = 0f;
    }

    // -------------------------------------------------------------------------
    // Public API — called by PlayerRoleManager
    // -------------------------------------------------------------------------

    public void AssignHandler(MultiRoleInputHandler handler)
    {
        DetachHandler();

        inputHandler = handler;

        if (inputHandler != null)
        {
            inputHandler.OnEngineerSelectEntered += HandleSelectModeEntered;
            inputHandler.OnEngineerSelectExited += HandleSelectModeExited;
            inputHandler.OnRepairPressed += HandleRepairButtonPressed;
        }

        DebugLog($"Handler assigned (Player {handler?.PlayerIndex})");
    }

    public void DetachHandler()
    {
        if (inputHandler != null)
        {
            inputHandler.OnEngineerSelectEntered -= HandleSelectModeEntered;
            inputHandler.OnEngineerSelectExited -= HandleSelectModeExited;
            inputHandler.OnRepairPressed -= HandleRepairButtonPressed;
        }

        inputHandler = null;

        if (isInSelectMode)
        {
            isInSelectMode = false;
            DeselectCurrentRegion();
        }
    }

    // -------------------------------------------------------------------------
    // Region selection
    // -------------------------------------------------------------------------

    private void UpdateRegionSelection()
    {
        Vector2 input = inputHandler.MovementInput;
        SubmarineHealthRegion newRegion = DetermineRegionFromInput(input);
        if (newRegion != currentSelectedRegion) SelectRegion(newRegion);
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
        if (currentSelectedRegion != null) DeselectCurrentRegion();

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
        if (currentSelectedRegion?.repairOutline != null)
        {
            StopOutlineFlash();
            currentSelectedRegion.repairOutline.OutlineWidth = 0f;
        }

        currentSelectedRegion = null;
        currentSelectedRegionName = "None";
    }

    // -------------------------------------------------------------------------
    // Outline flashing
    // -------------------------------------------------------------------------

    private void StartOutlineFlash()
    {
        StopOutlineFlash();
        if (currentSelectedRegion?.repairOutline != null)
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
        while (currentSelectedRegion?.repairOutline != null)
        {
            currentSelectedRegion.repairOutline.OutlineWidth =
                Mathf.PingPong(Time.time * flashSpeed, maxOutlineWidth);
            yield return null;
        }
    }

    // -------------------------------------------------------------------------
    // Input event handlers
    // -------------------------------------------------------------------------

    private void HandleSelectModeEntered()
    {
        isInSelectMode = true;
        UpdateRegionSelection();
        DebugLog("Entered select mode");
    }

    private void HandleSelectModeExited()
    {
        isInSelectMode = false;
        DeselectCurrentRegion();
        DebugLog("Exited select mode");
    }

    private void HandleRepairButtonPressed()
    {
        if (!isInSelectMode || currentSelectedRegion == null) return;
        if (Time.time - lastRepairTime < repairCooldown) return;

        RepairSelectedRegion();
        lastRepairTime = Time.time;
    }

    // -------------------------------------------------------------------------
    // Repair logic
    // -------------------------------------------------------------------------

    private void RepairSelectedRegion()
    {
        if (currentSelectedRegion == null) return;

        if (currentSelectedRegion.CurrentHealth >= currentSelectedRegion.MaxHealth)
        {
            DebugLog($"{currentSelectedRegion.RegionName} already at max health");
            return;
        }

        float before = currentSelectedRegion.CurrentHealth;
        currentSelectedRegion.RestoreHealth(repairAmountPerPress);
        float repaired = currentSelectedRegion.CurrentHealth - before;

        DebugLog($"Repaired {currentSelectedRegion.RegionName} by {repaired:F1} " +
                 $"({currentSelectedRegion.CurrentHealth:F0}/{currentSelectedRegion.MaxHealth:F0})");

        // Fetch the gamepad fresh at rumble time — not cached — so it is always valid
        RumbleManager.Instance.RumblePulse(
            inputHandler?.GetAssignedGamepad(),
            repairRumbleLow, repairRumbleHigh, repairRumbleDuration);
    }

    private void HandleDamageRumble(float low, float high, float duration)
    {
        RumbleManager.Instance.RumblePulse(
            inputHandler?.GetAssignedGamepad(), low, high, duration);
    }

    // -------------------------------------------------------------------------
    // Debug
    // -------------------------------------------------------------------------

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[EngineerController] {message}");
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || inputHandler == null) return;
        if (currentSelectedRegion != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentSelectedRegion.transform.position, 0.5f);
        }
    }

#if UNITY_EDITOR
    [Button("Test Repair Current Region")]
    private void TestRepair()
    {
        if (currentSelectedRegion != null) RepairSelectedRegion();
        else Debug.Log("[EngineerController] No region selected");
    }
#endif
}