using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Core.Logging;
using SRD.Utils;

namespace SRD.Core
{
    /// <summary>
    /// SRDMirrorCamera の行列計算・投影行列式・幾何状態をデバッグ監視・ログ出力する独立クラス。
    /// AppLogManager 一元管理に対応し、SRDMirrorCamera 本体からデバッグ責務を完全に分離します。
    /// </summary>
    [ExecuteAlways]
    [AppLoggable("SRD Display (PCD/SRD)")]
    [DisallowMultipleComponent]
    public class SRDMirrorDebugLogger : MonoBehaviour, IAppLoggable
    {
        public const string TagMirrorDebug = "SRD_MirrorCamDebug";
        public const string TagProjDetCheck = "SRD_ProjDetCheck";
        public const string TagNativeLog = SRDCorePlugin.TagNativeLog;

        private SRDManager _srdManager;
        private SRDMirrorCamera _mirrorCamera;

        private int _lastLoggedFrame = -1;
        private HashSet<string> _loggedEyesThisFrame = new HashSet<string>();

        public void RegisterLogTriggers(LogCategoryGroup group, HashSet<string> existingLabels)
        {
            AddSubTriggerIfNotExists(group, this, "[SRD_MirrorCamDebug] Matrix Deconstruction & NDC Verification", TagMirrorDebug, existingLabels);
            AddSubTriggerIfNotExists(group, this, "[SRD_ProjDetCheck] Projection Matrix Determinant Verification", TagProjDetCheck, existingLabels);
            AddSubTriggerIfNotExists(group, this, "[SRD_NativeLog] Native SDK Internal Log (oz-debug-log)", TagNativeLog, existingLabels);
        }

        private void AddSubTriggerIfNotExists(LogCategoryGroup group, Object targetObj, string label, string tag, HashSet<string> existingLabels)
        {
            if (!existingLabels.Contains(label) && !existingLabels.Contains(tag))
            {
                group.entries.Add(new LogInstanceEntry
                {
                    label = label,
                    tag = tag,
                    target = targetObj,
                    enableInfo = true,
                    enableWarning = true,
                    enableError = true
                });
                existingLabels.Add(label);
                existingLabels.Add(tag);
            }
        }

        private void Awake()
        {
            _mirrorCamera = GetComponent<SRDMirrorCamera>();
            _srdManager = GetComponentInParent<SRDManager>() ?? FindFirstObjectByType<SRDManager>();

#if UNITY_2019_1_OR_NEWER
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
#if UNITY_2021_1_OR_NEWER
            RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
#else
            RenderPipelineManager.beginFrameRendering += OnBeginFrameRendering;
#endif
#endif
        }

        private void OnDestroy()
        {
#if UNITY_2019_1_OR_NEWER
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
#if UNITY_2021_1_OR_NEWER
            RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
#else
            RenderPipelineManager.beginFrameRendering -= OnBeginFrameRendering;
#endif
#endif
        }

#if UNITY_2019_1_OR_NEWER
#if UNITY_2021_1_OR_NEWER
        private void OnBeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (!Application.isPlaying || _mirrorCamera == null || !_mirrorCamera.enableMirror) return;
            foreach (var cam in cameras)
            {
                if (IsSRDEyeCamera(cam))
                {
                    LogProjDetCheck("OnBeginContextRendering", cam);
                }
            }
        }
#else
        private void OnBeginFrameRendering(ScriptableRenderContext context, Camera[] cameras)
        {
            if (!Application.isPlaying || _mirrorCamera == null || !_mirrorCamera.enableMirror) return;
            foreach (var cam in cameras)
            {
                if (IsSRDEyeCamera(cam))
                {
                    LogProjDetCheck("OnBeginFrameRendering", cam);
                }
            }
        }
#endif

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (!Application.isPlaying || _mirrorCamera == null || !_mirrorCamera.enableMirror) return;

            if (IsSRDEyeCamera(cam))
            {
                LogProjDetCheck("OnBeginCameraRendering", cam);
                LogMirrorRelation(cam);
            }
        }
#endif

        private bool IsSRDEyeCamera(Camera cam)
        {
            if (cam == null) return false;
            return cam.name.Contains("LeftEyeCamera") || cam.name.Contains("RightEyeCamera");
        }

        private void LogProjDetCheck(string stageName, Camera cam)
        {
            if (!AppLogger.IsEnabled(this, TagProjDetCheck) && !AppLogger.IsEnabled(TagProjDetCheck)) return;
            if (Time.frameCount % 60 != 0) return;

            AppLogger.Log(this, $"[Proj Det Check] {stageName}: Frame={Time.frameCount} Cam={cam.name} det={cam.projectionMatrix.determinant:F4}", TagProjDetCheck);
        }

        private void LogMirrorRelation(Camera cam)
        {
            if (_srdManager == null) _srdManager = GetComponentInParent<SRDManager>() ?? FindFirstObjectByType<SRDManager>();
            if (_srdManager == null) return;

            if (!AppLogger.IsEnabled(this, TagMirrorDebug) && !AppLogger.IsEnabled(TagMirrorDebug)) return;
            if (Time.frameCount % 60 != 0) return;

            if (_lastLoggedFrame != Time.frameCount)
            {
                _lastLoggedFrame = Time.frameCount;
                _loggedEyesThisFrame.Clear();
            }

            if (_loggedEyesThisFrame.Contains(cam.name)) return;
            _loggedEyesThisFrame.Add(cam.name);

            string eyeTag = cam.name.Contains("Left") ? "LEFT EYE [左目]" : (cam.name.Contains("Right") ? "RIGHT EYE [右目]" : cam.name);

            System.Text.StringBuilder sb = new System.Text.StringBuilder(2048);
            sb.AppendLine($"=== [SRDMirrorCamera Matrix Method Debug: {eyeTag}] ===");
            sb.AppendLine($"[Eye] {eyeTag} (Camera: {cam.name})");
            sb.AppendLine($"[Config] ViewMode=CaseB_MirroredBasisTRS, ProjMode=Unmodified, InvertCulling=True, OverrideCullMatrix=True, CullingMask=0x{cam.cullingMask:X}");

            Matrix4x4 currentCullMatrix = cam.cullingMatrix;
            float cullDet = currentCullMatrix.determinant;
            Matrix4x4 expectedCullMatrix = cam.projectionMatrix * cam.worldToCameraMatrix;
            float expectedCullDet = expectedCullMatrix.determinant;
            float cullDiff = 0f;
            for (int i = 0; i < 16; i++) cullDiff += Mathf.Abs(currentCullMatrix[i] - expectedCullMatrix[i]);

            string targetRtName = (cam.targetTexture != null) ? $"{cam.targetTexture.name} ({cam.targetTexture.width}x{cam.targetTexture.height})" : "Null (Screen Backbuffer)";
            string rendererName = "Standard / Built-in";

            sb.AppendLine("■ 0. Camera Render State & URP Renderer Verification");
            sb.AppendLine($"   Camera Enabled: {cam.enabled}, ActiveInHierarchy: {cam.gameObject.activeInHierarchy}");
            sb.AppendLine($"   URP ScriptableRenderer: {rendererName}");
            sb.AppendLine($"   Target Texture: {targetRtName}");
            sb.AppendLine($"   Pixel Viewport: {cam.pixelRect}");
            sb.AppendLine($"   Clear Flags: {cam.clearFlags}, BG Color: {cam.backgroundColor}");
            sb.AppendLine($"   Current camera.cullingMatrix det: {cullDet:+0.0000;-0.0000}");
            sb.AppendLine($"   Expected (P * V) cullingMatrix det: {expectedCullDet:+0.0000;-0.0000}");
            sb.AppendLine($"   Difference (|Current - Expected| sum): {cullDiff:F6}");
            sb.AppendLine($"   Current CullingMask: 0x{cam.cullingMask:X8}");

            Vector3 realLocalPos = _srdManager.transform.InverseTransformPoint(cam.transform.position);
            Vector3 rf = _srdManager.transform.InverseTransformDirection(cam.transform.forward);
            Vector3 ru = _srdManager.transform.InverseTransformDirection(cam.transform.up);
            Vector3 rr = _srdManager.transform.InverseTransformDirection(cam.transform.right);
            float realDet = Vector3.Dot(rr, Vector3.Cross(ru, rf));

            Vector3 expLocalPos = new Vector3(-realLocalPos.x, realLocalPos.y, realLocalPos.z);
            Vector3 expForward = Vector3.Reflect(cam.transform.forward, _srdManager.transform.right).normalized;
            Vector3 expUp = Vector3.Reflect(cam.transform.up, _srdManager.transform.right).normalized;
            Vector3 expRight = Vector3.Reflect(cam.transform.right, _srdManager.transform.right).normalized;
            float expDet = Vector3.Dot(expRight, Vector3.Cross(expUp, expForward));

            Matrix4x4 mirrorView = cam.worldToCameraMatrix;
            float mirrorViewDet = mirrorView.determinant;

            sb.AppendLine("\n■ 1. SRDMirrorCamera Handedness & Basis Determinant");
            sb.AppendLine($"   Original Real Cam Basis Det (R . (U x F)): {realDet:+0.0000;-0.0000} (expected +1.0)");
            sb.AppendLine($"   Expected Mirrored Basis Det:               {expDet:+0.0000;-0.0000} (expected -1.0)");
            sb.AppendLine($"   Case B Mirrored View Matrix Det:           {mirrorViewDet:+0.0000;-0.0000} (expected -1.0)");

            sb.AppendLine("\n■ 2. Real vs Expected World Target Positions");
            sb.AppendLine($"   Real Cam World Pos:     {cam.transform.position:F4}");
            sb.AppendLine($"   Real Cam SRD-Local Pos: {realLocalPos:F4}");
            sb.AppendLine($"   Exp  Cam SRD-Local Pos: {expLocalPos:F4}");

            Matrix4x4 proj = cam.projectionMatrix;
            float projDet = proj.determinant;
            sb.AppendLine("\n■ 3. Projection Matrix Check");
            sb.AppendLine($"   Current proj.determinant: {projDet:+0.0000;-0.0000}");
            sb.AppendLine($"   Projection Matrix (SRD Off-Axis Unmodified):\n{FormatMatrix(proj)}");

            Vector3 testTargetLocal = new Vector3(0f, 0f, 0.5f);
            Vector3 testTargetWorld = _srdManager.transform.TransformPoint(testTargetLocal);

            Vector4 clipMirror = (proj * mirrorView) * new Vector4(testTargetWorld.x, testTargetWorld.y, testTargetWorld.z, 1.0f);
            Vector3 ndcMirror = (clipMirror.w != 0f) ? new Vector3(clipMirror.x / clipMirror.w, clipMirror.y / clipMirror.w, clipMirror.z / clipMirror.w) : Vector3.zero;

            sb.AppendLine("\n■ 4. Target Point (SRD-Local: (0, 0, 0.5m)) NDC Projection Test");
            sb.AppendLine($"   Target World Pos: {testTargetWorld:F4}");
            sb.AppendLine($"   Mirror Cam View NDC: {ndcMirror:F4}");

            AppLogger.Log(this, sb.ToString(), TagMirrorDebug);
        }

        private string FormatMatrix(Matrix4x4 m)
        {
            return $"[{m.m00:F4}, {m.m01:F4}, {m.m02:F4}, {m.m03:F4}]\n" +
                   $"[{m.m10:F4}, {m.m11:F4}, {m.m12:F4}, {m.m13:F4}]\n" +
                   $"[{m.m20:F4}, {m.m21:F4}, {m.m22:F4}, {m.m23:F4}]\n" +
                   $"[{m.m30:F4}, {m.m31:F4}, {m.m32:F4}, {m.m33:F4}]";
        }
    }
}
