using UnityEditor;
using UnityEngine;
using Core.Logging;

namespace Core.Editor
{
    [CustomEditor(typeof(AppLogManager))]
    public class AppLogManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var manager = (AppLogManager)target;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "AppLogManager は、対象コンポーネント (HCD 等) の Inspector を一切汚さず、\n" +
                "本マネージャー上でモジュール機能ごとにフォルダ階層化してログを集中コントロールします。",
                MessageType.Info);

            EditorGUILayout.Space();
            SerializedProperty globalEnableProp = serializedObject.FindProperty("globalEnableLogging");
            EditorGUILayout.PropertyField(globalEnableProp, new GUIContent("Global Enable Logging"));

            SerializedProperty minLogLevelProp = serializedObject.FindProperty("minLogLevel");
            if (minLogLevelProp != null)
            {
                EditorGUILayout.PropertyField(minLogLevelProp, new GUIContent("Minimum Log Level"));
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Log Type Filters", EditorStyles.boldLabel);
            SerializedProperty enableInfoProp = serializedObject.FindProperty("enableInfoLogs");
            SerializedProperty enableWarningProp = serializedObject.FindProperty("enableWarningLogs");
            SerializedProperty enableErrorProp = serializedObject.FindProperty("enableErrorLogs");

            if (enableInfoProp != null) EditorGUILayout.PropertyField(enableInfoProp, new GUIContent("Enable Info Logs (Log)"));
            if (enableWarningProp != null) EditorGUILayout.PropertyField(enableWarningProp, new GUIContent("Enable Warning Logs (LogWarning)"));
            if (enableErrorProp != null) EditorGUILayout.PropertyField(enableErrorProp, new GUIContent("Enable Error Logs (LogError)"));

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔍 Scan Scene Components", GUILayout.Height(30)))
            {
                Undo.RecordObject(manager, "Scan Components");
                manager.ScanSceneComponents();
                EditorUtility.SetDirty(manager);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Enable All Groups"))
            {
                Undo.RecordObject(manager, "Enable All");
                manager.SetAllEnabled(true);
                EditorUtility.SetDirty(manager);
            }
            if (GUILayout.Button("Disable All Groups"))
            {
                Undo.RecordObject(manager, "Disable All");
                manager.SetAllEnabled(false);
                EditorUtility.SetDirty(manager);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Log Target Category Groups", EditorStyles.boldLabel);

            SerializedProperty groupsProp = serializedObject.FindProperty("categoryGroups");
            if (groupsProp != null && groupsProp.arraySize > 0)
            {
                for (int g = 0; g < groupsProp.arraySize; g++)
                {
                    SerializedProperty groupProp = groupsProp.GetArrayElementAtIndex(g);
                    SerializedProperty catNameProp = groupProp.FindPropertyRelative("categoryName");
                    SerializedProperty isExpandedProp = groupProp.FindPropertyRelative("isExpanded");
                    SerializedProperty entriesProp = groupProp.FindPropertyRelative("entries");

                    int count = entriesProp?.arraySize ?? 0;
                    string catName = catNameProp.stringValue;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    // --- Group Header ---
                    EditorGUILayout.BeginHorizontal();
                    isExpandedProp.boolValue = EditorGUILayout.Foldout(isExpandedProp.boolValue, $"📂 {catName} ({count})", true, EditorStyles.foldoutHeader);

                    if (GUILayout.Button("All ON", EditorStyles.miniButtonLeft, GUILayout.Width(55)))
                    {
                        Undo.RecordObject(manager, "Group All ON");
                        manager.SetGroupEnabled(catName, true);
                        EditorUtility.SetDirty(manager);
                    }
                    if (GUILayout.Button("All OFF", EditorStyles.miniButtonRight, GUILayout.Width(55)))
                    {
                        Undo.RecordObject(manager, "Group All OFF");
                        manager.SetGroupEnabled(catName, false);
                        EditorUtility.SetDirty(manager);
                    }
                    EditorGUILayout.EndHorizontal();

                    // --- Group Items ---
                    if (isExpandedProp.boolValue && entriesProp != null)
                    {
                        EditorGUI.indentLevel++;
                        for (int i = 0; i < entriesProp.arraySize; i++)
                        {
                            SerializedProperty entryProp = entriesProp.GetArrayElementAtIndex(i);
                            SerializedProperty labelProp = entryProp.FindPropertyRelative("label");
                            SerializedProperty targetProp = entryProp.FindPropertyRelative("target");
                            SerializedProperty enabledProp = entryProp.FindPropertyRelative("enabled");

                            EditorGUILayout.BeginHorizontal();

                            EditorGUI.BeginChangeCheck();
                            bool newEnabled = EditorGUILayout.Toggle(enabledProp.boolValue, GUILayout.Width(24));
                            if (EditorGUI.EndChangeCheck())
                            {
                                enabledProp.boolValue = newEnabled;
                                serializedObject.ApplyModifiedProperties();
                                manager.BuildLookup();
                            }

                            string labelText = !string.IsNullOrEmpty(labelProp?.stringValue)
                                ? labelProp.stringValue
                                : (targetProp.objectReferenceValue != null ? targetProp.objectReferenceValue.name : "Unassigned");

                            EditorGUILayout.LabelField(labelText, GUILayout.MinWidth(180), GUILayout.MaxWidth(320));
                            EditorGUILayout.PropertyField(targetProp, GUIContent.none);

                            if (GUILayout.Button("X", GUILayout.Width(24)))
                            {
                                entriesProp.DeleteArrayElementAtIndex(i);
                                break;
                            }

                            EditorGUILayout.EndHorizontal();
                        }
                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("ターゲットがありません。[Scan Scene Components] ボタンを押して自動検出してください。", MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                manager.BuildLookup();
            }
        }
    }
}
