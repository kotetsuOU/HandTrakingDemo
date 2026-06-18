using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(RsGlobalPointCloudManager))]
public class RsGlobalPointCloudManagerEditor : Editor
{
    private readonly BoxBoundsHandle _boundsHandle = new BoxBoundsHandle();
    private bool _isVerticesSaved;
    private RsGlobalPointCloudManager _manager;

    private void OnEnable()
    {
        _manager = (RsGlobalPointCloudManager)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Exclude script field from inspector drawing
        DrawPropertiesExcluding(serializedObject, "m_Script");

        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.Space();

        // Draw batch controls
        DrawBatchControlSection();
        EditorGUILayout.Space(20);
    }

    private void OnSceneGUI()
    {
        if (Application.isPlaying) return;

        DrawScanRangeGizmo();
    }

    #region Inspector Sections

    private void DrawBatchControlSection()
    {
        var capturer = _manager.GetComponent<RsPointCloudCapturer>();
        if (capturer != null)
        {
            EditorGUI.BeginDisabledGroup(capturer.IsCapturing || !Application.isPlaying);

            var so = new SerializedObject(capturer);
            so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("captureFrames"), new GUIContent("Frames to Capture"));
            EditorGUILayout.PropertyField(so.FindProperty("outputDirectory"), new GUIContent("Output Directory"));
            so.ApplyModifiedProperties();

            if (Application.isPlaying && capturer.IsCapturing)
            {
                EditorGUILayout.HelpBox("Capturing PointCloud...", MessageType.Info);
            }

            GUI.backgroundColor = Color.cyan;
            string btnText = capturer.captureFrames > 1 ? $"Capture {capturer.captureFrames} Frames (Ground Truth)" : "Export Snapshot (1 Frame)";

            if (GUILayout.Button(btnText))
            {
                capturer.StartCapturePLY();
            }

            GUI.backgroundColor = Color.white;
            EditorGUI.EndDisabledGroup();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Capture is available only during Play Mode.", MessageType.Info);
            }
            EditorGUILayout.Space();
        }
        else
        {
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Export All Current Vertices (Legacy txt)"))
            {
                ExportAllVertices();
                _isVerticesSaved = true;
            }
            GUI.backgroundColor = Color.white;

            if (_isVerticesSaved && GUILayout.Button("Reset Save Status"))
            {
                _isVerticesSaved = false;
            }
        }

        bool anyFiltersEnabled = _manager.AreAnyRangeFiltersEnabled();
        bool allFiltersEnabled = _manager.AreAllRangeFiltersEnabled();
        string filterStateLabel = allFiltersEnabled ? "ON" : anyFiltersEnabled ? "MIXED" : "OFF";
        EditorGUILayout.LabelField($"Range Filter on All: {filterStateLabel}");

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = allFiltersEnabled ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.6f, 1f, 0.6f);
        EditorGUI.BeginDisabledGroup(allFiltersEnabled);
        if (GUILayout.Button("Set Range Filter ON for All"))
        {
            foreach (var renderer in _manager.GetChildRenderers()) { Undo.RecordObject(renderer, "Set Range Filter ON for All"); }
            _manager.SetAllRangeFilters(true);
            foreach (var renderer in _manager.GetChildRenderers()) { EditorUtility.SetDirty(renderer); }
            SceneView.RepaintAll();
            Debug.Log("[RsGlobalPointCloudManager] Set Range Filter ON for All");
        }
        EditorGUI.EndDisabledGroup();

        GUI.backgroundColor = allFiltersEnabled ? new Color(1f, 0.6f, 0.6f) : new Color(0.7f, 0.7f, 0.7f);
        EditorGUI.BeginDisabledGroup(!allFiltersEnabled);
        if (GUILayout.Button("Set Range Filter OFF for All"))
        {
            foreach (var renderer in _manager.GetChildRenderers()) { Undo.RecordObject(renderer, "Set Range Filter OFF for All"); }
            _manager.SetAllRangeFilters(false);
            foreach (var renderer in _manager.GetChildRenderers()) { EditorUtility.SetDirty(renderer); }
            SceneView.RepaintAll();
            Debug.Log("[RsGlobalPointCloudManager] Set Range Filter OFF for All");
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();
        GUI.backgroundColor = Color.white;
    }

    #endregion

    #region Scene GUI

    private void DrawScanRangeGizmo()
    {
        var deviceController = UnityEngine.Object.FindFirstObjectByType<RsDeviceController>();
        if (deviceController == null)
        {
            DrawWarningWindow("RsDeviceController がシーンに見つかりません。スキャン範囲を描画できません。");
            return;
        }

        Vector3 scanMin = deviceController.ScanMin;
        Vector3 scanMax = deviceController.ScanMax;
        Vector3 size = scanMax - scanMin;

        if (size.x < 0 || size.y < 0 || size.z < 0) return;

        // Set up BoxBoundsHandle values
        _boundsHandle.center = scanMin + size * 0.5f;
        _boundsHandle.size = size;
        _boundsHandle.handleColor = Color.yellow;
        _boundsHandle.wireframeColor = new Color(1f, 0.92f, 0.016f, 0.7f); // Transparent yellow

        // Draw the handle with the manager's transform matrix
        Matrix4x4 originalMatrix = Handles.matrix;
        Handles.matrix = _manager.transform.localToWorldMatrix;

        EditorGUI.BeginChangeCheck();
        _boundsHandle.DrawHandle();
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(deviceController, "Change RealSense Scan Range");

            Vector3 newCenter = _boundsHandle.center;
            Vector3 newSize = _boundsHandle.size;

            // Enforce minimum dimensions to prevent negative bounds
            newSize.x = Mathf.Max(0.01f, newSize.x);
            newSize.y = Mathf.Max(0.01f, newSize.y);
            newSize.z = Mathf.Max(0.01f, newSize.z);

            deviceController.ScanMin = newCenter - newSize * 0.5f;
            deviceController.ScanMax = newCenter + newSize * 0.5f;

            EditorUtility.SetDirty(deviceController);
        }

        Handles.matrix = originalMatrix;
    }

    private void DrawWarningWindow(string message)
    {
        Handles.BeginGUI();
        GUILayout.Window(0, new Rect(10, 10, 320, 50), _ =>
        {
            EditorGUILayout.HelpBox(message, MessageType.Warning);
        }, "スキャン範囲 警告");
        Handles.EndGUI();
    }

    #endregion

    #region Export

    private void ExportAllVertices()
    {
        _manager.ApplyToAllRenderers(renderer =>
        {
            var vertices = renderer.GetFilteredVertices();
            var exportFileName = GetExportFileName(renderer);

            if (vertices != null && vertices.Length > 0 && !string.IsNullOrWhiteSpace(exportFileName))
            {
                RsPointCloudExportTool.SaveToFile(vertices, exportFileName);
            }
        });
    }

    private string GetExportFileName(RsPointCloudRenderer renderer)
    {
        var field = typeof(RsPointCloudRenderer).GetField("exportFileName", BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(renderer) as string;
    }

    #endregion
}