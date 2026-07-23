#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Features.Animation
{
    [CustomEditor(typeof(PR_LiftController))]
    public class PR_LiftControllerEditor : Editor
    {
        private SerializedProperty targetTransformProp;
        private SerializedProperty frontLeftFootProp;
        private SerializedProperty frontRightFootProp;
        private SerializedProperty backLeftFootProp;
        private SerializedProperty backRightFootProp;
        private SerializedProperty contactThresholdProp;
        private SerializedProperty liftSensitivityProp;
        private SerializedProperty fallbackPointProp;
        private SerializedProperty fallSpeedProp;

        private SerializedProperty enableFrontLeftProp;
        private SerializedProperty enableFrontRightProp;
        private SerializedProperty enableBackLeftProp;
        private SerializedProperty enableBackRightProp;

        private void OnEnable()
        {
            targetTransformProp = serializedObject.FindProperty("targetTransform");
            frontLeftFootProp = serializedObject.FindProperty("frontLeftFoot");
            frontRightFootProp = serializedObject.FindProperty("frontRightFoot");
            backLeftFootProp = serializedObject.FindProperty("backLeftFoot");
            backRightFootProp = serializedObject.FindProperty("backRightFoot");

            enableFrontLeftProp = serializedObject.FindProperty("enableFrontLeft");
            enableFrontRightProp = serializedObject.FindProperty("enableFrontRight");
            enableBackLeftProp = serializedObject.FindProperty("enableBackLeft");
            enableBackRightProp = serializedObject.FindProperty("enableBackRight");

            contactThresholdProp = serializedObject.FindProperty("contactThreshold");
            liftSensitivityProp = serializedObject.FindProperty("liftSensitivity");
            fallbackPointProp = serializedObject.FindProperty("fallbackPoint");
            fallSpeedProp = serializedObject.FindProperty("fallSpeed");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(targetTransformProp);
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(frontLeftFootProp);
            EditorGUILayout.PropertyField(frontRightFootProp);
            EditorGUILayout.PropertyField(backLeftFootProp);
            EditorGUILayout.PropertyField(backRightFootProp);
            if (GUILayout.Button("Auto Detect Target & Bones"))
            {
                ((PR_LiftController)target).AutoDetectBones();
            }
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(enableFrontLeftProp);
            EditorGUILayout.PropertyField(enableFrontRightProp);
            EditorGUILayout.PropertyField(enableBackLeftProp);
            EditorGUILayout.PropertyField(enableBackRightProp);
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(contactThresholdProp);
            EditorGUILayout.PropertyField(liftSensitivityProp);
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(fallbackPointProp);
            EditorGUILayout.PropertyField(fallSpeedProp);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
