/*
 * Copyright 2019,2020,2024 Sony Corporation
 */

using UnityEditor;

using SRD.Core;
using SRD.Utils;

namespace SRD.Editor
{
    [CustomEditor(typeof(SRDManager))]
    internal class SRDManagerInspector : UnityEditor.Editor
    {
        private const string _errorMessage = "Too many SRDManagers in a scene is not supported. Remove unnecessary SRDManagers.";

        private SerializedProperty _isSRRenderingActive;
        private SerializedProperty _isSpatialClippingActive;
        private SerializedProperty _isCrosstalkCorrectionActive;
        private SerializedProperty _crosstalkCorrectionType;
        private SerializedProperty _isHighImageQualityMode;
        private SerializedProperty _useDirectGpuImageBuffer;
        private SerializedProperty _stereoCameraController;
        private SerializedProperty _enableCalibrationMode;
        private SerializedProperty _scalingMode;
        private SerializedProperty _gizmoSize;
        private SerializedProperty _isWallmountMode;
        private SerializedProperty _srdViewSpaceScale;
        private SerializedProperty _onSRDViewSpaceScaleChangedEvent;
        private SerializedProperty _onFaceTrackStateEvent;

        private void OnEnable()
        {
            var managersNum = SRDSceneEnvironment.GetSRDManagers().Length;
            if(managersNum > SRDProjectSettings.GetNumberOfDevices())
            {
                UnityEngine.Debug.LogError(_errorMessage);
                EditorUtility.DisplayDialog("ERROR", _errorMessage, "OK");
                var instance = (Core.SRDManager)target;
                EditorApplication.delayCall += () => UnityEngine.Object.DestroyImmediate(instance);
                return;
            }

            _isSRRenderingActive = serializedObject.FindProperty("IsSRRenderingActive");
            _isSpatialClippingActive = serializedObject.FindProperty("IsSpatialClippingActive");
            _isCrosstalkCorrectionActive = serializedObject.FindProperty("IsCrosstalkCorrectionActive");
            _crosstalkCorrectionType = serializedObject.FindProperty("CrosstalkCorrectionType");
            _isHighImageQualityMode = serializedObject.FindProperty("IsHighImageQualityMode");
            _useDirectGpuImageBuffer = serializedObject.FindProperty("UseDirectGpuImageBuffer");
            _stereoCameraController = serializedObject.FindProperty("StereoCameraController");
            _enableCalibrationMode = serializedObject.FindProperty("EnableCalibrationMode");
            _scalingMode = serializedObject.FindProperty("_scalingMode");
            _gizmoSize = serializedObject.FindProperty("_GIZMOSize");
            _isWallmountMode = serializedObject.FindProperty("IsWallmountMode");
            _srdViewSpaceScale = serializedObject.FindProperty("_SRDViewSpaceScale");
            _onSRDViewSpaceScaleChangedEvent = serializedObject.FindProperty("OnSRDViewSpaceScaleChangedEvent");
            _onFaceTrackStateEvent = serializedObject.FindProperty("OnFaceTrackStateEvent");
        }

        private void OnDisable()
        {
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (_useDirectGpuImageBuffer == null)
            {
                base.OnInspectorGUI();
                return;
            }

            bool useDirectGpu = _useDirectGpuImageBuffer.boolValue;

            EditorGUILayout.PropertyField(_useDirectGpuImageBuffer);

            if (useDirectGpu)
            {
                // Hide IsSpatialClippingActive and IsHighImageQualityMode when UseDirectGpuImageBuffer is True
                EditorGUILayout.PropertyField(_stereoCameraController);
                EditorGUILayout.PropertyField(_isSRRenderingActive);
                
                EditorGUILayout.PropertyField(_isCrosstalkCorrectionActive);
                if (_isCrosstalkCorrectionActive.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_crosstalkCorrectionType);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.PropertyField(_enableCalibrationMode);
                EditorGUILayout.PropertyField(_scalingMode);
                
                if (_scalingMode.enumValueIndex == (int)SRDManager.ScalingMode.OriginalSize)
                {
                    EditorGUILayout.PropertyField(_gizmoSize);
                }

                EditorGUILayout.PropertyField(_isWallmountMode);
                EditorGUILayout.PropertyField(_srdViewSpaceScale);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_onSRDViewSpaceScaleChangedEvent);
                EditorGUILayout.PropertyField(_onFaceTrackStateEvent);
            }
            else
            {
                // Normal layout when UseDirectGpuImageBuffer is False
                EditorGUILayout.PropertyField(_isSRRenderingActive);
                EditorGUILayout.PropertyField(_isSpatialClippingActive);
                
                EditorGUILayout.PropertyField(_isCrosstalkCorrectionActive);
                if (_isCrosstalkCorrectionActive.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_crosstalkCorrectionType);
                    EditorGUI.indentLevel--;
                }
                
                EditorGUILayout.PropertyField(_isHighImageQualityMode);
                EditorGUILayout.PropertyField(_stereoCameraController);
                EditorGUILayout.PropertyField(_enableCalibrationMode);
                EditorGUILayout.PropertyField(_scalingMode);
                
                if (_scalingMode.enumValueIndex == (int)SRDManager.ScalingMode.OriginalSize)
                {
                    EditorGUILayout.PropertyField(_gizmoSize);
                }
                
                EditorGUILayout.PropertyField(_isWallmountMode);
                EditorGUILayout.PropertyField(_srdViewSpaceScale);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_onSRDViewSpaceScaleChangedEvent);
                EditorGUILayout.PropertyField(_onFaceTrackStateEvent);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}

