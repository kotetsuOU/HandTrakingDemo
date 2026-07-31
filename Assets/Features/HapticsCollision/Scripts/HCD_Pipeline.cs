using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;

/// <summary>
/// GPU上での点群解析・接触判定（HCD）パイプラインを管理するクラス。
/// 登録された IHCD_Processor を順番にディスパッチし、ComputeBuffer の共有を管理します。
/// </summary>
public class HCD_Pipeline : MonoBehaviour
{
    public static HCD_Pipeline Instance { get; private set; }

    [Header("Processors (Settings)")]
    [Tooltip("距離・接触判定プロセッサの設定")]
    public HCD_DistanceProcessor distanceProcessor = new HCD_DistanceProcessor();

    [Tooltip("空間クラスタリングプロセッサの設定")]
    public HCD_SpatialClusteringProcessor clusteringProcessor = new HCD_SpatialClusteringProcessor();

    [Tooltip("フレーム間クラスタ追跡の設定")]
    public HCD_ClusterTracker clusterTracker = new HCD_ClusterTracker();
    
    [Header("Debug")]
    [Tooltip("選択時にクラスタの重心をGizmoで描画します")]
    [SerializeField] private bool showDebugGizmos = true;
    
    private List<IHCD_Processor> _processors = new List<IHCD_Processor>();

    // プロセッサ間でデータを共有するためのバッファ辞書
    private Dictionary<string, ComputeBuffer> _sharedBuffers = new Dictionary<string, ComputeBuffer>();

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
    private Queue<ReadbackRequest> _readbackQueue = new Queue<ReadbackRequest>();

    private struct ReadbackRequest
    {
        public AsyncGPUReadbackRequest clusterReq;
        public AsyncGPUReadbackRequest precisionReq;
        public bool hasPrecision;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
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
            Debug.Log($"[HCD_Pipeline] Processor loaded: {processor.ProcessorName}");
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
        while (_readbackQueue.Count > 0)
        {
            var req = _readbackQueue.Peek();

            if (req.clusterReq.hasError || (req.hasPrecision && req.precisionReq.hasError))
            {
                Debug.LogWarning("[HCD_Pipeline] AsyncGPUReadback error. クラスタバッファ読み込みエラー。");
                _readbackQueue.Dequeue();
                continue;
            }

            if (!req.clusterReq.done || (req.hasPrecision && !req.precisionReq.done))
            {
                // まだ完了していない場合は待機（キューの先頭でブロック）
                break;
            }

            // 完了したリクエストを取り出し
            _readbackQueue.Dequeue();

            // NativeArray から配列へコピー
            req.clusterReq.GetData<ClusterData>().CopyTo(_clusterResults);
            if (req.hasPrecision)
            {
                req.precisionReq.GetData<ClusterPrecisionDataRaw>().CopyTo(_precisionResults);
            }

            // GPU 結果（最新の完了分）を使用してフレーム間クラスタ追跡を更新（ContactForceReduction 含む）
            GetActiveClusterInfos(out var centroids, out var normals, out var counts, out var precisions, out var rawPositions, out var meshPositions, out var minDistances);

            clusterTracker.Update(centroids, normals, counts, precisions, rawPositions, meshPositions, minDistances);

#if UNITY_EDITOR
            if (Time.frameCount % 120 == 0)  // 2秒に1回ログ出力
            {
                // 非ゼロのクラスタを数える（接触点がどれくらいあるか確認用）
                int nonZeroCount = 0;
                int maxCount = 0;
                for (int i = 0; i < _clusterResults.Length; i++)
                {
                    if (_clusterResults[i].count > 0)
                    {
                        nonZeroCount++;
                        if (_clusterResults[i].count > maxCount) maxCount = _clusterResults[i].count;
                    }
                }

                // Update 後の TrackedClusters の IsAlive 状態を確認
                int aliveCount = 0;
                string firstCentroid = "N/A";
                foreach (var tc in clusterTracker.TrackedClusters)
                {
                    if (tc.IsAlive)
                    {
                        aliveCount++;
                        if (aliveCount == 1) firstCentroid = tc.Centroid.ToString("F3");
                    }
                }

                Debug.Log($"[HCD_Pipeline] 非ゼロクラスタ={nonZeroCount}/{_clusterResults.Length}, 最大点数={maxCount}, " +
                          $"アクティブ重心={centroids.Count}, TrackedClusters={clusterTracker.TrackedClusters.Count}, " +
                          $"IsAlive数={aliveCount}, 最初の重心={firstCentroid}, " +
                          $"showDebugGizmos={showDebugGizmos}, isPlaying={Application.isPlaying}");
            }
#endif
        }
    }

    /// <summary>
    /// 最新の非同期読み込み結果から表面接触クラスタの重心座標・平均法線・接触点数・精密データを取得します。
    /// </summary>
    public void GetActiveClusterInfos(out List<Vector3> centroids, out List<Vector3> normals, out List<int> counts, out List<ClusterPrecision> precisions)
    {
        GetActiveClusterInfos(out centroids, out normals, out counts, out precisions, out _, out _, out _);
    }

    /// <summary>
    /// 最新の非同期読み込み結果から表面接触クラスタの重心座標・平均法線・接触点数・精密データ・実測/メッシュ座標・最小距離を取得します。
    /// </summary>
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
                    : (1.0f / (data.count * 10000.0f));

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

    /// <summary>
    /// GPU から現在の衝突重心（クラスタ）座標を取得します（重心のみの簡易版）。
    /// </summary>
    public List<Vector3> GetActiveCentroids()
    {
        GetActiveClusterInfos(out var c, out _, out _, out _);
        return c;
    }

    /// <summary>
    /// フレーム間追跡済みのクラスタ一覧を返します。
    /// 各クラスタには安定した ID・生存フレーム数・現在の重心が含まれます。
    /// AUTD3 連携や上位ロジックからはこちらを使用してください。
    /// </summary>
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

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if (!showDebugGizmos) return;

#if UNITY_EDITOR
        // 選択されている場合は OnDrawGizmosSelected で描画されるため重複を避ける
        if (UnityEditor.Selection.activeGameObject == gameObject) return;
#endif

        DrawClusterGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // 選択時は showDebugGizmos の値に関わらず必ず描画
        DrawClusterGizmos();
    }

    private void DrawClusterGizmos()
    {
        var clusters = clusterTracker.TrackedClusters;

#if UNITY_EDITOR
        // ── デバッグ: OnDrawGizmos が呼ばれているか、TrackedClusters 状態の確認 ──
        if (Time.frameCount % 120 == 0)
        {
            int alive = 0;
            foreach (var c in clusters) { if (c.IsAlive) alive++; }
            Debug.Log($"[HCD_Gizmo] DrawClusterGizmos呼び出し: TrackedClusters={clusters.Count}, IsAlive={alive}");
        }

        // 常に原点に赤い球を描画 → これが見えなければ OnDrawGizmos 自体が無効
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(Vector3.zero, 0.05f);
#endif

        // isColliding == 1 の表面接触クラスタ（マゼンタ）をトラッカー経由で描画
        foreach (var cluster in clusters)
        {
            if (!cluster.IsAlive) continue;

            // Age が大きいほど安定 → マゼンタ、新生 → 黄色
            float stability = Mathf.Clamp01(cluster.Age / 10.0f);
            Gizmos.color = Color.Lerp(Color.yellow, Color.magenta, stability);
            Gizmos.DrawWireSphere(cluster.Centroid, 0.02f);

            // 実測点群位置とメッシュ投影位置の差分を線で描画
            if ((cluster.RawPointPosition - cluster.MeshSurfacePosition).sqrMagnitude > 0.000001f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(cluster.RawPointPosition, 0.005f);
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(cluster.MeshSurfacePosition, 0.005f);
                Gizmos.color = Color.white;
                Gizmos.DrawLine(cluster.RawPointPosition, cluster.MeshSurfacePosition);
            }

            // 法線方向を矢印で描画
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(cluster.Centroid, cluster.Normal * 0.04f);

#if UNITY_EDITOR
            // ID・生存フレーム数・Force・最小距離 をラベル表示
            UnityEditor.Handles.Label(
                cluster.Centroid + Vector3.up * 0.03f,
                $"ID:{cluster.Id} Age:{cluster.Age} F:{cluster.Force:F2} MinD:{cluster.MinDistance * 1000f:F1}mm");
#endif
        }
    }
}
