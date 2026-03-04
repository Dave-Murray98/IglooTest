using Digger.Modules.Core.Sources;
using Digger.Modules.Core.Sources.Jobs;
using Digger.Modules.Core.Sources.Operations;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Digger.Modules.Core.Editor.Operations
{
    [OperationAttr("Add", 2)]
    public class AddOperationEditor : ABasicOperationEditor, IScriptableOperationEditor
    {
        private readonly BasicOperation basicOperation = new BasicOperation();

        private bool operationSettingsFoldout {
            get => EditorPrefs.GetBool("AddOperationEditor_operationSettingsFoldout", true);
            set => EditorPrefs.SetBool("AddOperationEditor_operationSettingsFoldout", value);
        }

        private bool destructabilityFoldout {
            get => EditorPrefs.GetBool("AddOperationEditor_destructabilityFoldout", true);
            set => EditorPrefs.SetBool("AddOperationEditor_destructabilityFoldout", value);
        }

        private bool reticleConstraintsFoldout {
            get => EditorPrefs.GetBool("AddOperationEditor_reticleConstraintsFoldout", false);
            set => EditorPrefs.SetBool("AddOperationEditor_reticleConstraintsFoldout", value);
        }

        public void OnInspectorGUI()
        {
            var diggerSystem = Object.FindFirstObjectByType<DiggerSystem>();
            if (!diggerSystem)
                return;

            BrushInspectorGUI();

            EditorGUILayout.Space();

            // Texture Section (always visible)
            EditorGUILayout.LabelField("Texture", EditorStyles.boldLabel);
            textureIndex = DiggerMasterEditor.TextureSelector(textureIndex, diggerSystem);

            EditorGUILayout.BeginHorizontal();
            var boldStyle = new GUIStyle(EditorStyles.boldLabel);
            boldStyle.normal.textColor = new Color(0.2f, 0.6f, 1f);
            EditorGUILayout.LabelField("✨ NEW FEATURE:", boldStyle);
            EditorGUILayout.EndHorizontal();
            isIndestructible = EditorGUILayout.Toggle(new GUIContent("Target becomes indestructible"), isIndestructible);

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // Operation Settings Section
            operationSettingsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(operationSettingsFoldout, "Operation Settings");
            if (operationSettingsFoldout)
            {
                EditorGUI.indentLevel++;
                opacity = EditorGUILayout.Slider(new GUIContent("Opacity", DiggerMasterEditor.shortcutsEnabled ? "Shortcut: keypad / or *" : ""), opacity, 0f, 1f);
                depth = EditorGUILayout.Slider("Depth", depth, -size.y, size.y);
                paintWhileDigging = EditorGUILayout.Toggle("Paint While Modifying", paintWhileDigging);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space();

            // Destructability Section
            destructabilityFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(destructabilityFoldout, "Destructability");
            if (destructabilityFoldout)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.HelpBox(
                    "Use 'Bypass Destructability' to ignore existing destructability values when adding terrain.",
                    MessageType.Info);
                
                bypassDestructability = EditorGUILayout.Toggle("Bypass Destructability", bypassDestructability);
                
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space();

            // Reticle Constraints Section
            reticleConstraintsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(reticleConstraintsFoldout, "Reticle Constraints");
            if (reticleConstraintsFoldout)
            {
                EditorGUI.indentLevel++;
                keepingHeight = EditorGUILayout.ToggleLeft("Constrain reticle to given altitude", keepingHeight);
                keptHeight = EditorGUILayout.FloatField("Reticle constrained altitude", keptHeight);
                EditorGUILayout.HelpBox("Press Shift to pick current reticle height.", MessageType.None);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        public void OnSceneGUI()
        {
        }

        protected override async Awaitable PerformModification(Vector3 p)
        {
            var op = OperationAt(p);
            foreach (var diggerSystem in diggerSystems) {
                await diggerSystem.Modify(op);
            }
        }

        public IOperation<VoxelModificationJob> OperationAt(Vector3 position)
        {
            var parameters = new ModificationParameters
            {
                Position = position,
                Brush = brush,
                Action = ActionType.Add,
                TextureIndex = textureIndex,
                Opacity = opacity,
                Size = size,
                StalagmiteUpsideDown = upsideDown,
                OpacityIsTarget = false,
                CustomBrush = customBrush,
                PaintWhileDigging = paintWhileDigging,
                BypassDestructability = bypassDestructability,
                IsIndestructible = isIndestructible,
                Callback = null
            };

            basicOperation.Params = parameters;
            return basicOperation;
        }
    }
}