#pragma warning disable 0618, 0672

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Core.Logging;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
#endif

namespace Core.Debug
{
    /// <summary>
    /// URP内部の CameraData (View / Projection 行列, CullingResults) と
    /// Unity Camera コンポーネントの行列状態を比較ログ出力するための独立した ScriptableRendererFeature。
    /// AppLogger / AppLogManager による統一制御に対応しています。
    /// </summary>
    [AppLoggable("URP / RenderPipelines")]
    public class URPMatrixDebugFeature : ScriptableRendererFeature, IAppLoggable
    {
        public const string TagURPMatrixDebug = URP_LogTriggers.TagMatrixDebug;

        [System.Serializable]
        public class DebugSettings
        {
            [Tooltip("RenderPassの実行イベントタイミング (初期状態検証のため BeforeRendering を推奨)")]
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRendering;

            [Tooltip("LeftEyeCamera / RightEyeCamera のみログ出力対象にする (False の場合は全メインカメラ対象)")]
            public bool filterSRDEyeCamerasOnly = false;
        }

        public DebugSettings settings = new DebugSettings();
        private URPMatrixDebugPass _debugPass;

        public void RegisterLogTriggers(LogCategoryGroup group, HashSet<string> existingLabels)
        {
            var triggers = FindFirstObjectByType<URP_LogTriggers>();
            if (triggers != null)
            {
                triggers.RegisterLogTriggers(group, existingLabels);
            }
            else
            {
                AddSubTriggerIfNotExists(group, this, "[URP_MatrixDebug] URP Pass State & Matrix Diagnostics", TagURPMatrixDebug, existingLabels);
            }
        }

        private void AddSubTriggerIfNotExists(LogCategoryGroup group, Object targetObj, string label, string tag, HashSet<string> existingLabels)
        {
            if (!existingLabels.Contains(label))
            {
                group.entries.Add(new LogInstanceEntry
                {
                    label = label,
                    tag = tag,
                    target = targetObj,
                    enabled = true
                });
                existingLabels.Add(label);
            }
        }

        public override void Create()
        {
            _debugPass = new URPMatrixDebugPass(this, settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // URPMatrixDebug はデバッグログ出力のみを行う機能のため、
            // ログ無効時（Info OFF）は RenderPass のエンキュー自体を完全スキップし、URP描画パイプライン構築コストをゼロに抑えます。
            bool isLogEnabled = AppLogger.IsEnabled(this, TagURPMatrixDebug) || AppLogger.IsEnabled(TagURPMatrixDebug);
            if (!isLogEnabled) return;

            if (renderingData.cameraData.cameraType == CameraType.Preview) return;

            if (settings.filterSRDEyeCamerasOnly)
            {
                string camName = renderingData.cameraData.camera.name;
                if (!camName.Contains("LeftEyeCamera") && !camName.Contains("RightEyeCamera"))
                    return;
            }

            renderer.EnqueuePass(_debugPass);
        }
    }

    public class URPMatrixDebugPass : ScriptableRenderPass
    {
        private readonly URPMatrixDebugFeature _owner;
        private readonly URPMatrixDebugFeature.DebugSettings _settings;

        public URPMatrixDebugPass(URPMatrixDebugFeature owner, URPMatrixDebugFeature.DebugSettings settings)
        {
            _owner = owner;
            _settings = settings;
            renderPassEvent = settings.renderPassEvent;
        }

#if UNITY_6000_0_OR_NEWER
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();

            if ((AppLogger.IsEnabled(_owner, URPMatrixDebugFeature.TagURPMatrixDebug) || AppLogger.IsEnabled(URPMatrixDebugFeature.TagURPMatrixDebug)) && Time.frameCount % 60 == 0)
            {
                LogURPPassState(cameraData.camera, cameraData.GetViewMatrix(), cameraData.GetProjectionMatrix(), renderingData.cullResults);
            }
        }
#endif

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if ((AppLogger.IsEnabled(_owner, URPMatrixDebugFeature.TagURPMatrixDebug) || AppLogger.IsEnabled(URPMatrixDebugFeature.TagURPMatrixDebug)) && Time.frameCount % 60 == 0)
            {
                LogURPPassState(renderingData.cameraData.camera, renderingData.cameraData.GetViewMatrix(), renderingData.cameraData.GetProjectionMatrix(), renderingData.cullResults);
            }
        }

        private void LogURPPassState(Camera cam, Matrix4x4 urpView, Matrix4x4 urpProj, CullingResults cullResults)
        {
            if (cam == null) return;

            Matrix4x4 camView = cam.worldToCameraMatrix;
            Matrix4x4 camProj = cam.projectionMatrix;
            Matrix4x4 camCull = cam.cullingMatrix;
            Matrix4x4 expectedCull = camProj * camView;

            Matrix4x4 gpuProjFalse = GL.GetGPUProjectionMatrix(camProj, false);
            Matrix4x4 gpuProjTrue = GL.GetGPUProjectionMatrix(camProj, true);

            float cullDet = camCull.determinant;
            float expectedDet = expectedCull.determinant;

            float urpViewDet = urpView.determinant;
            float urpProjDet = urpProj.determinant;
            float camViewDet = camView.determinant;
            float camProjDet = camProj.determinant;

            float viewDiff = MatrixDifference(urpView, camView);
            float projDiff = MatrixDifference(urpProj, camProj);

            System.Text.StringBuilder sb = new System.Text.StringBuilder(2048);
            sb.AppendLine($"=== URP Renderer Pass State Verification ===");
            sb.AppendLine($"Target Camera: {cam.name} (Type: {cam.cameraType})");
            sb.AppendLine($"Pixel Viewport: {cam.pixelRect}");
            sb.AppendLine($"CullingResults Visible Lights: {cullResults.visibleLights.Length}");

            sb.AppendLine("\n■ 1. Matrix Determinants & Handedness Check");
            sb.AppendLine($"   URP View Det:       {urpViewDet:+0.0000;-0.0000}");
            sb.AppendLine($"   Cam View Det:       {camViewDet:+0.0000;-0.0000} (Diff sum: {viewDiff:F6})");
            sb.AppendLine($"   URP Proj Det:       {urpProjDet:+0.0000;-0.0000}");
            sb.AppendLine($"   Cam Proj Det:       {camProjDet:+0.0000;-0.0000} (Diff sum: {projDiff:F6})");
            sb.AppendLine($"   Current Cull Det:   {cullDet:+0.0000;-0.0000}");
            sb.AppendLine($"   Expected (P*V) Det: {expectedDet:+0.0000;-0.0000}");

            sb.AppendLine("\n■ 2. GL.GetGPUProjectionMatrix Test");
            sb.AppendLine($"   GPU Proj (renderIntoTexture=false) det: {gpuProjFalse.determinant:+0.0000;-0.0000}");
            sb.AppendLine($"   GPU Proj (renderIntoTexture=true)  det: {gpuProjTrue.determinant:+0.0000;-0.0000}");

            sb.AppendLine("\n■ 3. Full Matrix Deconstruction");
            sb.AppendLine($"--- URP View Matrix ---\n{FormatMatrix(urpView)}");
            sb.AppendLine($"--- Cam View Matrix ---\n{FormatMatrix(camView)}");
            sb.AppendLine($"--- URP Proj Matrix ---\n{FormatMatrix(urpProj)}");
            sb.AppendLine($"--- Cam Proj Matrix ---\n{FormatMatrix(camProj)}");
            sb.AppendLine($"--- Current camera.cullingMatrix ---\n{FormatMatrix(camCull)}");

            AppLogger.Log(_owner, $"[{URPMatrixDebugFeature.TagURPMatrixDebug}]\n{sb}", URPMatrixDebugFeature.TagURPMatrixDebug);
        }

        private float MatrixDifference(Matrix4x4 m1, Matrix4x4 m2)
        {
            float diff = 0f;
            for (int i = 0; i < 16; i++) diff += Mathf.Abs(m1[i] - m2[i]);
            return diff;
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

#pragma warning restore 0618, 0672
