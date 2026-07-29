using System;
using UnityEngine;

namespace RealSense.DummyPointCloud
{
    /// <summary>
    /// RsDummyPointCloudProvider で生成されたダミーの実測点群を「動いたら更新 (Dirty-based Update)」方式で描画する専用レンダラー。
    /// ターゲットオブジェクトが移動・変更されたフレームのみ GPU に転送し、静止時は SetData を完全に回避して 0ms 超高速描画します。
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class RsDummyPointCloudRenderer : RsPointCloudRenderer
    {
        [Header("Dummy Dependencies")]
        [Tooltip("ダミー点群を供給する RsDummyPointCloudProvider")]
        public RsDummyPointCloudProvider dummyProvider;

        [Header("Debug & Gizmos Settings")]
        [Tooltip("True にすると、描画処理やバッファ確保の動作ログをコンソールに出力します")]
        public bool enableDebugLog = false;

        [Tooltip("True にすると Scene ビュー上に Gizmos のデバッグ球を重ねて表示します")]
        public bool showGizmos = false;

        private MeshRenderer _meshRenderer;
        private RsPointCloudVisualization _visualization;

        private ComputeBuffer _verticesBuffer;
        private ComputeBuffer _argsBuffer;
        private uint[] _argsData = new uint[4] { 0, 1, 0, 0 };

        private int _appliedDataVersion = -1;
        private int _lastLoggedPointCount = -1;
        private int _cachedValidPointCount = 0;

        private void Log(string message)
        {
            if (enableDebugLog)
            {
                UnityEngine.Debug.Log($"[RsDummyPointCloudRenderer: {gameObject.name}] {message}");
            }
        }

        private void OnEnable()
        {
            Log("RsDummyPointCloudRenderer enabled.");
            RegisterToGlobalManager();
        }

        private void Start()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            EnsureMaterial();

            _visualization = new RsPointCloudVisualization(_meshRenderer);
            _argsBuffer = new ComputeBuffer(1, sizeof(uint) * 4, ComputeBufferType.IndirectArguments);

            Log("Initialized Dirty-based GPU Single-Buffer & visualization renderer.");
            RegisterToGlobalManager();
        }

        private void RegisterToGlobalManager()
        {
            var manager = RsGlobalPointCloudManager.Instance;
            if (manager != null && manager.renderers != null)
            {
                if (!manager.renderers.Contains(this))
                {
                    manager.renderers.Add(this);
                    Log("Auto-registered RsDummyPointCloudRenderer to RsGlobalPointCloudManager.");
                }
            }
        }

        private void EnsureMaterial()
        {
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();

            if (_meshRenderer != null && _meshRenderer.sharedMaterial == null)
            {
                Material defaultMat = null;

                var allMats = Resources.FindObjectsOfTypeAll<Material>();
                foreach (var mat in allMats)
                {
                    if (mat != null && (mat.name.Contains("PointCloud") || mat.shader.name.Contains("PointCloud")))
                    {
                        defaultMat = mat;
                        break;
                    }
                }

                if (defaultMat == null)
                {
                    Shader pcdShader = Shader.Find("PointCloudViewer/PointCloudViewer") ??
                                       Shader.Find("HandTracking/PointCloudSprite") ??
                                       Shader.Find("Custom/PointCloud") ??
                                       Shader.Find("Unlit/Color");

                    if (pcdShader != null)
                    {
                        defaultMat = new Material(pcdShader) { name = "Generated_PointCloud_Material" };
                    }
                    else
                    {
                        defaultMat = new Material(Shader.Find("Hidden/InternalErrorShader"));
                    }
                }

                _meshRenderer.sharedMaterial = defaultMat;
                Log($"Auto-assigned Material '{defaultMat.name}' to MeshRenderer.");
            }
        }

        private void LateUpdate()
        {
            if (dummyProvider == null)
            {
#if UNITY_2023_1_OR_NEWER
                dummyProvider = FindFirstObjectByType<RsDummyPointCloudProvider>();
#else
                dummyProvider = FindObjectOfType<RsDummyPointCloudProvider>();
#endif
            }

            EnsureMaterial();

            // 1. オブジェクトが動いた / データが新しく更新された時 (DataVersion 変化時) のみ GPU バッファを更新
            if (dummyProvider != null && dummyProvider.DataVersion != _appliedDataVersion)
            {
                var sampledData = dummyProvider.LastSampledData;
                if (sampledData.Positions != null && sampledData.PointCount > 0)
                {
                    int count = sampledData.PointCount;
                    _cachedValidPointCount = count;

                    // バッファの確保・自動サイズ調整
                    if (_verticesBuffer == null || _verticesBuffer.count < count)
                    {
                        if (_verticesBuffer != null)
                        {
                            _verticesBuffer.Release();
                        }
                        int newSize = Mathf.NextPowerOfTwo(Mathf.Max(count, 1024));
                        _verticesBuffer = new ComputeBuffer(newSize, sizeof(float) * 3);
                        Log($"Reallocated ComputeBuffer to size {newSize} for {count} points.");
                    }

                    // 動いた瞬間だけ 1 回 GPU へ SetData 転送
                    _verticesBuffer.SetData(sampledData.Positions, 0, 0, count);

                    // Indirect Draw 引数の更新
                    _argsData[0] = (uint)count;
                    _argsBuffer.SetData(_argsData);

                    _appliedDataVersion = dummyProvider.DataVersion;
                    Log($"Updated GPU buffer for DataVersion {_appliedDataVersion} ({count} points).");
                }
            }

            // 2. 静止時は一切 SetData も計算も行わず、既存の GPU バッファで 0ms 高速描画のみ実行
            if (_verticesBuffer != null && _cachedValidPointCount > 0 && _visualization != null)
            {
                Color drawColor = (dummyProvider != null && dummyProvider.colorMode == PointColorMode.SolidColor)
                    ? dummyProvider.solidColor
                    : pointCloudColor;

                if (enableDebugLog && _cachedValidPointCount != _lastLoggedPointCount)
                {
                    _lastLoggedPointCount = _cachedValidPointCount;
                    Log($"Rendering {_cachedValidPointCount} points on GPU (Color: {drawColor}).");
                }

                _visualization.Draw(_verticesBuffer, _argsBuffer, drawColor, gameObject.layer);
            }
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            if (dummyProvider != null && dummyProvider.LastSampledData.Positions != null && dummyProvider.LastSampledData.PointCount > 0)
            {
                Gizmos.color = dummyProvider.colorMode == PointColorMode.SolidColor ? dummyProvider.solidColor : pointCloudColor;
                var positions = dummyProvider.LastSampledData.Positions;
                int count = dummyProvider.LastSampledData.PointCount;
                int step = Mathf.Max(1, count / 1000);

                for (int i = 0; i < count; i += step)
                {
                    Gizmos.DrawSphere(positions[i], 0.003f);
                }
            }
        }

        /// <summary> PCD マネージャーやオクルージョン計算等の外部コンポーネントへ点群バッファを提供するオーバーライド </summary>
        public override ComputeBuffer GetFilteredVerticesBuffer() => _verticesBuffer;
        public override int GetLastFilteredCount() => _cachedValidPointCount;
        public override ComputeBuffer GetPCDSourceBuffer() => _verticesBuffer;
        public override int GetPCDSourceCount() => _cachedValidPointCount;

        private void OnDisable()
        {
            Log("RsDummyPointCloudRenderer disabled.");
        }

        private void OnDestroy()
        {
            var manager = RsGlobalPointCloudManager.Instance;
            if (manager != null && manager.renderers != null)
            {
                manager.renderers.Remove(this);
            }

            if (_verticesBuffer != null)
            {
                _verticesBuffer.Release();
                _verticesBuffer = null;
            }

            if (_argsBuffer != null)
            {
                _argsBuffer.Release();
                _argsBuffer = null;
            }

            Log("GPU Single-Buffer resources destroyed.");
        }
    }
}
