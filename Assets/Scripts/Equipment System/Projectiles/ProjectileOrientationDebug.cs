using UnityEngine;

/// <summary>
/// DIAGNOSTIC HELPER - Add this to your projectile prefab temporarily
/// to visualize its forward direction in the scene view.
/// This helps identify if the projectile mesh is oriented wrong.
/// </summary>
public class ProjectileOrientationDebug : MonoBehaviour
{
    [Header("Visualization")]
    [SerializeField] private float arrowLength = 2f;
    [SerializeField] private Color forwardColor = Color.blue;
    [SerializeField] private Color upColor = Color.green;
    [SerializeField] private Color rightColor = Color.red;

    private void OnDrawGizmos()
    {
        // Draw forward direction (should point where projectile travels)
        Gizmos.color = forwardColor;
        Gizmos.DrawRay(transform.position, transform.forward * arrowLength);

        // Draw up direction
        Gizmos.color = upColor;
        Gizmos.DrawRay(transform.position, transform.up * arrowLength);

        // Draw right direction
        Gizmos.color = rightColor;
        Gizmos.DrawRay(transform.position, transform.right * arrowLength);

        // Draw sphere at origin
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.1f);
    }

    private void OnDrawGizmosSelected()
    {
        // Draw larger arrows when selected
        Gizmos.color = forwardColor;
        Gizmos.DrawRay(transform.position, transform.forward * arrowLength * 1.5f);
        DrawArrowHead(transform.position + transform.forward * arrowLength * 1.5f, transform.forward, 0.3f);
    }

    private void DrawArrowHead(Vector3 pos, Vector3 direction, float size)
    {
        Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + 20, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - 20, 0) * Vector3.forward;

        Gizmos.DrawRay(pos, right * size);
        Gizmos.DrawRay(pos, left * size);
    }
}