using UnityEngine;
using UnityEngine.UI;

public class ControlsSettingsMenu : MonoBehaviour
{
    [SerializeField] private Slider lookSensitivitySlider;

    private float currentLookSensitivity;

    private float minLookSensitivity = 1f;
    private float maxLookSensitivity = 10f;

    private void Awake()
    {
        if (lookSensitivitySlider != null)
        {
            lookSensitivitySlider.onValueChanged.AddListener(OnSliderValueChanged);
        }
    }

    private void Start()
    {
        // Convert the saved sensitivity (1-10) to slider value (0-1)
        if (GameManager.Instance.playerData == null) return;

        float sliderValue = ConvertSensitivityToSlider(GameManager.Instance.playerData.lookSensitivity);
        if (lookSensitivitySlider != null)
            lookSensitivitySlider.value = sliderValue;
    }

    private void OnSliderValueChanged(float sliderValue)
    {
        // Convert slider value (0-1) to sensitivity (1-10)
        currentLookSensitivity = ConvertSliderToSensitivity(sliderValue);

        UpdateLookSensitivityValue(currentLookSensitivity);
    }

    private void UpdateLookSensitivityValue(float sensitivity)
    {
        currentLookSensitivity = sensitivity;
        GameManager.Instance.playerData.lookSensitivity = currentLookSensitivity;
        GameManager.Instance.playerManager.controller.RefreshCameraControllerReferences();
    }

    // Converts slider value (0-1) to sensitivity (1-10)
    private float ConvertSliderToSensitivity(float sliderValue)
    {
        return Mathf.Lerp(minLookSensitivity, maxLookSensitivity, sliderValue);
    }

    // Converts sensitivity (1-10) back to slider value (0-1)
    private float ConvertSensitivityToSlider(float sensitivity)
    {
        return Mathf.InverseLerp(minLookSensitivity, maxLookSensitivity, sensitivity);
    }
}