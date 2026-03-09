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
            caveMonster.playerHealth = submarineHealthManager;
            caveMonster.Bite();
        }
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        SubmarineHealthManager submarineHealthManager = other.GetComponent<SubmarineHealthManager>();
        if (submarineHealthManager != null)
        {
            caveMonster.playerHealth = null;
            DebugLog("Player exited Cave Monster attack range!");
        }
    }

    protected override void DebugLog(string message)
    {
        Debug.Log("[CaveMonsterHurtBox]" + message);
    }

}