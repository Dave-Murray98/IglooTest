using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;

public class NPCConditional : Conditional
{
    protected NPC nPC;

    public override void OnAwake()
    {
        base.OnAwake();
        nPC = gameObject.GetComponentInParent<NPC>();
    }
}
