using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public class SubmarineShaderGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        Material material = materialEditor.target as Material;

        // Custom Rendering Order Section
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Rendering Order", EditorStyles.boldLabel);

        MaterialProperty renderQueue = FindProperty("_RenderQueue", properties);
        MaterialProperty zTest = FindProperty("_ZTest", properties);

        EditorGUI.BeginChangeCheck();

        float newRenderQueue = EditorGUILayout.FloatField("Render Queue", renderQueue.floatValue);
        if (EditorGUI.EndChangeCheck())
        {
            renderQueue.floatValue = newRenderQueue;
            material.renderQueue = (int)newRenderQueue;
        }

        EditorGUILayout.HelpBox("Higher values render later. Use 3500+ to render after water and fish.", MessageType.Info);

        // ZTest dropdown
        CompareFunction currentZTest = (CompareFunction)zTest.floatValue;
        CompareFunction newZTest = (CompareFunction)EditorGUILayout.EnumPopup("ZTest", currentZTest);
        if (newZTest != currentZTest)
        {
            zTest.floatValue = (float)newZTest;
        }

        EditorGUILayout.HelpBox("Use 'Always' to render over everything (fixes fish poke-through). Use 'LessEqual' for normal depth testing.", MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Standard Properties", EditorStyles.boldLabel);

        // Draw the default lit shader GUI for everything else
        base.OnGUI(materialEditor, properties);
    }
}