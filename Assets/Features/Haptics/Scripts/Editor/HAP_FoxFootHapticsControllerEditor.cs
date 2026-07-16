#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(HAP_FoxFootHapticsController))]
public class HAP_FoxFootHapticsControllerEditor : Editor
{
    private SerializedProperty autdControllerProp;
    private SerializedProperty frontLeftFootProp;
    private SerializedProperty frontRightFootProp;
    private SerializedProperty backLeftFootProp;
    private SerializedProperty backRightFootProp;

    private SerializedProperty enableFrontLeftProp;
    private SerializedProperty enableFrontRightProp;
    private SerializedProperty enableBackLeftProp;
    private SerializedProperty enableBackRightProp;

    private SerializedProperty disableWhenInAirProp;
    private SerializedProperty airborneHeightThresholdProp;
    private SerializedProperty rootTransformProp;
    
    private SerializedProperty onlyTargetHandContactProp;
    private SerializedProperty handContactThresholdProp;
    
    private SerializedProperty footTargetNormalProp;

    private SerializedProperty stmModeProp;
    private SerializedProperty sequentialSTMFrequencyProp;
    private SerializedProperty trackModeProp;
    private SerializedProperty customInnerAlgorithmProp;

    private SerializedProperty drawGizmosProp;
    private SerializedProperty activeColorProp;
    private SerializedProperty inactiveColorProp;

    private void OnEnable()
    {
        autdControllerProp = serializedObject.FindProperty("autdController");
        frontLeftFootProp = serializedObject.FindProperty("frontLeftFoot");
        frontRightFootProp = serializedObject.FindProperty("frontRightFoot");
        backLeftFootProp = serializedObject.FindProperty("backLeftFoot");
        backRightFootProp = serializedObject.FindProperty("backRightFoot");

        enableFrontLeftProp = serializedObject.FindProperty("enableFrontLeft");
        enableFrontRightProp = serializedObject.FindProperty("enableFrontRight");
        enableBackLeftProp = serializedObject.FindProperty("enableBackLeft");
        enableBackRightProp = serializedObject.FindProperty("enableBackRight");

        disableWhenInAirProp = serializedObject.FindProperty("disableWhenInAir");
        airborneHeightThresholdProp = serializedObject.FindProperty("airborneHeightThreshold");
        rootTransformProp = serializedObject.FindProperty("rootTransform");

        onlyTargetHandContactProp = serializedObject.FindProperty("onlyTargetHandContact");
        handContactThresholdProp = serializedObject.FindProperty("handContactThreshold");

        footTargetNormalProp = serializedObject.FindProperty("footTargetNormal");

        stmModeProp = serializedObject.FindProperty("stmMode");
        sequentialSTMFrequencyProp = serializedObject.FindProperty("sequentialSTMFrequency");
        trackModeProp = serializedObject.FindProperty("trackMode");
        customInnerAlgorithmProp = serializedObject.FindProperty("customInnerAlgorithm");

        drawGizmosProp = serializedObject.FindProperty("drawGizmos");
        activeColorProp = serializedObject.FindProperty("activeColor");
        inactiveColorProp = serializedObject.FindProperty("inactiveColor");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(autdControllerProp);
        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(frontLeftFootProp);
        EditorGUILayout.PropertyField(frontRightFootProp);
        EditorGUILayout.PropertyField(backLeftFootProp);
        EditorGUILayout.PropertyField(backRightFootProp);
        if (GUILayout.Button("Auto Detect Bones"))
        {
            ((HAP_FoxFootHapticsController)target).AutoDetectBones();
        }
        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(enableFrontLeftProp);
        EditorGUILayout.PropertyField(enableFrontRightProp);
        EditorGUILayout.PropertyField(enableBackLeftProp);
        EditorGUILayout.PropertyField(enableBackRightProp);
        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(disableWhenInAirProp);
        if (disableWhenInAirProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(airborneHeightThresholdProp);
            EditorGUILayout.PropertyField(rootTransformProp);
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.PropertyField(onlyTargetHandContactProp);
        if (onlyTargetHandContactProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(handContactThresholdProp);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.PropertyField(footTargetNormalProp);
        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(stmModeProp);
        
        // STM Mode に応じた表示の切り替え
        HAP_FoxFootHapticsController.FoxFootSTMMode mode = (HAP_FoxFootHapticsController.FoxFootSTMMode)stmModeProp.enumValueIndex;
        
        EditorGUI.indentLevel++;
        
        // FociSTM / GainSTM 共通
        EditorGUILayout.PropertyField(sequentialSTMFrequencyProp, new GUIContent("STM Frequency (Hz)"));
        
        if (mode == HAP_FoxFootHapticsController.FoxFootSTMMode.GainSTM)
        {
            // GainSTM の場合のみ表示
            EditorGUILayout.PropertyField(trackModeProp);
            EditorGUILayout.PropertyField(customInnerAlgorithmProp);
        }
        else
        {
            // FociSTM の場合は非表示（固定値の案内）
            EditorGUILayout.HelpBox("FociSTM (Hardware STM) forces Track Mode to Sequential and Algorithm to Naive (Hardware single-focus calculation).", MessageType.Info);
        }
        
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(drawGizmosProp);
        if (drawGizmosProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(activeColorProp);
            EditorGUILayout.PropertyField(inactiveColorProp);
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
