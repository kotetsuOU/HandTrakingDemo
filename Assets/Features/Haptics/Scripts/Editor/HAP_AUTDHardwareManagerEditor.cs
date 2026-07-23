#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// HAP_AUTDHardwareManager の Inspector 表示をカスタマイズし、
/// 通信リンクおよびハードウェア動作パラメータ（Modulation, Silencer, Fan, Temp）をスッキリまとめるエディタ。
/// </summary>
[CustomEditor(typeof(HAP_AUTDHardwareManager))]
public class HAP_AUTDHardwareManagerEditor : Editor
{
    private SerializedProperty linkTypeProp = null!;
    private SerializedProperty soemAdapterNameProp = null!;
    private SerializedProperty settingsProp = null!;

    private void OnEnable()
    {
        linkTypeProp = serializedObject.FindProperty("linkType");
        soemAdapterNameProp = serializedObject.FindProperty("soemAdapterName");
        settingsProp = serializedObject.FindProperty("settings");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var manager = (HAP_AUTDHardwareManager)target;

        // Connection Status Banner
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (Application.isPlaying)
        {
            if (manager.IsConnected)
            {
                EditorGUILayout.HelpBox($"Status: Connected via {manager.linkType} ({manager.connectedDevices.Count} devices)", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Status: Disconnected / Bypassed", MessageType.Warning);
            }
        }
        else
        {
            EditorGUILayout.LabelField("AUTD Hardware Manager", EditorStyles.boldLabel);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();

        // Connection Link Settings
        EditorGUILayout.LabelField("Connection & Link", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(linkTypeProp);
        if ((AUTDLinkType)linkTypeProp.enumValueIndex == AUTDLinkType.SOEM)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(soemAdapterNameProp);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.Space();

        // Embedded Hardware Settings (Modulation, Silencer, Fan, Temp)
        EditorGUILayout.PropertyField(settingsProp, true);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
