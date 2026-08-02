using UnityEditor;
using UnityEngine;
using RealSense.DummyPointCloud;

namespace RealSense.Editor
{
    [CustomEditor(typeof(RsDummyPointCloudProvider))]
    public class RsDummyPointCloudProviderEditor : UnityEditor.Editor
    {
        private SerializedProperty _targetObjectsProp;
        private SerializedProperty _includeChildrenProp;
        private SerializedProperty _densityUnitProp;
        private SerializedProperty _densityValueProp;
        private SerializedProperty _maxPointLimitProp;
        private SerializedProperty _colorModeProp;
        private SerializedProperty _solidColorProp;
        private SerializedProperty _applyColorToMaterialAndRendererProp;
        private SerializedProperty _noiseSettingsProp;
        private SerializedProperty _useCameraPerspectiveProp;
        private SerializedProperty _simulatedCameraTransformProp;
        private SerializedProperty _depthWidthProp;
        private SerializedProperty _depthHeightProp;
        private SerializedProperty _updateFPSProp;

        private void OnEnable()
        {
            _targetObjectsProp = serializedObject.FindProperty("targetObjects");
            _includeChildrenProp = serializedObject.FindProperty("includeChildren");
            _densityUnitProp = serializedObject.FindProperty("densityUnit");
            _densityValueProp = serializedObject.FindProperty("densityValue");
            _maxPointLimitProp = serializedObject.FindProperty("maxPointLimit");
            _colorModeProp = serializedObject.FindProperty("colorMode");
            _solidColorProp = serializedObject.FindProperty("solidColor");
            _applyColorToMaterialAndRendererProp = serializedObject.FindProperty("applyColorToMaterialAndRenderer");
            _noiseSettingsProp = serializedObject.FindProperty("noiseSettings");
            _useCameraPerspectiveProp = serializedObject.FindProperty("useCameraPerspective");
            _simulatedCameraTransformProp = serializedObject.FindProperty("simulatedCameraTransform");
            _depthWidthProp = serializedObject.FindProperty("depthWidth");
            _depthHeightProp = serializedObject.FindProperty("depthHeight");
            _updateFPSProp = serializedObject.FindProperty("updateFPS");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var provider = (RsDummyPointCloudProvider)target;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "RsDummyPointCloudProvider は Unity 3D Object (MeshFilter / SkinnedMeshRenderer) のリストから" +
                "指定した物理密度 (例: 1cm^2あたりの点数) と色でダミーの実測点群をリアルタイム生成し、" +
                "RsProcessingPipe / RsDummyProcessingPipe の Source (RsFrameProvider) として供給します。",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_targetObjectsProp, new GUIContent("Target 3D Objects"), true);
            EditorGUILayout.PropertyField(_includeChildrenProp);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_densityUnitProp);
            EditorGUILayout.PropertyField(_densityValueProp);
            EditorGUILayout.PropertyField(_maxPointLimitProp, new GUIContent("Max Point Limit", "生成する点数の上限キャップ。過剰な重さを防止します"));
            EditorGUILayout.PropertyField(_colorModeProp);
            if (_colorModeProp.enumValueIndex == (int)PointColorMode.SolidColor)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_solidColorProp, new GUIContent("Solid Color (Material & PointCloud)"));
                EditorGUILayout.PropertyField(_applyColorToMaterialAndRendererProp, new GUIContent("Apply to Material & Renderer", "SolidColor 変更時に Target Objects のマテリアル色および RsPointCloudRenderer 描画色を連動・変更する"));
                if (EditorGUI.EndChangeCheck())
                {
                    provider.UpdateMaterialAndRendererColors();
                }
            }

            EditorGUILayout.Space();
            if (_noiseSettingsProp != null)
            {
                EditorGUILayout.PropertyField(_noiseSettingsProp, new GUIContent("Noise & Outliers Settings"), true);
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_useCameraPerspectiveProp, new GUIContent("Use Camera Perspective", "ON: カメラ視点・画角・遮蔽を適用 / OFF: カメラ向き不問で全方向の全点群を出力"));

            if (_useCameraPerspectiveProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("【カメラ視点モード】指定したカメラの位置・画角内に存在する点群のみを出力します（画角外・遮蔽点はカリングされます）。", MessageType.None);
                EditorGUILayout.PropertyField(_simulatedCameraTransformProp);
                EditorGUILayout.PropertyField(_depthWidthProp);
                EditorGUILayout.PropertyField(_depthHeightProp);
                EditorGUILayout.PropertyField(_updateFPSProp);
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("【全方向（Omnidirectional）モード】カメラの向きや画角に関係なく、メッシュ表面全体のすべての点群をそのまま出力します。", MessageType.None);
                EditorGUILayout.PropertyField(_updateFPSProp);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Information", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledGroupScope(true))
            {
                int currentPoints = provider.LastSampledData.PointCount;
                EditorGUILayout.IntField("Generated Point Count", currentPoints);
                EditorGUILayout.Toggle("Is Streaming", provider.Streaming);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
