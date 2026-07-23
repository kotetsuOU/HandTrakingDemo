#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// HAP_AUTDHardwareController の Inspector 表示をカスタマイズし、
/// 通信接続・環境・サイレンサー・変調設定を整理して表示するエディタ。
/// </summary>
[CustomEditor(typeof(HAP_AUTDHardwareController))]
public class HAP_AUTDHardwareControllerEditor : Editor
{
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

    private void OnEnable()
    {
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
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var manager = (HAP_AUTDHardwareController)target;

        // Connection Status Banner
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (Application.isPlaying)
        {
            if (manager.IsConnected)
            {
                EditorGUILayout.HelpBox($"Status: Connected via {manager.linkType} ({manager.ConnectedDevices.Count} devices)", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Status: Disconnected / Bypassed", MessageType.Warning);
            }
        }
        else
        {
            EditorGUILayout.LabelField("AUTD Hardware Controller", EditorStyles.boldLabel);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();

        // Connection Link Settings
        EditorGUILayout.LabelField("Connection & Link Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(linkTypeProp);
        if ((AUTDLinkType)linkTypeProp.enumValueIndex == AUTDLinkType.SOEM)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(soemAdapterNameProp);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.Space();

        // Hardware Environment
        EditorGUILayout.LabelField("Hardware Environment", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(temperatureProp);
        EditorGUILayout.PropertyField(enableFanProp);
        EditorGUILayout.Space();

        // Modulation Control
        EditorGUILayout.LabelField("Modulation Control", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(modulationModeProp);
        EditorGUI.indentLevel++;
        if ((ModulationMode)modulationModeProp.enumValueIndex == ModulationMode.Sine)
        {
            EditorGUILayout.PropertyField(sineFrequencyProp, new GUIContent("Sine Frequency (Hz)"));
        }
        else
        {
            EditorGUILayout.PropertyField(staticAmplitudeProp, new GUIContent("Static Amplitude (0..1)"));
        }
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();

        // Silencer Settings
        EditorGUILayout.LabelField("Silencer Noise Filter", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(silencerModeProp);
        EditorGUI.indentLevel++;
        if ((SilencerMode)silencerModeProp.enumValueIndex == SilencerMode.FixedUpdateRate)
        {
            EditorGUILayout.PropertyField(silencerStepPhaseProp, new GUIContent("Step Phase"));
            EditorGUILayout.PropertyField(silencerStepAmplitudeProp, new GUIContent("Step Amplitude"));
        }
        EditorGUI.indentLevel--;

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
