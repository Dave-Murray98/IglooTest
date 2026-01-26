using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "Scriptable Objects/PlayerData")]
public class PlayerData : EntityData
{
    [Header("Camera")]
    public float lookSensitivity = 2f;
    public float verticalLookLimit = 85f;
    public float crouchHeightMultiplier = 0.5f; // Default crouch height multiplier

    [Header("Stamina Settings")]
    [SerializeField] public float maxStamina = 100f;
    [SerializeField] public float staminaRegenRate = 10f; // Stamina regenerated per second
    [SerializeField] public float staminaRegenDelay = 3f; // Delay in seconds before stamina starts regenerating after depletion

    [Header("Oxygen Settings")]
    [SerializeField] public float maxOxygen = 100f;
    [SerializeField] public float oxygenDepletionRate = 2f; // Oxygen consumed per second

}
