using System.Collections;
using UnityEngine;

/// <summary>
/// Item type enumeration for different item categories
/// </summary>
public enum ItemType
{
    Consumable, // Food, meds, water - can be consumed to affect player stats
    Ammo      // Ammunition - stackable
}

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Basic Item Info")]
    public string itemName;
    [TextArea(2, 4)]
    public string description;

    [Header("Item Type")]
    public ItemType itemType = ItemType.Consumable;

    [Header("Visual Configuration")]
    public Sprite itemSprite;
    [Range(.1f, 5.0f)]
    public float spriteScaleX = 1.0f;
    [Range(.1f, 5.0f)]
    public float spriteScaleY = 1.0f;
    public Vector2 spritePositionOffset = Vector2.zero;

    [Header("Noise Settings (for creating a player noise (not sound effect))")]
    public float effectNoiseVolume = 10f;


    [Header("Item Type Specific Settings")]
    [Header("Consumable Settings")]
    [SerializeField] private ConsumableData consumableData;

    [Header("Ammo Settings")]
    [SerializeField] private AmmoData ammoData;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // Properties for accessing type-specific data
    public ConsumableData ConsumableData => consumableData;
    public AmmoData AmmoData => ammoData;

}