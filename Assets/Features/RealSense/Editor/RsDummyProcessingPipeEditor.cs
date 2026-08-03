using UnityEditor;
using UnityEngine;
using RealSense.DummyPointCloud;

namespace RealSense.Editor
{
    [CustomEditor(typeof(RsDummyProcessingPipe))]
    public class RsDummyProcessingPipeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "RsDummyProcessingPipe はダミー実測点群用のプロセッシングパイプラインです。" +
                "RsProcessingPipe を継承しており、実機なしでダミー点群の処理ブロックを管理します。",
                MessageType.Info);

            EditorGUILayout.Space();
            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
