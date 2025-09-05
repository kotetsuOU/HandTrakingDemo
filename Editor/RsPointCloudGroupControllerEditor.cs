using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RsPointCloudGroupController))]
public class RsPointCloudGroupControllerEditor : Editor
{
    private bool isVerticesSaved = false;
    private Color pointColor = Color.red;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Batch Control for RsPointCloudRenderer Children", EditorStyles.boldLabel);

        pointColor = EditorGUILayout.ColorField("Point Color", pointColor);

        if (GUILayout.Button("Export All Current Vertices"))
        {
            ApplyToAllRenderers(renderer =>
            {
                var vertices = renderer.GetFilteredVertices();
                var exportFileName = GetExportFileName(renderer);
                if (vertices != null && vertices.Length > 0 && !string.IsNullOrWhiteSpace(exportFileName))
                {
                    SaveVerticesToFile(vertices, exportFileName);
                }
            });
            isVerticesSaved = true;
        }

        if (isVerticesSaved && GUILayout.Button("Reset Save Status"))
        {
            isVerticesSaved = false;
        }

        if (GUILayout.Button("Toggle Range Filter on All"))
        {
            ApplyToAllRenderers(renderer =>
            {
                renderer.IsGlobalRangeFilterEnabled = !renderer.IsGlobalRangeFilterEnabled;
            });
            SceneView.RepaintAll();
            UnityEngine.Debug.Log("Toggle Range Filter on All");
        }
    }

    private void ApplyToAllRenderers(System.Action<RsPointCloudRenderer> action)
    {
        RsPointCloudGroupController group = (RsPointCloudGroupController)target;
        foreach (Transform child in group.transform)
        {
            var renderer = child.GetComponent<RsPointCloudRenderer>();
            if (renderer != null)
            {
                action.Invoke(renderer);
            }
        }
    }

    private string GetExportFileName(RsPointCloudRenderer renderer)
    {
        var type = typeof(RsPointCloudRenderer);
        var field = type.GetField("exportFileName", BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(renderer) as string;
    }

    private void SaveVerticesToFile(Vector3[] vertices, string fileName)
    {
        string path = $"Assets/HandTrakingSampleData/{fileName}";

        using (System.IO.StreamWriter writer = new System.IO.StreamWriter(path))
        {
            foreach (var v in vertices)
            {
                writer.WriteLine($"{v.x}, {v.y}, {v.z}");
            }
        }

        UnityEngine.Debug.Log($"Saved {vertices.Length} vertices to {path}");
        AssetDatabase.Refresh();
    }
}