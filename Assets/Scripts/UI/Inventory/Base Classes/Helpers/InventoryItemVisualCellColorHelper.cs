using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "InventoryItemVisualCellColorHelper", menuName = "Inventory/InventoryItemVisualCellColorHelper")]
public class InventoryItemVisualCellColorHelper : ScriptableObject
{

    [SerializeField] private Color consumeableColor;
    [SerializeField] private Color rangedWeaponColor;
    [SerializeField] private Color meleeWeaponColor;
    [SerializeField] private Color throwableColor;
    [SerializeField] private Color toolColor;
    [SerializeField] private Color keyItemColor;
    [SerializeField] private Color ammoColor;
    [SerializeField] private Color clothingColor;
    [SerializeField] private Color bowColor;
    [SerializeField] private Color toolEnergySourceColor;
    [SerializeField] private Color oxygenTankColor;

    public Color GetCellColor(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.Consumable:
                return consumeableColor;
            case ItemType.RangedWeapon:
                return rangedWeaponColor;
            case ItemType.MeleeWeapon:
                return meleeWeaponColor;
            case ItemType.Throwable:
                return throwableColor;
            case ItemType.Tool:
                return toolColor;
            case ItemType.KeyItem:
                return keyItemColor;
            case ItemType.Ammo:
                return ammoColor;
            case ItemType.Clothing:
                return clothingColor;
            case ItemType.Bow:
                return bowColor;
            case ItemType.ToolEnergySource:
                return toolEnergySourceColor;
            case ItemType.OxygenTank:
                return oxygenTankColor;
            default:
                return Color.white;
        }
    }
}