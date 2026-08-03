using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CameraRendererSwitcher))]
public class CameraRendererSwitcherEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (target == null) return;

        CameraRendererSwitcher switcher = (CameraRendererSwitcher)target;

        serializedObject.Update();

        // 標準インスペクター描画
        DrawDefaultInspector();

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("Batch Renderer Control", EditorStyles.boldLabel);

        // メインの一括適用ボタン
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
        if (GUILayout.Button($"Apply Renderer Index ({switcher.targetRendererIndex}) To All Cameras", GUILayout.Height(32)))
        {
            Undo.RecordObject(switcher, "Apply Renderer Index");
            switcher.ApplyRendererIndex();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Set Renderer 0 (PCD Renderer)"))
        {
            Undo.RecordObject(switcher, "Set Renderer 0");
            switcher.ApplyRendererIndex(0);
        }
        if (GUILayout.Button("Set Renderer 1 (Default Universal)"))
        {
            Undo.RecordObject(switcher, "Set Renderer 1");
            switcher.ApplyRendererIndex(1);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("Camera List Quick Tools", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Find All Scene Cameras"))
        {
            Undo.RecordObject(switcher, "Find All Scene Cameras");
            switcher.FindAllSceneCameras();
            EditorUtility.SetDirty(switcher);
        }
        if (GUILayout.Button("Find Child Cameras"))
        {
            Undo.RecordObject(switcher, "Find Child Cameras");
            switcher.FindChildCameras();
            EditorUtility.SetDirty(switcher);
        }
        if (GUILayout.Button("Clear Nulls"))
        {
            Undo.RecordObject(switcher, "Clear Null Cameras");
            switcher.RemoveNullEntries();
            EditorUtility.SetDirty(switcher);
        }
        EditorGUILayout.EndHorizontal();

        // リストの状態確認表示
        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("Camera Status Summary", EditorStyles.boldLabel);

        if (switcher.cameras == null || switcher.cameras.Count == 0)
        {
            EditorGUILayout.HelpBox("Camera リストが空です。'Find All Scene Cameras' や 'Find Child Cameras' を実行するか、手動で Camera を追加してください。", MessageType.Info);
        }
        else
        {
            int validCount = 0;
            int nullCount = 0;
            foreach (var cam in switcher.cameras)
            {
                if (cam == null)
                {
                    nullCount++;
                }
                else
                {
                    validCount++;
                }
            }

            string statusMsg = $"Total Cameras: {switcher.cameras.Count} (Valid: {validCount}, Null: {nullCount})";
            if (nullCount > 0)
            {
                EditorGUILayout.HelpBox($"{statusMsg}\n※ Null のカメラ参照が含まれています。'Clear Nulls' で削除できます。", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(statusMsg, MessageType.None);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
