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

        private Vector3[] _tempTransformBuffer;
        private Component _lastAdjuster;
        private bool _lastHalfMirrorEnabled = false;
        private Matrix4x4 _lastMirrorMatrix = Matrix4x4.identity;

        private static Type s_cameraAdjusterType;
        private static System.Reflection.FieldInfo s_isHalfMirrorEnabledField;
        private static System.Reflection.FieldInfo s_displayTransformField;
        private static bool s_typeSearched = false;

        private Matrix4x4 GetHalfMirrorMatrix(out Component adjusterComp, out bool isHalfMirrorEnabled)
        {
            adjusterComp = null;
            isHalfMirrorEnabled = false;

            if (!s_typeSearched)
            {
                s_typeSearched = true;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("CameraAdjuster");
                    if (t != null && typeof(Component).IsAssignableFrom(t))
                    {
                        s_cameraAdjusterType = t;
                        s_isHalfMirrorEnabledField = t.GetField("isHalfMirrorEnabled");
                        s_displayTransformField = t.GetField("displayTransform");
                        break;
                    }
                }
            }

            if (s_cameraAdjusterType == null)
            {
                return Matrix4x4.identity;
            }

#if UNITY_2023_1_OR_NEWER
            adjusterComp = UnityEngine.Object.FindFirstObjectByType(s_cameraAdjusterType) as Component;
#else
            adjusterComp = UnityEngine.Object.FindObjectOfType(s_cameraAdjusterType) as Component;
#endif

            if (adjusterComp == null)
            {
                return Matrix4x4.identity;
            }

            isHalfMirrorEnabled = true;
            if (s_isHalfMirrorEnabledField != null)
            {
                var val = s_isHalfMirrorEnabledField.GetValue(adjusterComp);
                if (val is bool b) isHalfMirrorEnabled = b;
            }

            if (!isHalfMirrorEnabled)
            {
                return Matrix4x4.identity;
            }

            Transform displayTransform = null;
            if (s_displayTransformField != null)
            {
                displayTransform = s_displayTransformField.GetValue(adjusterComp) as Transform;
            }

            if (displayTransform != null)
            {
                Vector3 center = displayTransform.position;
                Quaternion rotation = displayTransform.rotation;
                Matrix4x4 displayTRS = Matrix4x4.TRS(center, rotation, Vector3.one);
                Matrix4x4 flipX = Matrix4x4.Scale(new Vector3(-1, 1, 1));
                Matrix4x4 displayInverse = displayTRS.inverse;
                return displayTRS * flipX * displayInverse;
            }
            else
            {
                return Matrix4x4.Scale(new Vector3(-1, 1, 1));
            }
        }

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

                Shader pcdShader = Shader.Find("Custom/PointCloudSprite") ??
                                   Shader.Find("HandTracking/PointCloudSprite") ??
                                   Shader.Find("PointCloudViewer/PointCloudViewer") ??
                                   Shader.Find("Unlit/Color");

                if (pcdShader != null)
                {
                    defaultMat = new Material(pcdShader) { name = "Generated_PointCloud_Material" };
                }
                else
                {
                    defaultMat = new Material(Shader.Find("Hidden/InternalErrorShader"));
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

            Matrix4x4 mirrorMatrix = GetHalfMirrorMatrix(out var currentAdjuster, out bool isHalfMirrorEnabled);

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

                    // PCD / HCD 用には正統な素のワールド座標バッファを保持
                    _verticesBuffer.SetData(sampledData.Positions, 0, 0, count);

                    // Indirect Draw 引数の更新
                    _argsData[0] = (uint)count;
                    _argsBuffer.SetData(_argsData);

                    _appliedDataVersion = dummyProvider.DataVersion;
                    Log($"Updated GPU buffer for DataVersion {_appliedDataVersion} ({count} points).");
                }
            }

            _lastAdjuster = currentAdjuster;
            _lastHalfMirrorEnabled = isHalfMirrorEnabled;
            _lastMirrorMatrix = mirrorMatrix;

            // 2. 画面描画時にシェーダー経由で HalfMirror 行列を適用
            if (_verticesBuffer != null && _cachedValidPointCount > 0 && _visualization != null)
            {
                Color drawColor = (dummyProvider != null && dummyProvider.colorMode == PointColorMode.SolidColor)
                    ? dummyProvider.solidColor
                    : pointCloudColor;

                if (enableDebugLog && _cachedValidPointCount != _lastLoggedPointCount)
                {
                    _lastLoggedPointCount = _cachedValidPointCount;
                    Log($"Rendering {_cachedValidPointCount} points on GPU (Color: {drawColor}, HalfMirror={isHalfMirrorEnabled}).");
                }

                _visualization.Draw(_verticesBuffer, _argsBuffer, drawColor, gameObject.layer, isHalfMirrorEnabled ? mirrorMatrix : Matrix4x4.identity);
            }
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            if (dummyProvider != null && dummyProvider.LastSampledData.Positions != null && dummyProvider.LastSampledData.PointCount > 0)
            {
                Matrix4x4 mirrorMatrix = GetHalfMirrorMatrix(out _, out bool isHalfMirrorEnabled);
                Gizmos.color = dummyProvider.colorMode == PointColorMode.SolidColor ? dummyProvider.solidColor : pointCloudColor;
                var positions = dummyProvider.LastSampledData.Positions;
                int count = dummyProvider.LastSampledData.PointCount;
                int step = Mathf.Max(1, count / 1000);

                for (int i = 0; i < count; i += step)
                {
                    Vector3 pos = isHalfMirrorEnabled ? mirrorMatrix.MultiplyPoint3x4(positions[i]) : positions[i];
                    Gizmos.DrawSphere(pos, 0.003f);
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
