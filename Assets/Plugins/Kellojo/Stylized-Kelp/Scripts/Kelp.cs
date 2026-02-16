using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Kellojo.StylizedKelp {
    
    [ExecuteInEditMode]
    public class Kelp : MonoBehaviour {

        public Vector2Int LengthRange = new Vector2Int(5, 15);
        public int length = -1;
        public KelpSettings KelpSettings;
        public StylizedKelpRenderer KelpRenderer;
        private KelpInstance kelpInstance;
        private Vector3 lastPosition;
    
        private void Awake() {
            if (Application.isPlaying) InitializeKelp();
        }
        private void Start() {
            // Allow the editor to initialize the kelp in edit mode
            if (!Application.isPlaying) InitializeKelp();
        }

        void InitializeKelp() {
            if (KelpSettings == null) {
                Debug.LogError("KelpSettings is not assigned. Please make sure you assign them in the inspector", this);
                return;
            }
            
            if (KelpRenderer == null) KelpRenderer = FindAnyObjectByType<StylizedKelpRenderer>(FindObjectsInactive.Exclude);
            if (KelpRenderer == null) {
                if (Application.isPlaying) Debug.LogError("No active StylizedKelpRenderer system found in the scene. Please add one to your scene.", this);
                return;
            }
            
            if (length < 0) length = GetRandomLength();
            var newInstance = KelpRenderer.AddKelp(transform.position, length, KelpSettings);
            lastPosition = transform.position;
            
            if (newInstance != null) {
                kelpInstance = newInstance;
            }
        }
        public void ReinitializeKelp() {
            InitializeKelp();
        }

        private void OnEnable() {
            if (Application.isPlaying) return;
            if (KelpRenderer != null) KelpRenderer.Reinitialize();
        }
        private void OnDisable() {
            if (Application.isPlaying) return;
            if (KelpRenderer != null) KelpRenderer.Reinitialize();
        }

        private void OnValidate() {
            if (LengthRange.x < 1) LengthRange.x = 1;
            if (LengthRange.y < LengthRange.x) LengthRange.y = LengthRange.x;
        }

        private void OnDrawGizmos() {
            if (Application.isPlaying) return;
            
            var length = 1f;
            if (KelpSettings != null) length = KelpSettings.StemSegmentLength;
            
            var color = enabled ? Color.green : Color.gray;
            color.a = 0.1f;
            Gizmos.color = color;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * LengthRange.y * length);

            color.a = 0.5f;
            Gizmos.color = color;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * LengthRange.x * length);
            Gizmos.DrawSphere(transform.position, 0.25f);
        }
        
        #if UNITY_EDITOR // don't include in build, to avoid unnecessary Update calls
        
        /// <summary>
        /// Update the kelp position in edit mode.
        /// </summary>
        private void Update() {
            if (Application.isPlaying || KelpRenderer is null || kelpInstance == null) return;

            var newPosition = transform.position;
            if (newPosition == lastPosition) return;
            lastPosition = newPosition;
            
            KelpRenderer.MoveKelp(kelpInstance, transform.position);
        }
        
        #endif

        public void SnapToGround() {
            var hitSomething = Physics.Raycast(transform.position + Vector3.up * 10f, Vector3.down, out var hitInfo, 25f, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore);
            if (!hitSomething) return;
            
            transform.position = hitInfo.point;
            if (kelpInstance != null) KelpRenderer.MoveKelp(kelpInstance, hitInfo.point);
        }

        public void RandomizeLength() {
            length = GetRandomLength();
            KelpRenderer.Reinitialize();
        }

        private int GetRandomLength() {
            return Random.Range(LengthRange.x, LengthRange.y + 1);
        }

    }
}
