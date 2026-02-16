using UnityEditor;
using UnityEngine;

namespace Kellojo.StylizedKelp.Editor {
    [CustomEditor(typeof(Kelp))]
    public class KelpEditor : UnityEditor.Editor {
        public Texture2D banner;

        public override void OnInspectorGUI() {
            StylizedKelpRendererEditor.DrawBanner(banner);
            DrawDefaultInspector();

            Kelp kelp = (Kelp)target;
            RenderButtons(kelp);
        }

        void RenderButtons(Kelp kelp) {
            EditorGUILayout.Space(15);

            EditorGUILayout.LabelField("Helpers", EditorStyles.boldLabel);
            if (GUILayout.Button("Randomize Length")) kelp.RandomizeLength();
            if (GUILayout.Button("Snap to Ground")) kelp.SnapToGround();
        }
    }
}