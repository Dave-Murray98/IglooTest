using UnityEngine;

public class UploadTerminalHitbox : MonsterHitBox
{
    [SerializeField] private CompleteQuestInteractable uploadTerminalQuestInteractable;

    [SerializeField] private QuestData destroyUploadTerminalQuestData;

    public override void TakeDamage(float damage, Vector3 hitPoint)
    {
        if (uploadTerminalQuestInteractable == null) return;

        uploadTerminalQuestInteractable.gameObject.SetActive(false);

        ParticleFXPool.Instance.GetImpactFX(hitPoint, Quaternion.identity);

        QuestManager.Instance.CompleteQuest(destroyUploadTerminalQuestData.questID);

    }

    public override void StunMonster(float stunDuration)
    {
        DebugLog("Stun called on Upload Terminal Hitbox");

    }

    protected override void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[UploadTerminalHitbox] {message}");
    }
}
