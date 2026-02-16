using UnityEditor;
using UnityEngine;

namespace Kellojo.StylizedKelp.Editor
{
    [CustomEditor(typeof(StylizedKelpRenderer))]
    public class StylizedKelpRendererEditor : UnityEditor.Editor {
        public Texture2D banner;
        
        public override void OnInspectorGUI() {
            DrawBanner(banner);
            DrawDefaultInspector();
            
            StylizedKelpRenderer renderer = (StylizedKelpRenderer)target;
            RenderDebugInfo(renderer);
        }
        
        void RenderDebugInfo(StylizedKelpRenderer renderer) {
            GUI.enabled = false;
            EditorGUILayout.Toggle("Initialized", renderer.isInitialized);
            EditorGUILayout.IntField("Active Kelp Instances", renderer.activeKelp);
            EditorGUILayout.IntField("Active Kelp Stem Segments", renderer.activeKelpStems);
            EditorGUILayout.IntField("Active Kelp Leaves", renderer.activeKelpLeaves);
            GUI.enabled = true;
        }

        public static void DrawBanner(Texture2D banner) {
            if (banner == null) return;

            float aspect = (float)banner.width / banner.height;
            float width = EditorGUIUtility.currentViewWidth;
            float height = width / aspect;

            Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(true));
            
            GUIContent content = new GUIContent("", "View on the Unity Asset Store");
            if (GUI.Button(rect, content, GUIStyle.none)) {
                Application.OpenURL("https://assetstore.unity.com/packages/slug/332396");
            }
            
            GUI.DrawTexture(rect, banner, ScaleMode.ScaleToFit);
        }
    }
}
