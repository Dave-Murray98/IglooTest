using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

#if UNITY_EDITOR
/// <summary>
/// Editor utility window for managing audio log subtitles.
/// Helps with batch importing, editing, and testing subtitle data.
/// Access via Window > Audio Logs > Subtitle Manager
/// </summary>
public class AudioLogSubtitleManager : EditorWindow
{
    private AudioLogData selectedAudioLog;
    private Vector2 scrollPosition;
    private string bulkImportText = "";

    [MenuItem("Window/Audio Logs/Subtitle Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<AudioLogSubtitleManager>("Subtitle Manager");
        window.minSize = new Vector2(600, 400);
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawHeader();
        DrawAudioLogSelection();

        if (selectedAudioLog != null)
        {
            EditorGUILayout.Space(10);
            DrawSpeakerInfo();

            EditorGUILayout.Space(10);
            DrawSubtitleList();

            EditorGUILayout.Space(10);
            DrawBulkImport();

            EditorGUILayout.Space(10);
            DrawUtilityButtons();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Audio Log Subtitle Manager", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Use this tool to manage subtitles for your audio logs. " +
            "You can add, edit, and import subtitles in bulk.",
            MessageType.Info
        );
        EditorGUILayout.Space(5);
    }

    private void DrawAudioLogSelection()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Selected Audio Log:", GUILayout.Width(150));

        AudioLogData newSelection = (AudioLogData)EditorGUILayout.ObjectField(
            selectedAudioLog,
            typeof(AudioLogData),
            false
        );

        if (newSelection != selectedAudioLog)
        {
            selectedAudioLog = newSelection;
            bulkImportText = GenerateTemplateText();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawSpeakerInfo()
    {
        EditorGUILayout.LabelField("Speaker Information", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        // We'll need to use SerializedObject to properly edit ScriptableObject
        var serializedObject = new SerializedObject(selectedAudioLog);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("speakerName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("recordingDate"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("speakerPortrait"));

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(selectedAudioLog);
        }
    }

    private void DrawSubtitleList()
    {
        EditorGUILayout.LabelField("Subtitles", EditorStyles.boldLabel);

        var serializedObject = new SerializedObject(selectedAudioLog);
        var hasSubtitlesProperty = serializedObject.FindProperty("hasSubtitles");
        var subtitlesProperty = serializedObject.FindProperty("subtitles");

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.PropertyField(hasSubtitlesProperty);

        if (hasSubtitlesProperty.boolValue)
        {
            EditorGUILayout.PropertyField(subtitlesProperty, true);

            if (GUILayout.Button("Add Subtitle Entry"))
            {
                int newIndex = subtitlesProperty.arraySize;
                subtitlesProperty.InsertArrayElementAtIndex(newIndex);

                // Set default values for new entry
                var newElement = subtitlesProperty.GetArrayElementAtIndex(newIndex);
                newElement.FindPropertyRelative("timestamp").floatValue =
                    newIndex > 0 ? subtitlesProperty.GetArrayElementAtIndex(newIndex - 1)
                        .FindPropertyRelative("timestamp").floatValue + 2f : 0f;
                newElement.FindPropertyRelative("text").stringValue = "New subtitle";
                newElement.FindPropertyRelative("duration").floatValue = 0f;
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(selectedAudioLog);
        }
    }

    private void DrawBulkImport()
    {
        EditorGUILayout.LabelField("Bulk Import", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Import subtitles using this format (one per line):\n" +
            "timestamp|text\n" +
            "Example: 2.5|This is the subtitle text",
            MessageType.Info
        );

        bulkImportText = EditorGUILayout.TextArea(bulkImportText, GUILayout.Height(150));

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Import from Text"))
        {
            ImportSubtitlesFromText();
        }

        if (GUILayout.Button("Generate Template"))
        {
            bulkImportText = GenerateTemplateText();
        }

        if (GUILayout.Button("Clear Text"))
        {
            bulkImportText = "";
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawUtilityButtons()
    {
        EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Sort by Timestamp"))
        {
            SortSubtitles();
        }

        if (GUILayout.Button("Validate Subtitles"))
        {
            ValidateSubtitles();
        }

        if (GUILayout.Button("Clear All Subtitles"))
        {
            if (EditorUtility.DisplayDialog(
                "Clear Subtitles?",
                "This will remove all subtitles from this audio log. Are you sure?",
                "Yes, Clear",
                "Cancel"))
            {
                ClearAllSubtitles();
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void ImportSubtitlesFromText()
    {
        if (selectedAudioLog == null)
        {
            EditorUtility.DisplayDialog("Error", "No audio log selected!", "OK");
            return;
        }

        var serializedObject = new SerializedObject(selectedAudioLog);
        var subtitlesProperty = serializedObject.FindProperty("subtitles");
        var hasSubtitlesProperty = serializedObject.FindProperty("hasSubtitles");

        // Clear existing subtitles
        subtitlesProperty.ClearArray();

        // Parse text
        string[] lines = bulkImportText.Split('\n');
        int importedCount = 0;

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                continue;

            string[] parts = line.Split('|');
            if (parts.Length < 2)
                continue;

            if (float.TryParse(parts[0].Trim(), out float timestamp))
            {
                string text = parts[1].Trim();
                float duration = 0f;

                // Optional third parameter for duration
                if (parts.Length >= 3 && float.TryParse(parts[2].Trim(), out float parsedDuration))
                {
                    duration = parsedDuration;
                }

                // Add subtitle entry
                int newIndex = subtitlesProperty.arraySize;
                subtitlesProperty.InsertArrayElementAtIndex(newIndex);

                var newElement = subtitlesProperty.GetArrayElementAtIndex(newIndex);
                newElement.FindPropertyRelative("timestamp").floatValue = timestamp;
                newElement.FindPropertyRelative("text").stringValue = text;
                newElement.FindPropertyRelative("duration").floatValue = duration;

                importedCount++;
            }
        }

        // Enable subtitles if we imported any
        if (importedCount > 0)
        {
            hasSubtitlesProperty.boolValue = true;
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(selectedAudioLog);

        EditorUtility.DisplayDialog(
            "Import Complete",
            $"Imported {importedCount} subtitle entries.",
            "OK"
        );

        // Sort after import
        SortSubtitles();
    }

    private string GenerateTemplateText()
    {
        if (selectedAudioLog == null || selectedAudioLog.AudioClip == null)
            return "// Format: timestamp|text|duration(optional)\n// Example:\n0.0|First subtitle line\n2.5|Second subtitle line\n5.0|Third subtitle line|3.0";

        float duration = selectedAudioLog.AudioClip.length;

        string template = $"// Audio Log: {selectedAudioLog.LogTitle}\n";
        template += $"// Duration: {duration:F2} seconds\n";
        template += $"// Format: timestamp|text|duration(optional)\n\n";
        template += "0.0|First subtitle line\n";
        template += $"{(duration * 0.3f):F1}|Second subtitle line\n";
        template += $"{(duration * 0.6f):F1}|Third subtitle line\n";

        return template;
    }

    private void SortSubtitles()
    {
        if (selectedAudioLog == null || !selectedAudioLog.HasSubtitles)
            return;

        var serializedObject = new SerializedObject(selectedAudioLog);
        var subtitlesProperty = serializedObject.FindProperty("subtitles");

        // Convert to list for sorting
        List<(float timestamp, string text, float duration)> subtitles = new List<(float, string, float)>();

        for (int i = 0; i < subtitlesProperty.arraySize; i++)
        {
            var element = subtitlesProperty.GetArrayElementAtIndex(i);
            float timestamp = element.FindPropertyRelative("timestamp").floatValue;
            string text = element.FindPropertyRelative("text").stringValue;
            float duration = element.FindPropertyRelative("duration").floatValue;
            subtitles.Add((timestamp, text, duration));
        }

        // Sort by timestamp
        subtitles.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));

        // Write back
        for (int i = 0; i < subtitles.Count; i++)
        {
            var element = subtitlesProperty.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("timestamp").floatValue = subtitles[i].timestamp;
            element.FindPropertyRelative("text").stringValue = subtitles[i].text;
            element.FindPropertyRelative("duration").floatValue = subtitles[i].duration;
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(selectedAudioLog);

        Debug.Log($"Sorted {subtitles.Count} subtitles by timestamp");
    }

    private void ValidateSubtitles()
    {
        if (selectedAudioLog == null)
            return;

        bool isValid = selectedAudioLog.ValidateSubtitles();

        if (isValid)
        {
            EditorUtility.DisplayDialog(
                "Validation Success",
                "All subtitles are valid and in chronological order!",
                "OK"
            );
        }
        else
        {
            EditorUtility.DisplayDialog(
                "Validation Failed",
                "Some subtitles have issues. Check the console for details.",
                "OK"
            );
        }
    }

    private void ClearAllSubtitles()
    {
        if (selectedAudioLog == null)
            return;

        var serializedObject = new SerializedObject(selectedAudioLog);
        var subtitlesProperty = serializedObject.FindProperty("subtitles");

        subtitlesProperty.ClearArray();

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(selectedAudioLog);

        Debug.Log("Cleared all subtitles");
    }
}
#endif