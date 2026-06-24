using System.Collections.Generic;
using UnityEngine;
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

        // GPU 結果を読み戻してフレーム間クラスタ追跡を更新（ContactForceReduction 含む）
        GetActiveClusterInfos(out var centroids, out var normals, out var counts);
        clusterTracker.Update(centroids, normals, counts);
    }

    /// <summary>
    /// GPU から現在の表面接触クラスタの重心座標・平均法線・接触点数を取得します。
    /// </summary>
    public void GetActiveClusterInfos(out List<Vector3> centroids, out List<Vector3> normals, out List<int> counts)
    {
        centroids = new List<Vector3>();
        normals   = new List<Vector3>();
        counts    = new List<int>();
        var clusterBuffer = GetSharedBuffer(HCD_SpatialClusteringProcessor.ClusterBufferName);
        if (clusterBuffer == null) return;

        clusterBuffer.GetData(_clusterResults);

        foreach (var data in _clusterResults)
        {
            if (data.count > 0)
            {
                float invScale = 1.0f / (data.count * 10000.0f);
                centroids.Add(new Vector3(data.posX, data.posY, data.posZ) * invScale);
                var avgNormal = new Vector3(data.normalX, data.normalY, data.normalZ) * invScale;
                normals.Add(avgNormal.sqrMagnitude > 0.0001f ? avgNormal.normalized : Vector3.up);
                counts.Add(data.count);
            }
        }
    }

    /// <summary>
    /// GPU から現在の衝突重心（クラスタ）座標を取得します（重心のみの簡易版）。
    /// </summary>
    public List<Vector3> GetActiveCentroids()
    {
        GetActiveClusterInfos(out var c, out _, out _);
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
        public int posX;
        public int posY;
        public int posZ;
        public int normalX;
        public int normalY;
        public int normalZ;
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
        // isColliding == 1 の表面接触クラスタ（マゼンタ）をトラッカー経由で描画
        foreach (var cluster in clusterTracker.TrackedClusters)
        {
            if (!cluster.IsAlive) continue;

            // Age が大きいほど安定 → マゼンタ、新生 → 黄色
            float stability = Mathf.Clamp01(cluster.Age / 10.0f);
            Gizmos.color = Color.Lerp(Color.yellow, Color.magenta, stability);
            Gizmos.DrawWireSphere(cluster.Centroid, 0.02f);

            // 法線方向を矢印で描画（TactileClustering の効果確認用）
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(cluster.Centroid, cluster.Normal * 0.04f);

#if UNITY_EDITOR
            // ID・生存フレーム数・Force をラベル表示
            UnityEditor.Handles.Label(
                cluster.Centroid + Vector3.up * 0.03f,
                $"ID:{cluster.Id} Age:{cluster.Age} F:{cluster.Force:F2}");
#endif
        }
    }
}
