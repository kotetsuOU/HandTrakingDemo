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
    private SerializedProperty transformLoaderProp = null!;
    private SerializedProperty sourceModeProp = null!;
    private SerializedProperty hcdPipelineProp = null!;
    private SerializedProperty hcdFociSettingsProp = null!;
    private SerializedProperty objectHapticsControllersProp = null!;

    private SerializedProperty holoAlgorithmProp = null!;
    private SerializedProperty focusIntensityPascalProp = null!;

    private SerializedProperty stmModeProp = null!;
    private SerializedProperty stmFrequencyProp = null!;
    private SerializedProperty gainStmModeProp = null!;

    private SerializedProperty enableDirectionalGroupingProp = null!;
    private SerializedProperty directionalAngleThresholdProp = null!;

    private SerializedProperty visualizeDevicesProp = null!;
    private SerializedProperty enableProfilingProp = null!;
    private SerializedProperty synchronousSendProp = null!;
    private SerializedProperty profilingLogIntervalProp = null!;

    private SerializedProperty activeObjectControllerIndexProp = null!;

    private void OnEnable()
    {
        hardwareControllerProp = serializedObject.FindProperty("hardwareController");
        transformLoaderProp = serializedObject.FindProperty("transformLoader");
        sourceModeProp = serializedObject.FindProperty("sourceMode");
        hcdPipelineProp = serializedObject.FindProperty("hcdPipeline");
        hcdFociSettingsProp = serializedObject.FindProperty("hcdFociSettings");
        objectHapticsControllersProp = serializedObject.FindProperty("objectHapticsControllers");
        activeObjectControllerIndexProp = serializedObject.FindProperty("activeObjectControllerIndex");

        holoAlgorithmProp = serializedObject.FindProperty("holoAlgorithm");
        focusIntensityPascalProp = serializedObject.FindProperty("focusIntensityPascal");

        stmModeProp = serializedObject.FindProperty("stmMode");
        stmFrequencyProp = serializedObject.FindProperty("stmFrequency");
        gainStmModeProp = serializedObject.FindProperty("gainStmMode");

        enableDirectionalGroupingProp = serializedObject.FindProperty("enableDirectionalGrouping");
        directionalAngleThresholdProp = serializedObject.FindProperty("directionalAngleThreshold");

        visualizeDevicesProp = serializedObject.FindProperty("visualizeDevices");
        enableProfilingProp = serializedObject.FindProperty("enableProfiling");
        synchronousSendProp = serializedObject.FindProperty("synchronousSend");
        profilingLogIntervalProp = serializedObject.FindProperty("profilingLogInterval");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Hardware Component References
        EditorGUILayout.PropertyField(hardwareControllerProp);
        if (hardwareControllerProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("HardwareController is not assigned. It will be auto-detected or created on Awake.", MessageType.Info);
        }

        EditorGUILayout.PropertyField(transformLoaderProp);
        if (transformLoaderProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("TransformLoader is not assigned. It will be auto-detected on Awake.", MessageType.Info);
        }
        EditorGUILayout.Space();

        // Target Source & Dependencies
        EditorGUILayout.PropertyField(sourceModeProp);
        HapticsSourceMode sourceMode = (HapticsSourceMode)sourceModeProp.enumValueIndex;

        EditorGUI.indentLevel++;
        if (sourceMode == HapticsSourceMode.AutoHCD)
        {
            EditorGUILayout.PropertyField(hcdPipelineProp);
            EditorGUILayout.PropertyField(hcdFociSettingsProp);
        }
        else if (sourceMode == HapticsSourceMode.ObjectTarget)
        {
            EditorGUILayout.PropertyField(objectHapticsControllersProp, new GUIContent("Object Target Controllers"), true);

            var controller = (HAP_AUTDHapticsController)target;
            if (controller.objectHapticsControllers != null && controller.objectHapticsControllers.Count > 0)
            {
                var list = controller.objectHapticsControllers;
                string[] displayOptions = new string[list.Count];

                for (int i = 0; i < list.Count; i++)
                {
                    var ctrl = list[i];
                    if (ctrl != null)
                    {
                        displayOptions[i] = ctrl.gameObject.name;
                    }
                    else
                    {
                        displayOptions[i] = "Unassigned Object";
                    }
                }

                int currentIndex = Mathf.Clamp(controller.activeObjectControllerIndex, 0, list.Count - 1);
                int selectedIndex = EditorGUILayout.Popup("Active Controller Target", currentIndex, displayOptions);

                if (selectedIndex != controller.activeObjectControllerIndex)
                {
                    controller.SetActiveControllerIndex(selectedIndex);
                    EditorUtility.SetDirty(target);
                }
            }

            EditorGUILayout.PropertyField(hcdPipelineProp, new GUIContent("HCD Pipeline (Optional)"));
        }
        else if (sourceMode == HapticsSourceMode.Manual)
        {
            EditorGUILayout.HelpBox("Manual Mode: Automatic Update outputs are disabled. Control ultrasound outputs via API calls (SetFocus, SetFocusStm, etc.).", MessageType.Info);
        }
        EditorGUI.indentLevel--;
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

        // Directional Grouping
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
            EditorGUILayout.PropertyField(profilingLogIntervalProp);
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
