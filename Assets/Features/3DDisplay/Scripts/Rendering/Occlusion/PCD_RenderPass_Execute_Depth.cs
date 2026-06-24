// =============================================================================
// PCD_RenderPass_Execute_Depth.cs
// -----------------------------------------------------------------------------
// ExecuteComputePass の深度階層（深度ピラミッドの構築および勾配補正）を担う
// partial クラス。
// =============================================================================
using UnityEngine;
using UnityEngine.Rendering;

public partial class PCDRenderPass
{
    // =========================================================================
    // ステージ9a: 深度ピラミッドの構築
    // =========================================================================

    /// <summary>
    /// 全画面の深度情報を段階的に1/2に縮小し、6レベルのピラミッドを構築する。
    /// L1 は ViewPositionMap と OriginTypeMap から物理点群のみを抽出する。
    /// L2以降は前レベルのダウンサンプルを行う。
    /// </summary>
    private void ExecuteStageDepthPyramid(CommandBuffer cmd, ComputeShader cs, ComputePassData passData,
        int sw, int sh, bool needsDepthPyramid)
    {
        if (!needsDepthPyramid)
            return;

        // L1: ViewPositionMap → DepthPyramidL1 (物理点群のみ)
        int l1_w = Mathf.Max(1, (sw + 1) / 2);
        int l1_h = Mathf.Max(1, (sh + 1) / 2);
        cmd.SetComputeTextureParam(cs, passData.kernelBuildDepthPyramidL1, ShaderIDs.ViewPositionMap, passData.viewPositionMap);
        cmd.SetComputeTextureParam(cs, passData.kernelBuildDepthPyramidL1, ShaderIDs.OriginTypeMap, passData.originTypeMap);
        cmd.SetComputeTextureParam(cs, passData.kernelBuildDepthPyramidL1, ShaderIDs.DepthPyramidL1_RW, passData.depthPyramidL1);
        cmd.DispatchCompute(cs, passData.kernelBuildDepthPyramidL1, (l1_w + 7) / 8, (l1_h + 7) / 8, 1);

        // L2〜L6: 前レベルのダウンサンプル
        int prevW = l1_w, prevH = l1_h;
        var pyramidKernels = new[] {
            passData.kernelBuildDepthPyramidL2, passData.kernelBuildDepthPyramidL3,
            passData.kernelBuildDepthPyramidL4, passData.kernelBuildDepthPyramidL5,
            passData.kernelBuildDepthPyramidL6
        };
        var pyramidInputIDs = new[] {
            ShaderIDs.DepthPyramidL1, ShaderIDs.DepthPyramidL2,
            ShaderIDs.DepthPyramidL3, ShaderIDs.DepthPyramidL4,
            ShaderIDs.DepthPyramidL5
        };
        var pyramidOutputIDs = new[] {
            ShaderIDs.DepthPyramidL2_RW, ShaderIDs.DepthPyramidL3_RW,
            ShaderIDs.DepthPyramidL4_RW, ShaderIDs.DepthPyramidL5_RW,
            ShaderIDs.DepthPyramidL6_RW
        };
        var pyramidInputHandles = new[] {
            passData.depthPyramidL1, passData.depthPyramidL2,
            passData.depthPyramidL3, passData.depthPyramidL4,
            passData.depthPyramidL5
        };
        var pyramidOutputHandles = new[] {
            passData.depthPyramidL2, passData.depthPyramidL3,
            passData.depthPyramidL4, passData.depthPyramidL5,
            passData.depthPyramidL6
        };

        for (int i = 0; i < pyramidKernels.Length; i++)
        {
            int curW = Mathf.Max(1, (prevW + 1) / 2);
            int curH = Mathf.Max(1, (prevH + 1) / 2);
            cmd.SetComputeTextureParam(cs, pyramidKernels[i], pyramidInputIDs[i], pyramidInputHandles[i]);
            cmd.SetComputeTextureParam(cs, pyramidKernels[i], pyramidOutputIDs[i], pyramidOutputHandles[i]);
            cmd.DispatchCompute(cs, pyramidKernels[i], (curW + 7) / 8, (curH + 7) / 8, 1);
            prevW = curW;
            prevH = curH;
        }
    }

    // =========================================================================
    // ステージ9b: 勾配補正
    // =========================================================================

    /// <summary>
    /// 急な深度勾配がある領域の近傍サイズを補正する。
    /// 深度ピラミッド全レベルの情報を参照して、不適切に大きな近傍が設定されるのを防ぐ。
    /// </summary>
    private void ExecuteStageGradientCorrection(CommandBuffer cmd, ComputeShader cs, ComputePassData passData,
        int threadGroupsX, int threadGroupsY, bool needsDepthPyramid)
    {
        if (!needsDepthPyramid || !passData.settings.enableGradientCorrection)
            return;

        cmd.SetComputeTextureParam(cs, passData.kernelApplyGradient, ShaderIDs.DepthPyramidL1, passData.depthPyramidL1);
        cmd.SetComputeTextureParam(cs, passData.kernelApplyGradient, ShaderIDs.DepthPyramidL2, passData.depthPyramidL2);
        cmd.SetComputeTextureParam(cs, passData.kernelApplyGradient, ShaderIDs.DepthPyramidL3, passData.depthPyramidL3);
        cmd.SetComputeTextureParam(cs, passData.kernelApplyGradient, ShaderIDs.DepthPyramidL4, passData.depthPyramidL4);
        cmd.SetComputeTextureParam(cs, passData.kernelApplyGradient, ShaderIDs.DepthPyramidL5, passData.depthPyramidL5);
        cmd.SetComputeTextureParam(cs, passData.kernelApplyGradient, ShaderIDs.DepthPyramidL6, passData.depthPyramidL6);
        cmd.SetComputeTextureParam(cs, passData.kernelApplyGradient, ShaderIDs.NeighborhoodSizeMap, passData.neighborhoodSizeMap);
        cmd.SetComputeTextureParam(cs, passData.kernelApplyGradient, ShaderIDs.CorrectedNeighborhoodSizeMap_RW, passData.correctedNeighborhoodSizeMap);
        cmd.DispatchCompute(cs, passData.kernelApplyGradient, threadGroupsX, threadGroupsY, 1);
    }
}
