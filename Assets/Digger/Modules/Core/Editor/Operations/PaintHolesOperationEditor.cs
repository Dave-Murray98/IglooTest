using Digger.Modules.Core.Sources;
using Digger.Modules.Core.Sources.Jobs;
using Digger.Modules.Core.Sources.Operations;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Digger.Modules.Core.Editor.Operations
{
    [OperationAttr("Paint Holes", 4)]
    public class PaintHolesOperationEditor : ABasicOperationEditor, IOperationEditor
    {
        private readonly BasicOperation basicOperation = new BasicOperation();

        private bool reticleConstraintsFoldout {
            get => EditorPrefs.GetBool("PaintHolesOperationEditor_reticleConstraintsFoldout", false);
            set => EditorPrefs.SetBool("PaintHolesOperationEditor_reticleConstraintsFoldout", value);
        }

        public void OnInspectorGUI()
        {
            var diggerSystem = Object.FindFirstObjectByType<DiggerSystem>();
            if (!diggerSystem)
                return;

            BrushInspectorGUI();

            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Paint Holes creates invisible areas in the terrain. Hold Ctrl to fill holes instead of creating them.",
                MessageType.Info);

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

        private IOperation<VoxelModificationJob> OperationAt(Vector3 position)
        {
            var parameters = new ModificationParameters
            {
                Position = position,
                Brush = brush,
                Action = ActionType.PaintHoles,
                TextureIndex = 0,
                Opacity = Event.current.control ? -1f : 1f,
                Size = size,
                StalagmiteUpsideDown = false,
                OpacityIsTarget = false,
                Callback = null
            };

            basicOperation.Params = parameters;
            return basicOperation;
        }
    }
}