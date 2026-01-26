using Opsive.BehaviorDesigner.Runtime.Tasks;

/// <summary>
/// Checks if the monster has reached its destination.
/// 
/// IMPORTANT: This conditional uses an event-driven approach.
/// It subscribes to the movement system's OnDestinationReached event.
/// This means it doesn't check every frame - it waits for the movement system to notify it.
/// 
/// This is more efficient and prevents the behavior tree from constantly
/// interrupting the movement system.
/// </summary>
public class HasReachedDestination : EnemyConditional
{
    private bool hasReached = false;

    public override void OnAwake()
    {
        base.OnAwake();

        // Subscribe to the movement system's event
        if (controller.movement != null)
        {
            controller.movement.OnDestinationReached += OnDestinationReached;
        }
    }

    public override void OnStart()
    {
        base.OnStart();
        // Reset the flag when this node starts
        hasReached = false;
    }

    public override TaskStatus OnUpdate()
    {
        // Simply check the flag that gets set by the event
        return hasReached ? TaskStatus.Success : TaskStatus.Failure;
    }

    /// <summary>
    /// Called by the movement system when destination is reached.
    /// </summary>
    private void OnDestinationReached()
    {
        hasReached = true;
    }

    public override void OnEnd()
    {
        base.OnEnd();
        // Clean up
        hasReached = false;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        // Unsubscribe from events
        if (controller != null && controller.movement != null)
        {
            controller.movement.OnDestinationReached -= OnDestinationReached;
        }
    }
}