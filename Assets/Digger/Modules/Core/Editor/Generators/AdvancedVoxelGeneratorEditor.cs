using Digger.Modules.Core.Sources.Generators;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Digger.Modules.Core.Editor.Generators
{
    [VoxelGeneratorAttr("Advanced", 1)]
    public class AdvancedVoxelGeneratorEditor : AVoxelGeneratorEditor
    {
        private AdvancedVoxelGenerator advancedGenerator;
        private ReorderableList depthLayersList;
        private ReorderableList noiseLayersList;
        private SerializedObject serializedGenerator;
        
        private bool depthLayersFoldout = true;
        private bool noiseLayersFoldout = true;

        public override void OnEnable()
        {
            advancedGenerator = generator as AdvancedVoxelGenerator;
            if (advancedGenerator != null)
            {
                serializedGenerator = new SerializedObject(advancedGenerator);
                SetupDepthLayersList();
                SetupNoiseLayersList();
            }
        }

        public override void OnDisable()
        {
            serializedGenerator?.Dispose();
        }

        private void SetupDepthLayersList()
        {
            var depthLayersProperty = serializedGenerator.FindProperty("depthLayers");
            
            depthLayersList = new ReorderableList(serializedGenerator, depthLayersProperty, true, true, true, true)
            {
                drawHeaderCallback = rect =>
                {
                    EditorGUI.LabelField(rect, "Depth Layers (sorted by depth, deepest first)");
                },
                
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    var element = depthLayersProperty.GetArrayElementAtIndex(index);
                    var lineHeight = EditorGUIUtility.singleLineHeight;
                    var padding = EditorGUIUtility.standardVerticalSpacing;
                    var yPos = rect.y + padding;
                    
                    // Get values for the label
                    var minDepth = element.FindPropertyRelative("minDepth").floatValue;
                    var textureIndex = element.FindPropertyRelative("textureIndex").intValue;
                    var destructible = element.FindPropertyRelative("destructible").boolValue;
                    
                    // Create a descriptive label
                    var label = $"Depth {minDepth:F1} (Texture {textureIndex}, {(destructible ? "Destructible" : "Indestructible")})";
                    
                    EditorGUI.indentLevel++;
                    
                    // Use PropertyField for automatic handling and better layout
                    var propertyHeight = EditorGUI.GetPropertyHeight(element, true);
                    var propertyRect = new Rect(rect.x, yPos, rect.width, propertyHeight);
                    EditorGUI.PropertyField(propertyRect, element, new GUIContent(label), true);
                    
                    EditorGUI.indentLevel--;
                },
                
                elementHeightCallback = index =>
                {
                    var element = depthLayersProperty.GetArrayElementAtIndex(index);
                    var height = EditorGUI.GetPropertyHeight(element, true);
                    return height + EditorGUIUtility.standardVerticalSpacing * 2;
                },
                
                onAddCallback = list =>
                {
                    var index = list.serializedProperty.arraySize;
                    list.serializedProperty.arraySize++;
                    list.index = index;
                    var element = list.serializedProperty.GetArrayElementAtIndex(index);
                    
                    // Set default values
                    element.FindPropertyRelative("minDepth").floatValue = 5f;
                    element.FindPropertyRelative("textureIndex").intValue = 1;
                    element.FindPropertyRelative("destructible").boolValue = true;
                }
            };
        }

        private void SetupNoiseLayersList()
        {
            var noiseLayersProperty = serializedGenerator.FindProperty("noiseLayers");
            
            noiseLayersList = new ReorderableList(serializedGenerator, noiseLayersProperty, true, true, true, true)
            {
                drawHeaderCallback = rect =>
                {
                    EditorGUI.LabelField(rect, "Noise Layers (applied in order)");
                },
                
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    var element = noiseLayersProperty.GetArrayElementAtIndex(index);
                    var lineHeight = EditorGUIUtility.singleLineHeight;
                    var padding = EditorGUIUtility.standardVerticalSpacing;
                    var yPos = rect.y + padding;
                    
                    // Get values for the label
                    var scale = element.FindPropertyRelative("scale").floatValue;
                    var blendMode = (AdvancedVoxelGenerator.NoiseBlendMode)element.FindPropertyRelative("blendMode").intValue;
                    var destructible = element.FindPropertyRelative("destructible").boolValue;
                    
                    // Create a descriptive label
                    var label = $"Noise Layer {index + 1} (Scale {scale:F0}, {blendMode}, {(destructible ? "Destructible" : "Indestructible")})";
                    
                    EditorGUI.indentLevel++;
                    
                    // Use PropertyField for automatic handling and better layout
                    var propertyHeight = EditorGUI.GetPropertyHeight(element, true);
                    var propertyRect = new Rect(rect.x, yPos, rect.width, propertyHeight);
                    EditorGUI.PropertyField(propertyRect, element, new GUIContent(label), true);
                    
                    EditorGUI.indentLevel--;
                },
                
                elementHeightCallback = index =>
                {
                    var element = noiseLayersProperty.GetArrayElementAtIndex(index);
                    var height = EditorGUI.GetPropertyHeight(element, true);
                    return height + EditorGUIUtility.standardVerticalSpacing * 2;
                },
                
                onAddCallback = list =>
                {
                    var index = list.serializedProperty.arraySize;
                    list.serializedProperty.arraySize++;
                    list.index = index;
                    var element = list.serializedProperty.GetArrayElementAtIndex(index);
                    
                    // Set default values
                    element.FindPropertyRelative("scale").floatValue = 15f;
                    element.FindPropertyRelative("octaves").intValue = 2;
                    element.FindPropertyRelative("persistence").floatValue = 0.5f;
                    element.FindPropertyRelative("blendMode").intValue = 1; // Add mode
                    element.FindPropertyRelative("threshold").floatValue = -0.2f;
                    element.FindPropertyRelative("textureIndex").intValue = -1;
                    element.FindPropertyRelative("destructible").boolValue = true;
                }
            };
        }

        public override void OnInspectorGUI()
        {
            if (advancedGenerator == null || serializedGenerator == null)
            {
                EditorGUILayout.HelpBox("No Advanced Voxel Generator selected or created.", MessageType.Warning);
                return;
            }
            
            serializedGenerator.Update();
            
            EditorGUILayout.HelpBox(
                "Advanced Voxel Generator\n\n" +
                "Creates voxels with layered procedural variations. Depth layers are applied first based on depth below terrain surface, " +
                "then noise layers are applied in order to add procedural variation.",
                MessageType.Info);
            
            EditorGUILayout.Space();
            
            // Depth Layers Section
            depthLayersFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(depthLayersFoldout, "Depth-Based Layers");
            if (depthLayersFoldout)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.HelpBox(
                    "Depth layers assign textures and destructibility based on how deep the voxel is below the terrain surface. " +
                    "Layers are automatically sorted by depth (deepest first). Destructible voxels can be modified, while indestructible voxels cannot.",
                    MessageType.Info);
                
                EditorGUILayout.Space();
                
                if (depthLayersList != null)
                {
                    depthLayersList.DoLayoutList();
                }
                
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            EditorGUILayout.Space();
            
            // Noise Layers Section
            noiseLayersFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(noiseLayersFoldout, "Noise-Based Layers");
            if (noiseLayersFoldout)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.HelpBox(
                    "Noise layers add procedural variation using Perlin noise. They are applied in order after depth layers.\n\n" +
                    "Blend Modes:\n" +
                    "• Replace: Fully replaces the destructibility when noise influence is high\n" +
                    "• Add: Only makes voxels indestructible (if layer is indestructible), never makes them destructible\n\n" +
                    "Threshold: Noise layer effect smoothly fades in around this value (-1 to 1 range). " +
                    "The layer only affects voxels where noise influence exceeds 0.5 to prevent discontinuities.",
                    MessageType.Info);
                
                EditorGUILayout.Space();
                
                if (noiseLayersList != null)
                {
                    noiseLayersList.DoLayoutList();
                }
                
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            serializedGenerator.ApplyModifiedProperties();
            
            if (GUI.changed)
            {
                EditorUtility.SetDirty(advancedGenerator);
            }
        }
    }
}
