using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HapCollisionDetectors))]
public class HapCollisionDetectorsEditor : Editor
{
    private SerializedProperty _csvOutputDirectoryProp;

    private void OnEnable()
    {
        _csvOutputDirectoryProp = serializedObject.FindProperty("csvOutputDirectory");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // csvOutputDirectory 以外は通常通り描画
        DrawPropertiesExcluding(serializedObject, "m_Script", "csvOutputDirectory");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(_csvOutputDirectoryProp);
        if (GUILayout.Button("Choose", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
        {
            string path = EditorUtility.OpenFolderPanel("CSV Output Folder", _csvOutputDirectoryProp.stringValue, "");
            if (path.Length != 0)
            {
                serializedObject.Update();
                _csvOutputDirectoryProp.stringValue = path;
                serializedObject.ApplyModifiedProperties();
                GUI.FocusControl(null);
            }
            GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndHorizontal();

        if (string.IsNullOrEmpty(_csvOutputDirectoryProp.stringValue))
        {
            EditorGUILayout.HelpBox(
                "未指定の場合、Application.persistentDataPath/HapCollisionLogs に保存されます。",
                MessageType.Info);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
