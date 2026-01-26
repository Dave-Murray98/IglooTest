using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sirenix.OdinInspector;

/// <summary>
/// Manages the visual display of audio log UI elements including subtitles.
/// Attach this to your audio log UI prefab.
/// This component is controlled by AudioLogUIManager.
/// </summary>
public class AudioLogSubtitleDisplay : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The root panel that contains all audio log UI elements")]
    [SerializeField] private GameObject uiPanel;

    [Header("Speaker Information")]
    [Tooltip("Text field for speaker name")]
    [SerializeField] private TextMeshProUGUI speakerNameText;

    [Tooltip("Text field for recording date")]
    [SerializeField] private TextMeshProUGUI recordingDateText;

    [Tooltip("Image for speaker portrait")]
    [SerializeField] private Image speakerPortraitImage;

    [Header("Subtitle Display")]
    [Tooltip("Text field for subtitle text")]
    [SerializeField] private TextMeshProUGUI subtitleText;

    [Header("Optional Animation")]
    [Tooltip("Should subtitles fade in/out?")]
    [SerializeField] private bool useSubtitleFade = true;

    [Tooltip("Fade duration in seconds")]
    [SerializeField] private float fadeDuration = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // State tracking
    private CanvasGroup subtitleCanvasGroup;
    private string currentSubtitleText = "";
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        // Get or add CanvasGroup for subtitle fading
        if (useSubtitleFade && subtitleText != null)
        {
            subtitleCanvasGroup = subtitleText.GetComponent<CanvasGroup>();
            if (subtitleCanvasGroup == null)
            {
                subtitleCanvasGroup = subtitleText.gameObject.AddComponent<CanvasGroup>();
            }
        }

        // Start with UI hidden
        HideUI();
    }

    /// <summary>
    /// Shows the audio log UI with speaker information
    /// </summary>
    public void ShowUI(AudioLogData audioLogData)
    {
        if (audioLogData == null)
        {
            DebugLog("Cannot show UI - null AudioLogData");
            return;
        }

        DebugLog($"Showing UI for: {audioLogData.LogTitle}");

        // Show the UI panel
        if (uiPanel != null)
        {
            uiPanel.SetActive(true);
        }

        // Set speaker information
        UpdateSpeakerInfo(audioLogData);

        // Clear subtitles initially
        ClearSubtitle();
    }

    /// <summary>
    /// Hides the audio log UI
    /// </summary>
    public void HideUI()
    {
        DebugLog("Hiding UI");

        // Hide the UI panel
        if (uiPanel != null)
        {
            uiPanel.SetActive(false);
        }

        // Clear all text
        ClearSubtitle();
    }

    /// <summary>
    /// Updates speaker information display
    /// </summary>
    private void UpdateSpeakerInfo(AudioLogData audioLogData)
    {
        // Update speaker name
        if (speakerNameText != null)
        {
            string displayName = !string.IsNullOrEmpty(audioLogData.SpeakerName)
                ? audioLogData.SpeakerName
                : "Unknown Speaker";
            speakerNameText.text = displayName;
        }

        // Update recording date
        if (recordingDateText != null)
        {
            string displayDate = !string.IsNullOrEmpty(audioLogData.RecordingDate)
                ? audioLogData.RecordingDate
                : "Date Unknown";
            recordingDateText.text = displayDate;
        }

        // Update speaker portrait
        if (speakerPortraitImage != null)
        {
            if (audioLogData.SpeakerPortrait != null)
            {
                speakerPortraitImage.sprite = audioLogData.SpeakerPortrait;
                speakerPortraitImage.enabled = true;
            }
            else
            {
                // Hide portrait if none provided
                speakerPortraitImage.enabled = false;
            }
        }
    }

    /// <summary>
    /// Updates the subtitle text display
    /// </summary>
    public void UpdateSubtitle(string newSubtitleText)
    {
        if (subtitleText == null)
            return;

        // Check if subtitle changed
        if (currentSubtitleText == newSubtitleText)
            return;

        currentSubtitleText = newSubtitleText;

        if (string.IsNullOrEmpty(newSubtitleText))
        {
            // Clear subtitle with fade
            if (useSubtitleFade)
            {
                FadeOutSubtitle();
            }
            else
            {
                subtitleText.text = "";
            }
        }
        else
        {
            // Show new subtitle with fade
            if (useSubtitleFade)
            {
                FadeInSubtitle(newSubtitleText);
            }
            else
            {
                subtitleText.text = newSubtitleText;
            }
        }

        DebugLog($"Subtitle updated: {(string.IsNullOrEmpty(newSubtitleText) ? "[CLEARED]" : newSubtitleText)}");
    }

    /// <summary>
    /// Clears the subtitle display
    /// </summary>
    public void ClearSubtitle()
    {
        UpdateSubtitle("");
    }

    /// <summary>
    /// Fades in a new subtitle
    /// </summary>
    private void FadeInSubtitle(string text)
    {
        if (subtitleCanvasGroup == null)
        {
            subtitleText.text = text;
            return;
        }

        // Stop any existing fade
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(FadeSubtitleCoroutine(text, 0f, 1f));
    }

    /// <summary>
    /// Fades out the current subtitle
    /// </summary>
    private void FadeOutSubtitle()
    {
        if (subtitleCanvasGroup == null)
        {
            subtitleText.text = "";
            return;
        }

        // Stop any existing fade
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(FadeSubtitleCoroutine("", subtitleCanvasGroup.alpha, 0f));
    }

    /// <summary>
    /// Coroutine for fading subtitle in/out
    /// </summary>
    private System.Collections.IEnumerator FadeSubtitleCoroutine(string targetText, float startAlpha, float endAlpha)
    {
        float elapsed = 0f;

        // If fading in, set the text immediately
        if (endAlpha > startAlpha)
        {
            subtitleText.text = targetText;
        }

        // Animate alpha
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            subtitleCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            yield return null;
        }

        subtitleCanvasGroup.alpha = endAlpha;

        // If fading out, clear the text after fade completes
        if (endAlpha < startAlpha)
        {
            subtitleText.text = targetText;
        }

        fadeCoroutine = null;
    }

    /// <summary>
    /// Gets whether the UI is currently visible
    /// </summary>
    public bool IsVisible()
    {
        return uiPanel != null && uiPanel.activeSelf;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[AudioLogSubtitleDisplay] {message}");
        }
    }

#if UNITY_EDITOR
    [Button("Test Show UI")]
    private void TestShowUI()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Test functions only work in Play mode");
            return;
        }

        // Create test data
        var testData = ScriptableObject.CreateInstance<AudioLogData>();
        // Note: You'd need to set properties via reflection or make them public temporarily for testing

        ShowUI(testData);
    }

    [Button("Test Hide UI")]
    private void TestHideUI()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Test functions only work in Play mode");
            return;
        }

        HideUI();
    }

    [Button("Test Subtitle")]
    private void TestSubtitle()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Test functions only work in Play mode");
            return;
        }

        UpdateSubtitle("This is a test subtitle to verify the display is working correctly.");
    }

    [Button("Clear Test")]
    private void ClearTest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Test functions only work in Play mode");
            return;
        }

        ClearSubtitle();
    }

    private void OnValidate()
    {
        // Auto-find references if not set
        if (uiPanel == null)
        {
            uiPanel = gameObject;
        }
    }
#endif
}