#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// HAP_AUTDController の Inspector 表示を最適化し、
/// 触覚生成・アルゴリズム・アプリ設定に特化してスマートに表示するカスタムエディタ。
/// </summary>
[CustomEditor(typeof(HAP_AUTDController))]
public class HAP_AUTDControllerEditor : Editor
{
    private SerializedProperty hardwareManagerProp = null!;
    private SerializedProperty hcdPipelineProp = null!;
    private SerializedProperty objectHapticsControllerProp = null!;

    private SerializedProperty generationModeProp = null!;
    private SerializedProperty centroidSourceProp = null!;
    private SerializedProperty ellipseSourceProp = null!;
    private SerializedProperty randomSourceProp = null!;

    private SerializedProperty holoAlgorithmProp = null!;
    private SerializedProperty focusIntensityPascalProp = null!;

    private SerializedProperty stmModeProp = null!;
    private SerializedProperty stmFrequencyProp = null!;
    private SerializedProperty customInnerAlgorithmProp = null!;

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
        hardwareManagerProp = serializedObject.FindProperty("hardwareManager");
        hcdPipelineProp = serializedObject.FindProperty("hcdPipeline");
        objectHapticsControllerProp = serializedObject.FindProperty("objectHapticsController");

        generationModeProp = serializedObject.FindProperty("generationMode");
        centroidSourceProp = serializedObject.FindProperty("centroidSource");
        ellipseSourceProp = serializedObject.FindProperty("ellipseSource");
        randomSourceProp = serializedObject.FindProperty("randomSource");

        holoAlgorithmProp = serializedObject.FindProperty("holoAlgorithm");
        focusIntensityPascalProp = serializedObject.FindProperty("focusIntensityPascal");

        stmModeProp = serializedObject.FindProperty("stmMode");
        stmFrequencyProp = serializedObject.FindProperty("stmFrequency");
        customInnerAlgorithmProp = serializedObject.FindProperty("customInnerAlgorithm");

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

        // Hardware Manager Reference
        EditorGUILayout.LabelField("Hardware Component Link", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(hardwareManagerProp);
        if (hardwareManagerProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("HardwareManager is not assigned. It will be auto-detected or created on Awake.", MessageType.Info);
        }
        EditorGUILayout.Space();

        // Dependencies
        EditorGUILayout.LabelField("Pipeline Dependencies", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(hcdPipelineProp);
        EditorGUILayout.PropertyField(objectHapticsControllerProp);
        EditorGUILayout.Space();

        // Operation Settings
        EditorGUILayout.LabelField("Generation & Operation Mode", EditorStyles.boldLabel);
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

        // Acoustic Settings
        EditorGUILayout.LabelField("Acoustic Holography", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(holoAlgorithmProp);
        EditorGUILayout.PropertyField(focusIntensityPascalProp, new GUIContent("Focus Intensity (Pa)"));
        EditorGUILayout.Space();

        // STM Settings
        EditorGUILayout.LabelField("Spatio-Temporal Modulation (STM)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stmModeProp);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(stmFrequencyProp, new GUIContent("STM Frequency (Hz)"));

        HoloAlgorithm holoAlg = (HoloAlgorithm)holoAlgorithmProp.enumValueIndex;
        HapticsSTMMode stmMode = (HapticsSTMMode)stmModeProp.enumValueIndex;

        if (stmMode == HapticsSTMMode.GainSTM || holoAlg == HoloAlgorithm.Custom)
        {
            EditorGUILayout.PropertyField(customInnerAlgorithmProp);
        }

        if (stmMode == HapticsSTMMode.FociSTM && holoAlg != HoloAlgorithm.Custom)
        {
            EditorGUILayout.HelpBox("FociSTM mode utilizes hardware single-focus calculation.", MessageType.Info);
        }
        else if (stmMode == HapticsSTMMode.GainSTM)
        {
            EditorGUILayout.HelpBox("GainSTM mode utilizes CPU GSPAT/PatternStm calculation for multi-focus STM.", MessageType.Info);
        }
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();

        // Coordinate & Directional Settings
        EditorGUILayout.LabelField("Coordinate & Directional Grouping", EditorStyles.boldLabel);
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
        EditorGUILayout.LabelField("Debug & Performance Profiling", EditorStyles.boldLabel);
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
