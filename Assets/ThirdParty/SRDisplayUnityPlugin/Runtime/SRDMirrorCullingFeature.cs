#pragma warning disable 0618, 0672

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace SRD.Core
{
    /// <summary>
    /// URP Culling ステージの前に ScriptableCullingParameters / Camera.cullingMatrix を
    /// 鏡像 Projection * View 行列へ強制的に適用・同期する ScriptableRendererFeature。
    /// </summary>
    public class SRDMirrorCullingFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class FeatureSettings
        {
            [Tooltip("鏡像カリングのオーバーライドを有効化")]
            public bool enableMirrorCulling = true;

            [Tooltip("SRDの LeftEyeCamera / RightEyeCamera のみを対象にする")]
            public bool filterSRDEyeCamerasOnly = true;

            [Tooltip("RenderPassイベント (Culling準備前への挿入のため BeforeRenderingPrePasses を推奨)")]
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPrePasses;
        }

        public FeatureSettings settings = new FeatureSettings();
        private SRDMirrorCullingPass _cullingPass;

        public override void Create()
        {
            _cullingPass = new SRDMirrorCullingPass(settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!settings.enableMirrorCulling) return;
            if (renderingData.cameraData.cameraType == CameraType.Preview) return;

            if (settings.filterSRDEyeCamerasOnly)
            {
                string camName = renderingData.cameraData.camera.name;
                if (!camName.Contains("LeftEyeCamera") && !camName.Contains("RightEyeCamera"))
                    return;
            }

            renderer.EnqueuePass(_cullingPass);
        }
    }

    public class SRDMirrorCullingPass : ScriptableRenderPass
    {
        private readonly SRDMirrorCullingFeature.FeatureSettings _settings;

        public SRDMirrorCullingPass(SRDMirrorCullingFeature.FeatureSettings settings)
        {
            _settings = settings;
            renderPassEvent = settings.renderPassEvent;
        }

#if UNITY_6000_0_OR_NEWER
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData?.camera != null)
            {
                Camera cam = cameraData.camera;
                cam.cullingMatrix = cam.projectionMatrix * cam.worldToCameraMatrix;
            }
        }
#endif

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            Camera cam = renderingData.cameraData.camera;
            if (cam != null)
            {
                cam.cullingMatrix = cam.projectionMatrix * cam.worldToCameraMatrix;
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Camera cam = renderingData.cameraData.camera;
            if (cam == null) return;

            if (cam.TryGetCullingParameters(out ScriptableCullingParameters cullingParams))
            {
                cullingParams.cullingMatrix = cam.projectionMatrix * cam.worldToCameraMatrix;
                renderingData.cullResults = context.Cull(ref cullingParams);
            }
        }
    }
}

#pragma warning restore 0618, 0672
