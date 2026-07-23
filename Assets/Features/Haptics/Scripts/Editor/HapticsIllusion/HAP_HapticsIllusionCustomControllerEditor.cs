#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(HAP_HapticsIllusionCustomController))]
public class HAP_HapticsIllusionCustomControllerEditor : Editor
{
    private SerializedProperty autdControllerProp;
    private SerializedProperty focusConfigsProp;
    private SerializedProperty drawGizmosProp;

    private void OnEnable()
    {
        autdControllerProp = serializedObject.FindProperty("autdController");
        focusConfigsProp = serializedObject.FindProperty("focusConfigs");
        drawGizmosProp = serializedObject.FindProperty("drawGizmos");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox("【Haptics Illusion Custom Mode】\n各AUTDデバイスに任意の独立焦点/STMターゲットを割り当てて干渉考慮なしで照射します。", MessageType.Info);
        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(autdControllerProp);
        EditorGUILayout.PropertyField(drawGizmosProp);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Illusion Focus Target Configurations", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(focusConfigsProp, true);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
