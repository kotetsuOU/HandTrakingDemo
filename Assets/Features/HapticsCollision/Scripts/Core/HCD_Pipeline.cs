using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;
using Core.Logging;
using Features.HapticsCollision.Processors;

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

    public ComputeBuffer GetSharedBuffer(string name)
    {
        if (_sharedBuffers.TryGetValue(name, out var buffer)) return buffer;
        return null;
    }

    public void SetSharedBuffer(string name, ComputeBuffer buffer)
    {
        _sharedBuffers[name] = buffer;
    }

    // 事前確保されたクラスタデータ格納用配列（GC Alloc防止）
    private ClusterData[] _clusterResults;
    private ClusterPrecisionDataRaw[] _precisionResults;

    // 非同期読み込みリクエストのキュー
    private readonly Queue<ReadbackRequest> _readbackQueue = new Queue<ReadbackRequest>();

    private struct ReadbackRequest
    {
        public AsyncGPUReadbackRequest clusterReq;
        public AsyncGPUReadbackRequest precisionReq;
        public bool hasPrecision;
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
        // 内部でプロセッサをリスト化し、順番にセットアップ
        _processors.Add(distanceProcessor);
        _processors.Add(clusteringProcessor);

        // 配列の事前確保
        _clusterResults = new ClusterData[clusteringProcessor.maxClusters];
        _precisionResults = new ClusterPrecisionDataRaw[clusteringProcessor.maxClusters];

        foreach (var processor in _processors)
        {
            processor.Setup(this);
            AppLogger.Log(this, "HCD_Pipeline", $"Processor loaded: {processor.ProcessorName}");
        }
    }

    private void Update()
    {
        if (RsGlobalPointCloudManager.Instance == null) return;

        var globalBuffer = RsGlobalPointCloudManager.Instance.GetGlobalBuffer();
        int pointsCount = RsGlobalPointCloudManager.Instance.CurrentTotalCount;

        if (globalBuffer == null || pointsCount == 0) return;

        // 全てのプロセッサを順番にディスパッチ
        foreach (var processor in _processors)
        {
            processor.Dispatch(globalBuffer, pointsCount);
        }

        // 非同期読み込みをリクエスト
        RequestAsyncReadback();

        // 完了した非同期読み込みを処理してフレーム間クラスタ追跡を更新
        ProcessReadbackQueue();
    }

    private void RequestAsyncReadback()
    {
        var clusterBuffer = GetSharedBuffer(HCD_SpatialClusteringProcessor.ClusterBufferName);
        if (clusterBuffer == null) return;

        bool precisionMode = clusteringProcessor.precisionMode;
        var precisionBuffer = GetSharedBuffer(HCD_SpatialClusteringProcessor.PrecisionBufferName);
        bool hasPrecision = precisionMode && precisionBuffer != null;

        _readbackQueue.Enqueue(new ReadbackRequest
        {
            clusterReq = AsyncGPUReadback.Request(clusterBuffer),
            precisionReq = hasPrecision ? AsyncGPUReadback.Request(precisionBuffer) : default,
            hasPrecision = hasPrecision
        });
    }

    private void ProcessReadbackQueue()
    {
        ReadbackRequest? latestDoneReq = null;

        while (_readbackQueue.Count > 0)
        {
            var req = _readbackQueue.Peek();

            if (req.clusterReq.hasError || (req.hasPrecision && req.precisionReq.hasError))
            {
                AppLogger.LogWarning(this, "HCD_Pipeline", "AsyncGPUReadback error. クラスタバッファ読み込みエラー。");
                _readbackQueue.Dequeue();
                continue;
            }

            if (!req.clusterReq.done || (req.hasPrecision && !req.precisionReq.done))
            {
                break;
            }

            latestDoneReq = _readbackQueue.Dequeue();
        }

        if (latestDoneReq.HasValue)
        {
            var req = latestDoneReq.Value;

            req.clusterReq.GetData<ClusterData>().CopyTo(_clusterResults);
            if (req.hasPrecision)
            {
                req.precisionReq.GetData<ClusterPrecisionDataRaw>().CopyTo(_precisionResults);
            }

            GetActiveClusterInfos(out var centroids, out var normals, out var counts, out var precisions, out var rawPositions, out var meshPositions, out var minDistances);
            clusterTracker.Update(centroids, normals, counts, precisions, rawPositions, meshPositions, minDistances, this);

#if UNITY_EDITOR
            if (AppLogger.IsEnabled(this, "HCD_Pipeline") && Time.frameCount % 120 == 0)
            {
                AppLogger.Log(this, "HCD_Pipeline", $"Mode: {clusteringProcessor.aggregationMode} ({clusteringProcessor.positionSource}) | " +
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
        centroids     = new List<Vector3>();
        normals       = new List<Vector3>();
        counts        = new List<int>();
        precisions    = new List<ClusterPrecision>();
        rawPositions  = new List<Vector3>();
        meshPositions = new List<Vector3>();
        minDistances  = new List<float>();

        if (_clusterResults == null) return;

        bool precisionMode = clusteringProcessor.precisionMode;

        for (int i = 0; i < _clusterResults.Length; i++)
        {
            var data = _clusterResults[i];
            if (data.count > 0)
            {
                float invScale = (data.weightSum > 0)
                    ? (1.0f / (float)data.weightSum)
                    : (1.0f / (data.count * 100000.0f));

                centroids.Add(new Vector3(data.posX, data.posY, data.posZ) * invScale);
                rawPositions.Add(new Vector3(data.rawPosX, data.rawPosY, data.rawPosZ) * invScale);
                meshPositions.Add(new Vector3(data.meshPosX, data.meshPosY, data.meshPosZ) * invScale);

                var avgNormal = new Vector3(data.normalX, data.normalY, data.normalZ) * invScale;
                normals.Add(avgNormal.sqrMagnitude > 0.0001f ? avgNormal.normalized : Vector3.up);
                counts.Add(data.count);

                float minDist = (data.minDistInt < 2147483640) ? (data.minDistInt / 1000000.0f) : 0.0f;
                minDistances.Add(minDist);

                if (precisionMode && _precisionResults != null)
                {
                    var pData = _precisionResults[i];
                    float inv1e6 = 1.0f / 1000000.0f;
                    float inv1e4 = 1.0f / 10000.0f;
                    precisions.Add(new ClusterPrecision {
                        covXX = pData.covXX * inv1e6,
                        covYY = pData.covYY * inv1e6,
                        covZZ = pData.covZZ * inv1e6,
                        covXY = pData.covXY * inv1e6,
                        covXZ = pData.covXZ * inv1e6,
                        covYZ = pData.covYZ * inv1e6,
                        rp00 = new Vector3(pData.rp00X, pData.rp00Y, pData.rp00Z) * inv1e4,
                        rp01 = new Vector3(pData.rp01X, pData.rp01Y, pData.rp01Z) * inv1e4,
                        rp02 = new Vector3(pData.rp02X, pData.rp02Y, pData.rp02Z) * inv1e4,
                        rp03 = new Vector3(pData.rp03X, pData.rp03Y, pData.rp03Z) * inv1e4,
                        rp04 = new Vector3(pData.rp04X, pData.rp04Y, pData.rp04Z) * inv1e4,
                        rp05 = new Vector3(pData.rp05X, pData.rp05Y, pData.rp05Z) * inv1e4,
                        rp06 = new Vector3(pData.rp06X, pData.rp06Y, pData.rp06Z) * inv1e4,
                        rp07 = new Vector3(pData.rp07X, pData.rp07Y, pData.rp07Z) * inv1e4,
                        rp08 = new Vector3(pData.rp08X, pData.rp08Y, pData.rp08Z) * inv1e4,
                        rp09 = new Vector3(pData.rp09X, pData.rp09Y, pData.rp09Z) * inv1e4,
                        rp10 = new Vector3(pData.rp10X, pData.rp10Y, pData.rp10Z) * inv1e4,
                        rp11 = new Vector3(pData.rp11X, pData.rp11Y, pData.rp11Z) * inv1e4,
                        rp12 = new Vector3(pData.rp12X, pData.rp12Y, pData.rp12Z) * inv1e4,
                        rp13 = new Vector3(pData.rp13X, pData.rp13Y, pData.rp13Z) * inv1e4,
                        rp14 = new Vector3(pData.rp14X, pData.rp14Y, pData.rp14Z) * inv1e4,
                        rp15 = new Vector3(pData.rp15X, pData.rp15Y, pData.rp15Z) * inv1e4,
                    });
                }
                else
                {
                    precisions.Add(default);
                }
            }
        }
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
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ClusterData
    {
        public int count;
        public int weightSum;
        public int posX;
        public int posY;
        public int posZ;
        public int normalX;
        public int normalY;
        public int normalZ;
        public int rawPosX;
        public int rawPosY;
        public int rawPosZ;
        public int meshPosX;
        public int meshPosY;
        public int meshPosZ;
        public int minDistInt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ClusterPrecisionDataRaw
    {
        public int covXX, covYY, covZZ, covXY, covXZ, covYZ;
        public int rp00X, rp00Y, rp00Z;
        public int rp01X, rp01Y, rp01Z;
        public int rp02X, rp02Y, rp02Z;
        public int rp03X, rp03Y, rp03Z;
        public int rp04X, rp04Y, rp04Z;
        public int rp05X, rp05Y, rp05Z;
        public int rp06X, rp06Y, rp06Z;
        public int rp07X, rp07Y, rp07Z;
        public int rp08X, rp08Y, rp08Z;
        public int rp09X, rp09Y, rp09Z;
        public int rp10X, rp10Y, rp10Z;
        public int rp11X, rp11Y, rp11Z;
        public int rp12X, rp12Y, rp12Z;
        public int rp13X, rp13Y, rp13Z;
        public int rp14X, rp14Y, rp14Z;
        public int rp15X, rp15Y, rp15Z;
    }
}
