using System.Collections.Generic;
using UnityEngine;
using Core.Logging;
using Features.HapticsCollision.Processors;
using Features.HapticsCollision.Core;

/// <summary>
/// GPU上での点群解析・接触判定（HCD）パイプラインを管理するコアマネージャ。
/// 登録された IHCD_Processor を順次ディスパッチし、ComputeBuffer の共有と非同期 GPU Readback を管理します。
/// </summary>
[AppLoggable("HCD (Haptic Collision)")]
public class HCD_Pipeline : MonoBehaviour, IAppLoggable
{
    public const string TagPipeline = "HCD_Pipeline";
    public const string TagDistanceProcessor = "HCD_DistanceProcessor";
    public const string TagSpatialClusteringProcessor = "HCD_SpatialClusteringProcessor";
    public const string TagClusterTracker = "HCD_ClusterTracker";

    public void RegisterLogTriggers(LogCategoryGroup group, HashSet<string> existingLabels)
    {
        AddSubTriggerIfNotExists(group, "[HCD_Pipeline] Summary & Readback", TagPipeline, existingLabels);
        AddSubTriggerIfNotExists(group, "[HCD_DistanceProcessor] Mesh & Bounds Debug", TagDistanceProcessor, existingLabels);
        AddSubTriggerIfNotExists(group, "[HCD_SpatialClusteringProcessor] Clustering Debug", TagSpatialClusteringProcessor, existingLabels);
        AddSubTriggerIfNotExists(group, "[HCD_ClusterTracker] Cluster Tracking Info", TagClusterTracker, existingLabels);
    }

    private void AddSubTriggerIfNotExists(LogCategoryGroup group, string label, string tag, HashSet<string> existingLabels)
    {
        if (!existingLabels.Contains(label))
        {
            group.entries.Add(new LogInstanceEntry
            {
                label = label,
                tag = tag,
                target = this,
                enabled = true
            });
            existingLabels.Add(label);
        }
    }

    public static HCD_Pipeline Instance { get; private set; }

    [Header("Processors (Settings)")]
    [Tooltip("距離・接触判定プロセッサの設定")]
    public HCD_DistanceProcessor distanceProcessor = new HCD_DistanceProcessor();

    [Tooltip("空間クラスタリングプロセッサの設定")]
    public HCD_SpatialClusteringProcessor clusteringProcessor = new HCD_SpatialClusteringProcessor();

    [Tooltip("フレーム間クラスタ追跡の設定")]
    public HCD_ClusterTracker clusterTracker = new HCD_ClusterTracker();

    private readonly List<IHCD_Processor> _processors = new List<IHCD_Processor>();
    private readonly Dictionary<string, ComputeBuffer> _sharedBuffers = new Dictionary<string, ComputeBuffer>();

    private readonly HCD_ReadbackHandler _readbackHandler = new HCD_ReadbackHandler();
    private readonly HCD_ClusterDecoder _clusterDecoder = new HCD_ClusterDecoder();

    public ComputeBuffer GetSharedBuffer(string name)
    {
        if (_sharedBuffers.TryGetValue(name, out var buffer)) return buffer;
        return null;
    }

    public void SetSharedBuffer(string name, ComputeBuffer buffer)
    {
        _sharedBuffers[name] = buffer;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;

        if (GetComponent<Features.HapticsCollision.Debug.HCD_DebugVisualizer>() == null)
        {
            gameObject.AddComponent<Features.HapticsCollision.Debug.HCD_DebugVisualizer>();
        }

        if (GetComponent<Features.HapticsCollision.Debug.HCD_LogTriggers>() == null)
        {
            gameObject.AddComponent<Features.HapticsCollision.Debug.HCD_LogTriggers>();
        }
    }

    private void Start()
    {
        _processors.Add(distanceProcessor);
        _processors.Add(clusteringProcessor);

        _clusterDecoder.AllocateBuffers(clusteringProcessor.maxClusters);

        foreach (var processor in _processors)
        {
            processor.Setup(this);
            AppLogger.Log(this, TagPipeline, $"Processor loaded: {processor.ProcessorName}");
        }
    }

    private void Update()
    {
        if (RsGlobalPointCloudManager.Instance == null) return;

        var globalBuffer = RsGlobalPointCloudManager.Instance.GetGlobalBuffer();
        int pointsCount = RsGlobalPointCloudManager.Instance.CurrentTotalCount;

        if (globalBuffer == null || pointsCount == 0) return;

        foreach (var processor in _processors)
        {
            processor.Dispatch(globalBuffer, pointsCount);
        }

        var clusterBuffer = GetSharedBuffer(HCD_SpatialClusteringProcessor.ClusterBufferName);
        var precisionBuffer = GetSharedBuffer(HCD_SpatialClusteringProcessor.PrecisionBufferName);
        _readbackHandler.RequestAsyncReadback(clusterBuffer, precisionBuffer, clusteringProcessor.precisionMode);

        if (_readbackHandler.ProcessQueue(_clusterDecoder, this))
        {
            GetActiveClusterInfos(out var centroids, out var normals, out var counts, out var precisions, out var rawPositions, out var meshPositions, out var minDistances);
            clusterTracker.Update(centroids, normals, counts, precisions, rawPositions, meshPositions, minDistances, this);

#if UNITY_EDITOR
            if (AppLogger.IsEnabled(this, TagPipeline) && Time.frameCount % 120 == 0)
            {
                AppLogger.Log(this, TagPipeline, $"Mode: {clusteringProcessor.aggregationMode} ({clusteringProcessor.positionSource}) | " +
                          $"Active Clusters: {centroids.Count} | Tracked: {clusterTracker.TrackedClusters.Count}");
            }
#endif
        }
    }

    public void GetActiveClusterInfos(out List<Vector3> centroids, out List<Vector3> normals, out List<int> counts, out List<ClusterPrecision> precisions)
    {
        GetActiveClusterInfos(out centroids, out normals, out counts, out precisions, out _, out _, out _);
    }

    public void GetActiveClusterInfos(out List<Vector3> centroids, out List<Vector3> normals, out List<int> counts, out List<ClusterPrecision> precisions, out List<Vector3> rawPositions, out List<Vector3> meshPositions, out List<float> minDistances)
    {
        _clusterDecoder.DecodeActiveClusterInfos(clusteringProcessor.precisionMode, out centroids, out normals, out counts, out precisions, out rawPositions, out meshPositions, out minDistances);
    }

    public List<Vector3> GetActiveCentroids()
    {
        GetActiveClusterInfos(out var c, out _, out _, out _);
        return c;
    }

    public IReadOnlyList<TrackedCluster> GetTrackedClusters() => clusterTracker.TrackedClusters;

    private void OnDestroy()
    {
        foreach (var processor in _processors)
        {
            processor.Release();
        }

        foreach (var buffer in _sharedBuffers.Values)
        {
            buffer?.Release();
        }
        _sharedBuffers.Clear();
        _readbackHandler.Clear();
    }
}
