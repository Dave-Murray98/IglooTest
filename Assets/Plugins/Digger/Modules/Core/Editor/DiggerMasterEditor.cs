using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Digger.Modules.Core.Editor.Generators;
using Digger.Modules.Core.Sources;
using Digger.Modules.Core.Sources.Generators;
using Digger.Modules.Core.Sources.Jobs;
using Digger.Modules.Core.Sources.TerrainInterface;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Digger.Modules.Core.Editor
{
    [CustomEditor(typeof(DiggerMaster))]
    public class DiggerMasterEditor : UnityEditor.Editor
    {
        private const float RaycastLength = 3000;

        private DiggerMaster master;
        private DiggerSystem[] diggerSystems;
        private IOperationEditor operationEditor;
        private AVoxelGeneratorEditor generatorEditor;

        private List<Type> operationEditors;
        private List<Type> generatorEditors;

        private int selectedOperationEditorIndex {
            get => EditorPrefs.GetInt("diggerMaster_selectedOperationEditorIndex", 0);
            set => EditorPrefs.SetInt("diggerMaster_selectedOperationEditorIndex", value);
        }


        private int activeTab {
            get => EditorPrefs.GetInt("diggerMaster_activeTab", 0);
            set => EditorPrefs.SetInt("diggerMaster_activeTab", value);
        }

        public static bool shortcutsEnabled {
            get => EditorPrefs.GetBool("diggerMaster_shortcutsEnabled", true);
            set => EditorPrefs.SetBool("diggerMaster_shortcutsEnabled", value);
        }

        public void OnEnable()
        {
            master = (DiggerMaster)target;
            CheckDiggerVersion();
            diggerSystems = FindObjectsByType<DiggerSystem>(FindObjectsSortMode.None);
            foreach (var diggerSystem in diggerSystems) {
                DiggerSystemEditor.Init(diggerSystem, false);
            }

            var type = typeof(IOperationEditor);
            operationEditors = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => type.IsAssignableFrom(p) && !p.IsAbstract && !p.IsInterface)
                .ToList();
            operationEditors.Sort(new OperationAttr.Comparer());

            if (selectedOperationEditorIndex < 0 || selectedOperationEditorIndex >= operationEditors.Count) {
                selectedOperationEditorIndex = 0;
            }

            if (operationEditor == null && operationEditors.Count > 0) {
                operationEditor = (IOperationEditor)Activator.CreateInstance(operationEditors[selectedOperationEditorIndex]);
            }

            operationEditor?.OnDisable();
            operationEditor?.OnEnable();

            // Load generator editors
            var generatorType = typeof(AVoxelGeneratorEditor);
            generatorEditors = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => generatorType.IsAssignableFrom(p) && !p.IsAbstract)
                .ToList();
            generatorEditors.Sort(new VoxelGeneratorAttr.Comparer());

            // Ensure a default generator is assigned if none exists
            if (master.VoxelGenerator == null)
            {
                var defaultGenerator = DefaultGeneratorAssetsCreator.GetOrCreateDefaultSimpleGenerator();
                if (defaultGenerator != null)
                {
                    master.VoxelGenerator = defaultGenerator;
                    EditorUtility.SetDirty(master);
                }
            }

            // Initialize the generator editor based on the current generator
            UpdateGeneratorEditorForCurrentGenerator();

            SceneView.duringSceneGui -= OnScene;
            SceneView.duringSceneGui += OnScene;
            Undo.undoRedoPerformed -= UndoCallback;
            Undo.undoRedoPerformed += UndoCallback;

            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        public void OnDisable()
        {
            Undo.undoRedoPerformed -= UndoCallback;
            SceneView.duringSceneGui -= OnScene;
            operationEditor?.OnDisable();
            generatorEditor?.OnDisable();
        }

        private static void UndoCallback()
        {
            var diggers = FindObjectsByType<DiggerSystem>(FindObjectsSortMode.None);
            foreach (var digger in diggers) {
                digger.DoUndo();
            }
        }

        public override void OnInspectorGUI()
        {
            activeTab = GUILayout.Toolbar(activeTab, new[]
            {
                EditorGUIUtility.TrTextContentWithIcon("Edit", "d_TerrainInspector.TerrainToolSplat"),
                EditorGUIUtility.TrTextContentWithIcon("Generation", "d_PreMatCube"),
                EditorGUIUtility.TrTextContentWithIcon("Lighting", "d_SceneViewLighting On"),
                EditorGUIUtility.TrTextContentWithIcon("Settings", "d_TerrainInspector.TerrainToolSettings"),
                EditorGUIUtility.TrTextContentWithIcon("Help", "_Help")
            });
            switch (activeTab) {
                case 0:
                    OnInspectorGUIEditTab();
                    break;
                case 1:
                    OnInspectorGUIGenerationTab();
                    break;
                case 2:
                    OnInspectorGUILightingTab();
                    break;
                case 3:
                    OnInspectorGUISettingsTab();
                    break;
                case 4:
                    OnInspectorGUIHelpTab();
                    break;
                default:
                    activeTab = 0;
                    break;
            }
        }


        public void OnInspectorGUIHelpTab()
        {
            EditorGUILayout.HelpBox("Thanks for using Digger!\n\n" +
                                    "Need help? Checkout the documentation and join us on Discord to get support!\n\n" +
                                    "Want to help the developer and support the project? Please write a review on the Asset Store!",
                MessageType.Info);


            if (GUILayout.Button("Open documentation")) {
                Application.OpenURL("https://ofux.github.io/Digger-Documentation/");
            }

            if (GUILayout.Button("Open Digger Asset Store page")) {
                Application.OpenURL("https://assetstore.unity.com/packages/tools/terrain/digger-terrain-caves-overhangs-135178");
            }

            if (GUILayout.Button("Open Digger PRO Asset Store page")) {
                Application.OpenURL("https://assetstore.unity.com/packages/tools/terrain/digger-pro-149753");
            }

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Support is on Discord:", EditorStyles.boldLabel, GUILayout.Width(140));
            var style = new GUIStyle(EditorStyles.textField);
            EditorGUILayout.SelectableLabel("https://discord.gg/C2X6C6s", style, GUILayout.Height(18));
            EditorGUILayout.EndHorizontal();
        }

        public void OnInspectorGUIGenerationTab()
        {
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            
            // Big visible warning about clearing modifications
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.8f, 0.2f); // Orange/yellow tint for warning
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = originalColor;
            
            var warningStyle = new GUIStyle(EditorStyles.boldLabel);
            warningStyle.fontSize = 13;
            warningStyle.normal.textColor = new Color(1f, 0.6f, 0f);
            EditorGUILayout.LabelField("⚠️ IMPORTANT WARNING", warningStyle);
            
            EditorGUILayout.HelpBox(
                "🚨​ For any changes to voxel generation settings to take effect, you MUST use the \"Regenerate unmodified voxels\" button or the \"Clear all Digger modifications and regenerate voxels\" button " +
                "at the bottom of this tab after making changes.",
                MessageType.Warning);
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Voxel Generation", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                "Configure how voxels are generated from the terrain heightmap. " +
                "Different generators can add procedural variations, textures, and effects to your voxels.",
                MessageType.Info);
            
            EditorGUILayout.Space();

            // Generator asset field
            EditorGUI.BeginChangeCheck();
            var newGenerator = EditorGUILayout.ObjectField(
                new GUIContent("Voxel Generator", "The ScriptableObject that defines how voxels are generated"),
                master.VoxelGenerator as ScriptableObject,
                typeof(IVoxelGenerator),
                false) as ScriptableObject;
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(master, "Change Voxel Generator");
                master.VoxelGenerator = newGenerator as IVoxelGenerator;
                
                // Update the generator editor to match the new generator type
                UpdateGeneratorEditorForCurrentGenerator();
                EditorUtility.SetDirty(master);
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // Display the generator's custom GUI
            if (generatorEditor != null && master.VoxelGenerator != null)
            {
                generatorEditor.OnInspectorGUI();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "No generator assigned. Create or select a Voxel Generator asset above.\n\n" +
                    "You can create a new generator by right-clicking in the Project window and selecting:\n" +
                    "Create > Digger > Voxel Generators > [Generator Type]",
                    MessageType.Warning);
            }

            EditorGUILayout.Space();

            // Quick actions
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Simple Generator"))
            {
                CreateAndAssignGenerator<SimpleVoxelGenerator>("SimpleVoxelGenerator");
            }
            
            if (GUILayout.Button("Create Advanced Generator"))
            {
                CreateAndAssignGenerator<AdvancedVoxelGenerator>("AdvancedVoxelGenerator");
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Use Default Simple Generator"))
            {
                Undo.RecordObject(master, "Assign Default Simple Generator");
                master.VoxelGenerator = DefaultGeneratorAssetsCreator.GetOrCreateDefaultSimpleGenerator();
                UpdateGeneratorEditorForCurrentGenerator();
                EditorUtility.SetDirty(master);
            }
            
            if (GUILayout.Button("Use Default Advanced Generator"))
            {
                Undo.RecordObject(master, "Assign Default Advanced Generator");
                master.VoxelGenerator = DefaultGeneratorAssetsCreator.GetOrCreateDefaultAdvancedGenerator();
                UpdateGeneratorEditorForCurrentGenerator();
                EditorUtility.SetDirty(master);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            OnInspectorGUIClearButtons(true, true, "Regenerate unmodified voxels", "Clear all Digger modifications and regenerate voxels");
        }

        private void UpdateGeneratorEditorForCurrentGenerator()
        {
            if (master.VoxelGenerator == null)
            {
                generatorEditor?.OnDisable();
                generatorEditor = null;
                return;
            }

            // Find the appropriate editor for the current generator type
            var generatorType = master.VoxelGenerator.GetType();
            AVoxelGeneratorEditor newEditor = null;
            
            // Try to find an editor that matches this generator type
            if (generatorEditors != null)
            {
                foreach (var editorType in generatorEditors)
                {
                    var tempEditor = (AVoxelGeneratorEditor)Activator.CreateInstance(editorType);
                    tempEditor.SetGenerator(master.VoxelGenerator);
                    
                    // Check if this editor can handle this generator
                    // For now, we'll match by checking the generator type name
                    // SimpleVoxelGenerator -> SimpleVoxelGeneratorEditor
                    // AdvancedVoxelGenerator -> AdvancedVoxelGeneratorEditor
                    var editorName = editorType.Name.Replace("Editor", "");
                    var generatorName = generatorType.Name.Replace("VoxelGenerator", "");
                    
                    if (editorName.Contains(generatorName))
                    {
                        newEditor = tempEditor;
                        break;
                    }
                }
            }
            
            // If no specific editor found, use the first one as default
            if (newEditor == null && generatorEditors != null && generatorEditors.Count > 0)
            {
                newEditor = (AVoxelGeneratorEditor)Activator.CreateInstance(generatorEditors[0]);
                newEditor.SetGenerator(master.VoxelGenerator);
            }
            
            // Replace the current editor
            if (newEditor != null)
            {
                generatorEditor?.OnDisable();
                generatorEditor = newEditor;
                generatorEditor.OnEnable();
            }
        }

        private void CreateAndAssignGenerator<T>(string assetName) where T : ScriptableObject, IVoxelGenerator
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Save Voxel Generator",
                assetName,
                "asset",
                "Choose where to save the voxel generator asset");

            if (!string.IsNullOrEmpty(path))
            {
                var generator = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(generator, path);
                AssetDatabase.SaveAssets();
                
                Undo.RecordObject(master, "Assign Voxel Generator");
                master.VoxelGenerator = generator;
                
                UpdateGeneratorEditorForCurrentGenerator();
                
                EditorUtility.SetDirty(master);
                EditorGUIUtility.PingObject(generator);
            }
        }

        public void OnInspectorGUILightingTab()
        {
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Lightmap support", EditorStyles.boldLabel);
            EditorGUILayout.Space();
#if UNITY_6000_0_OR_NEWER && (USING_URP || USING_HDRP)
            var isRuntime = FindFirstObjectByType<ADiggerRuntimeMonoBehaviour>() != null;
            if (isRuntime) {
                EditorGUILayout.HelpBox("Lightmapping support is disabled because the Digger Master Runtime component has been detected in the scene (hence, Digger is setup for runtime usage).\n\n" +
                                        "If you do not need to use Digger at runtime, just remove the Digger Master Runtime component from the scene to enable lightmapping support.",
                    MessageType.Info);
                foreach (var terrain in diggerSystems.Select(diggerSystem => diggerSystem.Terrain)) {
                    if (GameObjectUtility.GetStaticEditorFlags(terrain.gameObject).HasFlag(StaticEditorFlags.ContributeGI))
                    {
                        EditorGUILayout.HelpBox("Contribute GI is enabled on the terrain itself. You probably want to disable it when using Digger at runtime.", MessageType.Warning);
                        break;
                    }
                }
            }
            else
            {
                var helpBoxStyle = GUI.skin.GetStyle("HelpBox");
                helpBoxStyle.richText = true;
                EditorGUILayout.TextArea("Lightmapping is now supported.\n\n<b>Always press the button below before baking light</b> in order to generate secondary UV sets and prepare Digger meshes for lightmapping.\n\n" +
                                         "Note: Unity terrains use very-low-resolution lightmaps, which may result in slightly noticeable lighting differences between the terrain surface and Digger meshes in certain areas. ", helpBoxStyle);
                EditorGUILayout.HelpBox("It is required to press the button below before " +
                                        "baking light with the Unity's 'Generate Lighting' button.\n\n" +
                                        "You must do this everytime, unless no change has been made with the Digger tool.", MessageType.Warning);
                if (GUILayout.Button(new GUIContent("Prepare for lightmapping",
                        "This must be done before generating lighting."), GUILayout.Height(30))) {
                    PrepareLightmapping();
                }
                foreach (var terrain in diggerSystems.Select(diggerSystem => diggerSystem.Terrain)) {
                    if (!GameObjectUtility.GetStaticEditorFlags(terrain.gameObject).HasFlag(StaticEditorFlags.ContributeGI))
                    {
                        EditorGUILayout.HelpBox("Contribute GI is disabled on the terrain itself. You probably want to enable it when using lightmaps with Digger.", MessageType.Warning);
                        break;
                    }
                }
            }
#else
            EditorGUILayout.HelpBox("Lightmapping is only available in Unity 6+ with URP or HDRP.",
                MessageType.Info);
            foreach (var terrain in diggerSystems.Select(diggerSystem => diggerSystem.Terrain)) {
                if (GameObjectUtility.GetStaticEditorFlags(terrain.gameObject).HasFlag(StaticEditorFlags.ContributeGI))
                {
                    EditorGUILayout.HelpBox("Contribute GI is enabled on the terrain itself. You probably want to disable it when using Digger without lightmap support.", MessageType.Warning);
                    break;
                }
            }
#endif
        }

        public void OnInspectorGUISettingsTab()
        {
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            
            // ===== NEW FEATURE HIGHLIGHT =====
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.8f, 1f, 0.3f); // Light blue tint
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                GUI.backgroundColor = originalColor;
                
                EditorGUILayout.BeginHorizontal();
                var boldStyle = new GUIStyle(EditorStyles.boldLabel);
                boldStyle.normal.textColor = new Color(0.2f, 0.6f, 1f);
                EditorGUILayout.LabelField("✨ NEW FEATURE", boldStyle);
                EditorGUILayout.EndHorizontal();
                
                master.AutoRemoveFloatingVoxels = EditorGUILayout.Toggle("Auto remove floating voxels", master.AutoRemoveFloatingVoxels);
                if (master.AutoRemoveFloatingVoxels) {
                    EditorGUI.indentLevel++;
                    master.MaxFloatingVoxelGroupSizeToRemove = EditorGUILayout.IntSlider(
                        new GUIContent("Max size to remove",
                            "Floating voxel groups with GreatestSideLength (in voxels) smaller than this value will be automatically removed. " +
                            "Larger floating islands will be kept (useful for intentional floating platforms)."),
                        master.MaxFloatingVoxelGroupSizeToRemove, 1, 100);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.HelpBox("When enabled, Digger automatically removes floating voxels from the terrain, " +
                    "preventing disconnected blocks from remaining after digging operations. " +
                    "Use 'Max size to remove' to keep larger floating structures.",
                    MessageType.Info);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            
            // ===== DATA MANAGEMENT =====
            DrawGroupHeader("Data Management");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                master.SceneDataFolder = EditorGUILayout.TextField("Scene data folder", master.SceneDataFolder);
                EditorGUILayout.HelpBox($"Digger data for this scene can be found in {master.SceneDataPath}",
                    MessageType.Info);
                EditorGUILayout.HelpBox(
                    "Don\'t forget to backup this folder (including the \".internal\" folder) as well when you backup your project.",
                    MessageType.Warning);
                
                EditorGUILayout.Space();
                master.AutoSaveMeshesAsAssets = EditorGUILayout.Toggle("Auto save meshes as Assets", master.AutoSaveMeshesAsAssets);
                EditorGUILayout.HelpBox("When enabled, Digger meshes will be serialized into distinct .asset files on scene save. " +
                    "This makes the scene file smaller (recommended).",
                   MessageType.Info);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // ===== EDITOR SETTINGS =====
            DrawGroupHeader("Editor Settings");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                shortcutsEnabled = EditorGUILayout.Toggle("Enable shortcuts", shortcutsEnabled);
                if (shortcutsEnabled) {
                    EditorGUILayout.HelpBox("Change Brush: B\n" +
                                            "Change Action: N\n" +
                                            "Brush size: keypad - or +\n" +
                                            "Opacity: keypad / or *",
                        MessageType.Info);
                }

                EditorGUILayout.Space();

                var showUnderlyingObjects = EditorGUILayout.Toggle("Show underlying objects", master.ShowUnderlyingObjects);
                if (showUnderlyingObjects != master.ShowUnderlyingObjects) {
                    master.ShowUnderlyingObjects = showUnderlyingObjects;
                    var diggers = FindObjectsByType<DiggerSystem>(FindObjectsSortMode.None);
                    foreach (var digger in diggers) {
                        digger.ShowDebug = true;
                        foreach (Transform child in digger.transform) {
                            child.gameObject.hideFlags = showUnderlyingObjects
                                ? HideFlags.None
                                : HideFlags.HideInHierarchy | HideFlags.HideInInspector;
                        }
                    }

                    EditorApplication.DirtyHierarchyWindowSorting();
                    EditorApplication.RepaintHierarchyWindow();
                    if (showUnderlyingObjects) {
                        EditorUtility.DisplayDialog("Please reload the scene",
                            "You need to reload the scene (or restart Unity) in order to see chunk objects under LOD Groups.",
                            "Ok");
                    }
                }

                EditorGUILayout.HelpBox(
                    "Reveal all objects created by Digger in the hierarchy (as children of your terrain).",
                    MessageType.Info);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // ===== RENDERING SETTINGS =====
            DrawGroupHeader("Rendering Settings");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                var newLayer = EditorGUILayout.LayerField("Layer", master.Layer);
                EditorGUILayout.HelpBox("Layer assigned to meshes/objects generated by Digger.",
                    MessageType.Info);
                if (newLayer != master.Layer && EditorUtility.DisplayDialog(
                        $"Set new layer: {LayerMask.LayerToName(newLayer)}",
                        "Digger must recompute internal chunks for the new layer setting to take effect.\n\n" +
                        "This operation is not destructive, but can be long.\n\n" +
                        "Do you want to proceed?", "Yes", "Cancel")) {
                    master.Layer = newLayer;
                    DoReload();
                }

                EditorGUILayout.Space();
                
                var newTag = EditorGUILayout.TagField("Tag", master.ChunksTag);
                EditorGUILayout.HelpBox("Tag assigned to objects generated by Digger.",
                    MessageType.Info);
                if (newTag != master.ChunksTag && EditorUtility.DisplayDialog(
                        $"Set new tag: {newTag}",
                        "Digger must recompute internal chunks for the new tag setting to take effect.\n\n" +
                        "This operation is not destructive, but can be long.\n\n" +
                        "Do you want to proceed?", "Yes", "Cancel")) {
                    master.ChunksTag = newTag;
                    DoReload();
                }
                
                EditorGUILayout.Space();
                
                var newEnableOcclusionCulling =
                    EditorGUILayout.Toggle("Enable Occlusion Culling", master.EnableOcclusionCulling);
                if (newEnableOcclusionCulling != master.EnableOcclusionCulling && EditorUtility.DisplayDialog(
                        $"{(newEnableOcclusionCulling ? "Enable" : "Disable")} Occlusion Culling",
                        "Digger must recompute internal chunks for the new Occlusion Culling setting to take effect.\n\n" +
                        "This operation is not destructive, but can be long.\n\n" +
                        "Do you want to proceed?", "Yes", "Cancel")) {
                    master.EnableOcclusionCulling = newEnableOcclusionCulling;
                    DoReload();
                }

                EditorGUILayout.HelpBox(
                    "Note: No effect if DiggerMasterRuntime is present (chunks are not static in runtime mode).",
                    MessageType.Info);
                
                EditorGUILayout.Space();
                
                var newEnableContributeGI =
                    EditorGUILayout.Toggle("Contribute GI", master.EnableContributeGI);
                if (newEnableContributeGI != master.EnableContributeGI && EditorUtility.DisplayDialog(
                        $"{(newEnableContributeGI ? "Enable" : "Disable")} Contribute GI",
                        "Digger must recompute internal chunks for the new Contribute GI setting to take effect.\n\n" +
                        "This operation is not destructive, but can be long.\n\n" +
                        "Do you want to proceed?", "Yes", "Cancel")) {
                    master.EnableContributeGI = newEnableContributeGI;
                    DoReload();
                }

                EditorGUILayout.HelpBox(
                    "Note: No effect if DiggerMasterRuntime is present (chunks are not static in runtime mode).",
                    MessageType.Info);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // ===== PERFORMANCE & QUALITY =====
            DrawGroupHeader("Performance & Quality Settings");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                var newChunkSize = EditorGUILayout.IntPopup("Chunk size", master.ChunkSize, new[] { "16", "32", "64" },
                    new[] { 17, 33, 65 });
                EditorGUILayout.HelpBox(
                    "Lower chunk size = better editing performance but more meshes. Requires clearing all modifications.",
                    MessageType.Info);
                if (newChunkSize != master.ChunkSize && EditorUtility.DisplayDialog("Change chunk size & clear everything",
                        "All modifications must be cleared for new chunk size to take effect.\n\n" +
                        "THIS WILL CLEAR ALL MODIFICATIONS MADE WITH DIGGER.\n\n" +
                        "Terrain holes will be removed, but unlike undo (Ctrl+Z), details objects and trees that were removed by Digger won't be restored.\n\n" +
                        "This operation CANNOT BE UNDONE.\n\n" +
                        "Are you sure you want to proceed?", "Yes, clear it", "Cancel")) {
                    master.ChunkSize = newChunkSize;
                    DoClear();
                }

                EditorGUILayout.Space();
                
                var newResolutionMult = EditorGUILayout.IntPopup("Resolution", master.ResolutionMult,
                    new[] { "x1", "x2", "x4", "x8" }, new[] { 1, 2, 4, 8 });
                EditorGUILayout.HelpBox(
                    "Higher resolution = finer details but more performance cost. Use x2/x4/x8 for low-res heightmaps. Requires clearing.",
                    MessageType.Info);
                if (newResolutionMult != master.ResolutionMult && EditorUtility.DisplayDialog(
                        "Change resolution & clear everything",
                        "All modifications must be cleared for new resolution to take effect.\n\n" +
                        "THIS WILL CLEAR ALL MODIFICATIONS MADE WITH DIGGER.\n\n" +
                        "Terrain holes will be removed, but unlike undo (Ctrl+Z), details objects and trees that were removed by Digger won't be restored.\n\n" +
                        "This operation CANNOT BE UNDONE.\n\n" +
                        "Are you sure you want to proceed?", "Yes, clear it", "Cancel")) {
                    master.ResolutionMult = newResolutionMult;
                    DoClear();
                }

                EditorGUILayout.Space();
                
                var newAutoVoxelHeight = EditorGUILayout.Toggle("Auto Voxel Height", master.AutoVoxelHeight);
                if (!master.AutoVoxelHeight) {
                    var newVoxelHeight = EditorGUILayout.DelayedFloatField("Voxel Height", master.VoxelHeight);
                    if (!Mathf.Approximately(newVoxelHeight, master.VoxelHeight) && EditorUtility.DisplayDialog(
                            "Change voxel height & clear everything",
                            "All modifications must be cleared for new voxel height to take effect.\n\n" +
                            "THIS WILL CLEAR ALL MODIFICATIONS MADE WITH DIGGER.\n\n" +
                            "Terrain holes will be removed, but unlike undo (Ctrl+Z), details objects and trees that were removed by Digger won't be restored.\n\n" +
                            "This operation CANNOT BE UNDONE.\n\n" +
                            "Are you sure you want to proceed?", "Yes, clear it", "Cancel")) {
                        master.VoxelHeight = newVoxelHeight;
                        DoClear();
                    }
                }
                if (newAutoVoxelHeight != master.AutoVoxelHeight && EditorUtility.DisplayDialog(
                        "Change voxel height & clear everything",
                        "All modifications must be cleared for new voxel height to take effect.\n\n" +
                        "THIS WILL CLEAR ALL MODIFICATIONS MADE WITH DIGGER.\n\n" +
                        "Terrain holes will be removed, but unlike undo (Ctrl+Z), details objects and trees that were removed by Digger won't be restored.\n\n" +
                        "This operation CANNOT BE UNDONE.\n\n" +
                        "Are you sure you want to proceed?", "Yes, clear it", "Cancel")) {
                    master.AutoVoxelHeight = newAutoVoxelHeight;
                    DoClear();
                }

                EditorGUILayout.HelpBox(
                    "Auto mode adjusts voxel height based on heightmap resolution. Manual mode allows custom height. Requires clearing.",
                    MessageType.Info);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // ===== ADVANCED FEATURES =====
            DrawGroupHeader("Advanced Features");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                master.ForceMicroSplatMaterialAssetUpdate = EditorGUILayout.Toggle("Force MicroSplat Material Update", master.ForceMicroSplatMaterialAssetUpdate);
                EditorGUILayout.HelpBox(
                    "Forces MicroSplat material assets to be updated and re-imported on each sync. Slows the editor but may fix material asset issues.",
                    MessageType.Info);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // ===== LOD SETTINGS =====
            DrawGroupHeader("LOD Settings");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                var newCreateLODs = EditorGUILayout.Toggle("Enable LODs generation", master.CreateLODs);
                if (newCreateLODs != master.CreateLODs && EditorUtility.DisplayDialog(
                        $"{(newCreateLODs ? "Enable" : "Disable")} LODs generation",
                        "Digger must recompute internal chunks for the new LODs generation setting to take effect.\n\n" +
                        "This operation is not destructive, but can be long.\n\n" +
                        "Do you want to proceed?", "Yes", "Cancel")) {
                    master.CreateLODs = newCreateLODs;
                    DoReload();
                }

                if (master.CreateLODs) {
                    if (FindFirstObjectByType<ADiggerRuntimeMonoBehaviour>()) {
                        EditorGUILayout.HelpBox(
                            "It is recommended to disable LODs generation when using Digger at runtime to improve generation speed.",
                            MessageType.Warning);
                    }

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Screen Relative Transition Height:", EditorStyles.miniBoldLabel);
                    master.ScreenRelativeTransitionHeightLod0 = EditorGUILayout.Slider("    LOD 0",
                        master.ScreenRelativeTransitionHeightLod0, 0.002f, 0.9f);
                    master.ScreenRelativeTransitionHeightLod1 = EditorGUILayout.Slider("    LOD 1",
                        master.ScreenRelativeTransitionHeightLod1, 0.001f,
                        master.ScreenRelativeTransitionHeightLod0 - 0.001f);
                    
                    EditorGUILayout.Space();
                    master.ColliderLodIndex = EditorGUILayout.IntSlider(
                        new GUIContent("Collider LOD",
                            "LOD that will hold the collider. Increasing it will produce mesh colliders with fewer vertices but also less accuracy."),
                        master.ColliderLodIndex, 0, 2);
                }
            }
            EditorGUILayout.EndVertical();

            if (EditorGUI.EndChangeCheck()) {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            EditorGUILayout.Space();
            OnInspectorGUIClearButtons();
        }
        
        private void DrawGroupHeader(string title)
        {
            var style = new GUIStyle(EditorStyles.boldLabel);
            style.fontSize = 12;
            style.normal.textColor = EditorGUIUtility.isProSkin 
                ? new Color(0.8f, 0.8f, 0.8f) 
                : new Color(0.2f, 0.2f, 0.2f);
            EditorGUILayout.LabelField(title, style);
        }

        public void OnInspectorGUIEditTab()
        {
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editing", EditorStyles.boldLabel);

            HandleShortcuts();

            if (!AllTerrainsHaveSameLayers()) {
                EditorGUILayout.HelpBox(
                    "There are multiple terrains in your scene and some have different layers.\n\n" +
                    "For Digger to work properly, all terrains must have the exact same layers defined in the same order.",
                    MessageType.Warning);
                EditorGUILayout.Space();
            }

            var idx = EditorGUILayout.Popup("Operation", selectedOperationEditorIndex,
                operationEditors.Select(type => type.GetCustomAttribute(typeof(OperationAttr)) is OperationAttr attr ? attr.Name : type.Name).ToArray());
            if (idx != selectedOperationEditorIndex) {
                ChangeSelectedOperation(idx);
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            operationEditor?.OnInspectorGUI();

            EditorGUILayout.Space();
            OnInspectorGUIClearButtons();
        }

        private bool AllTerrainsHaveSameLayers()
        {
            if (diggerSystems == null || diggerSystems.Length <= 1)
                return true;

            var textures = diggerSystems[0].TerrainTextures;
            if (textures == null)
                return false;

            foreach (var diggerSystem in diggerSystems) {
                if (diggerSystem.TerrainTextures == null || textures.Length != diggerSystem.TerrainTextures.Length)
                    return false;
                if (textures.Where((t, i) => t != diggerSystem.TerrainTextures[i]).Any()) {
                    return false;
                }
            }

            return true;
        }

        private void ChangeSelectedOperation(int newIndex)
        {
            selectedOperationEditorIndex = newIndex;
            operationEditor?.OnDisable();
            operationEditor = (IOperationEditor)Activator.CreateInstance(operationEditors[selectedOperationEditorIndex]);
            operationEditor.OnEnable();
            SceneView.RepaintAll();
        }

        private void OnSceneGUI()
        {
            operationEditor?.OnSceneGUI();
        }

        public static int TextureSelector(int textureIndex, DiggerSystem diggerSystem)
        {
            GUIStyle gridList = "GridList";
            var errorMessage = new GUIContent("No texture to display.\n\n" +
                                              "You have to add at least one layer to the terrain with " +
                                              "both a texture and a normal map. Then, click on 'Sync & Refresh'.");

            return EditorUtils.AspectSelectionGrid(textureIndex, diggerSystem.TerrainTextures, 64,
                gridList, errorMessage);
        }

        private void HandleShortcuts()
        {
            if (!shortcutsEnabled)
                return;

            var current = Event.current;
            if (current.type != EventType.KeyDown)
                return;

            if (current.keyCode == KeyCode.N) {
                var idx = (selectedOperationEditorIndex + 1) % operationEditors.Count;
                ChangeSelectedOperation(idx);
            }
        }

        private void OnScene(SceneView sceneview)
        {
            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;
            if (e.type == EventType.Layout || e.type == EventType.Repaint) {
                HandleUtility.AddDefaultControl(controlId);
                return;
            }

            if (operationEditor == null)
                return;
            operationEditor.OnScene(this, sceneview).GetAwaiter().GetResult();
            HandleUtility.Repaint();
        }

        private void OnInspectorGUIClearButtons(bool withSync = true, bool withClear = true, string syncLabel = "Sync with terrain(s) & Refresh", string clearLabel = "Clear")
        {
            var col = GUI.backgroundColor;
            EditorGUILayout.LabelField("Global Actions", EditorStyles.boldLabel);
            GUI.backgroundColor = new Color(0.47f, 1f, 0.46f);
            var doReload = withSync && GUILayout.Button(new GUIContent(syncLabel, "Synchronize Digger with terrain data and recompute " +
                                                                                             "all modifications made with Digger. Press this button if you made any " +
                                                                                             "change to your terrain(s), like raising or lowering height, changing textures, etc."), GUILayout.Height(30));
            if (doReload) {
                DoReload();
            }

            GUI.backgroundColor = new Color(1f, 0.55f, 0.57f);
            var doClear = withClear && GUILayout.Button(new GUIContent(clearLabel, "Clear all modifications made with Digger."), GUILayout.Height(30)) && EditorUtility.DisplayDialog("Clear",
                "This will clear all modifications made with Digger.\n" +
                "This operation CANNOT BE UNDONE.\n\n" +
                "Terrain holes will be removed, but unlike undo (Ctrl+Z), details objects and trees that were removed by Digger won't be restored.\n\n" +
                "Are you sure you want to proceed?", "Yes, clear it", "Cancel");
            if (doClear) {
                DoClear();
            }

            GUI.backgroundColor = col;
        }

        private static void DoClear()
        {
            var diggers = FindObjectsByType<DiggerSystem>(FindObjectsSortMode.None);

            try {
                AssetDatabase.StartAssetEditing();
                foreach (var digger in diggers) {
                    digger.Clear();
                }
            } finally {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();

            try {
                AssetDatabase.StartAssetEditing();
                foreach (var digger in diggers) {
                    DiggerSystemEditor.Init(digger, true);
                    Undo.ClearUndo(digger);
                }
            } finally {
                AssetDatabase.StopAssetEditing();
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            GUIUtility.ExitGUI();
        }

        private static void DoReload()
        {
            var diggers = FindObjectsByType<DiggerSystem>(FindObjectsSortMode.None);
            try {
                AssetDatabase.StartAssetEditing();
                foreach (var digger in diggers) {
                    DiggerSystemEditor.Init(digger, true);
                    Undo.ClearUndo(digger);
                }
            } finally {
                AssetDatabase.StopAssetEditing();
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            GUIUtility.ExitGUI();
        }


        public static RaycastHit? GetIntersectionWithTerrainOrDigger(Ray ray)
        {
            if (Physics.Raycast(ray, out var hit, RaycastLength, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore)) {
                return hit;
            }

            return null;
        }

        public static RaycastHit? GetIntersectionWithTerrain(Ray ray)
        {
            var hits = Physics.RaycastAll(ray, RaycastLength, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            foreach (var hit in hits) {
                if (hit.transform.GetComponent<Terrain>() != null) {
                    return hit;
                }
            }

            return null;
        }

        [MenuItem("Tools/Digger/Setup terrains", false, 1)]
        public static void SetupTerrains()
        {
            if (!FindFirstObjectByType<DiggerMaster>()) {
                var goMaster = new GameObject("Digger Master");
                goMaster.transform.localPosition = Vector3.zero;
                goMaster.transform.localRotation = Quaternion.identity;
                goMaster.transform.localScale = Vector3.one;
                var master = goMaster.AddComponent<DiggerMaster>();
                master.CreateDirs();
            }
            
            DiggerSystemEditor.ImportShaders();
            var terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            try {
                AssetDatabase.StartAssetEditing();

                foreach (var terrain in terrains) {
                    var existingDiggers = terrain.gameObject.GetComponentsInChildren<DiggerSystem>();
                    if (existingDiggers.Count(system => system.Terrain.GetInstanceID() == terrain.GetInstanceID()) ==
                        0) {
                        var go = new GameObject("Digger");
                        go.transform.parent = terrain.transform;
                        go.transform.localPosition = Vector3.zero;
                        go.transform.localRotation = Quaternion.identity;
                        go.transform.localScale = Vector3.one;
                        var digger = go.AddComponent<DiggerSystem>();
                        DiggerSystemEditor.Init(digger, true);
                    }
                }
            } finally {
                AssetDatabase.StopAssetEditing();
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }


        [MenuItem("Tools/Digger/Remove Digger from the scene", false, 4)]
        public static void RemoveDiggerFromTerrains()
        {
            var confirm = EditorUtility.DisplayDialog("Remove Digger from the scene",
                "You are about to completely remove Digger from the scene and clear all related Digger data.\n" +
                "Terrain holes will be removed, but unlike undo (Ctrl+Z), details objects and trees won't be restored.\n\n" +
                "This operation CANNOT BE UNDONE.\n\n" +
                "Are you sure you want to proceed?", "Yes, remove Digger", "Cancel");
            if (!confirm)
                return;

            var terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            foreach (var terrain in terrains) {
                var digger = terrain.gameObject.GetComponentInChildren<DiggerSystem>();
                if (digger) {
                    digger.Clear();
                    DestroyImmediate(digger.gameObject);
                }
            }

            var diggerMasters = FindObjectsByType<ADiggerMonoBehaviour>(FindObjectsSortMode.None);
            foreach (var diggerMaster in diggerMasters) {
                DestroyImmediate(diggerMaster.gameObject);
            }

            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        public static void LoadAllChunks(Scene scene)
        {
            var diggers = FindObjectsOfTypeInScene<DiggerSystem>(scene);
            foreach (var diggerSystem in diggers) {
                DiggerSystemEditor.Init(diggerSystem, false);
                Undo.ClearUndo(diggerSystem);
            }
        }

        public static void OnEnterPlayMode(Scene scene)
        {
            var diggers = FindObjectsOfTypeInScene<DiggerSystem>(scene);
            foreach (var digger in diggers) {
                Undo.ClearUndo(digger);
                Undo.ClearUndo(digger.Terrain);
            }

            var cutters = FindObjectsOfTypeInScene<TerrainCutter>(scene);
            foreach (var cutter in cutters) {
                cutter.OnEnterPlayMode();
            }
        }

        private static List<T> FindObjectsOfTypeInScene<T>(Scene scene) where T : MonoBehaviour
        {
            var list = FindObjectsByType<T>(FindObjectsSortMode.None);
            var result = new List<T>();
            foreach (var item in list) {
                if (item.gameObject.scene == scene) {
                    result.Add(item);
                }
            }
            return result;
        }

        [MenuItem("Tools/Digger/Save meshes as assets", false, 30)]
        public static void SaveMeshesAsAssets()
        {
            SaveMeshesAsAssetsInternal();
        }
        
#if UNITY_6000_0_OR_NEWER && (USING_URP || USING_HDRP)
        [MenuItem("Tools/Digger/Prepare lightmapping", false, 30)]
        public static void PrepareLightmapping()
        {
            if (FindFirstObjectByType<ADiggerRuntimeMonoBehaviour>())
            {
                EditorUtility.DisplayDialog("Lightmapping is disabled",
                    "The Digger Master Runtime component has been detected in the scene.\n\n" +
                    "Consequently, Digger is setup for runtime usage and lightmapping support is disabled. If you do not need to use Digger at runtime, " +
                    "just remove the Digger Master Runtime component from your scene.", "Ok");
                return;
            }
            var diggers = FindObjectsByType<DiggerSystem>(FindObjectsSortMode.None);
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var digger in diggers)
                {
                    digger.UpdateStaticEditorFlags();
                    digger.PrepareLightmapping();
                }
            }
            finally
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.StopAssetEditing();
            }
            
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
#endif

        private static void SaveMeshesAsAssetsInternal()
        {
            var paths = new List<string>();
            var diggers = FindObjectsByType<DiggerSystem>(FindObjectsSortMode.None);
            foreach (var digger in diggers)
            {
                paths.Add(digger.BasePathData);
            }

            AssetDatabase.ForceReserializeAssets(paths);
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var digger in diggers)
                {
                    digger.SaveMeshesAsAssets();
                }
            }
            finally
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [MenuItem("Tools/Digger/Check Digger data", false, 31)]
        public static void CheckDiggerVersion()
        {
            var warned = false;
            var diggers = FindObjectsByType<DiggerSystem>(FindObjectsSortMode.None);
            foreach (var digger in diggers) {
                var ver = digger.GetDiggerVersion();
                if (ver != DiggerSystem.DiggerVersion) {
                    if (!warned) {
                        warned = true;
                        EditorUtility.DisplayDialog("New Digger version",
                            "Looks like Digger was updated. Digger is going to synchronize and reload all its data " +
                            "to ensure compatibility. This may take a while.\n\nDon't forget to save your scene once this is done.",
                            "Ok");
                    }

                    // ensure retro-compatibility before 4.0
                    if (ver < 40 || ver >= 10000 && ver < 10040) {
                        var diggerMaster = FindFirstObjectByType<DiggerMaster>();
                        if (diggerMaster) {
                            diggerMaster.AutoVoxelHeight = false;
                            diggerMaster.VoxelHeight = 1f;
                        }
                    }

                    DiggerSystemEditor.Init(digger, true);
                    Undo.ClearUndo(digger);
                }
            }

            if (warned) {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Undo.ClearAll();
                EditorUtility.DisplayDialog("Digger successfully updated",
                        "Digger successfully updated. Please, SAVE YOUR SCENE NOW.\n\n" +
                        "Release note:\n\n" +
                        "- add lightmapping support for Unity 6+ URP|HDRP\n" +
                        "- bug fixes",
                        "Ok");
            }
        }

        public static GameObject LoadAssetWithLabel(string label)
        {
            var guids = AssetDatabase.FindAssets($"l:{label}");
            if (guids == null || guids.Length == 0) {
                return null;
            }

            // we loop but there should be only one item in the list
            foreach (var guid in guids) {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var labels = AssetDatabase.GetLabels(asset);
                if (labels != null && labels.Contains(label)) {
                    return asset;
                }
            }

            return null;
        }


        public static string GetReticleLabel(string label)
        {
            if (GraphicsSettings.defaultRenderPipeline == null) {
                return label;
            }

            if (GraphicsSettings.defaultRenderPipeline.name.Contains("HDRenderPipeline") || GraphicsSettings.defaultRenderPipeline.name.Contains("HDRP")) {
                return label + "HDRP";
            }

            return label + "SRP";
        }

        private void OnBeforeAssemblyReload()
        {
            NativeCollectionsPool.Instance.Dispose();
        }

        private void OnAfterAssemblyReload()
        {
            CheckDiggerVersion();
        }
    }
}