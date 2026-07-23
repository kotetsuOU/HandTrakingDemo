#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// HAP_AUTDController の Inspector 表示を最適化し、
/// HoloAlgorithm や STM Mode (FociSTM/GainSTM) の選択に応じて動的に関連設定を表示するカスタムエディタ。
/// </summary>
[CustomEditor(typeof(HAP_AUTDController))]
public class HAP_AUTDControllerEditor : Editor
{
    private SerializedProperty hcdPipelineProp;
    private SerializedProperty objectHapticsControllerProp;

    private SerializedProperty linkTypeProp;
    private SerializedProperty soemAdapterNameProp;

    private SerializedProperty generationModeProp;
    private SerializedProperty centroidSourceProp;
    private SerializedProperty ellipseSourceProp;
    private SerializedProperty randomSourceProp;

    private SerializedProperty holoAlgorithmProp;
    private SerializedProperty focusIntensityPascalProp;

    private SerializedProperty stmModeProp;
    private SerializedProperty stmFrequencyProp;
    private SerializedProperty customInnerAlgorithmProp;

    private SerializedProperty modulationModeProp;
    private SerializedProperty sineFrequencyProp;
    private SerializedProperty staticAmplitudeProp;

    private SerializedProperty silencerModeProp;
    private SerializedProperty silencerStepPhaseProp;
    private SerializedProperty silencerStepAmplitudeProp;

    private SerializedProperty temperatureProp;
    private SerializedProperty enableFanProp;

    private SerializedProperty offsetProp;
    private SerializedProperty enableDirectionalGroupingProp;
    private SerializedProperty directionalAngleThresholdProp;

    private SerializedProperty visualizeDevicesProp;
    private SerializedProperty enableProfilingProp;
    private SerializedProperty synchronousSendProp;
    private SerializedProperty enableLogProp;
    private SerializedProperty profilingLogIntervalProp;

    private void OnEnable()
    {
        hcdPipelineProp = serializedObject.FindProperty("hcdPipeline");
        objectHapticsControllerProp = serializedObject.FindProperty("objectHapticsController");

        linkTypeProp = serializedObject.FindProperty("linkType");
        soemAdapterNameProp = serializedObject.FindProperty("soemAdapterName");

        generationModeProp = serializedObject.FindProperty("generationMode");
        centroidSourceProp = serializedObject.FindProperty("centroidSource");
        ellipseSourceProp = serializedObject.FindProperty("ellipseSource");
        randomSourceProp = serializedObject.FindProperty("randomSource");

        holoAlgorithmProp = serializedObject.FindProperty("holoAlgorithm");
        focusIntensityPascalProp = serializedObject.FindProperty("focusIntensityPascal");

        stmModeProp = serializedObject.FindProperty("stmMode");
        stmFrequencyProp = serializedObject.FindProperty("stmFrequency");
        customInnerAlgorithmProp = serializedObject.FindProperty("customInnerAlgorithm");

        modulationModeProp = serializedObject.FindProperty("modulationMode");
        sineFrequencyProp = serializedObject.FindProperty("sineFrequency");
        staticAmplitudeProp = serializedObject.FindProperty("staticAmplitude");

        silencerModeProp = serializedObject.FindProperty("silencerMode");
        silencerStepPhaseProp = serializedObject.FindProperty("silencerStepPhase");
        silencerStepAmplitudeProp = serializedObject.FindProperty("silencerStepAmplitude");

        temperatureProp = serializedObject.FindProperty("temperature");
        enableFanProp = serializedObject.FindProperty("enableFan");

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

        // Dependencies
        EditorGUILayout.PropertyField(hcdPipelineProp);
        EditorGUILayout.PropertyField(objectHapticsControllerProp);
        EditorGUILayout.Space();

        // Link Settings
        EditorGUILayout.PropertyField(linkTypeProp);
        if ((AUTDLinkType)linkTypeProp.enumValueIndex == AUTDLinkType.SOEM)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(soemAdapterNameProp);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.Space();

        // Operation Settings
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
        EditorGUILayout.PropertyField(holoAlgorithmProp);
        EditorGUILayout.PropertyField(focusIntensityPascalProp);
        EditorGUILayout.Space();

        // STM Settings
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

        // Modulation Settings
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
        EditorGUILayout.PropertyField(silencerModeProp);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(silencerStepPhaseProp);
        EditorGUILayout.PropertyField(silencerStepAmplitudeProp);
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();

        // Hardware Settings
        EditorGUILayout.PropertyField(temperatureProp);
        EditorGUILayout.PropertyField(enableFanProp);
        EditorGUILayout.Space();

        // Coordinate & Directional Settings
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
