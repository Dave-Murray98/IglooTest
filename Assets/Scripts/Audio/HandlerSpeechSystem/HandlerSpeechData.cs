using UnityEngine;

/// <summary>
/// A ScriptableObject that holds the data for a single piece of handler (boss) speech.
/// Create these in the Project window via:
///   Right-click → Create → Audio / Handler Speech Data
/// Then assign the audio clip and any other info you want to show/log.
/// </summary>
[CreateAssetMenu(fileName = "New Handler Speech", menuName = "Audio/Handler Speech Data")]
public class HandlerSpeechData : ScriptableObject
{
    [Header("Audio")]
    [Tooltip("The voice clip that will play through the intercom speaker.")]
    public AudioClip clip;

    [Header("Optional Info")]
    [Tooltip("A short label shown in debug logs so you know which speech fired.")]
    public string speechLabel = "Handler Speech";

    [TextArea(2, 4)]
    [Tooltip("Optional subtitle / transcript of what the handler says. " +
             "Useful for UI subtitles later.")]
    public string transcript;
}