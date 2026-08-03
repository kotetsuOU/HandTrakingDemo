using UnityEngine;
using System;
using Core.Logging;
using Features.HapticsCollision.Debug;

public enum ClusterAggregationMode
{
    UnweightedCentroid = 0,       // 従来の単純平均（平滑さ優先）
    DistanceWeightedCentroid = 1, // 距離加重平均（メッシュ表面 0mm に近い点を強調し本当の面を推定）
    NearestPoint = 2              // シャープな最小距離点推定
}

public enum ClusterPositionSource
{
    MeshSurface = 0,   // メッシュ表面座標 (hitPoint)
    PointCloudRaw = 1  // 実測点群空間座標 (pointPos)
}

[Serializable]
public class HCD_SpatialClusteringProcessor : IHCD_Processor
{
    public string ProcessorName => "SpatialHashClustering";

    [Header("Clustering Settings")]
    [Tooltip("抽出する接触重心の最大数です。多すぎるとGPUメモリの初期化負荷が増加しますが、少なすぎるとハッシュ衝突による座標バグが発生します。")]
    public int maxClusters = 1024;

    [Tooltip("空間を分割する解像度(m)。例: 0.02 の場合、2cm四方のボクセルに入った接触点は同じクラスタ（指など）として重心が合成されます。")]
    public float cellSize = 0.05f;

    [Header("Surface Estimation Settings")]
    [Tooltip("接触面・重心の集約アルゴリズムを選択します。DistanceWeightedCentroid は本当の面を高精度に推定します。")]
    public ClusterAggregationMode aggregationMode = ClusterAggregationMode.DistanceWeightedCentroid;

    [Tooltip("照射先・標準位置として出力する座標ソースを選択します。")]
    public ClusterPositionSource positionSource = ClusterPositionSource.MeshSurface;

    [Tooltip("DistanceWeightedCentroid モードでの距離減衰累乗数（数値が大きいほど 0mm 付近の点が強調されます）")]
    [Range(1.0f, 8.0f)]
    public float distanceWeightPower = 2.0f;
    
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

    private const int STRIDE = 60;
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
            AppLogger.LogWarning(_pipeline, HCD_LogTriggers.TagSpatialClusteringProcessor, "CollisionBuffer が見つかりません。HCD_DistanceProcessor が先に実行されているか確認してください。");
            return;
        }

        float surfaceThresh = (_pipeline != null && _pipeline.distanceProcessor != null)
            ? _pipeline.distanceProcessor.surfaceDistanceThreshold
            : 0.01f;

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

        int accGroups = Mathf.CeilToInt(pointCount / 256.0f);
        clusteringComputeShader.SetBuffer(_kernelAccumulate, "CollisionBuffer", collisionBuffer);
        clusteringComputeShader.SetBuffer(_kernelAccumulate, "ClusterBuffer", _clusterBuffer);
        clusteringComputeShader.SetInt("PointsCount", pointCount);
        clusteringComputeShader.SetInt("MaxClusters", maxClusters);
        clusteringComputeShader.SetFloat("CellSize", cellSize);

        clusteringComputeShader.SetInt("AggregationMode", (int)aggregationMode);
        clusteringComputeShader.SetInt("PositionSource", (int)positionSource);
        clusteringComputeShader.SetFloat("SurfaceDistanceThreshold", surfaceThresh);
        clusteringComputeShader.SetFloat("DistanceWeightPower", distanceWeightPower);

        clusteringComputeShader.Dispatch(_kernelAccumulate, accGroups, 1, 1);

        if (precisionMode)
        {
            clusteringComputeShader.SetBuffer(_kernelAccumulateCovariance, "CollisionBuffer", collisionBuffer);
            clusteringComputeShader.SetBuffer(_kernelAccumulateCovariance, "ClusterBuffer", _clusterBuffer);
            clusteringComputeShader.SetBuffer(_kernelAccumulateCovariance, "PrecisionBuffer", _precisionBuffer);
            clusteringComputeShader.SetInt("PointsCount", pointCount);
            clusteringComputeShader.SetFloat("CellSize", cellSize);
            clusteringComputeShader.SetInt("PositionSource", (int)positionSource);
            clusteringComputeShader.SetInt("RandomSeed", (int)(Time.time * 1000) ^ Time.frameCount);
            clusteringComputeShader.Dispatch(_kernelAccumulateCovariance, accGroups, 1, 1);
        }

#if UNITY_EDITOR
        if (AppLogger.IsEnabled(_pipeline, HCD_Pipeline.TagSpatialClusteringProcessor) && Time.frameCount % 120 == 0)
        {
            AppLogger.Log(_pipeline, HCD_Pipeline.TagSpatialClusteringProcessor,
                $"SpatialClustering Debug:\n" +
                $"  CellSize            : {cellSize:F3}m (MaxClusters={maxClusters})\n" +
                $"  AggregationMode     : {aggregationMode}\n" +
                $"  PositionSource      : {positionSource}\n" +
                $"  DistanceWeightPower : {distanceWeightPower:F2}\n" +
                $"  PrecisionMode       : {precisionMode}");
        }
#endif
    }

    public void Release()
    {
        _clusterBuffer?.Release();
        _precisionBuffer?.Release();
    }
}
