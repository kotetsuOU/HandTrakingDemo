using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PCDMeshRegistrarController))]
[CanEditMultipleObjects]
public class PCDMeshRegistrarControllerEditor : Editor
{
    private SerializedProperty _targetObjects;
    private SerializedProperty _includeSelf;
    private SerializedProperty _includeChildren;

    private SerializedProperty _layerSelectionMode;
    private SerializedProperty _pcdLayerMask;
    private SerializedProperty _uiLayerMask;

    private SerializedProperty _isDynamic;

    private void OnEnable()
    {
        _targetObjects = serializedObject.FindProperty("targetObjects");
        _includeSelf = serializedObject.FindProperty("includeSelf");
        _includeChildren = serializedObject.FindProperty("includeChildren");

        _layerSelectionMode = serializedObject.FindProperty("layerSelectionMode");
        _pcdLayerMask = serializedObject.FindProperty("pcdLayerMask");
        _uiLayerMask = serializedObject.FindProperty("uiLayerMask");

        _isDynamic = serializedObject.FindProperty("isDynamic");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "【位置づけ: Legacy / Fallback Mesh Source】\n" +
            "標準のオクルージョンは Camera Depth 由来の面深度を使用します。\n" +
            "本コンポーネントは Camera Depth 描画対象外のメッシュや 3D ワールド反転実験用の頂点登録機能です。",
            MessageType.Info);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Target Settings", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(_includeSelf);
        EditorGUILayout.PropertyField(_includeChildren);
        EditorGUILayout.PropertyField(_targetObjects, true);
        EditorGUI.indentLevel--;

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Layer Selection", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(_layerSelectionMode);
        EditorGUILayout.PropertyField(_pcdLayerMask);
        if (_layerSelectionMode.enumValueIndex == (int)PCDMeshRegistrarController.LayerSelectionMode.PCDAndUI)
        {
            EditorGUILayout.PropertyField(_uiLayerMask);
        }
        EditorGUI.indentLevel--;

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Dynamic Options", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(_isDynamic);
        EditorGUI.indentLevel--;

        if (GUI.changed)
        {
            serializedObject.ApplyModifiedProperties();
            var registrar = (PCDMeshRegistrarController)target;
            if (Application.isPlaying)
            {
                registrar.RegisterAllMeshes();
            }
        }
        else
        {
            serializedObject.ApplyModifiedProperties();
        }
    }
}
