using UnityEngine;

namespace Kellojo.StylizedKelp
{
    public class CollisionRotator : MonoBehaviour
    {
        
        public Transform[] objects;
        public float radius = 5f;
        public float speed = 30f;
        private float[] angles;

        void OnDrawGizmosSelected() {
            
            const int segments = 64;
            Vector3 prevPoint = transform.position + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++) {
                float angle = i * (360f / segments) * Mathf.Deg2Rad;
                Vector3 nextPoint = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                Gizmos.DrawLine(prevPoint, nextPoint);
                prevPoint = nextPoint;
            }
            
            Gizmos.DrawSphere(transform.position, 0.1f);
        }
        
        void Start() {
            angles = new float[objects.Length];
            for (int i = 0; i < objects.Length; i++) {
                angles[i] = i * (360f / objects.Length);
            }
        }

        void Update() {
            for (int i = 0; i < objects.Length; i++) {
                // Update angle
                angles[i] += speed * Time.deltaTime;

                // Convert angle to radians
                float rad = angles[i] * Mathf.Deg2Rad;

                // Compute new position (XZ plane)
                Vector3 offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius;
                objects[i].position = transform.position + offset;
            }
        }


    }
    
}
