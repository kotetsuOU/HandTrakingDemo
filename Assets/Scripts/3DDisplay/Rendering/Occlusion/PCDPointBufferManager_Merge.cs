// =============================================================================
// PCDPointBufferManager_Merge.cs
// -----------------------------------------------------------------------------
// CPU側の動的点群と静的メッシュ頂点の統合（キャッシュ化）および
// GPU側の ComputeBuffer 更新処理を担う PCDPointBufferManager の partial クラス。
// =============================================================================
using UnityEngine;

public partial class PCDPointBufferManager
{
    // データに変更があった場合のみ、キャッシュの再構築とバッファ更新をおこなう
    public void Update()
    {
        if (_isDataDirty)
        {
            MergeAndCachePoints();
            UpdateComputeBuffer();
        }
    }

    // 必要に応じて、外部バッファと内部バッファを結合するためのバッファサイズを確保・再確保する
    public void EnsureCombinedBuffer(int totalCount)
    {
        if (_combinedBuffer == null || !_combinedBuffer.IsValid() || _combinedBuffer.count < totalCount)
        {
            _combinedBuffer?.Release();
            _combinedBuffer = new ComputeBuffer(totalCount, STRIDE);
        }
    }

    // 静的メッシュの頂点と、CPU側の動的点群を一つのPoint構造体配列（キャッシュ）に統合する
    private void MergeAndCachePoints()
    {
        int dataPointCount = 0;
        // 外部バッファ（GPU）を使わない場合のみ、CPU側の点群データを統合対象とする
        if (!_useExternalBuffer && _dynamicData != null && _dynamicData.PointCount > 0)
        {
            dataPointCount = _dynamicData.PointCount;
        }

        int totalMeshPointCount = 0;
        // 点群モードに設定されているすべての静的メッシュの頂点数をカウントする
        foreach (var pair in _staticMeshes)
        {
            if (pair.mesh == null || pair.transform == null) continue;
            if (!pair.mesh.isReadable) continue;
            // PointCloudモードのメッシュのみポイントバッファに追加する
            if (pair.mode != PCDProcessingMode.PointCloud) continue;
            totalMeshPointCount += pair.mesh.vertexCount;
        }

        // 合計の頂点数
        _pointCount = dataPointCount + totalMeshPointCount;

        // 点数がゼロなら配列を破棄して終了
        if (_pointCount == 0)
        {
            _pointsCache = null;
            return;
        }

        // 配列の確保が必要なら十分なサイズを確保する（再生成によるGCを削減）
        if (_pointsCache == null || _pointsCache.Length < _pointCount)
        {
            int newSize = Mathf.Max(_pointCount, _pointsCache != null ? _pointsCache.Length * 2 : 1024);
            _pointsCache = new Point[newSize];
        }

        int cacheIndex = 0;

        // 1. CPU動的点群データを配列へ格納
        if (dataPointCount > 0)
        {
            for (int i = 0; i < dataPointCount; i++)
            {
                _pointsCache[cacheIndex] = new Point
                {
                    position = _dynamicData.Vertices[i],
                    color = new Vector3(_dynamicData.Colors[i].r, _dynamicData.Colors[i].g, _dynamicData.Colors[i].b),
                    originType = 0 // 点群由来フラグ
                };
                cacheIndex++;
            }
        }

        // 2. 静的メッシュ（PointCloudモード）の頂点情報を順番に配列へ格納
        foreach (var pair in _staticMeshes)
        {
            if (pair.mesh == null || !pair.mesh.isReadable || pair.transform == null) continue;
            if (pair.mode != PCDProcessingMode.PointCloud) continue;

            int meshPointCount = pair.mesh.vertexCount;
            if (meshPointCount == 0) continue;

            // ローカル座標からワールド座標へ変換するための行列
            Matrix4x4 localToWorld = pair.transform.localToWorldMatrix;

            // 行列が変わっているか、キャッシュがなければ再計算（毎フレームのVector3計算を避ける）
            if (pair.cachedPoints == null || pair.cachedPoints.Length != meshPointCount || pair.lastMatrix != localToWorld)
            {
                pair.mesh.GetVertices(_tempVertices);
                pair.mesh.GetColors(_tempColors);
                bool hasMeshColors = _tempColors.Count == meshPointCount;

                if (pair.cachedPoints == null || pair.cachedPoints.Length != meshPointCount)
                {
                    pair.cachedPoints = new Point[meshPointCount];
                }

                for (int i = 0; i < meshPointCount; i++)
                {
                    Vector3 color = hasMeshColors ? new Vector3(_tempColors[i].r, _tempColors[i].g, _tempColors[i].b) : Vector3.one;
                    Vector3 worldPos = localToWorld.MultiplyPoint3x4(_tempVertices[i]);

                    pair.cachedPoints[i] = new Point
                    {
                        position = worldPos,
                        color = color,
                        originType = 1 // メッシュ由来フラグ
                    };
                }
                pair.lastMatrix = localToWorld;
            }

            // 計算済みのキャッシュから高速コピー（1万以上の反復処理を省略）
            System.Array.Copy(pair.cachedPoints, 0, _pointsCache, cacheIndex, meshPointCount);
            cacheIndex += meshPointCount;
        }

        if (_isDataDirty)
        {
            string mode = _useExternalBuffer ? "External(GPU) + Static" : "Internal(CPU) + Static";
            // Reduce repetitive logs if needed, but keeping for parity
            // UnityEngine.Debug.Log($"[PCDPointBufferManager] Merged points [{mode}] - Dynamic(CPU): {dataPointCount}, Static Meshes: {totalMeshPointCount}, InternalTotal: {_pointCount}");
        }
    }

    // 結合・キャッシュされた頂点情報をもとに、ComputeShaderへ渡すためのバッファを更新する
    private void UpdateComputeBuffer()
    {
        if (_pointCount == 0 || _pointsCache == null)
        {
            _pointBuffer?.Release();
            _pointBuffer = null;
            _isDataDirty = false;
            return;
        }

        // バッファが未割り当てか、サイズが不足している場合のみ再生成する
        if (_pointBuffer == null || !_pointBuffer.IsValid() || _pointBuffer.count < _pointCount)
        {
            int oldSize = (_pointBuffer != null && _pointBuffer.IsValid()) ? _pointBuffer.count : 0;
            _pointBuffer?.Release();
            int newSize = Mathf.Max(_pointCount, Mathf.Max(oldSize * 2, 1024));
            _pointBuffer = new ComputeBuffer(newSize, STRIDE);
        }

        // キャッシュした頂点配列のうち、有効な部分だけをGPU側へ転送
        _pointBuffer.SetData(_pointsCache, 0, 0, _pointCount);
        if (_pointCount > 0 && _isDataDirty)
        {
            UnityEngine.Debug.Log($"[PCDPointBufferManager] ComputeBuffer updated with {_pointCount} points (Static/Internal).");
        }
        _isDataDirty = false; // 更新が完了したのでフラグを下ろす
    }
}
