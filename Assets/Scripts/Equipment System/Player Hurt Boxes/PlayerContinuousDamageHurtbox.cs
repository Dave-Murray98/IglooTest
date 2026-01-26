using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hurtbox script for weapons that deal continuous damage over time.
/// Perfect for weapons like blowtorches, flamethrowers, lasers, etc.
/// Inherits from PlayerHurtbox to maintain compatibility with the base system.
/// </summary>
public class PlayerContinuousDamageHurtbox : PlayerHurtbox
{
    [Header("Continuous Damage Settings")]
    [Tooltip("How much damage to deal per second while enemies are in the hurtbox")]
    [SerializeField] private float damagePerSecond = 5f;

    [Tooltip("How often to apply damage (in seconds). Lower = more frequent damage ticks")]
    [SerializeField] private float damageTickRate = 0.5f;

    // Tracks all monsters currently inside the hurtbox
    private List<MonsterHitBox> monstersInRange = new List<MonsterHitBox>();

    // Tracks when we last dealt damage to each monster
    private Dictionary<MonsterHitBox, float> damageTimers = new Dictionary<MonsterHitBox, float>();

    /// <summary>
    /// Called every frame. We use this to apply continuous damage to all monsters in range.
    /// </summary>
    private void Update()
    {
        // If there are no monsters in range, no need to do anything
        if (monstersInRange.Count == 0)
            return;

        // Go through each monster currently in the hurtbox
        for (int i = monstersInRange.Count - 1; i >= 0; i--)
        {
            MonsterHitBox monster = monstersInRange[i];

            // Safety check: if the monster was destroyed, remove it from our list
            if (monster == null)
            {
                monstersInRange.RemoveAt(i);
                damageTimers.Remove(monster);
                continue;
            }

            // Update the damage timer for this monster
            damageTimers[monster] += Time.deltaTime;

            // Check if enough time has passed to deal damage again
            if (damageTimers[monster] >= damageTickRate)
            {
                // Deal damage based on how much time has passed
                float damageMultiplier = damageTimers[monster] / damageTickRate;
                float damageThisTick = (damagePerSecond * damageTickRate) * damageMultiplier;

                DealContinuousDamage(monster, damageThisTick);

                // Reset the timer for this monster
                damageTimers[monster] = 0f;
            }
        }
    }

    /// <summary>
    /// Overrides the base OnTriggerEnter to add monsters to our tracking list
    /// instead of dealing instant damage.
    /// </summary>
    protected override void OnTriggerEnter(Collider other)
    {
        MonsterHitBox monsterHitbox = other.GetComponent<MonsterHitBox>();

        if (monsterHitbox != null)
        {
            // Only add if not already in the list (prevents duplicates)
            if (!monstersInRange.Contains(monsterHitbox))
            {
                monstersInRange.Add(monsterHitbox);

                // Initialize the damage timer for this monster
                damageTimers[monsterHitbox] = damageTickRate; // Start at tick rate so damage happens immediately

                DebugLog($"Monster entered PlayerHurtBox range. Total monsters: {monstersInRange.Count}");
            }
        }
    }

    /// <summary>
    /// Called when something exits the hurtbox trigger.
    /// We remove the monster from our tracking list.
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        MonsterHitBox monsterHitbox = other.GetComponent<MonsterHitBox>();

        if (monsterHitbox != null)
        {
            // Remove the monster from tracking
            if (monstersInRange.Contains(monsterHitbox))
            {
                monstersInRange.Remove(monsterHitbox);
                damageTimers.Remove(monsterHitbox);


                DebugLog($"Monster exited PlayerHurtBox range. Total monsters: {monstersInRange.Count}");
            }
        }
    }

    /// <summary>
    /// Deals continuous damage to a monster. Separate from base DealDamage
    /// in case you want different effects for continuous vs instant damage.
    /// </summary>
    private void DealContinuousDamage(MonsterHitBox hitbox, float damage)
    {
        DebugLog($"Dealt {damage} damage to {hitbox.gameObject.name}");

        // get the hit point 
        Vector3 hitPoint = hitbox.hitBoxCollider.ClosestPoint(transform.position);

        hitbox.TakeDamage(damage, hitPoint);
    }

    /// <summary>
    /// Called when this object is disabled. Cleans up our tracking lists.
    /// This is important so we don't keep references to monsters when the weapon is put away.
    /// </summary>
    private void OnDisable()
    {
        // Clear all tracked monsters when the hurtbox is disabled
        monstersInRange.Clear();
        damageTimers.Clear();
    }

    /// <summary>
    /// Optional: Visualize the hurtbox area in the editor (only visible in Scene view)
    /// Helps with debugging and setting up your weapon hitboxes.
    /// </summary>
    private void OnDrawGizmos()
    {
        // Draw the hurtbox in red if monsters are inside, yellow if empty
        Gizmos.color = monstersInRange.Count > 0 ? Color.red : Color.yellow;

        // If there's a collider, draw its bounds
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }

    protected override void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log("[PlayerContinuousDamageHurtbox]" + message);
    }
}