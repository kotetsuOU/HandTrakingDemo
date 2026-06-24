// =============================================================================
// PCD_RenderPass_RenderGraph_Blit.cs
// -----------------------------------------------------------------------------
// Unity の RenderGraph に最終画像またはデバッグ画像を書き戻す Blit パスを登録する。
// =============================================================================
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

public partial class PCDRenderPass
{
    /// <summary>
    /// 生成された点群（またはデバッグマップ）を最終画面に描画する(Blit)パスを登録する。
    /// </summary>
    private void EnqueueBlitPass(
        RenderGraph renderGraph, 
        UniversalResourceData resourceData, 
        RenderGraphHandles handles)
    {
        using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>("PCD Blit Pass", out var data))
        {
            data.cameraTarget = resourceData.activeColorTexture; 
            data.directGpuImageMap = null;
            
            data.enablePixelTagMap = _settings.enablePixelTagMap;
            data.enableOcclusionMap = _settings.enableOcclusionMap;
            data.useDirectGpuImageBuffer = false;

            // オリジンデバッグが有効ならそちらを描画元とし、無効なら最終画像をソースとする
            if (data.enablePixelTagMap || data.enableOcclusionMap)
            {
                data.sourceImage = handles.debugDisplayMap;
                builder.UseTexture(data.sourceImage, AccessFlags.Read);
            }
            else
            {
                data.sourceImage = handles.finalImage;
                builder.UseTexture(data.sourceImage, AccessFlags.Read);
            }

            builder.SetRenderAttachment(data.cameraTarget, 0, AccessFlags.ReadWrite);
            
            // Blit処理関数を登録
            builder.SetRenderFunc((BlitPassData passData, RasterGraphContext context) =>
            {
                ExecuteBlitPass(passData, context);
            });
        }
    }
}
