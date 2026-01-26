using UnityEngine;
using Sirenix.OdinInspector;
using System;

/// <summary>
/// REFACTORED: RangedWeaponHandler implementation using enum-based animation system.
/// Primary action: Shoot (instant with auto-fire support)
/// Secondary action: Toggle ADS (instant)
/// Reload action: Manual reload (instant)
/// Melee: Available through unified system
/// UPDATED: Full ammo system integration with inventory ammo consumption and projectile spawning
/// </summary>
public class RangedWeaponHandler : BaseEquippedItemHandler
{
    [Header("Weapon Audio")]
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioClip emptyFireSound;
    [SerializeField] private AudioClip reloadSound;

    [Header("Auto Fire Settings")]
    [SerializeField] private bool enableAutoFire = true;
    [SerializeField] private float autoFireRate = 10f; // shots per second

    [Header("Weapon State")]
    [SerializeField, ReadOnly] private bool isReloading = false;
    [SerializeField, ReadOnly] private bool isAiming = false;
    [SerializeField, ReadOnly] private bool isAutoFiring = false;
    [SerializeField, ReadOnly] private float lastShotTime = 0f;

    [Header("Ammo Display")]
    [SerializeField, ReadOnly] private int clipAmmo = 0;
    [SerializeField, ReadOnly] private int inventoryAmmo = 0;

    [Header("ADS System")]
    [SerializeField] private ADSController adsController;

    [Header("Hit Detection")]
    [SerializeField] private LayerMask targetLayers = -1;

    // Components
    private AudioSource audioSource;
    private Camera playerCamera;

    // Quick access to weapon instance data (not template!)
    private RangedWeaponInstanceData WeaponInstanceData =>
        currentItemInstance?.RangedWeaponInstanceData;

    // Template data (read-only stats like damage, range)
    private RangedWeaponData WeaponData => currentItemData?.RangedWeaponData;

    // Events
    public System.Action<ItemData> OnWeaponEquipped;
    public System.Action OnWeaponUnequipped;
    public System.Action OnWeaponFired;
    public System.Action OnWeaponReloaded;
    public System.Action<int, int, int> OnAmmoChanged; // clipAmmo, inventoryAmmo, maxClip

    public override ItemType HandledItemType => ItemType.RangedWeapon;

    #region Initialization

    protected override void Awake()
    {
        base.Awake();
        SetupComponents();
    }

    private void SetupComponents()
    {
        // Audio setup
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f;
            audioSource.playOnAwake = false;
        }

        // Camera reference
        playerCamera = Camera.main ?? FindFirstObjectByType<Camera>();

        // ADS controller
        if (adsController == null)
            adsController = GetComponent<ADSController>() ?? FindFirstObjectByType<ADSController>();
    }

    #endregion

    #region Base Handler Implementation

    protected override void OnItemEquippedInternal(ItemData itemData)
    {
        if (itemData?.itemType != ItemType.RangedWeapon)
        {
            Debug.LogError($"RangedWeaponHandler received invalid item: {itemData?.itemName ?? "null"}");
            return;
        }

        // Reset weapon state
        isReloading = false;
        isAiming = false;
        isAutoFiring = false;
        lastShotTime = 0f;

        // Verify instance data is available
        if (!HasValidInstance())
        {
            Debug.LogWarning($"RangedWeaponHandler equipped {itemData.itemName} but has no valid ItemInstance!");
        }

        // Update ammo display
        RefreshAmmoDisplay();

        UpdateFXProjectileSpawnPointPos(itemData.RangedWeaponData.projectileSpawnPoint, itemData.RangedWeaponData.projectileSpawnRotation);

        DebugLog($"Equipped weapon: {itemData.itemName} - Clip: {clipAmmo}/{GetMaxClip()}, Inventory: {inventoryAmmo}");

        OnWeaponEquipped?.Invoke(itemData);
        OnAmmoChanged?.Invoke(clipAmmo, inventoryAmmo, GetMaxClip());
    }

    protected override void OnItemUnequippedInternal()
    {
        base.OnItemUnequippedInternal();

        // Clean up weapon state
        if (isAiming) StopAiming();
        StopFiring();
        isReloading = false;

        DebugLog("Unequipped weapon");
        OnWeaponUnequipped?.Invoke();
    }

    protected override void HandlePrimaryActionInternal(InputContext context)
    {
        // Primary = Shoot
        if (context.isPressed)
        {
            StartFiring();
        }
        else if (context.isReleased)
        {
            StopFiring();
        }
        // Auto-fire continuation handled in Update
    }

    protected override void HandleSecondaryActionInternal(InputContext context)
    {
        // Secondary = Toggle ADS
        if (context.isPressed)
        {
            if (isAiming) StopAiming();
            else StartAiming();
        }
    }

    protected override void HandleReloadActionInternal(InputContext context)
    {
        // Manual reload
        if (context.isPressed)
        {
            StartReload();
        }
    }

    protected override bool CanPerformActionInternal(PlayerAnimationType actionType, PlayerStateType playerState)
    {
        switch (actionType)
        {
            case PlayerAnimationType.PrimaryAction:
                return CanShoot(playerState);

            case PlayerAnimationType.SecondaryAction:
                return CanAim();

            case PlayerAnimationType.ReloadAction:
                return CanReload();

            case PlayerAnimationType.MeleeAction:
            case >= PlayerAnimationType.HeldMeleeActionStart and <= PlayerAnimationType.HeldMeleeActionCancel:
                return currentActionState == ActionState.None && !isAutoFiring;

            default:
                return false;
        }
    }

    protected override void UpdateHandlerInternal(float deltaTime)
    {
        //need to come up with a proper auto fire system
        // // Handle auto-fire
        // if (isAutoFiring && CanShoot(GetCurrentPlayerState()))
        // {
        //     HandleAutoFire();
        // }
    }

    #endregion

    #region Unified System Configuration

    /// <summary>
    /// Primary action is instant (not held) for ranged weapons
    /// </summary>
    protected override bool ShouldPrimaryActionBeHeld(InputContext context) => false;

    /// <summary>
    /// Secondary action is instant (ADS toggle)
    /// </summary>
    protected override bool ShouldSecondaryActionBeHeld(InputContext context) => false;

    #endregion

    #region Shooting System

    /// <summary>
    /// Start firing process
    /// </summary>
    private void StartFiring()
    {
        if (!CanShoot(GetCurrentPlayerState()))
        {
            HandleEmptyFire();
            return;
        }

        // Fire first shot immediately
        FireSingleShot();

        //need to come up with a proper auto fire system
        // // Start auto-fire if enabled
        // if (enableAutoFire && GetFireRate() > 1f)
        // {
        //     isAutoFiring = true;
        // }
    }

    /// <summary>
    /// Stop firing process
    /// </summary>
    private void StopFiring()
    {
        isAutoFiring = false;
        DebugLog("Stopped firing");
    }

    //need to come up with a proper auto fire system
    // /// <summary>
    // /// Handle auto-fire timing
    // /// </summary>
    // private void HandleAutoFire()
    // {
    //     if (!InputManager.Instance?.PrimaryActionHeld == true)
    //     {
    //         StopFiring();
    //         return;
    //     }

    //     float fireInterval = 1f / GetFireRate();
    //     if (Time.time - lastShotTime >= fireInterval)
    //     {
    //         FireSingleShot();
    //     }
    // }

    /// <summary>
    /// Fire a single shot
    /// </summary>
    private void FireSingleShot()
    {
        if (!CanShoot(GetCurrentPlayerState()) || currentActionState != ActionState.None)
        {
            HandleEmptyFire();
            return;
        }

        lastShotTime = Time.time;
        DebugLog($"Firing shot - Clip: {GetClipAmmo()}/{GetMaxClip()}");

        // Trigger shoot animation
        TriggerInstantAction(PlayerAnimationType.PrimaryAction);
        ExecuteWeaponFire();
    }

    /// <summary>
    /// Execute weapon fire effects (called by animation completion)
    /// UPDATED: Now spawns projectiles from pool
    /// </summary>
    protected override void OnActionCompletedInternal(PlayerAnimationType actionType)
    {
        base.OnActionCompletedInternal(actionType);

        switch (actionType)
        {
            case PlayerAnimationType.PrimaryAction:
                DebugLog("Shoot animation completed");
                break;

            case PlayerAnimationType.ReloadAction:
                CompleteReload();
                break;
        }
    }

    /// <summary>
    /// Execute weapon fire effects
    /// UPDATED: Now spawns projectiles from pool AFTER Final IK updates
    /// </summary>
    private void ExecuteWeaponFire()
    {
        // Consume ammo from CLIP (not inventory!)
        if (WeaponInstanceData == null || !WeaponInstanceData.TryFire())
        {
            DebugLog("Failed to consume clip ammo");
            HandleEmptyFire();
            return;
        }

        // Play sound
        AudioClip randomShootClip = WeaponData.shootSounds[UnityEngine.Random.Range(0, WeaponData.shootSounds.Length)];
        if (randomShootClip != null)
            AudioManager.Instance.PlaySound2D(randomShootClip, AudioCategory.PlayerSFX);


        // CRITICAL FIX: Spawn projectile in next frame after IK updates (as the bullet spawn pos will have been moved by the IK calculation)
        StartCoroutine(SpawnProjectileAfterIK());

        NoisePool.CreateNoise(transform.position, currentItemData.effectNoiseVolume);

        // Play shoot sound
        // PlaySound(shootSound);

        // Update display
        RefreshAmmoDisplay();

        // Fire events
        OnWeaponFired?.Invoke();
        OnAmmoChanged?.Invoke(clipAmmo, inventoryAmmo, GetMaxClip());

        DebugLog($"Weapon fired - Clip: {clipAmmo}/{GetMaxClip()}, Inventory: {inventoryAmmo}");

        // Auto-reload if clip empty
        if (GetClipAmmo() <= 0)
        {
            StopFiring();
            Invoke(nameof(AutoReload), 0.2f);
        }
    }

    /// <summary>
    /// NEW: Spawn projectile after waiting for Final IK to complete (as the bullet spawn pos will have been updated by the IK)
    /// </summary>
    private System.Collections.IEnumerator SpawnProjectileAfterIK()
    {
        // Wait for end of frame (after all IK updates)
        yield return new WaitForEndOfFrame();

        // NOW cache the spawn point position after IK has finished
        SpawnProjectile();

        // Underwater bubble effect
        if (GameManager.Instance.PlayerStateManager.playerController.waterDetector.IsHeadUnderwater)
        {
            ParticleFXPool.CreateBubbleFX(
                fxProjectileSpawnPoint.position,
                Quaternion.identity
            );
        }
    }

    /// <summary>
    /// Spawn projectile from bullet pool and apply force
    /// </summary>
    private void SpawnProjectile()
    {
        if (WeaponData == null || WeaponData.requiredAmmoType == null)
        {
            Debug.LogWarning("[RangedWeaponHandler] Cannot spawn projectile - missing weapon or ammo data");
            return;
        }

        if (PlayerBulletPool.Instance == null)
        {
            Debug.LogError("[RangedWeaponHandler] PlayerBulletPool.Instance is null!");
            return;
        }

        if (fxProjectileSpawnPoint == null)
        {
            Debug.LogWarning("[RangedWeaponHandler] No projectile spawn point assigned!");
            return;
        }

        // Cache spawn transform
        Vector3 spawnPosition = fxProjectileSpawnPoint.position;
        Quaternion spawnRotation = fxProjectileSpawnPoint.rotation;

        DebugLog($"Requesting projectile spawn at Position: {spawnPosition}, Rotation: {spawnRotation.eulerAngles}");

        // Get projectile from pool (already positioned)
        PlayerProjectile projectile = PlayerBulletPool.Instance.GetProjectile(
            WeaponData.requiredAmmoType,
            spawnPosition,
            spawnRotation
        );

        if (projectile == null)
        {
            Debug.LogError("[RangedWeaponHandler] Failed to get projectile from pool");
            return;
        }

        // Calculate damage
        float baseDamage = GetDamage();
        float damageModifier = WeaponData.requiredAmmoType.AmmoData?.damageModifier ?? 1f;
        float finalDamage = baseDamage * damageModifier;

        // Initialize projectile (activates it)
        projectile.Initialize(finalDamage, WeaponData.requiredAmmoType, spawnPosition, spawnRotation);

        // Verify final position
        DebugLog($"Projectile spawned - Requested: {spawnPosition}, Actual: {projectile.transform.position}, Delta: {projectile.transform.position - spawnPosition}");

        // Apply force using projectile's forward direction
        Rigidbody projectileRb = projectile.GetComponent<Rigidbody>();
        if (projectileRb != null)
        {
            Vector3 forceDirection = projectile.transform.forward;
            float force = WeaponData.projectileForce;

            projectileRb.AddForce(forceDirection * force, ForceMode.Impulse);

            DebugLog($"Applied force: {force} in direction {forceDirection}");
        }

    }

    /// <summary>
    /// Handle empty fire attempt
    /// </summary>
    private void HandleEmptyFire()
    {
        DebugLog("Empty fire - no ammo or cannot shoot");
        //PlaySound(emptyFireSound);

        // Auto-reload if clip empty
        if (GetClipAmmo() <= 0 && CanReload())
        {
            DebugLog("Auto-reloading empty weapon");
            Invoke(nameof(AutoReload), 0.1f);
        }
    }

    #endregion

    #region Reload System

    /// <summary>
    /// Start reload process
    /// UPDATED: Now pulls ammo from inventory
    /// </summary>
    private void StartReload()
    {
        if (!CanReload())
        {
            DebugLog("Cannot reload weapon");
            return;
        }

        // Stop auto-fire during reload
        StopFiring();

        isReloading = true;
        DebugLog($"Starting reload - Clip: {GetClipAmmo()}/{GetMaxClip()}, Inventory: {GetInventoryAmmo()}");

        //PlaySound(reloadSound); //we're gonna make this animation event based and have the method in PlayerAudio
        TriggerInstantAction(PlayerAnimationType.ReloadAction);
    }

    /// <summary>
    /// Auto-reload after emptying weapon
    /// </summary>
    private void AutoReload()
    {
        if (currentActionState == ActionState.None && !isReloading && GetClipAmmo() <= 0)
        {
            DebugLog("Starting auto-reload");
            StartReload();
        }
    }

    /// <summary>
    /// Complete reload process
    /// UPDATED: Loads ammo from inventory into clip
    /// </summary>
    private void CompleteReload()
    {
        if (!isReloading) return;

        if (WeaponInstanceData == null || WeaponData == null)
        {
            DebugLog("Cannot complete reload - missing weapon data");
            isReloading = false;
            return;
        }

        // Calculate how much ammo we need
        int maxClip = WeaponData.maxAmmo;
        int currentClip = WeaponInstanceData.currentAmmoInClip;
        int ammoNeeded = maxClip - currentClip;

        if (ammoNeeded <= 0)
        {
            DebugLog("Clip already full");
            isReloading = false;
            return;
        }

        // Get required ammo type
        ItemData requiredAmmo = WeaponData.requiredAmmoType;
        if (requiredAmmo == null)
        {
            Debug.LogWarning($"[RangedWeaponHandler] Weapon {currentItemData.itemName} has no required ammo type!");
            isReloading = false;
            return;
        }

        // Pull ammo from inventory
        int ammoLoaded = AmmoInventoryHelper.ReloadFromInventory(requiredAmmo, ammoNeeded);

        if (ammoLoaded > 0)
        {
            // Load ammo into clip
            WeaponInstanceData.currentAmmoInClip += ammoLoaded;
            DebugLog($"Loaded {ammoLoaded} rounds into clip");
        }
        else
        {
            DebugLog("No ammo available in inventory for reload");
        }

        // Update display
        RefreshAmmoDisplay();

        isReloading = false;
        DebugLog($"Reload complete - Clip: {clipAmmo}/{GetMaxClip()}, Inventory: {inventoryAmmo}");

        OnWeaponReloaded?.Invoke();
        OnAmmoChanged?.Invoke(clipAmmo, inventoryAmmo, GetMaxClip());
    }

    #endregion

    #region ADS System

    /// <summary>
    /// Start aiming down sights
    /// </summary>
    private void StartAiming()
    {
        if (!CanAim()) return;

        isAiming = true;
        DebugLog("Started aiming down sights");

        if (adsController != null)
            adsController.StartAimingDownSights();
    }

    /// <summary>
    /// Stop aiming down sights
    /// </summary>
    private void StopAiming()
    {
        if (!isAiming) return;

        isAiming = false;
        DebugLog("Stopped aiming down sights");

        if (adsController != null)
            adsController.StopAimingDownSights();
    }

    #endregion

    #region Ammo Display & Tracking

    /// <summary>
    /// Refresh ammo display values from instance data and inventory
    /// </summary>
    private void RefreshAmmoDisplay()
    {
        clipAmmo = GetClipAmmo();
        inventoryAmmo = GetInventoryAmmo();
    }

    /// <summary>
    /// Get current clip ammo from instance data
    /// </summary>
    private int GetClipAmmo()
    {
        return WeaponInstanceData?.currentAmmoInClip ?? 0;
    }

    /// <summary>
    /// Get total ammo available in inventory
    /// </summary>
    private int GetInventoryAmmo()
    {
        if (WeaponData?.requiredAmmoType == null)
            return 0;

        return AmmoInventoryHelper.GetTotalAmmoCount(WeaponData.requiredAmmoType);
    }

    #endregion

    #region State Validation

    /// <summary>
    /// Check if weapon can shoot
    /// UPDATED: Checks clip ammo instead of instance ammo
    /// </summary>
    private bool CanShoot(PlayerStateType playerState)
    {
        if (WeaponData == null) return false;
        if (GetClipAmmo() <= 0) return false;
        if (isReloading) return false;
        if (!currentItemData.CanUseInState(playerState)) return false;
        return true;
    }

    /// <summary>
    /// Check if can aim
    /// </summary>
    private bool CanAim()
    {
        if (WeaponData == null) return false;
        if (isReloading) return false;
        return IsPlayerInValidState();
    }

    /// <summary>
    /// Check if can reload
    /// UPDATED: Checks if ammo available in inventory
    /// </summary>
    private bool CanReload()
    {
        if (WeaponData == null) return false;
        if (isReloading) return false;
        if (currentActionState != ActionState.None) return false;
        if (GetClipAmmo() >= GetMaxClip()) return false;

        // Check if ammo available in inventory
        if (GetInventoryAmmo() <= 0)
        {
            DebugLog("Cannot reload - no ammo in inventory");
            return false;
        }

        return true;
    }

    #endregion

    #region Weapon Data Access

    /// <summary>Get current weapon damage (from template)</summary>
    public float GetDamage() => WeaponData?.damage ?? 10f;

    /// <summary>Get current weapon range (from template)</summary>
    public float GetRange() => WeaponData?.range ?? 100f;

    /// <summary>Get current weapon fire rate (from template)</summary>

    /// <summary>Get maximum clip capacity (from template)</summary>
    public int GetMaxClip() => WeaponData?.maxAmmo ?? 30;

    #endregion

    #region Public API

    /// <summary>Check if currently firing</summary>
    public bool IsFiring() => currentActionState == ActionState.Instant &&
                              currentActionAnimation == PlayerAnimationType.PrimaryAction;

    /// <summary>Check if auto-firing</summary>
    public bool IsAutoFiring() => isAutoFiring;

    /// <summary>Check if currently reloading</summary>
    public bool IsReloading() => isReloading;

    /// <summary>Check if currently aiming</summary>
    public bool IsAiming() => isAiming;

    /// <summary>Check if weapon has ammo in clip</summary>
    public bool HasClipAmmo() => GetClipAmmo() > 0;

    /// <summary>Check if weapon has ammo available (clip or inventory)</summary>
    public bool HasAmmo() => HasClipAmmo() || GetInventoryAmmo() > 0;

    /// <summary>Get ammo status string for UI</summary>
    public string GetAmmoStatus() => $"{clipAmmo}/{GetMaxClip()} ({inventoryAmmo})";

    /// <summary>Force stop all weapon actions</summary>
    public void ForceStopAllActions()
    {
        if (isAiming) StopAiming();
        StopFiring();
        if (currentActionState != ActionState.None) ForceCompleteAction();
        isReloading = false;
        DebugLog("Force stopped all weapon actions");
    }

    /// <summary>Debug info</summary>
    public override string GetDebugInfo()
    {
        return $"{GetType().Name} - Active: {isActive}, " +
               $"Item: {currentItemData?.itemName ?? "None"}, " +
               $"Instance Valid: {hasValidInstance}, " +
               $"Action State: {currentActionState} ({currentActionAnimation}), " +
               $"Clip: {clipAmmo}/{GetMaxClip()}, Inventory: {inventoryAmmo}, " +
               $"Auto-Fire: {isAutoFiring}, Reloading: {isReloading}, Aiming: {isAiming}";
    }

    private void OnDrawGizmos()
    {
        if (fxProjectileSpawnPoint != null && Application.isPlaying)
        {
            // Draw spawn point
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(fxProjectileSpawnPoint.position, 0.1f);

            // Draw forward direction
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(fxProjectileSpawnPoint.position, fxProjectileSpawnPoint.forward * 2f);

            // Draw coordinate axes
            Gizmos.color = Color.red;
            Gizmos.DrawRay(fxProjectileSpawnPoint.position, fxProjectileSpawnPoint.right * 0.5f);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(fxProjectileSpawnPoint.position, fxProjectileSpawnPoint.up * 0.5f);
        }
    }

    #endregion
}