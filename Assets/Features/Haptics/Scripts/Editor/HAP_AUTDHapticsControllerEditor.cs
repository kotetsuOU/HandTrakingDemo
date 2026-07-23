#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// HAP_AUTDHapticsController の Inspector 表示を最適化し、
/// 依存関係、触覚演算アルゴリズム、STM、プロファイリング設定を分かりやすく表示する専用エディタ。
/// </summary>
[CustomEditor(typeof(HAP_AUTDHapticsController))]
public class HAP_AUTDHapticsControllerEditor : Editor
{
    private SerializedProperty hardwareControllerProp = null!;
    private SerializedProperty sourceModeProp = null!;
    private SerializedProperty hcdPipelineProp = null!;
    private SerializedProperty objectHapticsControllersProp = null!;

    private SerializedProperty generationModeProp = null!;
    private SerializedProperty centroidSourceProp = null!;
    private SerializedProperty ellipseSourceProp = null!;
    private SerializedProperty randomSourceProp = null!;

    private SerializedProperty holoAlgorithmProp = null!;
    private SerializedProperty focusIntensityPascalProp = null!;

    private SerializedProperty stmModeProp = null!;
    private SerializedProperty stmFrequencyProp = null!;
    private SerializedProperty gainStmModeProp = null!;

    private SerializedProperty offsetProp = null!;
    private SerializedProperty enableDirectionalGroupingProp = null!;
    private SerializedProperty directionalAngleThresholdProp = null!;

    private SerializedProperty visualizeDevicesProp = null!;
    private SerializedProperty enableProfilingProp = null!;
    private SerializedProperty synchronousSendProp = null!;
    private SerializedProperty enableLogProp = null!;
    private SerializedProperty profilingLogIntervalProp = null!;

    private void OnEnable()
    {
        hardwareControllerProp = serializedObject.FindProperty("hardwareController");
        sourceModeProp = serializedObject.FindProperty("sourceMode");
        hcdPipelineProp = serializedObject.FindProperty("hcdPipeline");
        objectHapticsControllersProp = serializedObject.FindProperty("objectHapticsControllers");

        generationModeProp = serializedObject.FindProperty("generationMode");
        centroidSourceProp = serializedObject.FindProperty("centroidSource");
        ellipseSourceProp = serializedObject.FindProperty("ellipseSource");
        randomSourceProp = serializedObject.FindProperty("randomSource");

        holoAlgorithmProp = serializedObject.FindProperty("holoAlgorithm");
        focusIntensityPascalProp = serializedObject.FindProperty("focusIntensityPascal");

        stmModeProp = serializedObject.FindProperty("stmMode");
        stmFrequencyProp = serializedObject.FindProperty("stmFrequency");
        gainStmModeProp = serializedObject.FindProperty("gainStmMode");

        offsetProp = serializedObject.FindProperty("offset");
        enableDirectionalGroupingProp = serializedObject.FindProperty("enableDirectionalGrouping");
        directionalAngleThresholdProp = serializedObject.FindProperty("directionalAngleThreshold");

        visualizeDevicesProp = serializedObject.FindProperty("visualizeDevices");
        enableProfilingProp = serializedObject.FindProperty("enableProfiling");
        synchronousSendProp = serializedObject.FindProperty("synchronousSend");
        enableLogProp = serializedObject.FindProperty("enableLog");
        profilingLogIntervalProp = serializedObject.FindProperty("profilingLogInterval");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Hardware Component Reference
        EditorGUILayout.PropertyField(hardwareControllerProp);
        if (hardwareControllerProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("HardwareController is not assigned. It will be auto-detected or created on Awake.", MessageType.Info);
        }
        EditorGUILayout.Space();

        // Target Source & Dependencies
        EditorGUILayout.PropertyField(sourceModeProp);
        HapticsSourceMode sourceMode = (HapticsSourceMode)sourceModeProp.enumValueIndex;

        EditorGUI.indentLevel++;
        if (sourceMode == HapticsSourceMode.AutoHCD)
        {
            EditorGUILayout.PropertyField(hcdPipelineProp);
        }
        else if (sourceMode == HapticsSourceMode.ObjectTarget)
        {
            EditorGUILayout.PropertyField(objectHapticsControllersProp, new GUIContent("Object Target Controllers"), true);
            EditorGUILayout.PropertyField(hcdPipelineProp, new GUIContent("HCD Pipeline (Optional)"));
        }
        else if (sourceMode == HapticsSourceMode.Manual)
        {
            EditorGUILayout.HelpBox("Manual Mode: Automatic Update outputs are disabled. Control ultrasound outputs via API calls (SetFocus, SetFocusStm, etc.).", MessageType.Info);
        }
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();

        // Operation Mode
        EditorGUILayout.PropertyField(generationModeProp);
        if ((HapticsGenerationMode)generationModeProp.enumValueIndex == HapticsGenerationMode.Precision)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(centroidSourceProp, true);
            EditorGUILayout.PropertyField(ellipseSourceProp, true);
            EditorGUILayout.PropertyField(randomSourceProp, true);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.Space();

        // Acoustic Holography
        EditorGUILayout.PropertyField(holoAlgorithmProp);
        EditorGUILayout.PropertyField(focusIntensityPascalProp, new GUIContent("Focus Intensity (Pa)"));
        EditorGUILayout.Space();

        // STM Settings
        EditorGUILayout.PropertyField(stmModeProp);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(stmFrequencyProp, new GUIContent("STM Frequency (Hz)"));

        HapticsSTMMode stmMode = (HapticsSTMMode)stmModeProp.enumValueIndex;

        if (stmMode == HapticsSTMMode.FociSTM)
        {
            EditorGUILayout.HelpBox("FociSTM uses hardware single-focus (Naive) calculation at the specified frequency.", MessageType.Info);
        }
        else if (stmMode == HapticsSTMMode.GainSTM)
        {
            if (gainStmModeProp != null)
            {
                EditorGUILayout.PropertyField(gainStmModeProp, new GUIContent("Gain STM Mode"));
            }
        }
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();

        // Coordinate & Directional Grouping
        EditorGUILayout.PropertyField(offsetProp);
        EditorGUILayout.PropertyField(enableDirectionalGroupingProp);
        if (enableDirectionalGroupingProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(directionalAngleThresholdProp);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.Space();

        // Debug & Profiling
        EditorGUILayout.PropertyField(visualizeDevicesProp);
        EditorGUILayout.PropertyField(enableProfilingProp);
        if (enableProfilingProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(synchronousSendProp);
            EditorGUILayout.PropertyField(enableLogProp);
            if (enableLogProp.boolValue)
            {
                EditorGUILayout.PropertyField(profilingLogIntervalProp);
            }
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
