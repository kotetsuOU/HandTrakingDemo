using UnityEngine;
using System;
using System.Runtime.InteropServices;

[Serializable]
public class HCD_SpatialClusteringProcessor : IHCD_Processor
{
    public string ProcessorName => "SpatialHashClustering";

    [Header("Clustering Settings")]
    [Tooltip("抽出する接触重心の最大数です。多すぎるとGPUメモリの初期化負荷が増加しますが、少なすぎるとハッシュ衝突による座標バグが発生します。")]
    public int maxClusters = 1024;

    [Tooltip("空間を分割する解像度(m)。例: 0.02 の場合、2cm四方のボクセルに入った接触点は同じクラスタ（指など）として重心が合成されます。")]
    public float cellSize = 0.05f;
    
    [Header("Precision Settings")]
    [Tooltip("有効にするとGPUで第2パスを実行し、接触面の広がり（共分散）やランダム座標を計算します。高度な触覚（なぞる感覚・ザラザラ感）を提示する場合に必要です。")]
    public bool precisionMode = true;

    [Tooltip("使用するコンピュートシェーダー。空間ハッシュアルゴリズムを実装したものが必要です。")]
    public ComputeShader clusteringComputeShader;

    public const string ClusterBufferName = "ClusterResultBuffer";
    public const string PrecisionBufferName = "ClusterPrecisionBuffer";

    private HCD_Pipeline _pipeline;
    private ComputeBuffer _clusterBuffer;
    private ComputeBuffer _precisionBuffer;
    private int _kernelClear;
    private int _kernelAccumulate;
    private int _kernelClearCovariance;
    private int _kernelAccumulateCovariance;

    // Struct size: int(4) * 7 = 28 bytes  (count + posX/Y/Z + normalX/Y/Z)
    private const int STRIDE = 28;
    // Struct size: int(4) * (6 + 16*3) = 216 bytes
    private const int STRIDE_PRECISION = 216;

    public void Setup(HCD_Pipeline pipeline)
    {
        _pipeline = pipeline;
        if (clusteringComputeShader != null)
        {
            _kernelClear = clusteringComputeShader.FindKernel("ClearClusters");
            _kernelAccumulate = clusteringComputeShader.FindKernel("AccumulateClusters");
            _kernelClearCovariance = clusteringComputeShader.FindKernel("ClearCovariance");
            _kernelAccumulateCovariance = clusteringComputeShader.FindKernel("AccumulateCovariance");
        }

        _clusterBuffer = new ComputeBuffer(maxClusters, STRIDE);
        _pipeline.SetSharedBuffer(ClusterBufferName, _clusterBuffer);
        
        _precisionBuffer = new ComputeBuffer(maxClusters, STRIDE_PRECISION);
        _pipeline.SetSharedBuffer(PrecisionBufferName, _precisionBuffer);
    }

    public void Dispatch(ComputeBuffer pointCloudBuffer, int pointCount)
    {
        if (clusteringComputeShader == null || pointCount == 0) return;

        var collisionBuffer = _pipeline.GetSharedBuffer(HCD_DistanceProcessor.ResultBufferName);
        if (collisionBuffer == null)
        {
            Debug.LogWarning("[HCD_SpatialClusteringProcessor] CollisionBuffer が見つかりません。HCD_DistanceProcessor が先に実行されているか確認してください。");
            return;
        }

        // 1. クラスタバッファをクリア
        int clearGroups = Mathf.CeilToInt(maxClusters / 256.0f);
        clusteringComputeShader.SetBuffer(_kernelClear, "ClusterBuffer", _clusterBuffer);
        clusteringComputeShader.SetInt("MaxClusters", maxClusters);
        clusteringComputeShader.Dispatch(_kernelClear, clearGroups, 1, 1);

        if (precisionMode)
        {
            clusteringComputeShader.SetBuffer(_kernelClearCovariance, "PrecisionBuffer", _precisionBuffer);
            clusteringComputeShader.SetInt("MaxClusters", maxClusters);
            clusteringComputeShader.Dispatch(_kernelClearCovariance, clearGroups, 1, 1);
        }

        // 2. 接触している点群をボクセルにアキュミュレート（集約）
        int accGroups = Mathf.CeilToInt(pointCount / 256.0f);
        clusteringComputeShader.SetBuffer(_kernelAccumulate, "CollisionBuffer", collisionBuffer);
        clusteringComputeShader.SetBuffer(_kernelAccumulate, "ClusterBuffer", _clusterBuffer);
        clusteringComputeShader.SetInt("PointsCount", pointCount);
        clusteringComputeShader.SetInt("MaxClusters", maxClusters);
        clusteringComputeShader.SetFloat("CellSize", cellSize);
        clusteringComputeShader.Dispatch(_kernelAccumulate, accGroups, 1, 1);

        // 3. Precisionモードの場合は共分散とランダムポイントを計算（2パス目）
        if (precisionMode)
        {
            clusteringComputeShader.SetBuffer(_kernelAccumulateCovariance, "CollisionBuffer", collisionBuffer);
            clusteringComputeShader.SetBuffer(_kernelAccumulateCovariance, "ClusterBuffer", _clusterBuffer);
            clusteringComputeShader.SetBuffer(_kernelAccumulateCovariance, "PrecisionBuffer", _precisionBuffer);
            clusteringComputeShader.SetInt("PointsCount", pointCount);
            clusteringComputeShader.SetFloat("CellSize", cellSize);
            clusteringComputeShader.SetInt("RandomSeed", (int)(Time.time * 1000) ^ Time.frameCount);
            clusteringComputeShader.Dispatch(_kernelAccumulateCovariance, accGroups, 1, 1);
        }
    }

    public void Release()
    {
        _clusterBuffer?.Release();
        _precisionBuffer?.Release();
    }
}
