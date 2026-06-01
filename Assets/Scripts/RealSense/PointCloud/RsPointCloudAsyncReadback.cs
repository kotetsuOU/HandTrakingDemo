using System;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class RsPointCloudAsyncReadback : IDisposable
{
    #region Constants

    private const int MAX_SAMPLE_TRANSFER = 2000; // PCA用にCPUへ戻す点の最大数

    #endregion

    #region Public Properties

    // プロファイリング用に、従来手法でもスキップさせず毎回リードバックを発行させるフラグ
    public bool ForceReadbackEveryFrame { get; set; } = true;

    #endregion

    #region Private Fields

    // ComputeBuffer.CopyCount で GPU上の要素数を取得するためのバッファ
    // 3要素 (filtered, sampled, discarded) を1つのバッファにまとめる
    private ComputeBuffer _countsBuffer;

    private bool _countReadbackPending = false; // 値の取得待機状態のフラグ
    private bool _samplesReadbackPending = false;

    // 直近で取得できた結果（少し遅れて反映される）
    private int _lastFilteredCount = 0;
    private int _lastSampledCount = 0;
    private int _lastDiscardedCount = 0;

    private Vector3[] _cachedSamples; // CPU側で確保しているPCA対象データ用配列
    private int _cachedSamplesCount = 0;
    private bool _hasCachedSamples = false;

    private readonly RsComputeStats _stats;

    #endregion

    #region Public Properties

    public int LastFilteredCount => _lastFilteredCount;
    public int LastSampledCount => _lastSampledCount;
    public int LastDiscardedCount => _lastDiscardedCount;
    public bool HasCachedSamples => _hasCachedSamples;
    public int CachedSamplesCount => _cachedSamplesCount;
    public Vector3[] CachedSamples => _cachedSamples;
    public bool IsCountReadbackPending => _countReadbackPending;

    #endregion

    #region Constructor

    public RsPointCloudAsyncReadback(RsComputeStats stats)
    {
        _stats = stats;
        InitializeBuffers();
    }

    #endregion

    #region Initialization

    private void InitializeBuffers()
    {
        // 3つのint要素を1つのバッファにまとめる (最適化)
        _countsBuffer = new ComputeBuffer(3, sizeof(int), ComputeBufferType.Raw);
        _cachedSamples = new Vector3[MAX_SAMPLE_TRANSFER];
    }

    #endregion

    #region Async Readback Operations

    // GPU側の要素数をカウントバッファに転写し、非同期でCPU側への読み込みリクエストを発行する
    public void RequestAsyncReadback(
        ComputeBuffer filteredVerticesBuffer, 
        ComputeBuffer samplingBuffer, 
        ComputeBuffer distanceDiscardBuffer)
    {
        if (filteredVerticesBuffer == null || samplingBuffer == null || distanceDiscardBuffer == null)
        {
            UnityEngine.Debug.LogWarning("RsPointCloudAsyncReadback: One or more input buffers are null. Async readback request aborted.");
            return;
        }

        if (_countsBuffer == null)
        {
            UnityEngine.Debug.LogWarning("RsPointCloudAsyncReadback: _countsBuffer is null. Async readback request aborted.");
            return;
        }

        // 既に直前の要求が処理中(Pending)の場合は、今回のフレームでの要求は見送る(スキップ)
        // ただし ForceReadbackEveryFrame が true ならばスキップせずに発行する
        if (!ForceReadbackEveryFrame && _countReadbackPending)
        {
            _stats?.RecordCountReadbackSkipped();
            return;
        }

        _countReadbackPending = true;

        // AppendBuffer から要素数を取り出して1つのバッファの異なるオフセット(0, 4, 8)に書き込む
        ComputeBuffer.CopyCount(filteredVerticesBuffer, _countsBuffer, 0);
        ComputeBuffer.CopyCount(samplingBuffer, _countsBuffer, 4);
        ComputeBuffer.CopyCount(distanceDiscardBuffer, _countsBuffer, 8);

        // AsyncGPUReadbackに取得を依頼 (3つ分まとめて1回のリクエスト)
        AsyncGPUReadback.Request(_countsBuffer, OnCountsReadback);

        // 点群自体のデータの非同期読み取り要求も行う（PCA用）
        RequestAsyncSamplesReadback(samplingBuffer);
    }

    public void RequestFilteredCountReadback(ComputeBuffer filteredVerticesBuffer)
    {
        if (!ForceReadbackEveryFrame && _countReadbackPending)
        {
            _stats?.RecordCountReadbackSkipped();
            return;
        }
        
        if (filteredVerticesBuffer == null || _countsBuffer == null)
        {
            return;
        }
        
        _countReadbackPending = true;
        
        ComputeBuffer.CopyCount(filteredVerticesBuffer, _countsBuffer, 0);
        AsyncGPUReadback.Request(_countsBuffer, OnCountsReadback);
    }

    private void RequestAsyncSamplesReadback(ComputeBuffer samplingBuffer)
    {
        if (!ForceReadbackEveryFrame && _samplesReadbackPending)
        {
            _stats?.RecordSamplesReadbackSkipped();
            return;
        }
        
        if (samplingBuffer == null)
        {
            return;
        }
        
        _samplesReadbackPending = true;
        
        AsyncGPUReadback.Request(samplingBuffer, OnSamplesReadback);
    }

    #endregion

    #region Readback Callbacks

    private void OnCountsReadback(AsyncGPUReadbackRequest request)
    {
        _countReadbackPending = false;
        if (request.hasError) return;
        
        var data = request.GetData<int>();
        if (data.Length >= 1) 
            _lastFilteredCount = data[0];
        if (data.Length >= 2) 
            _lastSampledCount = data[1];
        if (data.Length >= 3) 
            _lastDiscardedCount = data[2];
    }

    // GPUから届いたサンプリング点群データをCPU配列へコピーする
    private void OnSamplesReadback(AsyncGPUReadbackRequest request)
    {
        _samplesReadbackPending = false;
        if (request.hasError) return;

        // 取得した生データを読み取り可能なNativeArrayとして取得
        var data = request.GetData<Vector3>();
        int count = Mathf.Min(data.Length, _lastSampledCount, MAX_SAMPLE_TRANSFER);

        if (count > 0)
        {
            // ネイティブ配列から通常配列(キャッシュ用配列)へのコピー
            NativeArray<Vector3>.Copy(data, 0, _cachedSamples, 0, count);
            _cachedSamplesCount = count;
            _hasCachedSamples = true; // キャッシュが利用可能であることをフラグ立て
        }
    }

    #endregion

    #region Public Methods

    public void ClearCache()
    {
        _hasCachedSamples = false;
        _cachedSamplesCount = 0;
    }

    public ComputeBuffer GetCountsBuffer()
    {
        return _countsBuffer;
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        _countReadbackPending = false;
        _samplesReadbackPending = false;
        _hasCachedSamples = false;
        
        _countsBuffer?.Release();
        _countsBuffer = null;
    }

    #endregion
}
