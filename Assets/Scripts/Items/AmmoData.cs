using Sirenix.OdinInspector;
using UnityEngine;

[System.Serializable]
public class AmmoData
{
    [Header("Ammo Properties")]
    [Tooltip("Weapons that can use this ammo type")]
    public ItemData[] compatibleWeapons;

    [Tooltip("Projectile prefab for this ammo type")]
    public GameObject projectilePrefab;

    [Tooltip("Damage modifier for this ammo type")]
    public float damageModifier = 1f;

    [Header("Ammo Stack Configuration")]
    [Tooltip("Maximum ammo count per stack/item instance")]
    [Range(1, 1000)]
    public int maxAmmoPerStack = 50;
}