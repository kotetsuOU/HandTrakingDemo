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

            base.OnInspectorGUI();
        }
    }
}

