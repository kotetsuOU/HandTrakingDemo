using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using Intel.RealSense;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

/// <summary>
/// ComputeShaderを利用してDepthFrameとColorFrameを合成し、
/// 色ベースのカリング（抽出）と座標変換を行いながら点群のComputeBufferを生成・更新する専用プロセッサ。
/// </summary>
public class RsIntegratedPointCloudProcessor : IDisposable
{
    // プロファイリング用に、従来手法でもスキップさせず毎回リードバックを発行させるフラグ
    public bool ForceReadbackEveryFrame { get; set; } = true;

    [StructLayout(LayoutKind.Sequential)]
    private struct RsIntrinsics { public int width, height; public float ppx, ppy, fx, fy; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RsExtrinsics
    {
        public float r0, r1, r2, r3, r4, r5, r6, r7, r8;
        public float t0, t1, t2;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CullingParams
    {
        public int width, height, mode;
        public float minDist, maxDist, minHue, maxHue, minSat, maxSat, minVal, maxVal;
        public int minY, maxY, minCb, maxCb, minCr, maxCr;
        public Matrix4x4 transformMatrix;
        public int coordinateConversion;
    }

    private ComputeShader _shader;
    private int _kernelIndex;

    private ComputeBuffer _depthIntrinsicsBuffer;
    private ComputeBuffer _colorIntrinsicsBuffer;
    private ComputeBuffer _extrinsicsBuffer;

    private ComputeBuffer[] _paramsBuffers = new ComputeBuffer[2];
    private ComputeBuffer[] _inputDepthBuffers = new ComputeBuffer[2];
    private ComputeBuffer[] _pointCloudBuffers = new ComputeBuffer[2];
    private ComputeBuffer[] _pointCloudCountBuffers = new ComputeBuffer[2];
    private ComputeBuffer[] _inputColorBuffers = new ComputeBuffer[2];

    private int _bufferIndex = 0;
    private int _lastDispatchedIndex = 0;

    private bool _countReadbackPending = false;
    private int _pendingPointCount = 0;

    private int _latestPointCount = 0;
    private bool _hasNewPointCloud = false;

    private RsIntrinsics _dIntrin;
    private RsIntrinsics _cIntrin;
    private RsExtrinsics _extrin;
    private bool _initialized = false;

    private readonly CullingParams[] _cullingParamsCache = new CullingParams[1];

    private byte[] _colorDataCache;
    private byte[] _depthDataCache;
    private CullingParams _pendingParams;
    private volatile bool _hasPendingFrame = false;
    private readonly object _frameLock = new object();

    public ComputeBuffer PointCloudBuffer => _pointCloudBuffers != null ? _pointCloudBuffers[_lastDispatchedIndex] : null;
    public int LastPointCount => _latestPointCount;
    public bool HasNewPointCloud => _hasNewPointCloud;

    public RsIntegratedPointCloudProcessor(ComputeShader shader)
    {
        _shader = shader;
        _kernelIndex = _shader.FindKernel("CSMain");
    }

    public void Initialize(RsDepthToColorCalibration calibration)
    {
        var dp = calibration.DepthProfile;
        var cp = calibration.ColorProfile;
        var di = dp.GetIntrinsics();
        var ci = cp.GetIntrinsics();
        var ex = dp.GetExtrinsicsTo(cp);

        _dIntrin = new RsIntrinsics { width = dp.Width, height = dp.Height, ppx = di.ppx, ppy = di.ppy, fx = di.fx, fy = di.fy };
        _cIntrin = new RsIntrinsics { width = cp.Width, height = cp.Height, ppx = ci.ppx, ppy = ci.ppy, fx = ci.fx, fy = ci.fy };
        _extrin = new RsExtrinsics
        {
            r0 = ex.rotation[0],
            r1 = ex.rotation[1],
            r2 = ex.rotation[2],
            r3 = ex.rotation[3],
            r4 = ex.rotation[4],
            r5 = ex.rotation[5],
            r6 = ex.rotation[6],
            r7 = ex.rotation[7],
            r8 = ex.rotation[8],
            t0 = ex.translation[0],
            t1 = ex.translation[1],
            t2 = ex.translation[2]
        };

        RsUnityMainThreadDispatcher.Instance.EnqueueAndWait(AllocateResources);
        _initialized = true;
    }

    private void AllocateResources()
    {
        ReleaseBuffers();

        _depthIntrinsicsBuffer = new ComputeBuffer(1, Marshal.SizeOf(typeof(RsIntrinsics)));
        _colorIntrinsicsBuffer = new ComputeBuffer(1, Marshal.SizeOf(typeof(RsIntrinsics)));
        _extrinsicsBuffer = new ComputeBuffer(1, Marshal.SizeOf(typeof(RsExtrinsics)));

        _depthIntrinsicsBuffer.SetData(new RsIntrinsics[] { _dIntrin });
        _colorIntrinsicsBuffer.SetData(new RsIntrinsics[] { _cIntrin });
        _extrinsicsBuffer.SetData(new RsExtrinsics[] { _extrin });

        int colorBytes = _cIntrin.width * _cIntrin.height * 3;
        int alignedColorBytes = ((colorBytes + 3) / 4) * 4;
        int depthPixelCount = _dIntrin.width * _dIntrin.height;
        int totalBytes = depthPixelCount * sizeof(ushort);

        for (int i = 0; i < 2; i++)
        {
            _paramsBuffers[i] = new ComputeBuffer(1, Marshal.SizeOf(typeof(CullingParams)));
            _inputColorBuffers[i] = new ComputeBuffer(alignedColorBytes / 4, 4, ComputeBufferType.Raw);
            _inputDepthBuffers[i] = new ComputeBuffer(totalBytes / 4, 4, ComputeBufferType.Raw);
            _pointCloudBuffers[i] = new ComputeBuffer(depthPixelCount, sizeof(float) * 3, ComputeBufferType.Append);
            _pointCloudCountBuffers[i] = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        }

        _colorDataCache = new byte[alignedColorBytes]; // RGB24 aligned
        _depthDataCache = new byte[totalBytes];

        _shader.SetBuffer(_kernelIndex, "_DepthIntrinsics", _depthIntrinsicsBuffer);
        _shader.SetBuffer(_kernelIndex, "_ColorIntrinsics", _colorIntrinsicsBuffer);
        _shader.SetBuffer(_kernelIndex, "_DepthToColorExtrinsics", _extrinsicsBuffer);
    }

    /// <summary>
    /// 各フレームの画像データを受け取り、GPUへ転送するためのキャッシュにコピーします。
    /// 実際のComputeShader呼び出しはUnityのメインスレッドで実行されるようキューに積まれます。
    /// </summary>
    public Vector3[] Process(VideoFrame colorFrame, DepthFrame depthFrame, RsIntegratedPointCloud parent)
    {
        if (!_initialized) return null;

        var dispatcher = RsUnityMainThreadDispatcher.Instance;
        if (dispatcher == null)
        {
            return null;
        }

        int colorBytes = colorFrame.Stride * colorFrame.Height;
        int depthBytes = depthFrame.Stride * depthFrame.Height;

        // 非同期スレッドから呼ばれる可能性を考慮し、キャッシュバッファへのコピースレッドを保護する
        lock (_frameLock)
        {
            // RealSenseのC++側(アンマネージド)メモリからC#のマネージド配列へ高速にコピー
            Marshal.Copy(colorFrame.Data, _colorDataCache, 0, System.Math.Min(colorBytes, _colorDataCache.Length));
            Marshal.Copy(depthFrame.Data, _depthDataCache, 0, System.Math.Min(depthBytes, _depthDataCache.Length));

            // 現在の閾値パラメータや変換行列などの状態を退避させる
            _pendingParams = new CullingParams
            {
                width = _dIntrin.width,
                height = _dIntrin.height,
                mode = (int)parent._mode,
                minDist = parent._minDistance,
                maxDist = parent._maxDistance,
                minHue = parent._minHue,
                maxHue = parent._maxHue,
                minSat = parent._minSaturation,
                maxSat = parent._maxSaturation,
                minVal = parent._minValue,
                maxVal = parent._maxValue,
                minY = parent._minY,
                maxY = parent._maxY,
                minCb = parent._minCb,
                maxCb = parent._maxCb,
                minCr = parent._minCr,
                maxCr = parent._maxCr,
                transformMatrix = parent._transformMatrix,
                coordinateConversion = (int)parent._coordinateConversion
            };
            _hasPendingFrame = true;
        }

        // Textureの更新やComputeShaderのDispatch等、Unityの一部のAPIはメインスレッドでのみ実行可能なため委譲する
        dispatcher.Enqueue(ProcessPendingFrame);

        return null;
    }

    /// <summary>
    /// メインスレッド上で実行され、キャッシュされたカラー・深度データをGPUへ送り、
    /// 点群生成およびフィルタリングを行うComputeShaderカーネルをディスパッチ(実行)します。
    /// </summary>
    private void ProcessPendingFrame()
    {
        if (!_hasPendingFrame) return;

        lock (_frameLock)
        {
            _hasPendingFrame = false;

            _bufferIndex = (_bufferIndex + 1) % 2;

            // ComputeBufferにカラーデータを直接転送する
            _inputColorBuffers[_bufferIndex].SetData(_colorDataCache);

            // 深度データをバッファにセット
            _inputDepthBuffers[_bufferIndex].SetData(_depthDataCache);

            // カリング用のパラメータを更新
            _cullingParamsCache[0] = _pendingParams;
            _paramsBuffers[_bufferIndex].SetData(_cullingParamsCache);

            _shader.SetBuffer(_kernelIndex, "_Params", _paramsBuffers[_bufferIndex]);
            _shader.SetBuffer(_kernelIndex, "_InputColorBuffer", _inputColorBuffers[_bufferIndex]);
            _shader.SetBuffer(_kernelIndex, "_InputDepthBuffer", _inputDepthBuffers[_bufferIndex]);
            _shader.SetBuffer(_kernelIndex, "_OutputPointCloud", _pointCloudBuffers[_bufferIndex]);

            // 出力用Appendバッファ内の要素数(カウンタ)を0にリセットして初期化する
            _pointCloudBuffers[_bufferIndex].SetCounterValue(0);

            // 深度画像の総ピクセル数に応じたスレッドグループ数を計算してComputeShaderを実行
            int threadGroups = ((_dIntrin.width * _dIntrin.height) + 63) / 64;
            _shader.Dispatch(_kernelIndex, threadGroups, 1, 1);

            _lastDispatchedIndex = _bufferIndex;

            // フィルタリングされた点群の数を非同期にCPUへ読み戻すリクエストを発行
            if (ForceReadbackEveryFrame || !_countReadbackPending) RequestAsyncReadback();
        }
    }

    private void RequestAsyncReadback()
    {
        _countReadbackPending = true;
        ComputeBuffer.CopyCount(_pointCloudBuffers[_lastDispatchedIndex], _pointCloudCountBuffers[_lastDispatchedIndex], 0);
        AsyncGPUReadback.Request(_pointCloudCountBuffers[_lastDispatchedIndex], OnCountReadbackComplete);
    }

    private void OnCountReadbackComplete(AsyncGPUReadbackRequest request)
    {
        _countReadbackPending = false;
        if (request.hasError) return;

        var countData = request.GetData<int>();
        if (countData.Length > 0)
        {
            _pendingPointCount = countData[0];
            _latestPointCount = _pendingPointCount;
            _hasNewPointCloud = true;
        }
    }

    private void UpdateParams(RsIntegratedPointCloud p)
    {
        _cullingParamsCache[0] = new CullingParams
        {
            width = _dIntrin.width,
            height = _dIntrin.height,
            mode = (int)p._mode,
            minDist = p._minDistance,
            maxDist = p._maxDistance,
            minHue = p._minHue,
            maxHue = p._maxHue,
            minSat = p._minSaturation,
            maxSat = p._maxSaturation,
            minVal = p._minValue,
            maxVal = p._maxValue,
            minY = p._minY,
            maxY = p._maxY,
            minCb = p._minCb,
            maxCb = p._maxCb,
            minCr = p._minCr,
            maxCr = p._maxCr,
            transformMatrix = p._transformMatrix,
            coordinateConversion = (int)p._coordinateConversion
        };
        _paramsBuffers[_bufferIndex].SetData(_cullingParamsCache);
        _shader.SetBuffer(_kernelIndex, "_Params", _paramsBuffers[_bufferIndex]);
    }



    public void UpdateTransformMatrix(Matrix4x4 matrix)
    {
        // This will be picked up in the next UpdateParams call during Process
        // We can also force an update here if needed, but Process is called every frame anyway
    }

    private void ReleaseBuffers()
    {
        _countReadbackPending = false;
        if (_depthIntrinsicsBuffer != null) { _depthIntrinsicsBuffer.Release(); _depthIntrinsicsBuffer = null; }
        if (_colorIntrinsicsBuffer != null) { _colorIntrinsicsBuffer.Release(); _colorIntrinsicsBuffer = null; }
        if (_extrinsicsBuffer != null) { _extrinsicsBuffer.Release(); _extrinsicsBuffer = null; }

        if (_paramsBuffers != null)
        {
            for (int i = 0; i < 2; i++)
            {
                if (_paramsBuffers[i] != null) { _paramsBuffers[i].Release(); _paramsBuffers[i] = null; }
                if (_inputDepthBuffers[i] != null) { _inputDepthBuffers[i].Release(); _inputDepthBuffers[i] = null; }
                if (_pointCloudBuffers[i] != null) { _pointCloudBuffers[i].Release(); _pointCloudBuffers[i] = null; }
                if (_pointCloudCountBuffers[i] != null) { _pointCloudCountBuffers[i].Release(); _pointCloudCountBuffers[i] = null; }
                if (_inputColorBuffers[i] != null) { _inputColorBuffers[i].Release(); _inputColorBuffers[i] = null; }
            }
        }
    }

    public void Dispose() => ReleaseBuffers();
}
