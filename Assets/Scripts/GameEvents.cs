using System;
using UnityEngine;

public static class GameEvents
{

    #region Player Events

    public static event Action<float, float> OnPlayerHealthChanged;
    public static event Action OnPlayerDeath;

    // STAMINA EVENTS
    public static event Action<float, float> OnPlayerStaminaChanged;
    public static event Action OnPlayerStaminaDepleted;
    public static event Action OnPlayerStaminaRecovered;

    // OXYGEN EVENTS
    public static event Action<float, float> OnPlayerOxygenChanged;
    public static event Action OnPlayerOxygenDepleted;
    public static event Action OnPlayerOxygenRecovered;

    #endregion


    #region Game State Events
    public static event Action OnGamePaused;
    public static event Action OnGameResumed;

    #endregion

    #region UI Events
    public static event Action OnInventoryOpened;
    public static event Action OnInventoryClosed;
    #endregion


    #region Trigger Methods
    public static void TriggerPlayerHealthChanged(float currentHealth, float maxHealth) =>
        OnPlayerHealthChanged?.Invoke(currentHealth, maxHealth);

    public static void TriggerPlayerDeath()
    {
        Debug.Log("[GameEvents]Triggering player death event");
        OnPlayerDeath?.Invoke();
    }

    // STAMINA TRIGGER METHODS
    public static void TriggerPlayerStaminaChanged(float currentStamina, float maxStamina) =>
        OnPlayerStaminaChanged?.Invoke(currentStamina, maxStamina);

    public static void TriggerPlayerStaminaDepleted() => OnPlayerStaminaDepleted?.Invoke();

    public static void TriggerPlayerStaminaRecovered() => OnPlayerStaminaRecovered?.Invoke();

    // OXYGEN TRIGGER METHODS
    public static void TriggerPlayerOxygenChanged(float currentOxygen, float maxOxygen) =>
        OnPlayerOxygenChanged?.Invoke(currentOxygen, maxOxygen);

    public static void TriggerPlayerOxygenDepleted() => OnPlayerOxygenDepleted?.Invoke();

    public static void TriggerPlayerOxygenRecovered() => OnPlayerOxygenRecovered?.Invoke();

    public static void TriggerGamePaused() => OnGamePaused?.Invoke();

    public static void TriggerGameResumed() => OnGameResumed?.Invoke();

    public static void TriggerInventoryOpened() => OnInventoryOpened?.Invoke();
    public static void TriggerInventoryClosed() => OnInventoryClosed?.Invoke();

    #endregion

}