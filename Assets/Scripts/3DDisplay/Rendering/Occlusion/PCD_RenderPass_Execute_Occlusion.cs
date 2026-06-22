// =============================================================================
// PCD_RenderPass_Execute_Occlusion.cs
// -----------------------------------------------------------------------------
// ExecuteComputePass におけるオクルージョン判定コア処理を担う partial クラス。
// =============================================================================
using UnityEngine;
using UnityEngine.Rendering;

public partial class PCDRenderPass
{
    // =========================================================================
    // ステージ10: オクルージョン判定
    // =========================================================================

    /// <summary>
    /// 各ピクセルについて、仮想オブジェクトが点群の手前にあるか奥にあるかを
    /// 深度ピラミッドサンプリングにより判定し、オクルージョン結果マップを生成する。
    /// </summary>
    private void ExecuteStageComputeOcclusion(CommandBuffer cmd, ComputeShader cs, ComputePassData passData,
        int threadGroupsX, int threadGroupsY)
    {
        if (!passData.hasVirtualObjects)
        {
            // 仮想オブジェクトが存在しないが、ホールフィリングが有効な場合は ColorMap をそのままコピー
            bool useHoleFilling = passData.settings.holeFillingMethod != PCDRendererFeature.PCD_HoleFillingMethod.None;
            if (useHoleFilling)
            {
                cmd.SetComputeTextureParam(cs, passData.kernelCopyColorToOcclusion, ShaderIDs.ColorMap, passData.colorMap);
                cmd.SetComputeTextureParam(cs, passData.kernelCopyColorToOcclusion, ShaderIDs.ViewPositionMap, passData.viewPositionMap);
                cmd.SetComputeTextureParam(cs, passData.kernelCopyColorToOcclusion, ShaderIDs.OriginTypeMap_RW, passData.originTypeMap);
                cmd.SetComputeTextureParam(cs, passData.kernelCopyColorToOcclusion, ShaderIDs.OcclusionResultMap_RW, passData.occlusionResultMap);
                cmd.DispatchCompute(cs, passData.kernelCopyColorToOcclusion, threadGroupsX, threadGroupsY, 1);
            }
            return;
        }

        if (passData.settings.kernelType == PCDRendererFeature.PCD_OcclusionKernel.Skip)
        {
            // オクルージョン判定をスキップし、色情報をそのままコピー
            cmd.SetComputeTextureParam(cs, passData.kernelCopyColorToOcclusion, ShaderIDs.ColorMap, passData.colorMap);
            cmd.SetComputeTextureParam(cs, passData.kernelCopyColorToOcclusion, ShaderIDs.ViewPositionMap, passData.viewPositionMap);
            cmd.SetComputeTextureParam(cs, passData.kernelCopyColorToOcclusion, ShaderIDs.OriginTypeMap_RW, passData.originTypeMap);
            cmd.SetComputeTextureParam(cs, passData.kernelCopyColorToOcclusion, ShaderIDs.OcclusionResultMap_RW, passData.occlusionResultMap);
            cmd.DispatchCompute(cs, passData.kernelCopyColorToOcclusion, threadGroupsX, threadGroupsY, 1);
        }
        else
        {
            // フルオクルージョン判定を実行
            cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.ColorMap, passData.colorMap);
            cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.ViewPositionMap, passData.viewPositionMap);
            cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.VirtualDepthMap, passData.virtualDepthTexture);
            cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.OriginTypeMap_RW, passData.originTypeMap);
            cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.FinalNeighborhoodSizeMap, passData.settings.enableGradientCorrection ? passData.correctedNeighborhoodSizeMap : passData.neighborhoodSizeMap);

            // 深度ピラミッド全レベルをバインド
            cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.DepthPyramidL1, passData.depthPyramidL1);
            cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.DepthPyramidL2, passData.depthPyramidL2);
            cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.DepthPyramidL3, passData.depthPyramidL3);
            cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.DepthPyramidL4, passData.depthPyramidL4);
            cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.DepthPyramidL5, passData.depthPyramidL5);
            cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.DepthPyramidL6, passData.depthPyramidL6);
            cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.OcclusionResultMap_RW, passData.occlusionResultMap);

            // デバッグ出力用のフラグとマップ
            int shouldRecordDebug = (passData.settings.recordOcclusionDebugMap || passData.settings.recordPixelTagMap || passData.settings.enablePixelTagMap || passData.settings.enableOcclusionMap || passData.settings.recordNeighborCountMap) ? 1 : 0;
            cmd.SetComputeIntParam(cs, ShaderIDs.RecordOcclusionDebug, shouldRecordDebug);

            cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.NeighborCountMap_RW, passData.neighborCountMap);
            cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.OcclusionValueMap_RW, passData.occlusionValueMap);
            cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.OriginMap_RW, passData.debugDisplayMap);
            cmd.DispatchCompute(cs, passData.kernelComputeOcclusion, threadGroupsX, threadGroupsY, 1);
        }
    }
}
