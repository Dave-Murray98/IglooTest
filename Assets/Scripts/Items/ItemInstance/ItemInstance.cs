using System;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Represents a single instance of an item with its own mutable state.
/// Each ItemInstance references an ItemData template (ScriptableObject) but maintains
/// its own runtime data like current ammo, durability, condition, etc.
///
/// This separates template data (shared across all items of this type) from
/// instance data (unique to this specific item).
/// </summary>
[System.Serializable]
public class ItemInstance
{
    [Header("Template Reference")]
    [SerializeField] private string itemDataName; // Name of the ItemData ScriptableObject
    [NonSerialized] private ItemData _cachedItemData; // Runtime cache

    [Header("Instance Identity")]
    [SerializeField] private string instanceID; // Unique identifier for this instance

    [Header("Instance Data")]
    [SerializeField] private ConsumableInstanceData consumableInstanceData;
    [SerializeField] private RangedWeaponInstanceData rangedWeaponInstanceData;
    [SerializeField] private AmmoInstanceData ammoInstanceData;
    [SerializeField] private ClothingInstanceData clothingInstanceData;
    [SerializeField] private MeleeWeaponInstanceData meleeWeaponInstanceData;
    [SerializeField] private ToolInstanceData toolInstanceData;
    [SerializeField] private ThrowableInstanceData throwableInstanceData;
    [SerializeField] private BowInstanceData bowInstanceData;
    [SerializeField] private KeyItemInstanceData keyItemInstanceData;
    [SerializeField] private OxygenTankInstanceData oxygenTankInstanceData;
    [SerializeField] private ToolEnergySourceInstanceData toolEnergySourceInstanceData;


    /// <summary>
    /// Create a new item instance from an ItemData template.
    /// Automatically initializes instance data based on item type.
    /// </summary>
    public ItemInstance(ItemData itemDataTemplate)
    {
        if (itemDataTemplate == null)
        {
            Debug.LogError("Cannot create ItemInstance - ItemData template is null");
            return;
        }

        itemDataName = itemDataTemplate.name;
        _cachedItemData = itemDataTemplate;
        instanceID = GenerateUniqueInstanceID();

        InitializeInstanceData(itemDataTemplate);
    }

    /// <summary>
    /// Constructor for deserialization from save data.
    /// </summary>
    public ItemInstance(string dataName, string id)
    {
        itemDataName = dataName;
        instanceID = id;
        _cachedItemData = null; // Will be loaded on first access
    }

    #region Properties

    /// <summary>
    /// Gets the ItemData template (ScriptableObject) for this instance.
    /// Uses cache or loads from Resources if needed.
    /// </summary>
    public ItemData ItemData
    {
        get
        {
            if (_cachedItemData == null)
            {
                _cachedItemData = FindItemDataByName(itemDataName);
            }
            return _cachedItemData;
        }
    }

    /// <summary>
    /// Unique identifier for this specific item instance.
    /// </summary>
    public string InstanceID => instanceID;

    /// <summary>
    /// Name of the ItemData template this instance is based on.
    /// </summary>
    public string ItemDataName => itemDataName;

    #endregion

    #region Instance Data Access

    public ConsumableInstanceData ConsumableInstanceData => consumableInstanceData;
    public RangedWeaponInstanceData RangedWeaponInstanceData => rangedWeaponInstanceData;
    public AmmoInstanceData AmmoInstanceData => ammoInstanceData;
    public ClothingInstanceData ClothingInstanceData => clothingInstanceData;
    public MeleeWeaponInstanceData MeleeWeaponInstanceData => meleeWeaponInstanceData;
    public ToolInstanceData ToolInstanceData => toolInstanceData;
    public ThrowableInstanceData ThrowableInstanceData => throwableInstanceData;
    public BowInstanceData BowInstanceData => bowInstanceData;
    public KeyItemInstanceData KeyItemInstanceData => keyItemInstanceData;
    public OxygenTankInstanceData OxygenTankInstanceData => oxygenTankInstanceData;
    public ToolEnergySourceInstanceData ToolEnergySourceInstanceData => toolEnergySourceInstanceData;


    #endregion

    #region Initialization

    /// <summary>
    /// Initialize instance data based on the item type.
    /// Creates the appropriate instance data object with default values from template.
    /// </summary>
    private void InitializeInstanceData(ItemData template)
    {
        switch (template.itemType)
        {
            case ItemType.Consumable:
                consumableInstanceData = new ConsumableInstanceData(template.ConsumableData);
                break;

            case ItemType.RangedWeapon:
                rangedWeaponInstanceData = new RangedWeaponInstanceData(template.RangedWeaponData);
                break;

            case ItemType.Ammo:
                ammoInstanceData = new AmmoInstanceData(template.AmmoData);
                break;

            case ItemType.Clothing:
                clothingInstanceData = new ClothingInstanceData(template.ClothingData);
                break;

            case ItemType.MeleeWeapon:
                meleeWeaponInstanceData = new MeleeWeaponInstanceData(template.MeleeWeaponData);
                break;

            case ItemType.Tool:
                toolInstanceData = new ToolInstanceData(template.ToolData);
                break;

            case ItemType.Throwable:
                throwableInstanceData = new ThrowableInstanceData(template.ThrowableData);
                break;

            case ItemType.Bow:
                bowInstanceData = new BowInstanceData(template.BowData);
                break;

            case ItemType.KeyItem:
                keyItemInstanceData = new KeyItemInstanceData(template.KeyItemData);
                break;

            case ItemType.OxygenTank:
                oxygenTankInstanceData = new OxygenTankInstanceData(template.OxygenTankData);
                break;

            case ItemType.ToolEnergySource:
                toolEnergySourceInstanceData = new ToolEnergySourceInstanceData(template.ToolEnergySourceData);
                break;

            case ItemType.Unarmed:
                // Unarmed has no instance data
                break;

            default:
                Debug.LogWarning($"Unknown item type: {template.itemType}");
                break;
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Generate a unique instance ID using GUID.
    /// </summary>
    private string GenerateUniqueInstanceID()
    {
        return Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Find ItemData ScriptableObject by name using Resources system.
    /// </summary>
    private ItemData FindItemDataByName(string dataName)
    {
        // First try to load from Resources with the correct path
        ItemData itemData = Resources.Load<ItemData>(SaveManager.Instance.itemDataPath + dataName);
        if (itemData != null)
            return itemData;

        // Fallback: Search all loaded ItemData assets
        ItemData[] allItemData = Resources.FindObjectsOfTypeAll<ItemData>();
        foreach (var data in allItemData)
        {
            if (data.name == dataName)
                return data;
        }

        Debug.LogWarning($"ItemData '{dataName}' not found. Make sure it exists in Resources folder.");
        return null;
    }

    /// <summary>
    /// Create a copy of this item instance with a new unique ID.
    /// Useful for stacking, splitting, or duplicating items.
    /// </summary>
    public ItemInstance CreateCopy()
    {
        var copy = new ItemInstance(ItemData);

        // Copy instance data based on type
        if (consumableInstanceData != null)
            copy.consumableInstanceData = consumableInstanceData.CreateCopy();
        if (rangedWeaponInstanceData != null)
            copy.rangedWeaponInstanceData = rangedWeaponInstanceData.CreateCopy();
        if (ammoInstanceData != null)
            copy.ammoInstanceData = ammoInstanceData.CreateCopy();
        if (clothingInstanceData != null)
            copy.clothingInstanceData = clothingInstanceData.CreateCopy();
        if (meleeWeaponInstanceData != null)
            copy.meleeWeaponInstanceData = meleeWeaponInstanceData.CreateCopy();
        if (toolInstanceData != null)
            copy.toolInstanceData = toolInstanceData.CreateCopy();
        if (throwableInstanceData != null)
            copy.throwableInstanceData = throwableInstanceData.CreateCopy();
        if (bowInstanceData != null)
            copy.bowInstanceData = bowInstanceData.CreateCopy();
        if (keyItemInstanceData != null)
            copy.keyItemInstanceData = keyItemInstanceData.CreateCopy();
        if (oxygenTankInstanceData != null)
            copy.oxygenTankInstanceData = oxygenTankInstanceData.CreateCopy();
        if (toolEnergySourceInstanceData != null)
            copy.toolEnergySourceInstanceData = toolEnergySourceInstanceData.CreateCopy();

        return copy;
    }

    #endregion

    #region Debug

    public override string ToString()
    {
        return $"ItemInstance[{instanceID}] {itemDataName}";
    }

    #endregion
}