using UnityEngine;
using System;
using System.Runtime.InteropServices;

[Serializable]
public class HCD_SpatialClusteringProcessor : IHCD_Processor
{
    public string ProcessorName => "SpatialHashClustering";

    [Header("Settings")]
    [Tooltip("クラスタリングのグリッドサイズ（メートル）。0.05 なら 5cm 四方のボクセルでクラスタリングします。")]
    public float cellSize = 0.05f;
    [Tooltip("ハッシュテーブルのサイズ。想定される最大クラスタ数より余裕を持った値を指定します。")]
    public int maxClusters = 1024;
    public ComputeShader clusteringComputeShader;

    public const string ClusterBufferName = "ClusterResultBuffer";

    private HCD_Pipeline _pipeline;
    private ComputeBuffer _clusterBuffer;
    private int _kernelClear;
    private int _kernelAccumulate;

    // Struct size: int(4) * 4 = 16 bytes
    private const int STRIDE = 16;

    public void Setup(HCD_Pipeline pipeline)
    {
        _pipeline = pipeline;
        if (clusteringComputeShader != null)
        {
            _kernelClear = clusteringComputeShader.FindKernel("ClearClusters");
            _kernelAccumulate = clusteringComputeShader.FindKernel("AccumulateClusters");
        }

        _clusterBuffer = new ComputeBuffer(maxClusters, STRIDE);
        _pipeline.SetSharedBuffer(ClusterBufferName, _clusterBuffer);
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

        // 2. 接触している点群をボクセルにアキュミュレート（集約）
        int accGroups = Mathf.CeilToInt(pointCount / 256.0f);
        clusteringComputeShader.SetBuffer(_kernelAccumulate, "CollisionBuffer", collisionBuffer);
        clusteringComputeShader.SetBuffer(_kernelAccumulate, "ClusterBuffer", _clusterBuffer);
        clusteringComputeShader.SetInt("PointsCount", pointCount);
        clusteringComputeShader.SetInt("MaxClusters", maxClusters);
        clusteringComputeShader.SetFloat("CellSize", cellSize);
        clusteringComputeShader.Dispatch(_kernelAccumulate, accGroups, 1, 1);
    }

    public void Release()
    {
        _clusterBuffer?.Release();
    }
}
