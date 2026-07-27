using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// シーン内の複数の Camera に対して、URP の ScriptableRenderer (SetRenderer) を
/// 一括で切り替えるためのコントローラークラス。
/// </summary>
[ExecuteAlways]
public class CameraRendererSwitcher : MonoBehaviour
{
    [Header("Target Cameras")]
    [Tooltip("renderer をまとめて変更する対象の Camera リスト")]
    public List<Camera> cameras = new List<Camera>();

    [Header("Renderer Settings")]
    [Tooltip("適用する URP ScriptableRenderer のインデックス（例: 0=Default Universal Renderer, 1=PCD Renderer など）")]
    public int targetRendererIndex = 0;

    [Header("Auto Apply Options")]
    [Tooltip("Start 実行時に自動的に targetRendererIndex を全カメラへ適用します")]
    public bool applyOnStart = true;

    [Tooltip("Inspector でプロパティ変更時 (OnValidate) に自動適用します")]
    public bool applyOnValidate = false;

    private void Start()
    {
        if (applyOnStart)
        {
            ApplyRendererIndex();
        }
    }

    private void OnValidate()
    {
        if (applyOnValidate)
        {
            ApplyRendererIndex();
        }
    }

    /// <summary>
    /// 現在の targetRendererIndex をリスト内のすべてのカメラに一括適用します。
    /// </summary>
    public void ApplyRendererIndex()
    {
        ApplyRendererIndex(targetRendererIndex);
    }

    /// <summary>
    /// 指定された rendererIndex を targetRendererIndex に設定し、リスト内のすべてのカメラに一括適用します。
    /// </summary>
    /// <param name="rendererIndex">URP pipeline asset で登録されている ScriptableRenderer のインデックス</param>
    public void ApplyRendererIndex(int rendererIndex)
    {
        targetRendererIndex = rendererIndex;

        if (cameras == null || cameras.Count == 0) return;

        foreach (var cam in cameras)
        {
            if (cam == null) continue;

            var additionalData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (additionalData == null)
            {
                additionalData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            additionalData.SetRenderer(targetRendererIndex);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(additionalData);
                UnityEditor.EditorUtility.SetDirty(cam.gameObject);
            }
#endif
        }
    }

    /// <summary>
    /// シーン内のすべてのカメラを取得してリストに設定します。
    /// </summary>
    public void FindAllSceneCameras()
    {
        cameras.Clear();
#if UNITY_2023_1_OR_NEWER
        var foundCameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var foundCameras = Object.FindObjectsOfType<Camera>(true);
#endif
        foreach (var cam in foundCameras)
        {
            if (cam != null && !cameras.Contains(cam))
            {
                cameras.Add(cam);
            }
        }
    }

    /// <summary>
    /// この Transform 以下の全子オブジェクトからカメラを取得してリストに設定します。
    /// </summary>
    public void FindChildCameras()
    {
        cameras.Clear();
        var childCameras = GetComponentsInChildren<Camera>(true);
        foreach (var cam in childCameras)
        {
            if (cam != null && !cameras.Contains(cam))
            {
                cameras.Add(cam);
            }
        }
    }

    /// <summary>
    /// リスト内の null 要素を削除します。
    /// </summary>
    public void RemoveNullEntries()
    {
        if (cameras != null)
        {
            cameras.RemoveAll(c => c == null);
        }
    }
}
