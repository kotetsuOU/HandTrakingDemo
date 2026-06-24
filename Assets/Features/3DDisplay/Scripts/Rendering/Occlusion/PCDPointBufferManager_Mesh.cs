// =============================================================================
// PCDPointBufferManager_Mesh.cs
// -----------------------------------------------------------------------------
// 静的メッシュの登録・解除と、処理モード（DepthMap/PointCloud）の判定を担う
// PCDPointBufferManager の partial クラス。
// =============================================================================
using UnityEngine;

public partial class PCDPointBufferManager
{
    // オクルージョン干渉用の静的メッシュを追加する
    public void AddStaticMesh(Mesh mesh, Transform transform, PCDProcessingMode mode)
    {
        if (mesh != null && transform != null)
        {
            var existing = _staticMeshes.Find(p => p.mesh == mesh && p.transform == transform);
            if (existing == null)
            {
                _staticMeshes.Add(new MeshTransformPair { mesh = mesh, transform = transform, mode = mode });
                _isDataDirty = true;
                UnityEngine.Debug.Log($"[PCDPointBufferManager] Static mesh '{mesh.name}' added from Transform '{transform.name}'.");
            }
            else if (existing.mode != mode)
            {
                // モードだけが変更になった場合
                existing.mode = mode;
                _isDataDirty = true;
            }
        }
    }

    // 登録されている静的メッシュを削除する
    public void RemoveStaticMesh(Mesh mesh, Transform transform)
    {
        var pair = _staticMeshes.Find(p => p.mesh == mesh && p.transform == transform);
        if (pair != null)
        {
            _staticMeshes.Remove(pair);
            _isDataDirty = true;
            UnityEngine.Debug.Log($"[PCDPointBufferManager] Static mesh '{mesh.name}' removed from Transform '{transform.name}'.");
        }
    }

    // 登録済みメッシュにDepthMap（深さのレンダリング用）モードのものが存在するか確認する
    public bool HasDepthMapMeshes()
    {
        return _staticMeshes.Exists(p => p.mode == PCDProcessingMode.DepthMap);
    }

    // 登録済みメッシュにPointCloud（点群として扱う）モードのものが存在するか確認する
    public bool HasPointCloudMeshes()
    {
        return _staticMeshes.Exists(p => p.mode == PCDProcessingMode.PointCloud);
    }
}
