using UnityEditor;
using UnityEngine;
using RealSense.DummyPointCloud;

namespace RealSense.Editor
{
    [CustomEditor(typeof(RsDummyPointCloudRenderer))]
    public class RsDummyPointCloudRendererEditor : UnityEditor.Editor
    {
        private SerializedProperty _enableDebugLogProp;
        private SerializedProperty _showGizmosProp;

        private void OnEnable()
        {
            _enableDebugLogProp = serializedObject.FindProperty("enableDebugLog");
            _showGizmosProp = serializedObject.FindProperty("showGizmos");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var renderer = (RsDummyPointCloudRenderer)target;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "RsDummyPointCloudRenderer は RsDummyPointCloudProvider で生成されたダミー点群を" +
                "高速な GPU Procedural 描画でレンダリングします。RsPointCloudRenderer と完全な互換性を持ちます。",
                MessageType.Info);

            EditorGUILayout.Space();
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Info & Render Controls", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledGroupScope(true))
            {
                int currentPoints = renderer.GetLastFilteredCount();
                EditorGUILayout.IntField("Rendered Point Count", currentPoints);

                var buffer = renderer.GetFilteredVerticesBuffer();
                string bufferStatus = buffer != null ? $"Allocated ({buffer.count} elements)" : "None / Unallocated";
                EditorGUILayout.TextField("GPU Buffer State", bufferStatus);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
