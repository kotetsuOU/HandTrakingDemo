using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PCV_Controller))]
public class PCV_ControllerEditor : Editor
{
    private PCV_Controller controller;
    private SerializedObject settingsObject;
    private PCV_Settings settingsComponent;

    // Profile Management
    private string profileName = "DefaultProfile";
    private bool showProfileSettings = false;

    // Properties
    private SerializedProperty renderingSourceProp;
    private SerializedProperty fileSettingsProp;
    private bool showDataFiles = false;
    void OnEnable()
    {
        controller = (PCV_Controller)target;
        settingsComponent = controller.GetComponent<PCV_Settings>();

        if (settingsComponent != null)
        {
            settingsObject = new SerializedObject(settingsComponent);

            renderingSourceProp = settingsObject.FindProperty("renderingSource");

            fileSettingsProp = settingsObject.FindProperty("fileSettings");
        }
    }

    public override void OnInspectorGUI()
    {
        if (settingsObject == null)
        {
            EditorGUILayout.HelpBox("PCV_Settings component is missing.", MessageType.Error);
            return;
        }
        settingsObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("General Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(renderingSourceProp);
        EditorGUILayout.Space();

        EditorGUILayout.Space();
        showProfileSettings = EditorGUILayout.Foldout(showProfileSettings, "Profile Management (JSON)", true, EditorStyles.foldoutHeader);
        if (showProfileSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Manage Settings Preset", EditorStyles.miniBoldLabel);
            profileName = EditorGUILayout.TextField("Profile Name", profileName);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.7f, 0.8f, 1f);
            if (GUILayout.Button("Save Profile"))
            {
                if (EditorUtility.DisplayDialog("Save Profile",
                    $"Save current settings to '{profileName}.json'?", "Save", "Cancel"))
                {
                    PCV_ConfigIO.SaveConfig(settingsComponent, profileName);
                }
            }

            GUI.backgroundColor = new Color(1f, 0.8f, 0.7f);
            if (GUILayout.Button("Load Profile"))
            {
                if (EditorUtility.DisplayDialog("Load Profile",
                    $"Load settings from '{profileName}.json'?\nCurrent settings will be overwritten.", "Load", "Cancel"))
                {
                    PCV_ConfigIO.LoadConfig(settingsComponent, profileName);
                }
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.Space();

        showDataFiles = EditorGUILayout.Foldout(showDataFiles, "Data Files", true, EditorStyles.foldoutHeader);

        if (showDataFiles)
        {
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(1f, 0.8f, 0.4f);
            if (GUILayout.Button("Apply Transform (Rot & Pos)"))
            {
                if (EditorUtility.DisplayDialog("Apply Transform",
                    "Viewerの現在の位置・回転をターゲットオブジェクトに適用します。\nこの操作は元に戻せません（※ターゲットのTransformはUndo可能ですが、Viewer姿勢はリセットされます）。\n実行しますか？", "Yes", "Cancel"))
                {
                    controller.ApplyTransformCorrection();
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
        if (GUILayout.Button("全ファイル ON")) SetAllFileUsage(true);
        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("全ファイル OFF")) SetAllFileUsage(false);
        EditorGUILayout.EndHorizontal();

        GUI.backgroundColor = new Color(0.8f, 0.8f, 0.6f);
        if (GUILayout.Button("点群の再構築")) controller.RebuildPointCloud();
        GUI.backgroundColor = Color.white;

        if (showDataFiles)
        {
            EditorGUI.indentLevel++;
            DrawFileSettingsList();
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.Space();

        settingsObject.ApplyModifiedProperties();
    }

    private void DrawFileSettingsList()
    {
        EditorGUILayout.LabelField("File Settings List", EditorStyles.boldLabel);
        for (int i = 0; i < fileSettingsProp.arraySize; i++)
        {
            SerializedProperty element = fileSettingsProp.GetArrayElementAtIndex(i);
            SerializedProperty useFile = element.FindPropertyRelative("useFile");
            SerializedProperty filePath = element.FindPropertyRelative("filePath");
            SerializedProperty color = element.FindPropertyRelative("color");
            SerializedProperty useFileColor = element.FindPropertyRelative("useFileColor");
            SerializedProperty targetObject = element.FindPropertyRelative("targetObject");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            useFile.boolValue = EditorGUILayout.ToggleLeft($"File {i + 1} (Use: {useFile.boolValue})", useFile.boolValue, EditorStyles.boldLabel);
            
            if (useFile.boolValue)
            {
                EditorGUI.indentLevel++;

                // File Path Selection row
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(filePath, new GUIContent("File Path"));
                if (GUILayout.Button("Browse", EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    var path = EditorUtility.OpenFilePanel("Select Point Cloud File", "Assets", "txt;ply");
                    if (!string.IsNullOrEmpty(path))
                    {
                        settingsObject.Update();
                        filePath.stringValue = MakeRelativePath(path);
                        settingsObject.ApplyModifiedProperties();
                        GUI.FocusControl(null);
                    }
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(color, new GUIContent("Gizmo Color"));
                EditorGUILayout.PropertyField(useFileColor, new GUIContent("Use File Color (PLY)"));
                EditorGUILayout.PropertyField(targetObject, new GUIContent("Target Object"));

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }
    }

    private void SetAllFileUsage(bool value)
    {
        for (int i = 0; i < fileSettingsProp.arraySize; i++)
        {
            SerializedProperty element = fileSettingsProp.GetArrayElementAtIndex(i);
            SerializedProperty useFile = element.FindPropertyRelative("useFile");
            useFile.boolValue = value;
        }
    }

    private static string MakeRelativePath(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath)) return absolutePath;
        absolutePath = absolutePath.Replace("\\", "/");

        string projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..")).Replace("\\", "/");

        if (absolutePath.StartsWith(projectRoot, System.StringComparison.OrdinalIgnoreCase))
        {
            string relativePath = absolutePath.Substring(projectRoot.Length);
            if (relativePath.StartsWith("/"))
            {
                relativePath = relativePath.Substring(1);
            }
            return relativePath;
        }

        return absolutePath;
    }
}
