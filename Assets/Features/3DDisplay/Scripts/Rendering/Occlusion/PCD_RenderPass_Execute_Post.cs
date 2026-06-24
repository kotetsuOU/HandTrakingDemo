// =============================================================================
// PCD_RenderPass_Execute_Post.cs
// -----------------------------------------------------------------------------
// ExecuteComputePass の後段処理（画像補完やマージ、デバッグ可視化マップの生成）
// を担う partial クラス。
// =============================================================================
using UnityEngine;
using UnityEngine.Rendering;

public partial class PCDRenderPass
{
    // =========================================================================
    // ステージ12: 補完とマージ
    // =========================================================================

    /// <summary>
    /// ホールフィリング後のオクルージョン結果を、仮想深度マップやカメラカラーバッファと
    /// マージして最終的な合成画像を生成する。
    /// </summary>
    private void ExecuteStageInterpolate(CommandBuffer cmd, ComputeShader cs, ComputePassData passData,
        int threadGroupsX, int threadGroupsY)
    {
        if (passData.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.None)
            return;

        cmd.SetComputeTextureParam(cs, passData.kernelInterpolate, ShaderIDs.OcclusionResultMap, passData.occlusionResultMap);
        cmd.SetComputeTextureParam(cs, passData.kernelInterpolate, ShaderIDs.VirtualDepthMap, passData.virtualDepthTexture);
        cmd.SetComputeTextureParam(cs, passData.kernelInterpolate, ShaderIDs.CameraColorTexture, passData.cameraColorTexture);
        cmd.SetComputeTextureParam(cs, passData.kernelInterpolate, ShaderIDs.OriginTypeMap, passData.originTypeMap);
        cmd.SetComputeTextureParam(cs, passData.kernelInterpolate, ShaderIDs.FinalImage_RW, passData.finalImage);
        cmd.SetComputeTextureParam(cs, passData.kernelInterpolate, ShaderIDs.OriginMap_RW, passData.debugDisplayMap);
        cmd.DispatchCompute(cs, passData.kernelInterpolate, threadGroupsX, threadGroupsY, 1);
    }

    // =========================================================================
    // ステージ13: デバッグ可視化
    // =========================================================================

    /// <summary>
    /// PixelTag または OcclusionMap の可視化が有効な場合、
    /// OcclusionValueMap をカラー/グレースケールに変換してデバッグ表示用マップに書き込む。
    /// </summary>
    private void ExecuteStageDebugVisualize(CommandBuffer cmd, ComputeShader cs, ComputePassData passData,
        int threadGroupsX, int threadGroupsY)
    {
        if (!passData.settings.enablePixelTagMap && !passData.settings.enableOcclusionMap)
            return;

        int displayMode = passData.settings.enablePixelTagMap ? 1 : 2;
        cmd.SetComputeIntParam(cs, ShaderIDs.DebugDisplayMode, displayMode);
        cmd.SetComputeTextureParam(cs, passData.kernelVisualizeOcclusionDebug, ShaderIDs.OcclusionValueMap_RW, passData.occlusionValueMap);
        cmd.SetComputeTextureParam(cs, passData.kernelVisualizeOcclusionDebug, ShaderIDs.OriginMap_RW, passData.debugDisplayMap);
        cmd.DispatchCompute(cs, passData.kernelVisualizeOcclusionDebug, threadGroupsX, threadGroupsY, 1);
    }
}
