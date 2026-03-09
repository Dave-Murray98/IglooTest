using UnityEngine;

/// <summary>
/// Added to a player who holds both the Pilot and Engineer roles simultaneously.
///
/// The problem it solves:
///   Both the PilotController and EngineerController want to read the left stick.
///   When the player is just navigating, the left stick should steer the submarine.
///   When the player holds the Engineer Select button (left trigger), the left stick
///   should instead select which hull region to repair — and the submarine should
///   stop responding to steering so it doesn't move unexpectedly.
///
/// How it works:
///   This bridge sits between the MultiRoleInputHandler and the two controllers.
///   Each frame, it checks if engineer select mode is active and tells the
///   PilotController to suppress its input if so. The EngineerController reads
///   directly from the handler and is unaffected — it already only acts when
///   select mode is held.
///
/// Lifetime:
///   Added by PlayerRoleManager when a Pilot+Engineer assignment is applied.
///   Destroyed by PlayerRoleManager before any reconfiguration.
/// </summary>
public class SoloPilotEngineerBridge : MonoBehaviour
{
    private MultiRoleInputHandler handler;
    private PilotController pilotController;
    private EngineerController engineerController;

    private bool isInitialised = false;

    /// <summary>
    /// Called immediately after this component is added by PlayerRoleManager.
    /// Wires up the references needed for input routing.
    /// </summary>
    public void Initialise(
        MultiRoleInputHandler inputHandler,
        PilotController pilot,
        EngineerController engineer)
    {
        handler = inputHandler;
        pilotController = pilot;
        engineerController = engineer;

        isInitialised = true;
    }

    private void Update()
    {
        if (!isInitialised || handler == null) return;

        // When engineer select mode is active, suppress pilot movement input
        // so the submarine stays still while the player selects a repair region.
        if (handler.EngineerSelectHeld)
        {
            pilotController?.SuppressMovementInput(true);
        }
        else
        {
            pilotController?.SuppressMovementInput(false);
        }
    }

    private void OnDestroy()
    {
        // Make sure pilot input suppression is lifted when this bridge is removed
        pilotController?.SuppressMovementInput(false);
    }
}