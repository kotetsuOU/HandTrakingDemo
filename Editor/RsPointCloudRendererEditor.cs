using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;

[CustomEditor(typeof(RsPointCloudRenderer))]
public class RsPointCloudRendererEditor : Editor
{
    private bool isVerticesSaved = false;
    private Color lineColor = Color.red;
    private SerializedProperty exportFileNameProp;

    void OnEnable()
    {
        exportFileNameProp = serializedObject.FindProperty("exportFileName");
    }

    public override void OnInspectorGUI()
    {
        if (exportFileNameProp == null)
        {
            OnEnable();
        }

        serializedObject.Update();
        base.OnInspectorGUI();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug Tools", EditorStyles.boldLabel);

        lineColor = EditorGUILayout.ColorField("Line Color", lineColor);

        EditorGUI.BeginChangeCheck();

        if (exportFileNameProp != null)
        {
            EditorGUILayout.PropertyField(exportFileNameProp);
        }
        else
        {
            EditorGUILayout.HelpBox("SerializedProperty 'exportFileName' not found.", MessageType.Error);
        }

        if (EditorGUI.EndChangeCheck())
            isVerticesSaved = false;

        EditorGUILayout.Space();

        var renderer = (RsPointCloudRenderer)target;

        if (GUILayout.Button("Export Current Frame Vertices"))
        {
            Vector3[] vertices = renderer.GetFilteredVertices();
            if (vertices != null && vertices.Length > 0)
            {
                if (!isVerticesSaved)
                {
                    SaveToFile(vertices, exportFileNameProp.stringValue);
                    isVerticesSaved = true;
                }
            }
            else
                UnityEngine.Debug.LogWarning("Filtered vertices not available.");
        }

        if (isVerticesSaved && GUILayout.Button("Reset Save Status"))
            isVerticesSaved = false;

        EditorGUILayout.Space();
        string label = renderer.IsGlobalRangeFilterEnabled ? "Disable Range Filter" : "Enable Range Filter";
        if (GUILayout.Button(label))
        {
            renderer.IsGlobalRangeFilterEnabled = !renderer.IsGlobalRangeFilterEnabled;
            SceneView.RepaintAll();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void SaveToFile(Vector3[] vertices, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            UnityEngine.Debug.LogWarning("Export file name is empty.");
            return;
        }

        string path = Path.Combine("Assets/HandTrakingSampleData", fileName);
        using (var writer = new StreamWriter(path))
        {
            foreach (var v in vertices)
                writer.WriteLine($"{v.x}, {v.y}, {v.z}");
        }

        UnityEngine.Debug.Log($"Saved {vertices.Length} vertices to {path}");
        AssetDatabase.Refresh();
    }

    private string VectorToString(Vector3 v) => $"{v.x},{v.y},{v.z}";

    private Vector3 StringToVector(string s)
    {
        var parts = s.Split(',');
        if (parts.Length == 3 &&
            float.TryParse(parts[0], out float x) &&
            float.TryParse(parts[1], out float y) &&
            float.TryParse(parts[2], out float z))
            return new Vector3(x, y, z);
        return Vector3.zero;
    }

    private string GetKey(string baseKey)
    {
        var renderer = (RsPointCloudRenderer)target;
        string scene = renderer.gameObject.scene.name;
        string path = GetHierarchyPath(renderer.gameObject);
        return $"{baseKey}_{scene}_{path}";
    }

    private string GetHierarchyPath(GameObject obj)
    {
        string path = obj.name;
        Transform current = obj.transform;
        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }
        return path;
    }
}