using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(HAP_AUTDDebugDisabler))]
public class HAP_AUTDDebugDisablerEditor : Editor
{
    private HAP_AUTDDebugDisabler script;

    private void OnEnable()
    {
        script = (HAP_AUTDDebugDisabler)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox("ここにチェックを入れたデバイスは、自動トラッキング出力やキャリブレーション出力など、いかなる場合でも強制的に停止（Null）が出力されます。", MessageType.Warning);
        EditorGUILayout.Space();

        var devices = FindObjectsByType<AUTD3Device>(FindObjectsSortMode.None).OrderBy(d => d.ID).ToArray();
        
        // リストのサイズ調整
        while (script.disabledDevices.Count < devices.Length)
        {
            script.disabledDevices.Add(false);
        }

        EditorGUI.BeginChangeCheck();

        for (int i = 0; i < devices.Length; i++)
        {
            EditorGUILayout.BeginHorizontal();
            
            // チェックが入っている場合は警告色にする
            bool currentVal = script.disabledDevices[i];
            if (currentVal) GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            
            script.disabledDevices[i] = EditorGUILayout.ToggleLeft($"Disable Device {devices[i].ID}", currentVal);
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(script);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
