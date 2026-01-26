using UnityEngine;

/// <summary>
/// Defines the operational states a manager can be in.
/// Used to coordinate manager behavior between menu and gameplay contexts.
/// </summary>
public enum ManagerOperationalState
{
    /// <summary>
    /// Manager is in menu context - minimal operations, no gameplay references.
    /// Used when in MainMenu scene or similar non-gameplay contexts.
    /// </summary>
    Menu,

    /// <summary>
    /// Manager is in full gameplay mode with all systems active.
    /// Used during normal gameplay in game scenes.
    /// </summary>
    Gameplay,

    /// <summary>
    /// Manager is transitioning between states - preparing for state change.
    /// Used briefly during scene transitions or state changes.
    /// </summary>
    Transition
}

/// <summary>
/// Interface for managers that need to respond to operational state changes.
/// Allows managers to adapt their behavior based on whether the game is in
/// menu mode or gameplay mode, preventing null reference errors and enabling
/// clean transitions between MainMenu and gameplay scenes.
/// </summary>
public interface IManagerState
{
    /// <summary>
    /// Gets the current operational state of this manager.
    /// </summary>
    ManagerOperationalState CurrentOperationalState { get; }

    /// <summary>
    /// Transitions this manager to a new operational state.
    /// Managers should clean up/prepare their systems based on the new state.
    /// </summary>
    /// <param name="newState">The state to transition to</param>
    void SetOperationalState(ManagerOperationalState newState);

    /// <summary>
    /// Called when entering Menu state. Managers should:
    /// - Disable gameplay Update loops
    /// - Clear references to scene-based objects
    /// - Maintain singleton integrity
    /// - Keep only menu-relevant functionality active
    /// </summary>
    void OnEnterMenuState();

    /// <summary>
    /// Called when entering Gameplay state. Managers should:
    /// - Enable all gameplay systems
    /// - Refresh scene references
    /// - Resume normal operation
    /// </summary>
    void OnEnterGameplayState();

    /// <summary>
    /// Called when entering Transition state. Managers should:
    /// - Prepare for upcoming state change
    /// - Save any necessary data
    /// - Begin cleanup of current state
    /// </summary>
    void OnEnterTransitionState();

    /// <summary>
    /// Returns whether this manager can safely operate in the current state.
    /// Used to prevent operations when manager is in wrong state.
    /// </summary>
    bool CanOperateInCurrentState();
}