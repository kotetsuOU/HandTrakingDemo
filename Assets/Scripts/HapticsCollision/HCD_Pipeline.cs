using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

/// <summary>
/// GPU上での点群解析・接触判定（HCD）パイプラインを管理するクラス。
/// 登録された IHCD_Processor を順番にディスパッチし、ComputeBuffer の共有を管理します。
/// </summary>
public class HCD_Pipeline : MonoBehaviour
{
    [Header("Processors (Settings)")]
    [Tooltip("距離・接触判定プロセッサの設定")]
    public HCD_DistanceProcessor distanceProcessor = new HCD_DistanceProcessor();

    [Tooltip("空間クラスタリングプロセッサの設定")]
    public HCD_SpatialClusteringProcessor clusteringProcessor = new HCD_SpatialClusteringProcessor();
    
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
    }

    /// <summary>
    /// GPUから現在の衝突重心（クラスタ）座標を取得します。
    /// GC Alloc（ガベージコレクション）を発生させない最適化されたメソッドです。
    /// </summary>
    public List<Vector3> GetActiveCentroids()
    {
        var centroids = new List<Vector3>();
        var clusterBuffer = GetSharedBuffer(HCD_SpatialClusteringProcessor.ClusterBufferName);
        if (clusterBuffer == null) return centroids;

        // GC Allocを防ぐため、事前に確保した配列に上書きコピー
        clusterBuffer.GetData(_clusterResults);

        foreach (var data in _clusterResults)
        {
            if (data.count > 0)
            {
                centroids.Add(new Vector3(data.posX, data.posY, data.posZ) / (data.count * 10000.0f));
            }
        }
        return centroids;
    }

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
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos || !Application.isPlaying) return;

        var centroids = GetActiveCentroids();
        if (centroids.Count == 0) return;

        Gizmos.color = Color.magenta;
        foreach (var centroid in centroids)
        {
            Gizmos.DrawWireSphere(centroid, 0.02f);
        }
    }
}
