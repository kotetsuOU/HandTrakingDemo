// =============================================================================
// PCD_RenderPass_Execute.cs
// -----------------------------------------------------------------------------
// コンピュートシェーダーを使ったオクルージョンパイプラインの実行ロジック。
//
// ExecuteComputePass() がエントリーポイントとなり、以下のステージを順に実行する:
//
//   ステージ1:  マップクリア (ClearMaps)
//   ステージ2:  仮想深度マップからの初期化 (InitFromCamera)
//   ステージ3:  3D点群のスクリーンスペースへの投影 (ProjectPoints)
//   ステージ4-8: 密度計算とLOD (GridZMin → Density → GridLevel → MedianFilter → NeighborhoodSize)
//   ステージ9a: 深度ピラミッド構築 (BuildDepthPyramid L1〜L6)
//   ステージ9b: 勾配補正 (ApplyAdaptiveGradientCorrection)
//   ステージ10: オクルージョン判定 (ComputeOcclusion)
//   ステージ11: ホールフィリング (JointBilateral / PullPush / Morphology)
//   ステージ12: 補完とマージ (Interpolate)
//   ステージ13: デバッグ可視化 (VisualizeOcclusionDebug)
//   Readback:   仮想メッシュのピクセル数を非同期取得
//
// =============================================================================
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public partial class PCDRenderPass
{
    /// <summary>
    /// コンピュートシェーダーによるオクルージョンパイプラインを実行するメインメソッド。
    /// 各ステージは個別のメソッドに分割されており、ここでは制御フローのみを担当する。
    /// </summary>
    private void ExecuteComputePass(ComputePassData passData, UnsafeGraphContext context)
    {
        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
        var cs = passData.computeShader;

        // リバースZバッファへの対応フラグをセット（DX11等で正しいオクルージョン判定を行うため）
        cmd.SetComputeIntParam(cs, Shader.PropertyToID("_IsReversedZ"), SystemInfo.usesReversedZBuffer ? 1 : 0);

        // バッファの結合（外部＋内部が両方存在する場合）
        MergeExternalAndInternalBuffers(cmd, cs, passData);

        // コンピュートシェーダーのグローバルパラメータを設定
        SetGlobalComputeParams(cmd, cs, passData);

        // スレッドグループ数を事前計算
        int sw = (int)passData.screenParams.x;
        int sh = (int)passData.screenParams.y;
        int gs = (int)passData.settings.gridSize;
        if (gs == 0) gs = 16;
        int threadGroupsX = (sw + 7) / 8;
        int threadGroupsY = (sh + 7) / 8;
        int gridGroupsX = (sw + gs - 1) / gs;
        int gridGroupsY = (sh + gs - 1) / gs;

        bool runInitFromCamera = passData.hasVirtualDepth && passData.settings.enableVirtualDepthIntegration;

        // --- パイプラインの各ステージを順に実行 ---
        ExecuteStageClearMaps(cmd, cs, passData, threadGroupsX, threadGroupsY, runInitFromCamera);
        ExecuteStageInitFromCamera(cmd, cs, passData, threadGroupsX, threadGroupsY, runInitFromCamera);
        ExecuteStageProjectPoints(cmd, cs, passData);

        bool needsNeighborhoodSize = passData.hasVirtualObjects && 
            (passData.settings.kernelType != PCDRendererFeature.PCD_OcclusionKernel.Skip || 
             passData.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.JointBilateral || 
             passData.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.Morphology_OC ||
             passData.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.Morphology_CO);

        bool needsDepthPyramid = passData.hasVirtualObjects && 
            (passData.settings.kernelType != PCDRendererFeature.PCD_OcclusionKernel.Skip || 
             (needsNeighborhoodSize && passData.settings.enableGradientCorrection));

        ExecuteStageDensityAndLOD(cmd, cs, passData, threadGroupsX, threadGroupsY, gridGroupsX, gridGroupsY, needsNeighborhoodSize);
        ExecuteStageDepthPyramid(cmd, cs, passData, sw, sh, needsDepthPyramid);
        ExecuteStageGradientCorrection(cmd, cs, passData, threadGroupsX, threadGroupsY, needsDepthPyramid);
        ExecuteStageComputeOcclusion(cmd, cs, passData, threadGroupsX, threadGroupsY);
        ExecuteStageHoleFilling(cmd, cs, passData, sw, sh, threadGroupsX, threadGroupsY, needsNeighborhoodSize);
        ExecuteStageInterpolate(cmd, cs, passData, threadGroupsX, threadGroupsY);
        ExecuteStageDebugVisualize(cmd, cs, passData, threadGroupsX, threadGroupsY);

        // 仮想メッシュのピクセル数を非同期で読み戻す（次フレームの密度倍率計算に使用）
        RequestStaticMeshCounterReadback(passData);
    }

    // =========================================================================
    // バッファ結合
    // =========================================================================

    /// <summary>
    /// 外部バッファと内部バッファの両方が存在する場合、それらを結合バッファにコピーする。
    /// </summary>
    private void MergeExternalAndInternalBuffers(CommandBuffer cmd, ComputeShader cs, ComputePassData passData)
    {
        if (!passData.useExternal || passData.externalCount <= 0 || passData.internalCount <= 0)
            return;

        // 外部バッファを結合先の先頭にコピー
        cmd.SetComputeBufferParam(cs, passData.kernelMerge, ShaderIDs.MergeDstBuffer, passData.combinedBuffer);
        cmd.SetComputeBufferParam(cs, passData.kernelMerge, ShaderIDs.MergeSrcBuffer, passData.externalBuffer);
        cmd.SetComputeIntParam(cs, ShaderIDs.MergeSrcOffset, 0);
        cmd.SetComputeIntParam(cs, ShaderIDs.MergeDstOffset, 0);
        cmd.SetComputeIntParam(cs, ShaderIDs.MergeCopyCount, passData.externalCount);
        int mergeGroupsExt = (passData.externalCount + 255) / 256;
        cmd.DispatchCompute(cs, passData.kernelMerge, mergeGroupsExt, 1, 1);

        // 内部バッファを結合先の外部バッファの後ろにコピー
        cmd.SetComputeBufferParam(cs, passData.kernelMerge, ShaderIDs.MergeSrcBuffer, passData.internalBuffer);
        cmd.SetComputeIntParam(cs, ShaderIDs.MergeSrcOffset, 0);
        cmd.SetComputeIntParam(cs, ShaderIDs.MergeDstOffset, passData.externalCount);
        cmd.SetComputeIntParam(cs, ShaderIDs.MergeCopyCount, passData.internalCount);
        int mergeGroupsInt = (passData.internalCount + 255) / 256;
        cmd.DispatchCompute(cs, passData.kernelMerge, mergeGroupsInt, 1, 1);
    }

    // =========================================================================
    // グローバルパラメータ設定
    // =========================================================================

    /// <summary>
    /// コンピュートシェーダーのグローバルパラメータ（密度閾値、近傍パラメータ、グリッドサイズなど）を設定する。
    /// </summary>
    private void SetGlobalComputeParams(CommandBuffer cmd, ComputeShader cs, ComputePassData passData)
    {
        cmd.SetComputeIntParam(cs, ShaderIDs.PointCount, passData.pointCount);
        cmd.SetComputeVectorParam(cs, ShaderIDs.ScreenParams, passData.screenParams);
        cmd.SetComputeMatrixParam(cs, ShaderIDs.ViewMatrix, passData.viewMatrix);
        cmd.SetComputeMatrixParam(cs, ShaderIDs.ProjectionMatrix, passData.projectionMatrix);
        cmd.SetComputeFloatParam(cs, ShaderIDs.DensityThreshold_e, passData.settings.densityThreshold_e);
        cmd.SetComputeFloatParam(cs, ShaderIDs.NeighborhoodParam_p_prime, passData.settings.neighborhoodParam_p_prime);
        cmd.SetComputeFloatParam(cs, ShaderIDs.GradientThreshold_g_th, passData.settings.gradientThreshold_g_th);
        cmd.SetComputeIntParam(cs, ShaderIDs.KernelType, (int)passData.settings.kernelType);
        cmd.SetComputeIntParam(cs, ShaderIDs.EvaluationMode, (int)passData.settings.evaluationMode);
        cmd.SetComputeIntParam(cs, ShaderIDs.MinOccludedSectors, passData.settings.minOccludedSectors);
        cmd.SetComputeIntParam(cs, ShaderIDs.MinSearchLevel, passData.settings.minSearchLevel);
        cmd.SetComputeFloatParam(cs, ShaderIDs.Alpha, passData.settings.exponentAlpha);
        cmd.SetComputeFloatParam(cs, ShaderIDs.OcclusionThreshold, passData.settings.occlusionThreshold);
        cmd.SetComputeFloatParam(cs, ShaderIDs.OcclusionFadeWidth, passData.settings.occlusionFadeWidth);

        // 提案手法の各最適化フラグ
        cmd.SetComputeIntParam(cs, Shader.PropertyToID("_EnableTagBasedOptimization"), passData.settings.enableTagBasedOptimization ? 1 : 0);
        cmd.SetComputeIntParam(cs, Shader.PropertyToID("_EnableTypeAwareDensity"), passData.settings.enableTypeAwareDensity ? 1 : 0);
        cmd.SetComputeIntParam(cs, Shader.PropertyToID("_EnableSoftOcclusionFade"), passData.settings.enableSoftOcclusionFade ? 1 : 0);
        cmd.SetComputeIntParam(cs, Shader.PropertyToID("_EnableJointBilateralHoleFilling"), (passData.settings.holeFillingMethod != PCDRendererFeature.PCD_HoleFillingMethod.None) ? 1 : 0);

        // 仮想物体における密度倍率（深度バッファ / 実点群数 を加味）
        uint densityMultiplier = System.Math.Max(1u, passData.settings._dynamicMultiplierRuntimeValue);
        cmd.SetComputeIntParam(cs, Shader.PropertyToID("_StaticMeshDensityMultiplier"), (int)densityMultiplier);

        // グリッドサイズに対応するシェーダーキーワードを切り替え
        int gs = (int)passData.settings.gridSize;
        if (gs == 0) gs = 16;
        cmd.DisableShaderKeyword("GRID_SIZE_8");
        cmd.DisableShaderKeyword("GRID_SIZE_16");
        cmd.DisableShaderKeyword("GRID_SIZE_32");
        cmd.EnableShaderKeyword($"GRID_SIZE_{gs}");
    }


    // =========================================================================
    // 非同期読み戻し
    // =========================================================================

    /// <summary>
    /// 仮想メッシュのピクセルカウントを非同期で読み戻す。
    /// 次フレームの密度倍率（_dynamicMultiplierRuntimeValue）の計算に使用される。
    /// </summary>
    private void RequestStaticMeshCounterReadback(ComputePassData passData)
    {
        int totalPoints = passData.pointCount;
        UnityEngine.Rendering.AsyncGPUReadback.Request(passData.staticMeshCounterBuffer, (request) =>
        {
            if (request.hasError || PCDRendererFeature.Instance == null)
            {
                return;
            }
            var data = request.GetData<uint>();
            uint virtualMeshPixelCount = data[0];
            PCDRendererFeature.Instance.LastFrameVirtualMeshPixelCount = virtualMeshPixelCount;

            if (totalPoints > 0)
            {
                uint multiplier = System.Math.Max(1u, virtualMeshPixelCount / (uint)totalPoints);
                PCDRendererFeature.Instance._internalDynamicMultiplier = multiplier;
            }
            else
            {
                PCDRendererFeature.Instance._internalDynamicMultiplier = 1;
            }
        });
    }

    // =========================================================================
    // Blit パス
    // =========================================================================

    /// <summary>
    /// 最終画像またはデバッグ画像をカメラターゲットに描画する。
    /// 出力先の RenderTarget は RecordRenderGraph で builder.SetRenderAttachment を通じて設定済み。
    /// </summary>
    private static void ExecuteBlitPass(BlitPassData passData, RasterGraphContext context)
    {
        Blitter.BlitTexture(context.cmd, passData.sourceImage, new Vector2(1, 1), 0.0f, false);
    }
}
