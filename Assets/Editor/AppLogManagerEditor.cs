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
            if (GUILayout.Button("Enable All"))
            {
                Undo.RecordObject(manager, "Enable All");
                manager.SetAllEnabled(true);
                EditorUtility.SetDirty(manager);
            }
            if (GUILayout.Button("Disable All"))
            {
                Undo.RecordObject(manager, "Disable All");
                manager.SetAllEnabled(false);
                EditorUtility.SetDirty(manager);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Log ON", EditorStyles.miniButton)) { Undo.RecordObject(manager, "Log All ON"); manager.SetAllEnabled(true, AppLogLevel.Info); EditorUtility.SetDirty(manager); }
            if (GUILayout.Button("Log OFF", EditorStyles.miniButton)) { Undo.RecordObject(manager, "Log All OFF"); manager.SetAllEnabled(false, AppLogLevel.Info); EditorUtility.SetDirty(manager); }
            if (GUILayout.Button("Warn ON", EditorStyles.miniButton)) { Undo.RecordObject(manager, "Warn All ON"); manager.SetAllEnabled(true, AppLogLevel.Warning); EditorUtility.SetDirty(manager); }
            if (GUILayout.Button("Warn OFF", EditorStyles.miniButton)) { Undo.RecordObject(manager, "Warn All OFF"); manager.SetAllEnabled(false, AppLogLevel.Warning); EditorUtility.SetDirty(manager); }
            if (GUILayout.Button("Err ON", EditorStyles.miniButton)) { Undo.RecordObject(manager, "Err All ON"); manager.SetAllEnabled(true, AppLogLevel.Error); EditorUtility.SetDirty(manager); }
            if (GUILayout.Button("Err OFF", EditorStyles.miniButton)) { Undo.RecordObject(manager, "Err All OFF"); manager.SetAllEnabled(false, AppLogLevel.Error); EditorUtility.SetDirty(manager); }
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
                        EditorGUILayout.Space(2);
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(16);

                        if (GUILayout.Button("Log", EditorStyles.miniButton, GUILayout.Width(28)))
                        {
                            Undo.RecordObject(manager, "Toggle Group Log");
                            bool enable = ToggleGroupLevel(groupProp, "enableInfo");
                            manager.SetGroupEnabled(catName, enable, AppLogLevel.Info);
                            EditorUtility.SetDirty(manager);
                        }
                        if (GUILayout.Button("Warn", EditorStyles.miniButton, GUILayout.Width(32)))
                        {
                            Undo.RecordObject(manager, "Toggle Group Warn");
                            bool enable = ToggleGroupLevel(groupProp, "enableWarning");
                            manager.SetGroupEnabled(catName, enable, AppLogLevel.Warning);
                            EditorUtility.SetDirty(manager);
                        }
                        if (GUILayout.Button("Err", EditorStyles.miniButton, GUILayout.Width(28)))
                        {
                            Undo.RecordObject(manager, "Toggle Group Err");
                            bool enable = ToggleGroupLevel(groupProp, "enableError");
                            manager.SetGroupEnabled(catName, enable, AppLogLevel.Error);
                            EditorUtility.SetDirty(manager);
                        }

                        EditorGUILayout.LabelField("Target / Label", EditorStyles.miniBoldLabel);
                        EditorGUILayout.EndHorizontal();

                        EditorGUI.indentLevel++;
                        for (int i = 0; i < entriesProp.arraySize; i++)
                        {
                            SerializedProperty entryProp = entriesProp.GetArrayElementAtIndex(i);
                            SerializedProperty labelProp = entryProp.FindPropertyRelative("label");
                            SerializedProperty targetProp = entryProp.FindPropertyRelative("target");
                            SerializedProperty infoProp = entryProp.FindPropertyRelative("enableInfo");
                            SerializedProperty warnProp = entryProp.FindPropertyRelative("enableWarning");
                            SerializedProperty errProp = entryProp.FindPropertyRelative("enableError");

                            EditorGUILayout.BeginHorizontal();

                            EditorGUI.BeginChangeCheck();
                            bool newInfo = EditorGUILayout.Toggle(infoProp?.boolValue ?? true, GUILayout.Width(28));
                            bool newWarn = EditorGUILayout.Toggle(warnProp?.boolValue ?? true, GUILayout.Width(32));
                            bool newErr = EditorGUILayout.Toggle(errProp?.boolValue ?? true, GUILayout.Width(28));

                            if (EditorGUI.EndChangeCheck())
                            {
                                if (infoProp != null) infoProp.boolValue = newInfo;
                                if (warnProp != null) warnProp.boolValue = newWarn;
                                if (errProp != null) errProp.boolValue = newErr;
                                serializedObject.ApplyModifiedProperties();
                                manager.BuildLookup();
                            }

                            string labelText = !string.IsNullOrEmpty(labelProp?.stringValue)
                                ? labelProp.stringValue
                                : (targetProp.objectReferenceValue != null ? targetProp.objectReferenceValue.name : "Unassigned");

                            EditorGUILayout.LabelField(labelText, GUILayout.MinWidth(140), GUILayout.MaxWidth(280));
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

        private bool ToggleGroupLevel(SerializedProperty groupProp, string propName)
        {
            SerializedProperty entriesProp = groupProp.FindPropertyRelative("entries");
            if (entriesProp == null || entriesProp.arraySize == 0) return true;
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                SerializedProperty entryProp = entriesProp.GetArrayElementAtIndex(i);
                SerializedProperty p = entryProp.FindPropertyRelative(propName);
                if (p != null && !p.boolValue) return true;
            }
            return false;
        }
    }
}
