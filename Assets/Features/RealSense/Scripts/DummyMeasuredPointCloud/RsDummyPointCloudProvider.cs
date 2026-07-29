using System;
using System.Collections;
using System.Collections.Generic;
using Intel.RealSense;
using UnityEngine;

namespace RealSense.DummyPointCloud
{
    /// <summary>
    /// Unityの3D Object (MeshFilter / SkinnedMeshRenderer) のリストから
    /// 物理密度や色を指定してダミーの実測点群を生成し、
    /// RsProcessingPipe の Source (RsFrameProvider) として供給するコンポーネント。
    /// </summary>
    [DisallowMultipleComponent]
    public class RsDummyPointCloudProvider : RsFrameProvider
    {
        [Header("Target 3D Objects Settings")]
        [Tooltip("実測点群の生成元となる Unity 3D オブジェクトのリスト")]
        public List<GameObject> targetObjects = new List<GameObject>();

        [Tooltip("各オブジェクトの子要素にある全ての Mesh/SkinnedMeshRenderer を含めて点群化するかどうか")]
        public bool includeChildren = true;

        [Header("Physical Point Density & Color")]
        [Tooltip("点群の物理密度の指定単位")]
        public PointDensityUnit densityUnit = PointDensityUnit.PointsPerMm2;

        [Tooltip("密度の数値（1mm^2あたりの点数、または点間隔mmなど）")]
        [Range(0.001f, 1000f)]
        public float densityValue = 1.0f; // デフォルト: 1 mm^2 あたり 1 点

        [Tooltip("点群のカラー指定モード")]
        public PointColorMode colorMode = PointColorMode.SolidColor;

        [Tooltip("SolidColor モード時の点群およびマテリアルの色")]
        public Color solidColor = new Color(241f / 255f, 187f / 255f, 147f / 255f, 1f);

        [Tooltip("SolidColor 変更時にターゲットオブジェクトのマテリアルカラーおよび RsPointCloudRenderer の描画色も連動して変更するかどうか")]
        public bool applyColorToMaterialAndRenderer = true;

        [Header("Camera Perspective Settings")]
        [Tooltip("true: カメラ視点・画角・オクルージョンを適用 / false: カメラの向き問わず全方向の全点群を出力")]
        public bool useCameraPerspective = true;

        [Tooltip("ダミー視点となる仮想 RealSense カメラの位置・姿勢（指定しない場合は本オブジェクトの Transform）")]
        public Transform simulatedCameraTransform;

        [Tooltip("視覚的な仮解像度 (Width)")]
        public int depthWidth = 640;

        [Tooltip("視覚的な仮解像度 (Height)")]
        public int depthHeight = 480;

        [Tooltip("更新フレームレート (FPS)")]
        [Range(1, 60)]
        public int updateFPS = 30;

        public override event Action<PipelineProfile> OnStart;
        public override event Action OnStop;
        public override event Action<Frame> OnNewSample;

        private RsMeshPointCloudSampler _sampler;
        private RsDummySoftwareDevice _softwareDevice;
        private Coroutine _streamingCoroutine;
        private MaterialPropertyBlock _materialPropertyBlock;

        public SampledPointCloudData LastSampledData { get; private set; }

        private void Awake()
        {
            _sampler = new RsMeshPointCloudSampler();
            _materialPropertyBlock = new MaterialPropertyBlock();
            if (simulatedCameraTransform == null)
            {
                simulatedCameraTransform = transform;
            }
        }

        private void OnEnable()
        {
            StartStreaming();
            UpdateMaterialAndRendererColors();
        }

        private void OnDisable()
        {
            StopStreaming();
        }

        private void OnDestroy()
        {
            StopStreaming();
            _sampler = null;
        }

        private void OnValidate()
        {
            UpdateMaterialAndRendererColors();
        }

        /// <summary>
        /// SolidColor モード時に、対象 Target Objects のマテリアルカラーおよび RsPointCloudRenderer の描画色を solidColor に直接変更・同期します。
        /// </summary>
        public void UpdateMaterialAndRendererColors()
        {
            if (!applyColorToMaterialAndRenderer || colorMode != PointColorMode.SolidColor) return;

            // 1. Target Objects のマテリアルカラーを変更
            if (targetObjects != null)
            {
                if (_materialPropertyBlock == null) _materialPropertyBlock = new MaterialPropertyBlock();

                foreach (var obj in targetObjects)
                {
                    if (obj == null) continue;

                    var renderers = includeChildren
                        ? obj.GetComponentsInChildren<Renderer>()
                        : obj.GetComponents<Renderer>();

                    foreach (var r in renderers)
                    {
                        if (r == null) continue;

                        r.GetPropertyBlock(_materialPropertyBlock);
                        _materialPropertyBlock.SetColor("_Color", solidColor);
                        _materialPropertyBlock.SetColor("_BaseColor", solidColor);
                        r.SetPropertyBlock(_materialPropertyBlock);
                    }
                }
            }

            // 2. 紐づく RsPointCloudRenderer の描画色 (pointCloudColor) を変更
#if UNITY_2023_1_OR_NEWER
            var pcdRenderers = FindObjectsByType<RsPointCloudRenderer>(FindObjectsSortMode.None);
#else
            var pcdRenderers = FindObjectsOfType<RsPointCloudRenderer>();
#endif
            foreach (var pcdRenderer in pcdRenderers)
            {
                if (pcdRenderer != null && pcdRenderer.processingPipe != null)
                {
                    pcdRenderer.pointCloudColor = solidColor;
                }
            }
        }

        public void StartStreaming()
        {
            if (Streaming) return;

            _softwareDevice = new RsDummySoftwareDevice();
            _softwareDevice.Initialize(depthWidth, depthHeight, updateFPS);
            _softwareDevice.OnFrameAvailable += HandleNewFrame;

            ActiveProfile = _softwareDevice.ActiveProfile;
            Streaming = true;

            OnStart?.Invoke(ActiveProfile);

            _streamingCoroutine = StartCoroutine(StreamingLoop());
        }

        public void StopStreaming()
        {
            if (!Streaming) return;

            if (_streamingCoroutine != null)
            {
                StopCoroutine(_streamingCoroutine);
                _streamingCoroutine = null;
            }

            if (_softwareDevice != null)
            {
                _softwareDevice.OnFrameAvailable -= HandleNewFrame;
                _softwareDevice.Dispose();
                _softwareDevice = null;
            }

            Streaming = false;
            OnStop?.Invoke();
        }

        private IEnumerator StreamingLoop()
        {
            while (Streaming)
            {
                UpdateMaterialAndRendererColors();

                if (targetObjects != null && targetObjects.Count > 0)
                {
                    // 1. Mesh / SkinnedMesh リストから物理密度・色に応じた点群をサンプリング
                    LastSampledData = _sampler.SamplePointCloud(
                        targetObjects,
                        includeChildren,
                        densityUnit,
                        densityValue,
                        colorMode,
                        solidColor);

                    // 2. SoftwareDevice 経由で RealSense DepthFrame / FrameSet として発行
                    if (_softwareDevice != null && LastSampledData.PointCount > 0)
                    {
                        Transform camXform = simulatedCameraTransform != null ? simulatedCameraTransform : transform;
                        _softwareDevice.PublishPointCloudAsDepthFrame(
                            LastSampledData.Positions,
                            camXform,
                            useCameraPerspective);
                    }
                }

                float waitSec = 1.0f / Mathf.Max(1, updateFPS);
                yield return new WaitForSeconds(waitSec);
            }
        }

        private void HandleNewFrame(Frame frame)
        {
            if (Streaming && frame != null)
            {
                OnNewSample?.Invoke(frame);
            }
        }
    }
}
