using UnityEngine;

public class PlayerTool : MonoBehaviour
{
    [SerializeField] protected bool enableDebugLogs = false;

    public float activeEffectNoiseVolume = 10f;

    /// <summary>
    /// Called when loop action is started (not the loop start animation, but the actual loop animation itself)
    /// Or when the instant effect is started
    /// </summary>
    public virtual void TriggerToolEffect() { }

    /// <summary>
    /// Called when loop action is stopped (not the loop end animation, but the actual loop animation itself)
    /// Or when the instant effect is ended
    /// </summary>
    public virtual void StopToolEffect() { }

    protected virtual void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PlayerTool] {message}");
    }
}
