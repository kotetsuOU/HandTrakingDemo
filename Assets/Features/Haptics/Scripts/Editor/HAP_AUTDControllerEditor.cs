#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// HAP_AUTDController の Inspector 表示を最適化し、
/// 依存コンポーネント、通信、ハードウェア、触覚生成設定を綺麗にグループ化して表示するカスタムエディタ。
/// </summary>
[CustomEditor(typeof(HAP_AUTDController))]
public class HAP_AUTDControllerEditor : Editor
{
    private SerializedProperty hcdPipelineProp = null!;
    private SerializedProperty objectHapticsControllerProp = null!;

    private SerializedProperty linkTypeProp = null!;
    private SerializedProperty soemAdapterNameProp = null!;

    private SerializedProperty temperatureProp = null!;
    private SerializedProperty enableFanProp = null!;

    private SerializedProperty modulationModeProp = null!;
    private SerializedProperty sineFrequencyProp = null!;
    private SerializedProperty staticAmplitudeProp = null!;

    private SerializedProperty silencerModeProp = null!;
    private SerializedProperty silencerStepPhaseProp = null!;
    private SerializedProperty silencerStepAmplitudeProp = null!;

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
        hcdPipelineProp = serializedObject.FindProperty("hcdPipeline");
        objectHapticsControllerProp = serializedObject.FindProperty("objectHapticsController");

        linkTypeProp = serializedObject.FindProperty("linkType");
        soemAdapterNameProp = serializedObject.FindProperty("soemAdapterName");

        temperatureProp = serializedObject.FindProperty("temperature");
        enableFanProp = serializedObject.FindProperty("enableFan");

        modulationModeProp = serializedObject.FindProperty("modulationMode");
        sineFrequencyProp = serializedObject.FindProperty("sineFrequency");
        staticAmplitudeProp = serializedObject.FindProperty("staticAmplitude");

        silencerModeProp = serializedObject.FindProperty("silencerMode");
        silencerStepPhaseProp = serializedObject.FindProperty("silencerStepPhase");
        silencerStepAmplitudeProp = serializedObject.FindProperty("silencerStepAmplitude");

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

        var controller = (HAP_AUTDController)target;

        // Status Banner
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (Application.isPlaying)
        {
            if (controller.LinkService.IsConnected)
            {
                EditorGUILayout.HelpBox($"Status: Connected via {controller.linkType} ({controller.connectedDevices.Count} devices)", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Status: Disconnected / Bypassed", MessageType.Warning);
            }
        }
        else
        {
            EditorGUILayout.LabelField("AUTD Controller System", EditorStyles.boldLabel);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();

        // Dependencies
        EditorGUILayout.LabelField("Pipeline Dependencies", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(hcdPipelineProp);
        EditorGUILayout.PropertyField(objectHapticsControllerProp);
        EditorGUILayout.Space();

        // Link Settings
        EditorGUILayout.LabelField("Connection & Link", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(linkTypeProp);
        if ((AUTDLinkType)linkTypeProp.enumValueIndex == AUTDLinkType.SOEM)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(soemAdapterNameProp);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.Space();

        // Hardware & Modulation Settings
        EditorGUILayout.LabelField("Hardware Environment & Modulation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(temperatureProp);
        EditorGUILayout.PropertyField(enableFanProp);
        EditorGUILayout.PropertyField(modulationModeProp);
        EditorGUI.indentLevel++;
        if ((ModulationMode)modulationModeProp.enumValueIndex == ModulationMode.Sine)
        {
            EditorGUILayout.PropertyField(sineFrequencyProp);
        }
        else
        {
            EditorGUILayout.PropertyField(staticAmplitudeProp);
        }
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();

        // Silencer Settings
        EditorGUILayout.LabelField("Silencer Filter", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(silencerModeProp);
        EditorGUI.indentLevel++;
        if ((SilencerMode)silencerModeProp.enumValueIndex == SilencerMode.FixedUpdateRate)
        {
            EditorGUILayout.PropertyField(silencerStepPhaseProp);
            EditorGUILayout.PropertyField(silencerStepAmplitudeProp);
        }
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();

        // Operation Settings
        EditorGUILayout.LabelField("Haptics Generation & Operation", EditorStyles.boldLabel);
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
