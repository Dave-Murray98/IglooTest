using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kellojo.StylizedKelp
{
    public class DemoCameraController : MonoBehaviour
    {
        
        public List<Transform> Stations;
        
        public float moveSpeed = 5f;
        public float rotateSpeed = 5f;
        private int currentIndex = 0;
        
        public Button NextButton;
        public Button PreviousButton;
        private Canvas canvas;
        
        void Start() {
            NextButton.onClick.AddListener(MoveNext);
            PreviousButton.onClick.AddListener(MovePrevious);
            canvas = PreviousButton.GetComponentInParent<Canvas>();
            
            if (Stations.Count > 0) {
                transform.position = Stations[0].position;
                transform.rotation = Stations[0].rotation;
            }
        }

        private void Update() {
            if (Stations.Count == 0) return;
            if (Input.GetKeyDown(KeyCode.H)) canvas.enabled = !canvas.enabled;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) MoveNext();
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) MovePrevious();
            
            transform.position = Vector3.Lerp(transform.position, Stations[currentIndex].position, Time.deltaTime * moveSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, Stations[currentIndex].rotation, Time.deltaTime * rotateSpeed);
            
        }


        void MoveNext() {
            if (Stations.Count == 0) return;
            currentIndex = (currentIndex + 1) % Stations.Count;
        }
        void MovePrevious() {
            if (Stations.Count == 0) return;
            currentIndex = (currentIndex - 1 + Stations.Count) % Stations.Count;
        }
        
    }
}
