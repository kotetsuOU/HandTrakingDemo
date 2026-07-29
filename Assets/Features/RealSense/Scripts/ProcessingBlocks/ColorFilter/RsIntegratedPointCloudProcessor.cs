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
        public int colorFormat; // 0: RGB8, 1: YUYV
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
        if (calibration == null || !calibration.IsValid || calibration.DepthProfile == null)
        {
            UnityEngine.Debug.LogWarning("[RsIntegratedPointCloudProcessor] Invalid calibration provided. Skipping initialization.");
            return;
        }

        var dp = calibration.DepthProfile;
        var cp = calibration.ColorProfile ?? dp;
        var di = dp.GetIntrinsics();
        var ci = cp.GetIntrinsics();
        
        Extrinsics ex = default;
        if (calibration.ColorProfile != null)
        {
            try { ex = dp.GetExtrinsicsTo(cp); } catch { }
        }

        _dIntrin = new RsIntrinsics { width = dp.Width, height = dp.Height, ppx = di.ppx, ppy = di.ppy, fx = di.fx, fy = di.fy };
        _cIntrin = new RsIntrinsics { width = cp.Width, height = cp.Height, ppx = ci.ppx, ppy = ci.ppy, fx = ci.fx, fy = ci.fy };
        _extrin = new RsExtrinsics
        {
            r0 = ex.rotation != null && ex.rotation.Length >= 9 ? ex.rotation[0] : 1,
            r1 = ex.rotation != null && ex.rotation.Length >= 9 ? ex.rotation[1] : 0,
            r2 = ex.rotation != null && ex.rotation.Length >= 9 ? ex.rotation[2] : 0,
            r3 = ex.rotation != null && ex.rotation.Length >= 9 ? ex.rotation[3] : 0,
            r4 = ex.rotation != null && ex.rotation.Length >= 9 ? ex.rotation[4] : 1,
            r5 = ex.rotation != null && ex.rotation.Length >= 9 ? ex.rotation[5] : 0,
            r6 = ex.rotation != null && ex.rotation.Length >= 9 ? ex.rotation[6] : 0,
            r7 = ex.rotation != null && ex.rotation.Length >= 9 ? ex.rotation[7] : 0,
            r8 = ex.rotation != null && ex.rotation.Length >= 9 ? ex.rotation[8] : 1,
            t0 = ex.translation != null && ex.translation.Length >= 3 ? ex.translation[0] : 0,
            t1 = ex.translation != null && ex.translation.Length >= 3 ? ex.translation[1] : 0,
            t2 = ex.translation != null && ex.translation.Length >= 3 ? ex.translation[2] : 0
        };

        if (RsUnityMainThreadDispatcher.Instance != null)
        {
            RsUnityMainThreadDispatcher.Instance.EnqueueAndWait(AllocateResources);
            _initialized = true;
        }
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

        int colorBytes = colorFrame != null ? colorFrame.Stride * colorFrame.Height : 0;
        int depthBytes = depthFrame != null ? depthFrame.Stride * depthFrame.Height : 0;

        lock (_frameLock)
        {
            if (colorFrame != null)
                Marshal.Copy(colorFrame.Data, _colorDataCache, 0, System.Math.Min(colorBytes, _colorDataCache.Length));
            if (depthFrame != null)
                Marshal.Copy(depthFrame.Data, _depthDataCache, 0, System.Math.Min(depthBytes, _depthDataCache.Length));

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
                coordinateConversion = (int)parent._coordinateConversion,
                colorFormat = (colorFrame != null && colorFrame.Profile.Format == Format.Yuyv) ? 1 : 0
            };
            _hasPendingFrame = true;
        }

        dispatcher.Enqueue(ProcessPendingFrame);

        return null;
    }

    private void ProcessPendingFrame()
    {
        if (!_hasPendingFrame) return;

        lock (_frameLock)
        {
            _hasPendingFrame = false;

            _bufferIndex = (_bufferIndex + 1) % 2;

            _inputColorBuffers[_bufferIndex].SetData(_colorDataCache);
            _inputDepthBuffers[_bufferIndex].SetData(_depthDataCache);

            _cullingParamsCache[0] = _pendingParams;
            _paramsBuffers[_bufferIndex].SetData(_cullingParamsCache);

            _shader.SetBuffer(_kernelIndex, "_Params", _paramsBuffers[_bufferIndex]);
            _shader.SetBuffer(_kernelIndex, "_InputColorBuffer", _inputColorBuffers[_bufferIndex]);
            _shader.SetBuffer(_kernelIndex, "_InputDepthBuffer", _inputDepthBuffers[_bufferIndex]);
            _shader.SetBuffer(_kernelIndex, "_OutputPointCloud", _pointCloudBuffers[_bufferIndex]);

            _pointCloudBuffers[_bufferIndex].SetCounterValue(0);

            int threadGroups = ((_dIntrin.width * _dIntrin.height) + 63) / 64;
            _shader.Dispatch(_kernelIndex, threadGroups, 1, 1);

            _lastDispatchedIndex = _bufferIndex;

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

    public void UpdateTransformMatrix(Matrix4x4 matrix)
    {
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
