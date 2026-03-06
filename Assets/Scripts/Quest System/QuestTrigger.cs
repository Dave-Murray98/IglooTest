using Sirenix.OdinInspector;
using Unity.Profiling;
using UnityEngine;

public class QuestTrigger : EventTrigger
{
    [Header("QuestData")]
    public QuestData questData;

    //called by oncollision enter
    protected override void TriggerEvent()
    {
        CompleteQuest();
    }

    [Button]
    protected virtual void CompleteQuest()
    {

        if (questData.AreRequirementsMet())
        {
            DebugLog($"Quest completed: {questData.questID}");

            QuestManager.Instance.CompleteQuest(questData.questID);
            col.enabled = false;
            SetVisuals(true);
            PlayHandlerSpeechIfAssigned();
        }
        else
        {
            DebugLog($"Requirements not met for quest: {questData.questID}");
        }
    }

    protected override void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log("[QuestTrigger " + questData.questID + "] " + message);
    }

    protected override void OnDrawGizmos()
    {
        if (col == null)
            return;

        Gizmos.color = new Color(0f, 1f, 0.5f, 0.25f); // light green
        Gizmos.matrix = transform.localToWorldMatrix;

        if (col is BoxCollider box)
        {
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0f, 1f, 0.5f, 1f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawSphere(sphere.center, sphere.radius);
            Gizmos.color = new Color(0f, 1f, 0.5f, 1f);
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
        else if (col is CapsuleCollider capsule)
        {
            // Unity has no built-in DrawWireCapsule, so we approximate
            // it with two spheres at each end and a wire cube for the body.
            float radius = capsule.radius;
            float halfHeight = Mathf.Max(0f, capsule.height * 0.5f - radius);
            Vector3 top = capsule.center + Vector3.up * halfHeight;
            Vector3 bottom = capsule.center - Vector3.up * halfHeight;

            Gizmos.DrawSphere(top, radius);
            Gizmos.DrawSphere(bottom, radius);
            Gizmos.DrawCube(capsule.center, new Vector3(radius * 2f, capsule.height - radius * 2f, radius * 2f));

            Gizmos.color = new Color(0f, 1f, 0.5f, 1f);
            Gizmos.DrawWireSphere(top, radius);
            Gizmos.DrawWireSphere(bottom, radius);
            Gizmos.DrawWireCube(capsule.center, new Vector3(radius * 2f, capsule.height - radius * 2f, radius * 2f));
        }
    }
}