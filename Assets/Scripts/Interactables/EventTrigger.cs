using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EventTrigger : MonoBehaviour
{
    [Header("Collision")]
    [SerializeField] protected Collider col;


    [Header("Visuals")]
    [SerializeField] protected GameObject incompleteVisual;
    [SerializeField] protected GameObject completeVisual;


    // -----------------------------------------------------------------------
    // Handler Speech
    // -----------------------------------------------------------------------
    [Header("Handler Speech (Optional)")]
    [Tooltip("Assign a HandlerSpeechData asset here if you want the handler (boss) " +
             "to speak when this trigger fires. Leave empty for silent triggers.")]
    [SerializeField] private HandlerSpeechData handlerSpeech;


    [Header("Debug")]
    [SerializeField] protected bool enableDebugLogs = false;

    protected virtual void Awake()
    {
        if (col == null)
            col = GetComponent<Collider>();

        SetVisuals(false);
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        PilotController pilot = other.GetComponent<PilotController>();
        if (pilot != null)
            TriggerEvent();
    }

    protected virtual void TriggerEvent()
    {
        col.enabled = false;
        SetVisuals(true);

        // If a handler speech has been assigned, play it through the intercom.
        // HandlerSpeechController handles interrupting any currently playing speech.
        PlayHandlerSpeechIfAssigned();
    }

    /// <summary>
    /// Plays the handler speech clip if one is assigned to this trigger.
    /// Safe to call even when handlerSpeech is null — it will simply do nothing.
    /// </summary>
    protected void PlayHandlerSpeechIfAssigned()
    {
        if (handlerSpeech == null)
            return;

        if (HandlerSpeechController.Instance == null)
        {
            Debug.LogWarning("[EventTrigger] HandlerSpeechData is assigned but no " +
                             "HandlerSpeechController exists in the scene. " +
                             "Add HandlerSpeechController to your intercom GameObject.");
            return;
        }

        DebugLog($"Triggering handler speech: '{handlerSpeech.speechLabel}'");
        HandlerSpeechController.Instance.Play(handlerSpeech);
    }

    protected virtual void SetVisuals(bool completed)
    {
        if (incompleteVisual != null)
            incompleteVisual.SetActive(!completed);

        if (completeVisual != null)
            completeVisual.SetActive(completed);

        DebugLog($"Set visuals: {(completed ? "Complete" : "Incomplete")}");
    }

    protected virtual void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log("[EventTrigger]" + " " + gameObject.name + " - " + message);
    }

    protected virtual void OnDrawGizmos()
    {
        if (col == null)
            return;

        Gizmos.color = Color.yellow * new Color(1, 1, 1, 0.5f);
        Gizmos.matrix = transform.localToWorldMatrix;

        if (col is BoxCollider box)
        {
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawSphere(sphere.center, sphere.radius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
        else if (col is CapsuleCollider capsule)
        {
            float radius = capsule.radius;
            float halfHeight = Mathf.Max(0f, capsule.height * 0.5f - radius);
            Vector3 top = capsule.center + Vector3.up * halfHeight;
            Vector3 bottom = capsule.center - Vector3.up * halfHeight;

            Gizmos.DrawSphere(top, radius);
            Gizmos.DrawSphere(bottom, radius);
            Gizmos.DrawCube(capsule.center, new Vector3(radius * 2f, capsule.height - radius * 2f, radius * 2f));

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(top, radius);
            Gizmos.DrawWireSphere(bottom, radius);
            Gizmos.DrawWireCube(capsule.center, new Vector3(radius * 2f, capsule.height - radius * 2f, radius * 2f));
        }
    }
}