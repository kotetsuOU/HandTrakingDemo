using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(HAP_AUTDCalibration))]
public class HAP_AUTDCalibrationEditor : Editor
{
    private HAP_AUTDCalibration script;
    private const string PREFS_KEY = "HAP_AUTDCalibration_State";

    private void OnEnable()
    {
        script = (HAP_AUTDCalibration)target;

        // Playモード終了時にEditorPrefsから状態を復元する
        if (!Application.isPlaying)
        {
            RestoreState();
        }
    }

    private void OnDisable()
    {
        // Playモード中なら終了時に備えて状態を保存する
        if (Application.isPlaying)
        {
            SaveState();
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginChangeCheck();

        // ------------------
        // Controller Ref
        // ------------------
        EditorGUILayout.PropertyField(serializedObject.FindProperty("autdController"));
        
        EditorGUILayout.Space();

        // ------------------
        // Calibration Toggle
        // ------------------
        GUI.color = script.enableCalibration ? new Color(1f, 0.6f, 0.6f) : Color.white;
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("enableCalibration"));
        EditorGUILayout.EndVertical();
        GUI.color = Color.white;

        EditorGUILayout.Space();

        // ------------------
        // Target Devices List
        // ------------------
        EditorGUILayout.LabelField("Target Devices (Check to Enable)", EditorStyles.boldLabel);
        
        // シーン内のAUTD3Deviceの数を取得してリストを自動調整
        var devices = FindObjectsByType<AUTD3Device>(FindObjectsSortMode.None);
        int devCount = devices.Length;

        if (devCount > 0)
        {
            if (script.targetDevices.Count != devCount)
            {
                // リストのサイズをデバイス数に合わせる
                while (script.targetDevices.Count < devCount) script.targetDevices.Add(true);
                if (script.targetDevices.Count > devCount) script.targetDevices.RemoveRange(devCount, script.targetDevices.Count - devCount);
                EditorUtility.SetDirty(script);
            }

            EditorGUI.indentLevel++;
            for (int i = 0; i < script.targetDevices.Count; i++)
            {
                script.targetDevices[i] = EditorGUILayout.Toggle($"Device [{i}]", script.targetDevices[i]);
            }
            EditorGUI.indentLevel--;
        }
        else
        {
            EditorGUILayout.HelpBox("No AUTD3Device found in the scene.", MessageType.Warning);
        }

        EditorGUILayout.Space();

        // ------------------
        // Focus Settings
        // ------------------
        EditorGUILayout.PropertyField(serializedObject.FindProperty("useMultiFocus"));
        
        if (script.useMultiFocus)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("multiFocusPositions"), true);
        }
        else
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("singleFocusTarget"));
            
            // Transformが指定されていない場合のみ、手入力のVector3フィールドを表示する
            if (script.singleFocusTarget == null)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("singleFocusPosition"));
            }
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("focusAmplitude"));

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            // Playモード中に変更されたら即座に保存
            if (Application.isPlaying)
            {
                SaveState();
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        // ------------------
        // Offset Tools
        // ------------------
        EditorGUILayout.LabelField("Calibration Tools", EditorStyles.boldLabel);
        if (GUILayout.Button("Apply Transform Offset to Controller", GUILayout.Height(30)))
        {
            if (script.autdController != null)
            {
                Undo.RecordObject(script.autdController, "Apply AUTD Offset");
                Undo.RecordObject(script.transform, "Reset Calibration Transform");
                
                script.ApplyOffset();
                
                EditorUtility.SetDirty(script.autdController);
                Debug.Log("[Calibration] Applied offset to AUTD Controller.");
            }
            else
            {
                Debug.LogWarning("[Calibration] AUTD Controller is not assigned.");
            }
        }
    }

    [System.Serializable]
    private class CalibrationState
    {
        public bool enableCalibration;
        public bool[] targetDevices;
        public bool useMultiFocus;
        public Vector3 singleFocusPosition;
        public Vector3[] multiFocusPositions;
        public float focusAmplitude;
    }

    private void SaveState()
    {
        if (script == null) return;

        var state = new CalibrationState
        {
            enableCalibration = script.enableCalibration,
            targetDevices = script.targetDevices.ToArray(),
            useMultiFocus = script.useMultiFocus,
            singleFocusPosition = script.singleFocusPosition,
            multiFocusPositions = script.multiFocusPositions.ToArray(),
            focusAmplitude = script.focusAmplitude
        };

        string json = JsonUtility.ToJson(state);
        EditorPrefs.SetString(PREFS_KEY, json);
    }

    private void RestoreState()
    {
        if (script == null || !EditorPrefs.HasKey(PREFS_KEY)) return;

        string json = EditorPrefs.GetString(PREFS_KEY);
        try
        {
            var state = JsonUtility.FromJson<CalibrationState>(json);
            if (state != null)
            {
                Undo.RecordObject(script, "Restore Calibration State");
                
                script.enableCalibration = state.enableCalibration;
                if (state.targetDevices != null) script.targetDevices = state.targetDevices.ToList();
                script.useMultiFocus = state.useMultiFocus;
                script.singleFocusPosition = state.singleFocusPosition;
                if (state.multiFocusPositions != null) script.multiFocusPositions = state.multiFocusPositions.ToList();
                script.focusAmplitude = state.focusAmplitude;

                EditorUtility.SetDirty(script);
                serializedObject.Update();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to restore calibration state: {e.Message}");
        }
    }
}
