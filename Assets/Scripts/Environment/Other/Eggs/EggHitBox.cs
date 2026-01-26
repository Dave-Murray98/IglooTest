using UnityEngine;

public class EggHitBox : MonsterHitBox
{
    [SerializeField] private Egg egg;

    public override void TakeDamage(float damage, Vector3 hitPoint)
    {
        if (egg == null) return;

        egg.BreakEgg();
    }

    public override void StunMonster(float stunDuration)
    {
        DebugLog("Stun called and will break egg");

        egg.BreakEgg();
    }

    protected override void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[EggHitBox] {message}");
    }

}
