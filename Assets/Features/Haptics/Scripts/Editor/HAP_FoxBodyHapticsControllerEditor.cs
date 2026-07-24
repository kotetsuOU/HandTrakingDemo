#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(HAP_FoxBodyHapticsController))]
public class HAP_FoxBodyHapticsControllerEditor : Editor
{
    private SerializedProperty autdControllerProp;

    // Bone Transforms
    private SerializedProperty headBoneProp;
    private SerializedProperty leftEarBoneProp;
    private SerializedProperty rightEarBoneProp;
    private SerializedProperty frontLeftFootProp;
    private SerializedProperty frontRightFootProp;
    private SerializedProperty backLeftFootProp;
    private SerializedProperty backRightFootProp;
    private SerializedProperty tailBoneProp;

    // Toggles
    private SerializedProperty enableHeadProp;
    private SerializedProperty enableLeftEarProp;
    private SerializedProperty enableRightEarProp;
    private SerializedProperty enableFrontLeftProp;
    private SerializedProperty enableFrontRightProp;
    private SerializedProperty enableBackLeftProp;
    private SerializedProperty enableBackRightProp;
    private SerializedProperty enableTailProp;

    // Normals
    private SerializedProperty headTargetNormalProp;
    private SerializedProperty footTargetNormalProp;

    // Airborne / Contact
    private SerializedProperty disableWhenInAirProp;
    private SerializedProperty airborneHeightThresholdProp;
    private SerializedProperty rootTransformProp;
    
    private SerializedProperty onlyTargetHandContactProp;
    private SerializedProperty handContactThresholdProp;

    // STM Settings
    private SerializedProperty stmModeProp;
    private SerializedProperty sequentialSTMFrequencyProp;
    private SerializedProperty trackModeProp;

    // Gizmos
    private SerializedProperty drawGizmosProp;
    private SerializedProperty activeColorProp;
    private SerializedProperty inactiveColorProp;

    private void OnEnable()
    {
        autdControllerProp = serializedObject.FindProperty("autdController");

        headBoneProp = serializedObject.FindProperty("headBone");
        leftEarBoneProp = serializedObject.FindProperty("leftEarBone");
        rightEarBoneProp = serializedObject.FindProperty("rightEarBone");
        frontLeftFootProp = serializedObject.FindProperty("frontLeftFoot");
        frontRightFootProp = serializedObject.FindProperty("frontRightFoot");
        backLeftFootProp = serializedObject.FindProperty("backLeftFoot");
        backRightFootProp = serializedObject.FindProperty("backRightFoot");
        tailBoneProp = serializedObject.FindProperty("tailBone");

        enableHeadProp = serializedObject.FindProperty("enableHead");
        enableLeftEarProp = serializedObject.FindProperty("enableLeftEar");
        enableRightEarProp = serializedObject.FindProperty("enableRightEar");
        enableFrontLeftProp = serializedObject.FindProperty("enableFrontLeft");
        enableFrontRightProp = serializedObject.FindProperty("enableFrontRight");
        enableBackLeftProp = serializedObject.FindProperty("enableBackLeft");
        enableBackRightProp = serializedObject.FindProperty("enableBackRight");
        enableTailProp = serializedObject.FindProperty("enableTail");

        headTargetNormalProp = serializedObject.FindProperty("headTargetNormal");
        footTargetNormalProp = serializedObject.FindProperty("footTargetNormal");

        disableWhenInAirProp = serializedObject.FindProperty("disableWhenInAir");
        airborneHeightThresholdProp = serializedObject.FindProperty("airborneHeightThreshold");
        rootTransformProp = serializedObject.FindProperty("rootTransform");

        onlyTargetHandContactProp = serializedObject.FindProperty("onlyTargetHandContact");
        handContactThresholdProp = serializedObject.FindProperty("handContactThreshold");

        stmModeProp = serializedObject.FindProperty("stmMode");
        sequentialSTMFrequencyProp = serializedObject.FindProperty("sequentialSTMFrequency");
        trackModeProp = serializedObject.FindProperty("trackMode");

        drawGizmosProp = serializedObject.FindProperty("drawGizmos");
        activeColorProp = serializedObject.FindProperty("activeColor");
        inactiveColorProp = serializedObject.FindProperty("inactiveColor");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(autdControllerProp);
        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(headBoneProp);
        EditorGUILayout.PropertyField(leftEarBoneProp);
        EditorGUILayout.PropertyField(rightEarBoneProp);
        EditorGUILayout.PropertyField(frontLeftFootProp);
        EditorGUILayout.PropertyField(frontRightFootProp);
        EditorGUILayout.PropertyField(backLeftFootProp);
        EditorGUILayout.PropertyField(backRightFootProp);
        EditorGUILayout.PropertyField(tailBoneProp);

        if (GUILayout.Button("Auto Detect Bones"))
        {
            ((HAP_FoxBodyHapticsController)target).AutoDetectBones();
        }
        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(enableHeadProp);
        EditorGUILayout.PropertyField(enableLeftEarProp);
        EditorGUILayout.PropertyField(enableRightEarProp);
        EditorGUILayout.PropertyField(enableFrontLeftProp);
        EditorGUILayout.PropertyField(enableFrontRightProp);
        EditorGUILayout.PropertyField(enableBackLeftProp);
        EditorGUILayout.PropertyField(enableBackRightProp);
        EditorGUILayout.PropertyField(enableTailProp);
        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(headTargetNormalProp, new GUIContent("Head/Ear Target Normal"));
        EditorGUILayout.PropertyField(footTargetNormalProp, new GUIContent("Foot/Tail Target Normal"));
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

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(stmModeProp);
        
        HapticsSTMMode mode = (HapticsSTMMode)stmModeProp.enumValueIndex;
        
        EditorGUI.indentLevel++;
        
        if (mode == HapticsSTMMode.GainSTM)
        {
            EditorGUILayout.PropertyField(trackModeProp);
        }
        else
        {
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
