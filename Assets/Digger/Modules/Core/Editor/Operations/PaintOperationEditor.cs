using System;
using System.Linq;
using Digger.Modules.Core.Sources;
using Digger.Modules.Core.Sources.Jobs;
using Digger.Modules.Core.Sources.Operations;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Digger.Modules.Core.Editor.Operations
{
    [OperationAttr("Paint", 3)]
    public class PaintOperationEditor : ABasicOperationEditor, IOperationEditor
    {
        private readonly BasicOperation basicOperation = new BasicOperation();

        private MicroSplatPaintType paintType {
            get => (MicroSplatPaintType)EditorPrefs.GetInt("diggerMaster_microSplatPaintType",
                (int)MicroSplatPaintType.Texture);
            set => EditorPrefs.SetInt("diggerMaster_microSplatPaintType", (int)value);
        }

        private bool paintSettingsFoldout {
            get => EditorPrefs.GetBool("PaintOperationEditor_paintSettingsFoldout", true);
            set => EditorPrefs.SetBool("PaintOperationEditor_paintSettingsFoldout", value);
        }

        private bool operationSettingsFoldout {
            get => EditorPrefs.GetBool("PaintOperationEditor_operationSettingsFoldout", true);
            set => EditorPrefs.SetBool("PaintOperationEditor_operationSettingsFoldout", value);
        }

        private bool destructabilityFoldout {
            get => EditorPrefs.GetBool("PaintOperationEditor_destructabilityFoldout", true);
            set => EditorPrefs.SetBool("PaintOperationEditor_destructabilityFoldout", value);
        }

        private bool reticleConstraintsFoldout {
            get => EditorPrefs.GetBool("PaintOperationEditor_reticleConstraintsFoldout", false);
            set => EditorPrefs.SetBool("PaintOperationEditor_reticleConstraintsFoldout", value);
        }

        private TerrainMaterialType MaterialType {
            get { return diggerSystems.Select(digger => digger.MaterialType).FirstOrDefault(); }
        }

        public void OnInspectorGUI()
        {
            var diggerSystem = Object.FindFirstObjectByType<DiggerSystem>();
            if (!diggerSystem)
                return;

            // Paint Type Settings Section
            if (MaterialType == TerrainMaterialType.MicroSplat) {
                paintSettingsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(paintSettingsFoldout, "Paint Settings");
                if (paintSettingsFoldout)
                {
                    EditorGUI.indentLevel++;
                    paintType = (MicroSplatPaintType)EditorGUILayout.EnumPopup("Type", paintType);
                    opacityIsTarget = EditorGUILayout.Toggle("Opacity is target", opacityIsTarget);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
                EditorGUILayout.Space();
            } else {
                paintType = MicroSplatPaintType.Texture;
                opacityIsTarget = false;
            }

            BrushInspectorGUI();

            EditorGUILayout.Space();

            // Texture Section (always visible)
            if (paintType == MicroSplatPaintType.Texture) {
                EditorGUILayout.LabelField("Texture", EditorStyles.boldLabel);
                textureIndex = DiggerMasterEditor.TextureSelector(textureIndex, diggerSystem);
                
                EditorGUILayout.BeginHorizontal();
                var boldStyle = new GUIStyle(EditorStyles.boldLabel);
                boldStyle.normal.textColor = new Color(0.2f, 0.6f, 1f);
                EditorGUILayout.LabelField("✨ NEW FEATURE:", boldStyle);
                EditorGUILayout.EndHorizontal();
                isIndestructible = EditorGUILayout.Toggle(new GUIContent("Is indestructible"), isIndestructible);

                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }

            // Operation Settings Section
            operationSettingsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(operationSettingsFoldout, "Operation Settings");
            if (operationSettingsFoldout)
            {
                EditorGUI.indentLevel++;
                opacity = EditorGUILayout.Slider(new GUIContent("Opacity", DiggerMasterEditor.shortcutsEnabled ? "Shortcut: keypad / or *" : ""), opacity, 0f, 1f);
                depth = EditorGUILayout.Slider("Depth", depth, -size.y, size.y);
                
                if (!opacityIsTarget) {
                    EditorGUILayout.HelpBox(
                        "Hold Ctrl to remove the texture instead of adding it.",
                        MessageType.Info);
                }
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
                    "Use 'Bypass Destructability' to ignore existing destructability values when painting.",
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
                Action = ActionType.Paint,
                TextureIndex = GetFixedTextureIndex(),
                Opacity = Event.current.control ? -opacity : opacity,
                Size = size,
                StalagmiteUpsideDown = upsideDown,
                OpacityIsTarget = opacityIsTarget,
                IsIndestructible = isIndestructible,
                BypassDestructability = bypassDestructability,
                Callback = null
            };

            basicOperation.Params = parameters;
            return basicOperation;
        }

        private int GetFixedTextureIndex()
        {
            if (paintType == MicroSplatPaintType.Wetness) {
                return 28;
            } else if (paintType == MicroSplatPaintType.Puddles) {
                return 29;
            } else if (paintType == MicroSplatPaintType.Stream) {
                return 30;
            } else {
                return textureIndex;
            }
        }
    }
}