using System;
using UnityEngine;

public class ScreenshotCapture : MonoBehaviour
{
    [SerializeField] private string path = "Assets/Resources/Art/ScreenCaptures/";
    [Range(1, 5)]
    [SerializeField] private int size = 2;

    [SerializeField] private KeyCode captureKey = KeyCode.P;

    void Update()
    {
        if (Input.GetKeyDown(captureKey))
        {
            // The "4" multiplies your current resolution by 4x
            ScreenCapture.CaptureScreenshot(path + "Screenshot" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png", size);
            Debug.Log("Screenshot taken!");
        }
    }
}