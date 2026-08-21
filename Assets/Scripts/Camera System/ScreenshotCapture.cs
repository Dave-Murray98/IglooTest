using UnityEngine;

public class ScreenshotCapture : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            // The "4" multiplies your current resolution by 4x
            ScreenCapture.CaptureScreenshot("Assets/Resources/Art/ScreenCaptures/PosterShot" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png", 4);
            Debug.Log("Screenshot taken!");
        }
    }
}