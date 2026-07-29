using System;
using UnityEngine;

namespace RealSense.DummyPointCloud
{
    /// <summary>
    /// RsDummyPointCloudProvider で生成されたダミーの実測点群を
    /// 高速な GPU Procedural 描画 (Graphics.DrawProcedural) でシーン上に直接レンダリングする専用レンダラー。
    /// RsPointCloudRenderer を継承することで、システム内の他の全処理コンポーネントと完全な互換性を保ちます。
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
        private int _lastLoggedPointCount = -1;

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
        }

        private void Start()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            EnsureMaterial();

            _visualization = new RsPointCloudVisualization(_meshRenderer);
            _argsBuffer = new ComputeBuffer(1, sizeof(uint) * 4, ComputeBufferType.IndirectArguments);
            Log("Initialized GPU buffers and visualization renderer.");
        }

        /// <summary>
        /// MeshRenderer に点群描画用のマテリアルがアタッチされていない場合、自動的に検索・生成して適用します。
        /// </summary>
        private void EnsureMaterial()
        {
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();

            if (_meshRenderer != null && _meshRenderer.sharedMaterial == null)
            {
                // 1. プロジェクト内から既存の点群マテリアルを検索
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

                // 2. 見つからない場合は点群対応のデフォルトマテリアルを作成
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
                if (dummyProvider == null) return;
            }

            EnsureMaterial();

            var sampledData = dummyProvider.LastSampledData;
            if (sampledData.Positions == null || sampledData.PointCount == 0) return;

            int count = sampledData.PointCount;

            // バッファの確保・自動リサイズ
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

            // GPUへの頂点データ転送
            _verticesBuffer.SetData(sampledData.Positions, 0, 0, count);

            // Indirect Draw 引数の設定
            _argsData[0] = (uint)count;
            _argsBuffer.SetData(_argsData);

            // カラー設定の同期
            Color drawColor = (dummyProvider.colorMode == PointColorMode.SolidColor)
                ? dummyProvider.solidColor
                : pointCloudColor;

            if (enableDebugLog && count != _lastLoggedPointCount)
            {
                _lastLoggedPointCount = count;
                Log($"Rendering {count} points on GPU (Color: {drawColor}, Material: {_meshRenderer.sharedMaterial?.name}).");
            }

            // RealSense 標準の GPU Procedural 描画を実行
            if (_visualization != null)
            {
                _visualization.Draw(_verticesBuffer, _argsBuffer, drawColor, gameObject.layer);
            }
        }

        private void OnDrawGizmos()
        {
            // showGizmos が true の場合のみ Scene ビュー上にデバッグ球を描画
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

        public new ComputeBuffer GetFilteredVerticesBuffer() => _verticesBuffer;
        public new int GetLastFilteredCount() => dummyProvider != null ? dummyProvider.LastSampledData.PointCount : 0;

        private void OnDisable()
        {
            Log("RsDummyPointCloudRenderer disabled.");
        }

        private void OnDestroy()
        {
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

            Log("GPU resources destroyed.");
        }
    }
}
