using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using SRD.Utils;

namespace SRD.Core
{
    /// <summary>
    /// SRDの実カメラに対し、鏡面反射の幾何モデル・カリング・行列修正を適用するコンポーネント。
    /// デバッグログ出力は独立した SRDMirrorDebugLogger コンポーネントへ完全に移譲しています。
    /// </summary>
    [ExecuteAlways]
    public class SRDMirrorCamera : MonoBehaviour
    {
        [Header("Mirror Options")]
        [Tooltip("鏡像処理を有効にする")]
        public bool enableMirror = true;

        private SRDManager srdManager;

        private Dictionary<Camera, Matrix4x4> _originalViewMatrices = new Dictionary<Camera, Matrix4x4>();
        private Dictionary<Camera, Matrix4x4> _originalCullMatrices = new Dictionary<Camera, Matrix4x4>();
        private Dictionary<Camera, int> _originalCullingMasks = new Dictionary<Camera, int>();
        private Dictionary<Camera, bool> _originalInvertCulling = new Dictionary<Camera, bool>();
        private Dictionary<Camera, CameraClearFlags> _originalClearFlags = new Dictionary<Camera, CameraClearFlags>();
        private Dictionary<Camera, Color> _originalBackgroundColors = new Dictionary<Camera, Color>();

        private void EnsureDebugLogger()
        {
            if (GetComponent<SRDMirrorDebugLogger>() == null)
            {
                gameObject.AddComponent<SRDMirrorDebugLogger>();
            }
        }

        void Awake()
        {
            EnsureDebugLogger();

#if UNITY_2019_1_OR_NEWER
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
#if UNITY_2021_1_OR_NEWER
            RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
#else
            RenderPipelineManager.beginFrameRendering += OnBeginFrameRendering;
#endif
#endif
        }

        void Start()
        {
            srdManager = GetComponentInParent<SRDManager>();
            if (srdManager == null)
            {
                srdManager = FindFirstObjectByType<SRDManager>();
            }

            EnsureDebugLogger();
        }

        void Update()
        {
            SRDStereoCompositer.FlipRenderTextureX = enableMirror;
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || !enableMirror || srdManager == null) return;

            Camera[] cameras = GetComponentsInChildren<Camera>(true);
            foreach (var cam in cameras)
            {
                if (cam == null || !cam.enabled) continue;
                bool isLeft = cam.name.Contains("LeftEyeCamera");
                bool isRight = cam.name.Contains("RightEyeCamera");
                if (!isLeft && !isRight) continue;

                ApplyMirrorToCamera(cam);
            }
        }

        void OnDestroy()
        {
#if UNITY_2019_1_OR_NEWER
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
#if UNITY_2021_1_OR_NEWER
            RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
#else
            RenderPipelineManager.beginFrameRendering -= OnBeginFrameRendering;
#endif
#endif
            RestoreAllCameras();
        }

        void OnDisable()
        {
            RestoreAllCameras();
        }

        private void RestoreAllCameras()
        {
            foreach (var kvp in _originalViewMatrices)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.worldToCameraMatrix = kvp.Value;
                    kvp.Key.ResetStereoViewMatrices();
                }
            }
            foreach (var kvp in _originalCullMatrices)
            {
                if (kvp.Key != null) kvp.Key.ResetCullingMatrix();
            }
            foreach (var kvp in _originalCullingMasks)
            {
                if (kvp.Key != null) kvp.Key.cullingMask = kvp.Value;
            }
            foreach (var kvp in _originalClearFlags)
            {
                if (kvp.Key != null) kvp.Key.clearFlags = kvp.Value;
            }
            foreach (var kvp in _originalBackgroundColors)
            {
                if (kvp.Key != null) kvp.Key.backgroundColor = kvp.Value;
            }

            GL.invertCulling = false;
            SRDStereoCompositer.FlipRenderTextureX = false;

            _originalViewMatrices.Clear();
            _originalCullMatrices.Clear();
            _originalCullingMasks.Clear();
            _originalInvertCulling.Clear();
            _originalClearFlags.Clear();
            _originalBackgroundColors.Clear();
        }

#if UNITY_2019_1_OR_NEWER
#if UNITY_2021_1_OR_NEWER
        private void OnBeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (!Application.isPlaying || !enableMirror || srdManager == null) return;
            foreach (var cam in cameras)
            {
                if (cam == null) continue;
                bool isLeft = cam.name.Contains("LeftEyeCamera");
                bool isRight = cam.name.Contains("RightEyeCamera");
                if (isLeft || isRight)
                {
                    ApplyMirrorToCamera(cam);
                }
            }
        }
#else
        private void OnBeginFrameRendering(ScriptableRenderContext context, Camera[] cameras)
        {
            if (!Application.isPlaying || !enableMirror || srdManager == null) return;
            foreach (var cam in cameras)
            {
                if (cam == null) continue;
                bool isLeft = cam.name.Contains("LeftEyeCamera");
                bool isRight = cam.name.Contains("RightEyeCamera");
                if (isLeft || isRight)
                {
                    ApplyMirrorToCamera(cam);
                }
            }
        }
#endif

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (!Application.isPlaying || !enableMirror || srdManager == null) return;

            bool isLeft = cam.name.Contains("LeftEyeCamera");
            bool isRight = cam.name.Contains("RightEyeCamera");

            if (!isLeft && !isRight) return;

            ApplyMirrorToCamera(cam);
        }
#endif

        private void ApplyMirrorToCamera(Camera cam)
        {
            // バックアップ (未バックアップ時のみ)
            if (!_originalViewMatrices.ContainsKey(cam)) _originalViewMatrices[cam] = cam.worldToCameraMatrix;
            if (!_originalCullMatrices.ContainsKey(cam)) _originalCullMatrices[cam] = cam.cullingMatrix;
            if (!_originalCullingMasks.ContainsKey(cam)) _originalCullingMasks[cam] = cam.cullingMask;
            if (!_originalInvertCulling.ContainsKey(cam)) _originalInvertCulling[cam] = GL.invertCulling;
            if (!_originalClearFlags.ContainsKey(cam)) _originalClearFlags[cam] = cam.clearFlags;
            if (!_originalBackgroundColors.ContainsKey(cam)) _originalBackgroundColors[cam] = cam.backgroundColor;

            // 1. View Matrix の設定 (Case B Mirrored Basis)
            cam.worldToCameraMatrix = CalculateCaseBViewMatrix(cam, isRigid: false);

            // 2. Projection Matrix の設定
            // SRDEyeViewRenderer が設定したオフアキシス投影を尊重し、改変しない。

            // 3. cullingMatrix の強制上書き（CPUカリング用）
            cam.cullingMatrix = cam.projectionMatrix * CalculateCaseBViewMatrix(cam, isRigid: true);

            // 4. GL.invertCulling の適用
            GL.invertCulling = true;
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (!Application.isPlaying || !enableMirror) return;

            bool isLeft = cam.name.Contains("LeftEyeCamera");
            bool isRight = cam.name.Contains("RightEyeCamera");

            if (!isLeft && !isRight) return;

            if (_originalInvertCulling.TryGetValue(cam, out bool origCulling))
            {
                GL.invertCulling = origCulling;
            }
            else
            {
                GL.invertCulling = false;
            }

            if (_originalViewMatrices.TryGetValue(cam, out Matrix4x4 v)) cam.worldToCameraMatrix = v;
            if (_originalCullMatrices.TryGetValue(cam, out Matrix4x4 c)) cam.cullingMatrix = c;
            if (_originalCullingMasks.TryGetValue(cam, out int mask)) cam.cullingMask = mask;
            if (_originalClearFlags.TryGetValue(cam, out CameraClearFlags cf)) cam.clearFlags = cf;
            if (_originalBackgroundColors.TryGetValue(cam, out Color bg)) cam.backgroundColor = bg;
        }

        private Matrix4x4 CalculateCaseBViewMatrix(Camera cam, bool isRigid)
        {
            Vector3 localPos = srdManager.transform.InverseTransformPoint(cam.transform.position);
            localPos.x = -localPos.x;
            Vector3 mirroredWorldPos = srdManager.transform.TransformPoint(localPos);

            Vector3 srdRight = srdManager.transform.right;
            Vector3 mirroredForward = Vector3.Reflect(cam.transform.forward, srdRight).normalized;
            Vector3 mirroredUp = Vector3.Reflect(cam.transform.up, srdRight).normalized;
            Vector3 mirroredRight = isRigid ? Vector3.Cross(mirroredUp, mirroredForward).normalized : Vector3.Reflect(cam.transform.right, srdRight).normalized;

            Matrix4x4 C_mirrored = Matrix4x4.identity;
            C_mirrored.SetColumn(0, new Vector4(mirroredRight.x, mirroredRight.y, mirroredRight.z, 0f));
            C_mirrored.SetColumn(1, new Vector4(mirroredUp.x, mirroredUp.y, mirroredUp.z, 0f));
            C_mirrored.SetColumn(2, new Vector4(mirroredForward.x, mirroredForward.y, mirroredForward.z, 0f));
            C_mirrored.SetColumn(3, new Vector4(mirroredWorldPos.x, mirroredWorldPos.y, mirroredWorldPos.z, 1f));

            return Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * C_mirrored.inverse;
        }
    }
}
