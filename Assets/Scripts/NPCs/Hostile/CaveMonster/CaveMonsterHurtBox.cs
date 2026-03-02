using UnityEngine;

public class CaveMonsterHurtBox : NPCHurtBox
{

    public CaveMonster caveMonster;

    private void Awake()
    {
        if (caveMonster == null)
        {
            caveMonster = GetComponentInParent<CaveMonster>();
        }
    }

    protected override void OnTriggerEnter(Collider other)
    {
        SubmarineHealthManager submarineHealthManager = other.GetComponent<SubmarineHealthManager>();
        if (submarineHealthManager != null)
        {
            DebugLog("Player entered Cave Monster attack range!");
            caveMonster.Bite(submarineHealthManager);
        }
    }

    protected override void DebugLog(string message)
    {
        Debug.Log("[CaveMonsterHurtBox]" + message);
    }

}