using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class Laser : MonoBehaviour
{
    [Header("Laser Settings")]
    public float maxDistance = 100f;       // How far the laser can travel if it hits nothing
    public LayerMask collisionMask;        // Which objects the laser can hit

    private LineRenderer lineRenderer;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 2;   // A laser is just a line with 2 points: start and end
    }

    void Update()
    {
        FireLaser();
    }

    void FireLaser()
    {
        Vector3 startPoint = transform.position;
        Vector3 direction = transform.forward; // Shoots in the direction this object is facing

        // Cast an invisible ray forward and check if it hits anything
        if (Physics.Raycast(startPoint, direction, out RaycastHit hit, maxDistance, collisionMask))
        {
            // If it hits something, the laser ends at the hit point
            lineRenderer.SetPosition(0, startPoint);
            lineRenderer.SetPosition(1, hit.point);
        }
        else
        {
            // If it hits nothing, the laser travels its full max distance
            lineRenderer.SetPosition(0, startPoint);
            lineRenderer.SetPosition(1, startPoint + direction * maxDistance);
        }
    }
}