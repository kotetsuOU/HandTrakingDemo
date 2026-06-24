// =============================================================================
// PCDPointBufferManager.cs
// -----------------------------------------------------------------------------
// 点群データとGPUバッファの管理を一元化するクラス。
//
// 【データソースの種類】
//   1. 外部バッファ (External): GPU側で直接生成された点群バッファ
//      - RsGlobalPointCloudManager 等から SetExternalBuffer() で設定
//   2. 内部バッファ (Internal): CPU側で構築された点群データ
//      - PCV_Data (動的点群) + 静的メッシュ頂点 → MergeAndCachePoints() で統合
//
// 【バッファ結合の流れ】
//   外部のみ → externalBuffer をそのまま使用
//   内部のみ → pointBuffer をそのまま使用
//   両方あり → combinedBuffer に結合（GPU側の MergeBuffer カーネルで実行）
//
// 【RTHandle の保持】
//   コンピュートシェーダーで使用する中間テクスチャの RTHandle もこのクラスが保持する。
//   アロケーション/解放は PCD_RenderPass_Allocation.cs が担当する。
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class PCDPointBufferManager
{
    // --- RTHandles for Internal Compute ---
    public RTHandle _colorMapHandle;
    public RTHandle _depthMapHandle;
    public RTHandle _viewPositionMapHandle;
    public RTHandle _originTypeMapHandle;
    public RTHandle _gridZMinMapHandle;
    public RTHandle _densityMapHandle;
    public RTHandle _gridLevelMapHandle;
    public RTHandle _filteredGridLevelMapHandle;
    public RTHandle _neighborhoodSizeMapHandle;
    public RTHandle _correctedNeighborhoodSizeMapHandle;
    
    // Output / Result Maps
    public RTHandle _occlusionResultMapHandle;
    public RTHandle _occlusionValueMapHandle;
    public RTHandle _debugDisplayMapHandle;
    public RTHandle _neighborCountMapHandle;
    public RTHandle _integratedDepthMapHandle;
    public RTHandle _neighborhoodMapHandle;
    public RTHandle _finalImageHandle;

    public RTHandle depthPyramidL1, depthPyramidL2, depthPyramidL3, depthPyramidL4, depthPyramidL5, depthPyramidL6;
    public RTHandle[] pullPushPyramid;
    
    public RTHandle _morphColorTempHandle;
    public RTHandle _morphTypeTempHandle;

    public RTHandle morphTypePyramidL1, morphTypePyramidL2, morphTypePyramidL3, morphTypePyramidL4, morphTypePyramidL5, morphTypePyramidL6;
    public RTHandle morphColorPyramidL1, morphColorPyramidL2, morphColorPyramidL3, morphColorPyramidL4, morphColorPyramidL5, morphColorPyramidL6;

    // 点群データの1点分を表す構造体
    public struct Point
    {
        public Vector3 position;
        public Vector3 color;
        public uint originType; // 0 = PointCloud（動的点群）, 1 = StaticMesh（静的メッシュからの頂点）
    }

    // 登録された静的メッシュとトランスフォーム、処理モードを保持するクラス
    private class MeshTransformPair
    {
        public Mesh mesh;
        public Transform transform;
        public PCDProcessingMode mode;

        // 計算済みのワールド座標ポイントキャッシュ
        public Point[] cachedPoints;
        public Matrix4x4 lastMatrix;
    }

    // --- 内部バッファ管理 (静的メッシュ及びCPUベースの点群用) ---
    private ComputeBuffer _pointBuffer;
    private int _pointCount = 0;
    private Point[] _pointsCache;
    private bool _isDataDirty = false; // データが変更され、再構築が必要かどうか

    // --- 外部バッファ管理 (GPU側の直接連携用) ---
    private ComputeBuffer _externalPointBuffer;
    private int _externalPointCount = 0;
    private bool _useExternalBuffer = false;

    // --- 仮想接触バッファ管理 ---
    private Point[] _virtualContactPoints;
    private int _virtualContactPointCount = 0;

    // --- 結合バッファ (外部バッファ + 内部バッファ) ---
    private ComputeBuffer _combinedBuffer;

    private PCV_Data _dynamicData; // CPU側から提供される動的点群データ
    private List<MeshTransformPair> _staticMeshes = new List<MeshTransformPair>();
    private const int STRIDE = 28; // 1要素のデータサイズ: sizeof(float)*3 + sizeof(float)*3 + sizeof(uint)

    // GC回避用の使い回しリスト
    private List<Vector3> _tempVertices = new List<Vector3>();
    private List<Color> _tempColors = new List<Color>();

    // 各種プロパティへのアクセス
    public ComputeBuffer PointBuffer => _pointBuffer;
    public int PointCount => _pointCount;
    public ComputeBuffer ExternalPointBuffer => _externalPointBuffer;
    public int ExternalPointCount => _externalPointCount;
    public bool UseExternalBuffer => _useExternalBuffer;
    public ComputeBuffer CombinedBuffer => _combinedBuffer;
    public bool IsDataDirty => _isDataDirty; // 最適化やデバッグ用のフラグ確認

    // 外部から渡されるGPUバッファを設定する
    public void SetExternalBuffer(ComputeBuffer buffer, int count)
    {
        bool prevUse = _useExternalBuffer;

        if (buffer != null && buffer.IsValid())
        {
            _externalPointBuffer = buffer;
            _externalPointCount = count;
            _useExternalBuffer = true;
        }
        else
        {
            _useExternalBuffer = false;
            _externalPointBuffer = null;
            _externalPointCount = 0;
        }

        if (prevUse != _useExternalBuffer)
        {
            _isDataDirty = true;
        }
    }

    // CPUから更新される動的な点群データをセットする
    public void SetPointCloudData(PCV_Data data)
    {
        // 参照が変わった、または頂点数が変わった場合はダーティフラグを立てる
        if (_dynamicData != data || (data != null && _dynamicData != null && _dynamicData.PointCount != data.PointCount))
        {
            _dynamicData = data;
            _isDataDirty = true;
        }
        else if (data == null && _dynamicData != null)
        {
            _dynamicData = null;
            _isDataDirty = true;
        }
    }

    // 仮想接触ポイントの配列をセットする
    public void SetVirtualContactPoints(Point[] points, int count)
    {
        if (count != _virtualContactPointCount || points != _virtualContactPoints)
        {
            _virtualContactPoints = points;
            _virtualContactPointCount = count;
            _isDataDirty = true;
        }
    }

    // 動的メッシュの更新を強制するためにダーティフラグを立てる
    public void SetDataDirty()
    {
        _isDataDirty = true;
    }

    // システムの破棄時に、割り当てた全GPUバッファ(ComputeBuffer)と参照を適切に解放・クリアする
    public void Cleanup()
    {
        _pointBuffer?.Release();
        _pointBuffer = null;

        _combinedBuffer?.Release();
        _combinedBuffer = null;

        _pointsCache = null;
        _dynamicData = null;
        _staticMeshes.Clear();

        _externalPointBuffer = null;
        _useExternalBuffer = false;
    }
}
