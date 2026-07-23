using UnityEngine.Rendering.RenderGraphModule;

/// <summary>
/// PCDPipelineContext などの ComputePass から出力され、
/// Blit パスやデバッグ出力パスで引き継がれる RenderGraph 用の TextureHandle 群です。
/// </summary>
public struct PCDRenderGraphHandles
{
    public TextureHandle finalImage;
    public TextureHandle debugDisplayMap;
    public TextureHandle occlusionValueMap;
    public TextureHandle neighborhoodMap;
    public TextureHandle neighborCountMap;
    public TextureHandle integratedDepthMap;
}
