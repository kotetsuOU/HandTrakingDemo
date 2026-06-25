#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(HAP_AUTDTransformLoader))]
public class HAP_AUTDTransformLoaderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default inspector properties
        DrawDefaultInspector();

        HAP_AUTDTransformLoader loader = (HAP_AUTDTransformLoader)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Operations", EditorStyles.boldLabel);

        if (GUILayout.Button("Save Transforms"))
        {
            loader.Save();
        }

        if (GUILayout.Button("Load Transforms"))
        {
            loader.Load();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate Prefabs"))
        {
            loader.GeneratePrefabs();
        }
    }
}
#endif
