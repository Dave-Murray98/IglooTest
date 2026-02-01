using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;

public class NPCAction : Action
{
    protected NPC nPC;

    public override void OnAwake()
    {
        base.OnAwake();
        nPC = gameObject.GetComponentInParent<NPC>();
    }
}

