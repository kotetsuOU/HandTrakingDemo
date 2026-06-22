using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public partial class PCDRenderPass
{
    private void ExecuteComputePass(ComputePassData passData, UnsafeGraphContext context)
    {
        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
        var cs = passData.computeShader;

        // リバースZバッファへの対応フラグをセット（DX11等で正しいオクルージョン判定を行うため）
        cmd.SetComputeIntParam(cs, Shader.PropertyToID("_IsReversedZ"), SystemInfo.usesReversedZBuffer ? 1 : 0);

        // 外部バッファと内部バッファの両方が存在する場合、それらを結合します
        if (passData.useExternal && passData.externalCount > 0 && passData.internalCount > 0)
        {
            cmd.SetComputeBufferParam(cs, passData.kernelMerge, ShaderIDs.MergeDstBuffer, passData.combinedBuffer);
            cmd.SetComputeBufferParam(cs, passData.kernelMerge, ShaderIDs.MergeSrcBuffer, passData.externalBuffer);
            cmd.SetComputeIntParam(cs, ShaderIDs.MergeSrcOffset, 0);
            cmd.SetComputeIntParam(cs, ShaderIDs.MergeDstOffset, 0);
            cmd.SetComputeIntParam(cs, ShaderIDs.MergeCopyCount, passData.externalCount);
            int mergeGroupsExt = (passData.externalCount + 255) / 256;
            cmd.DispatchCompute(cs, passData.kernelMerge, mergeGroupsExt, 1, 1);

            cmd.SetComputeBufferParam(cs, passData.kernelMerge, ShaderIDs.MergeSrcBuffer, passData.internalBuffer);
            cmd.SetComputeIntParam(cs, ShaderIDs.MergeSrcOffset, 0);
            cmd.SetComputeIntParam(cs, ShaderIDs.MergeDstOffset, passData.externalCount);
            cmd.SetComputeIntParam(cs, ShaderIDs.MergeCopyCount, passData.internalCount);
            int mergeGroupsInt = (passData.internalCount + 255) / 256;
            cmd.DispatchCompute(cs, passData.kernelMerge, mergeGroupsInt, 1, 1);
        }

        // --- コンピュートシェーダーのグローバルパラメータを設定 ---
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

        cmd.SetComputeIntParam(cs, Shader.PropertyToID("_EnableTagBasedOptimization"), passData.settings.enableTagBasedOptimization ? 1 : 0);
        cmd.SetComputeIntParam(cs, Shader.PropertyToID("_EnableTypeAwareDensity"), passData.settings.enableTypeAwareDensity ? 1 : 0);
        cmd.SetComputeIntParam(cs, Shader.PropertyToID("_EnableSoftOcclusionFade"), passData.settings.enableSoftOcclusionFade ? 1 : 0);
        cmd.SetComputeIntParam(cs, Shader.PropertyToID("_EnableJointBilateralHoleFilling"), (passData.settings.holeFillingMethod != PCDRendererFeature.PCD_HoleFillingMethod.None) ? 1 : 0);

        // 仮想物体など、スクリーン全体を埋めているメッシュにおける仮想的な密度倍率(x = 深度バッファ/実点群数などを加味)を設定
        uint densityMultiplier = System.Math.Max(1u, passData.settings._dynamicMultiplierRuntimeValue);
        cmd.SetComputeIntParam(cs, Shader.PropertyToID("_StaticMeshDensityMultiplier"), (int)densityMultiplier);

        int gs = (int)passData.settings.gridSize;
        if (gs == 0) gs = 16;
        cmd.DisableShaderKeyword("GRID_SIZE_8");
        cmd.DisableShaderKeyword("GRID_SIZE_16");
        cmd.DisableShaderKeyword("GRID_SIZE_32");
        cmd.EnableShaderKeyword($"GRID_SIZE_{gs}");

        // --- 最適なスレッドグループ数を計算 ---
        int sw = (int)passData.screenParams.x;
        int sh = (int)passData.screenParams.y;
        int threadGroupsX = (sw + 7) / 8;
        int threadGroupsY = (sh + 7) / 8;
        int gridGroupsX = (sw + gs - 1) / gs;
        int gridGroupsY = (sh + gs - 1) / gs;

        bool runInitFromCamera = passData.hasVirtualDepth && passData.settings.enableVirtualDepthIntegration;

        // --- ステージ1: 中間RTテクスチャのクリア ---
        // InitFromCamera が有効な場合、InitFromCamera が全ピクセルを上書き初期化するため ClearMaps をスキップする
        if (!runInitFromCamera)
        {
            cmd.SetComputeTextureParam(cs, passData.kernelClear, ShaderIDs.ColorMap_RW, passData.colorMap);
            cmd.SetComputeTextureParam(cs, passData.kernelClear, ShaderIDs.DepthMap_RW, passData.depthMap);
            cmd.SetComputeTextureParam(cs, passData.kernelClear, ShaderIDs.ViewPositionMap_RW, passData.viewPositionMap);
            cmd.SetComputeTextureParam(cs, passData.kernelClear, ShaderIDs.OcclusionResultMap_RW, passData.occlusionResultMap);
            cmd.SetComputeTextureParam(cs, passData.kernelClear, ShaderIDs.OcclusionValueMap_RW, passData.occlusionValueMap);
            
            var clearFinalImageTarget = (passData.settings.holeFillingMethod != PCDRendererFeature.PCD_HoleFillingMethod.None) ? passData.finalImage : passData.occlusionResultMap;
            cmd.SetComputeTextureParam(cs, passData.kernelClear, ShaderIDs.FinalImage_RW, clearFinalImageTarget);
            
            cmd.SetComputeTextureParam(cs, passData.kernelClear, ShaderIDs.OriginTypeMap_RW, passData.originTypeMap);
            cmd.SetComputeTextureParam(cs, passData.kernelClear, ShaderIDs.OriginMap_RW, passData.debugDisplayMap);
            cmd.DispatchCompute(cs, passData.kernelClear, threadGroupsX, threadGroupsY, 1);
        }

        // カウンターのクリアは常に実行
        cmd.SetComputeBufferParam(cs, passData.kernelClearCounter, ShaderIDs.StaticMeshCounter_RW, passData.staticMeshCounterBuffer);
        cmd.DispatchCompute(cs, passData.kernelClearCounter, 1, 1, 1);

        // --- ステージ2: 仮想深度マップからの初期化（提供されている場合） ---
        if (runInitFromCamera)
        {
            cmd.SetComputeIntParam(cs, ShaderIDs.UseVirtualDepth, 1);
            cmd.SetComputeTextureParam(cs, passData.kernelInitFromCamera, ShaderIDs.VirtualDepthMap, passData.virtualDepthTexture);

            if (passData.cameraColorTexture.IsValid())
            {
                cmd.SetComputeTextureParam(cs, passData.kernelInitFromCamera, ShaderIDs.CameraColorTexture, passData.cameraColorTexture);
            }

            cmd.SetComputeTextureParam(cs, passData.kernelInitFromCamera, ShaderIDs.DepthMap_RW, passData.depthMap);
            cmd.SetComputeTextureParam(cs, passData.kernelInitFromCamera, ShaderIDs.ColorMap_RW, passData.colorMap);
            cmd.SetComputeTextureParam(cs, passData.kernelInitFromCamera, ShaderIDs.ViewPositionMap_RW, passData.viewPositionMap);
            cmd.SetComputeTextureParam(cs, passData.kernelInitFromCamera, ShaderIDs.OriginTypeMap_RW, passData.originTypeMap);
            // ClearMapsの代わりにここでOriginMapも初期化するためにバインドを追加
            cmd.SetComputeTextureParam(cs, passData.kernelInitFromCamera, ShaderIDs.OriginMap_RW, passData.debugDisplayMap);
            
            cmd.SetComputeBufferParam(cs, passData.kernelInitFromCamera, ShaderIDs.StaticMeshCounter_RW, passData.staticMeshCounterBuffer);
            cmd.SetComputeMatrixParam(cs, ShaderIDs.InverseProjectionMatrix, passData.inverseProjectionMatrix);
            cmd.DispatchCompute(cs, passData.kernelInitFromCamera, threadGroupsX, threadGroupsY, 1);
        }
        else
        {
            cmd.SetComputeIntParam(cs, ShaderIDs.UseVirtualDepth, 0);
        }

        // --- ステージ3: 3D点群データのスクリーンスペースへの投影 ---
        if (!passData.depthMapOnlyMode)
        {
            cmd.SetComputeBufferParam(cs, passData.kernelProject, ShaderIDs.PointBuffer, passData.pointBuffer);
            cmd.SetComputeTextureParam(cs, passData.kernelProject, ShaderIDs.ColorMap_RW, passData.colorMap);
            cmd.SetComputeTextureParam(cs, passData.kernelProject, ShaderIDs.DepthMap_RW, passData.depthMap);
            cmd.SetComputeTextureParam(cs, passData.kernelProject, ShaderIDs.ViewPositionMap_RW, passData.viewPositionMap);
            cmd.SetComputeTextureParam(cs, passData.kernelProject, ShaderIDs.OriginTypeMap_RW, passData.originTypeMap);
            int projectGroups = (passData.pointCount + 255) / 256;
            cmd.DispatchCompute(cs, passData.kernelProject, projectGroups, 1, 1);
        }

        bool needsNeighborhoodSize = passData.hasVirtualObjects && 
            (passData.settings.kernelType != PCDRendererFeature.PCD_OcclusionKernel.Skip || 
             passData.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.JointBilateral || 
             passData.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.Morphology_OC ||
             passData.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.Morphology_CO);

        bool needsDepthPyramid = passData.hasVirtualObjects && 
            (passData.settings.kernelType != PCDRendererFeature.PCD_OcclusionKernel.Skip || 
             (needsNeighborhoodSize && passData.settings.enableGradientCorrection));

        if (needsNeighborhoodSize)
        {
            if (passData.settings.enableDensityBasedLOD)
            {
                // --- ステージ4: 各グリッドセルの最小深度を計算 ---
                cmd.SetComputeTextureParam(cs, passData.kernelCalcGridZMin, ShaderIDs.DepthMap, passData.depthMap);
                cmd.SetComputeTextureParam(cs, passData.kernelCalcGridZMin, ShaderIDs.GridZMinMap_RW, passData.gridZMinMap);
                cmd.DispatchCompute(cs, passData.kernelCalcGridZMin, gridGroupsX, gridGroupsY, 1);

                // --- ステージ5: グリッド解像度の要件を評価するために画面上のサンプル密度を計算 ---
                cmd.SetComputeTextureParam(cs, passData.kernelCalcDensity, ShaderIDs.DepthMap, passData.depthMap);
                cmd.SetComputeTextureParam(cs, passData.kernelCalcDensity, ShaderIDs.GridZMinMap, passData.gridZMinMap);
                cmd.SetComputeTextureParam(cs, passData.kernelCalcDensity, ShaderIDs.OriginTypeMap, passData.originTypeMap);
                cmd.SetComputeTextureParam(cs, passData.kernelCalcDensity, ShaderIDs.DensityMap_RW, passData.densityMap);
                cmd.DispatchCompute(cs, passData.kernelCalcDensity, gridGroupsX, gridGroupsY, 1);

                // --- ステージ6: ポイントの密度に応じて必要な詳細レベル（グリッドレベル）を決定 ---
                cmd.SetComputeTextureParam(cs, passData.kernelCalcGridLevel, ShaderIDs.DensityMap, passData.densityMap);
                cmd.SetComputeTextureParam(cs, passData.kernelCalcGridLevel, ShaderIDs.GridLevelMap_RW, passData.gridLevelMap);
                int gridThreadX = (gridGroupsX + 15) / 16;
                int gridThreadY = (gridGroupsY + 15) / 16;
                cmd.DispatchCompute(cs, passData.kernelCalcGridLevel, Mathf.Max(1, gridThreadX), Mathf.Max(1, gridThreadY), 1);

                // --- ステージ7: 穴やアーティファクトを防ぐために、メディアンフィルターを用いてグリッドレベルを平滑化 ---
                cmd.SetComputeTextureParam(cs, passData.kernelGridMedianFilter, ShaderIDs.GridLevelMap, passData.gridLevelMap);
                cmd.SetComputeTextureParam(cs, passData.kernelGridMedianFilter, ShaderIDs.FilteredGridLevelMap_RW, passData.filteredGridLevelMap);
                cmd.DispatchCompute(cs, passData.kernelGridMedianFilter, Mathf.Max(1, gridThreadX), Mathf.Max(1, gridThreadY), 1);

                // --- ステージ8: フィルター処理されたLODに基づいて基本的な近傍の半径サイズを算出 ---
                cmd.SetComputeTextureParam(cs, passData.kernelCalcNeighborhoodSize, ShaderIDs.FilteredGridLevelMap, passData.filteredGridLevelMap);
                cmd.SetComputeTextureParam(cs, passData.kernelCalcNeighborhoodSize, ShaderIDs.NeighborhoodSizeMap_RW, passData.neighborhoodSizeMap);
                cmd.DispatchCompute(cs, passData.kernelCalcNeighborhoodSize, threadGroupsX, threadGroupsY, 1);
            }
            else
            {
                // --- ステージ8（代替）: 密度計算をスキップし、_MinSearchLevel で近傍サイズマップを一括初期化 ---
                cmd.SetComputeTextureParam(cs, passData.kernelFillNeighborhoodSizeWithMinLevel, ShaderIDs.NeighborhoodSizeMap_RW, passData.neighborhoodSizeMap);
                cmd.DispatchCompute(cs, passData.kernelFillNeighborhoodSizeWithMinLevel, threadGroupsX, threadGroupsY, 1);
            }
        }

        if (needsDepthPyramid)
        {
            // --- ステージ9a: 深度ピラミッドの構築（全カーネルタイプ共通） ---
            // L1は_ViewPositionMapと_OriginTypeMapから物理点群のみを抽出
            {
                int l1_w = Mathf.Max(1, (sw + 1) / 2);
                int l1_h = Mathf.Max(1, (sh + 1) / 2);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildDepthPyramidL1, ShaderIDs.ViewPositionMap, passData.viewPositionMap);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildDepthPyramidL1, ShaderIDs.OriginTypeMap, passData.originTypeMap);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildDepthPyramidL1, ShaderIDs.DepthPyramidL1_RW, passData.depthPyramidL1);
                cmd.DispatchCompute(cs, passData.kernelBuildDepthPyramidL1, (l1_w + 7) / 8, (l1_h + 7) / 8, 1);

                int l2_w = Mathf.Max(1, (l1_w + 1) / 2);
                int l2_h = Mathf.Max(1, (l1_h + 1) / 2);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildDepthPyramidL2, ShaderIDs.DepthPyramidL1, passData.depthPyramidL1);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildDepthPyramidL2, ShaderIDs.DepthPyramidL2_RW, passData.depthPyramidL2);
                cmd.DispatchCompute(cs, passData.kernelBuildDepthPyramidL2, (l2_w + 7) / 8, (l2_h + 7) / 8, 1);

                int l3_w = Mathf.Max(1, (l2_w + 1) / 2);
                int l3_h = Mathf.Max(1, (l2_h + 1) / 2);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildDepthPyramidL3, ShaderIDs.DepthPyramidL2, passData.depthPyramidL2);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildDepthPyramidL3, ShaderIDs.DepthPyramidL3_RW, passData.depthPyramidL3);
                cmd.DispatchCompute(cs, passData.kernelBuildDepthPyramidL3, (l3_w + 7) / 8, (l3_h + 7) / 8, 1);

                int l4_w = Mathf.Max(1, (l3_w + 1) / 2);
                int l4_h = Mathf.Max(1, (l3_h + 1) / 2);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildDepthPyramidL4, ShaderIDs.DepthPyramidL3, passData.depthPyramidL3);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildDepthPyramidL4, ShaderIDs.DepthPyramidL4_RW, passData.depthPyramidL4);
                cmd.DispatchCompute(cs, passData.kernelBuildDepthPyramidL4, (l4_w + 7) / 8, (l4_h + 7) / 8, 1);

                int l5_w = Mathf.Max(1, (l4_w + 1) / 2);
                int l5_h = Mathf.Max(1, (l4_h + 1) / 2);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildDepthPyramidL5, ShaderIDs.DepthPyramidL4, passData.depthPyramidL4);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildDepthPyramidL5, ShaderIDs.DepthPyramidL5_RW, passData.depthPyramidL5);
                cmd.DispatchCompute(cs, passData.kernelBuildDepthPyramidL5, (l5_w + 7) / 8, (l5_h + 7) / 8, 1);

                int l6_w = Mathf.Max(1, (l5_w + 1) / 2);
                int l6_h = Mathf.Max(1, (l5_h + 1) / 2);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildDepthPyramidL6, ShaderIDs.DepthPyramidL5, passData.depthPyramidL5);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildDepthPyramidL6, ShaderIDs.DepthPyramidL6_RW, passData.depthPyramidL6);
                cmd.DispatchCompute(cs, passData.kernelBuildDepthPyramidL6, (l6_w + 7) / 8, (l6_h + 7) / 8, 1);
            }
            

            // --- ステージ9b: （オプション）急な深度勾配がある部分の近傍サイズを補正 ---
            if (passData.settings.enableGradientCorrection)
            {
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

        // --- ステージ10: ピラミッドサンプリングによるオクルージョン判定 ---
        if (passData.hasVirtualObjects)
        {
            if (passData.settings.kernelType == PCDRendererFeature.PCD_OcclusionKernel.Skip)
            {
                cmd.SetComputeTextureParam(cs, passData.kernelCopyColorToOcclusion, ShaderIDs.ColorMap, passData.colorMap);
                cmd.SetComputeTextureParam(cs, passData.kernelCopyColorToOcclusion, ShaderIDs.ViewPositionMap, passData.viewPositionMap);
                cmd.SetComputeTextureParam(cs, passData.kernelCopyColorToOcclusion, ShaderIDs.OriginTypeMap_RW, passData.originTypeMap);
                cmd.SetComputeTextureParam(cs, passData.kernelCopyColorToOcclusion, ShaderIDs.OcclusionResultMap_RW, passData.occlusionResultMap);
                cmd.DispatchCompute(cs, passData.kernelCopyColorToOcclusion, threadGroupsX, threadGroupsY, 1);
            }
            else
            {
                cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.ColorMap, passData.colorMap);
                cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.ViewPositionMap, passData.viewPositionMap);
                cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.VirtualDepthMap, passData.virtualDepthTexture);
                cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.OriginTypeMap_RW, passData.originTypeMap);
                cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.FinalNeighborhoodSizeMap, passData.settings.enableGradientCorrection ? passData.correctedNeighborhoodSizeMap : passData.neighborhoodSizeMap);
                // ピラミッドテクスチャをオクルージョンカーネルにバインド
                cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.DepthPyramidL1, passData.depthPyramidL1);
                cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.DepthPyramidL2, passData.depthPyramidL2);
                cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.DepthPyramidL3, passData.depthPyramidL3);
                cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.DepthPyramidL4, passData.depthPyramidL4);
                cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.DepthPyramidL5, passData.depthPyramidL5);
                cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.DepthPyramidL6, passData.depthPyramidL6);
                cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.OcclusionResultMap_RW, passData.occlusionResultMap);

                int shouldRecordDebug = (passData.settings.recordOcclusionDebugMap || passData.settings.recordPixelTagMap || passData.settings.enablePixelTagMap || passData.settings.enableOcclusionMap || passData.settings.recordNeighborCountMap) ? 1 : 0;
                cmd.SetComputeIntParam(cs, ShaderIDs.RecordOcclusionDebug, shouldRecordDebug);

                cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.NeighborCountMap_RW, passData.neighborCountMap);
                cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.OcclusionValueMap_RW, passData.occlusionValueMap);
                cmd.SetComputeTextureParam(cs, passData.kernelComputeOcclusion, ShaderIDs.OriginMap_RW, passData.debugDisplayMap);
                cmd.DispatchCompute(cs, passData.kernelComputeOcclusion, threadGroupsX, threadGroupsY, 1);
            }
        }
        else
        {
            // 仮想オブジェクトが存在しないが、ホールフィリングが有効な場合は単にColorMapをOcclusionResultMapにコピーする
            bool useHoleFilling = passData.settings.holeFillingMethod != PCDRendererFeature.PCD_HoleFillingMethod.None;
            if (useHoleFilling)
            {
                cmd.SetComputeTextureParam(cs, passData.kernelCopyColorToOcclusion, ShaderIDs.ColorMap, passData.colorMap);
                cmd.SetComputeTextureParam(cs, passData.kernelCopyColorToOcclusion, ShaderIDs.ViewPositionMap, passData.viewPositionMap);
                cmd.SetComputeTextureParam(cs, passData.kernelCopyColorToOcclusion, ShaderIDs.OriginTypeMap_RW, passData.originTypeMap);
                cmd.SetComputeTextureParam(cs, passData.kernelCopyColorToOcclusion, ShaderIDs.OcclusionResultMap_RW, passData.occlusionResultMap);
                cmd.DispatchCompute(cs, passData.kernelCopyColorToOcclusion, threadGroupsX, threadGroupsY, 1);
            }
        }

        // --- ステージ11: 点群が描画されなかったピクセルに対する穴埋め ---
        if (passData.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.JointBilateral)
        {
            cmd.SetComputeTextureParam(cs, passData.kernelFillHoles, ShaderIDs.ColorMap, passData.colorMap);
            cmd.SetComputeTextureParam(cs, passData.kernelFillHoles, ShaderIDs.DepthMap, passData.depthMap);
            cmd.SetComputeTextureParam(cs, passData.kernelFillHoles, ShaderIDs.VirtualDepthMap, passData.virtualDepthTexture);
            cmd.SetComputeTextureParam(cs, passData.kernelFillHoles, ShaderIDs.OriginTypeMap_RW, passData.originTypeMap);
            cmd.SetComputeTextureParam(cs, passData.kernelFillHoles, ShaderIDs.OcclusionResultMap_RW, passData.occlusionResultMap);
            cmd.SetComputeTextureParam(cs, passData.kernelFillHoles, ShaderIDs.OriginMap_RW, passData.debugDisplayMap);
            cmd.SetComputeTextureParam(cs, passData.kernelFillHoles, ShaderIDs.FinalNeighborhoodSizeMap, passData.settings.enableGradientCorrection ? passData.correctedNeighborhoodSizeMap : passData.neighborhoodSizeMap);
            cmd.DispatchCompute(cs, passData.kernelFillHoles, threadGroupsX, threadGroupsY, 1);
        }
        else if (passData.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.PullPush)
        {
            int maxLevel = 5;

            // Phase 0: Init
            cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPullPushInit, ShaderIDs.OcclusionResultMap, passData.occlusionResultMap);
            cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPullPushInit, ShaderIDs.OriginTypeMap, passData.originTypeMap);
            cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPullPushInit, ShaderIDs.PullPushLevel_Out_RW, passData.pullPushPyramid[0]);
            cmd.DispatchCompute(cs, passData.kernelFillHolesPullPushInit, threadGroupsX, threadGroupsY, 1);

            // Phase 1: Pull (Downsample)
            int pw = sw;
            int ph = sh;
            for (int i = 0; i < maxLevel - 1; i++)
            {
                pw = Mathf.Max(1, (pw + 1) / 2);
                ph = Mathf.Max(1, (ph + 1) / 2);
                int pullGroupsX = (pw + 7) / 8;
                int pullGroupsY = (ph + 7) / 8;

                cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPull, ShaderIDs.PullPushLevel_In, passData.pullPushPyramid[i]);
                cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPull, ShaderIDs.PullPushLevel_Out_RW, passData.pullPushPyramid[i + 1]);
                cmd.DispatchCompute(cs, passData.kernelFillHolesPull, pullGroupsX, pullGroupsY, 1);
            }

            // Phase 2: Push (Upsample and Fill)
            for (int i = maxLevel - 2; i >= 0; i--)
            {
                pw = Mathf.Max(1, sw >> i); // approximate size
                ph = Mathf.Max(1, sh >> i);
                if (i == 0) { pw = sw; ph = sh; } // ensure base level matches exact resolution
                else {
                    pw = Mathf.Max(1, sw >> i);
                    ph = Mathf.Max(1, sh >> i);
                }
                
                // more accurate width/height calculation
                int curr_w = sw; int curr_h = sh;
                for (int j = 0; j < i; j++) { curr_w = Mathf.Max(1, (curr_w + 1) / 2); curr_h = Mathf.Max(1, (curr_h + 1) / 2); }
                int pushGroupsX = (curr_w + 7) / 8;
                int pushGroupsY = (curr_h + 7) / 8;

                cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPush, ShaderIDs.PullPushLevel_In, passData.pullPushPyramid[i + 1]);
                cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPush, ShaderIDs.PullPushLevel_Out_RW, passData.pullPushPyramid[i]);
                cmd.DispatchCompute(cs, passData.kernelFillHolesPush, pushGroupsX, pushGroupsY, 1);
            }

            // Phase 3: Copy result back to OcclusionResultMap and update OriginTypeMap
            cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPullPushFinalize, ShaderIDs.PullPushLevel_In, passData.pullPushPyramid[0]);
            cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPullPushFinalize, ShaderIDs.OcclusionResultMap_RW, passData.occlusionResultMap);
            cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPullPushFinalize, ShaderIDs.OriginTypeMap_RW, passData.originTypeMap);
            cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPullPushFinalize, ShaderIDs.OriginMap_RW, passData.debugDisplayMap);
            cmd.DispatchCompute(cs, passData.kernelFillHolesPullPushFinalize, threadGroupsX, threadGroupsY, 1);
        }
        else if (passData.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.Morphology_OC ||
                 passData.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.Morphology_CO)
        {
            int halfSize = passData.settings.morphKernelHalfSize;

            cmd.SetComputeIntParam(cs, ShaderIDs.MorphKernelHalfSize, halfSize);

            bool currentInTemp = false;

            System.Action<int, string> runMorphPass = (kernelId, passName) =>
            {
                RTHandle colorIn = currentInTemp ? passData.morphColorTemp : passData.occlusionResultMap;
                RTHandle typeIn = currentInTemp ? passData.morphTypeTemp : passData.originTypeMap;
                RTHandle colorOut = currentInTemp ? passData.occlusionResultMap : passData.morphColorTemp;
                RTHandle typeOut = currentInTemp ? passData.originTypeMap : passData.morphTypeTemp;

                // Build Morph Pyramid first
                int l1_w = Mathf.Max(1, (sw + 1) / 2);
                int l1_h = Mathf.Max(1, (sh + 1) / 2);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL1, ShaderIDs.MorphTypeIn, typeIn);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL1, ShaderIDs.MorphColorIn, colorIn);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL1, ShaderIDs.MorphTypePyramidL1_RW, passData.morphTypePyramidL1);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL1, ShaderIDs.MorphColorPyramidL1_RW, passData.morphColorPyramidL1);
                cmd.DispatchCompute(cs, passData.kernelBuildMorphPyramidL1, (l1_w + 7) / 8, (l1_h + 7) / 8, 1);

                int l2_w = Mathf.Max(1, (l1_w + 1) / 2);
                int l2_h = Mathf.Max(1, (l1_h + 1) / 2);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL2, ShaderIDs.MorphTypePyramidL1, passData.morphTypePyramidL1);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL2, ShaderIDs.MorphColorPyramidL1, passData.morphColorPyramidL1);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL2, ShaderIDs.MorphTypePyramidL2_RW, passData.morphTypePyramidL2);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL2, ShaderIDs.MorphColorPyramidL2_RW, passData.morphColorPyramidL2);
                cmd.DispatchCompute(cs, passData.kernelBuildMorphPyramidL2, (l2_w + 7) / 8, (l2_h + 7) / 8, 1);

                int l3_w = Mathf.Max(1, (l2_w + 1) / 2);
                int l3_h = Mathf.Max(1, (l2_h + 1) / 2);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL3, ShaderIDs.MorphTypePyramidL2, passData.morphTypePyramidL2);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL3, ShaderIDs.MorphColorPyramidL2, passData.morphColorPyramidL2);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL3, ShaderIDs.MorphTypePyramidL3_RW, passData.morphTypePyramidL3);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL3, ShaderIDs.MorphColorPyramidL3_RW, passData.morphColorPyramidL3);
                cmd.DispatchCompute(cs, passData.kernelBuildMorphPyramidL3, (l3_w + 7) / 8, (l3_h + 7) / 8, 1);

                int l4_w = Mathf.Max(1, (l3_w + 1) / 2);
                int l4_h = Mathf.Max(1, (l3_h + 1) / 2);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL4, ShaderIDs.MorphTypePyramidL3, passData.morphTypePyramidL3);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL4, ShaderIDs.MorphColorPyramidL3, passData.morphColorPyramidL3);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL4, ShaderIDs.MorphTypePyramidL4_RW, passData.morphTypePyramidL4);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL4, ShaderIDs.MorphColorPyramidL4_RW, passData.morphColorPyramidL4);
                cmd.DispatchCompute(cs, passData.kernelBuildMorphPyramidL4, (l4_w + 7) / 8, (l4_h + 7) / 8, 1);

                int l5_w = Mathf.Max(1, (l4_w + 1) / 2);
                int l5_h = Mathf.Max(1, (l4_h + 1) / 2);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL5, ShaderIDs.MorphTypePyramidL4, passData.morphTypePyramidL4);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL5, ShaderIDs.MorphColorPyramidL4, passData.morphColorPyramidL4);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL5, ShaderIDs.MorphTypePyramidL5_RW, passData.morphTypePyramidL5);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL5, ShaderIDs.MorphColorPyramidL5_RW, passData.morphColorPyramidL5);
                cmd.DispatchCompute(cs, passData.kernelBuildMorphPyramidL5, (l5_w + 7) / 8, (l5_h + 7) / 8, 1);

                int l6_w = Mathf.Max(1, (l5_w + 1) / 2);
                int l6_h = Mathf.Max(1, (l5_h + 1) / 2);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL6, ShaderIDs.MorphTypePyramidL5, passData.morphTypePyramidL5);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL6, ShaderIDs.MorphColorPyramidL5, passData.morphColorPyramidL5);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL6, ShaderIDs.MorphTypePyramidL6_RW, passData.morphTypePyramidL6);
                cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL6, ShaderIDs.MorphColorPyramidL6_RW, passData.morphColorPyramidL6);
                cmd.DispatchCompute(cs, passData.kernelBuildMorphPyramidL6, (l6_w + 7) / 8, (l6_h + 7) / 8, 1);

                // Now execute the actual morph pass
                cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphColorIn, colorIn);
                cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphTypeIn, typeIn);
                cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphColorOut_RW, colorOut);
                cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphTypeOut_RW, typeOut);
                cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.FinalNeighborhoodSizeMap, passData.settings.enableGradientCorrection ? passData.correctedNeighborhoodSizeMap : passData.neighborhoodSizeMap);

                cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphTypePyramidL1, passData.morphTypePyramidL1);
                cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphTypePyramidL2, passData.morphTypePyramidL2);
                cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphTypePyramidL3, passData.morphTypePyramidL3);
                cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphTypePyramidL4, passData.morphTypePyramidL4);
                cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphTypePyramidL5, passData.morphTypePyramidL5);
                cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphTypePyramidL6, passData.morphTypePyramidL6);

                cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphColorPyramidL1, passData.morphColorPyramidL1);
                cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphColorPyramidL2, passData.morphColorPyramidL2);
                cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphColorPyramidL3, passData.morphColorPyramidL3);
                cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphColorPyramidL4, passData.morphColorPyramidL4);
                cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphColorPyramidL5, passData.morphColorPyramidL5);
                cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphColorPyramidL6, passData.morphColorPyramidL6);

                cmd.DispatchCompute(cs, kernelId, threadGroupsX, threadGroupsY, 1);
                currentInTemp = !currentInTemp;
            };

            if (passData.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.Morphology_OC)
            {
                // Opening (Erode -> Dilate) then Closing (Dilate -> Erode)
                for (int i = 0; i < passData.settings.morphErodeIterations; i++) runMorphPass(passData.kernelMorphologyErode, "Erode");
                for (int i = 0; i < passData.settings.morphDilateIterations; i++) runMorphPass(passData.kernelMorphologyDilate, "Dilate");
                for (int i = 0; i < passData.settings.morphDilateIterations; i++) runMorphPass(passData.kernelMorphologyDilate, "Dilate");
                for (int i = 0; i < passData.settings.morphErodeIterations; i++) runMorphPass(passData.kernelMorphologyErode, "Erode");
            }
            else if (passData.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.Morphology_CO)
            {
                // Closing (Dilate -> Erode) then Opening (Erode -> Dilate)
                // The user requested Pyramid -> Dilate -> Pyramid -> Erode
                for (int i = 0; i < passData.settings.morphDilateIterations; i++) runMorphPass(passData.kernelMorphologyDilate, "Dilate");
                for (int i = 0; i < passData.settings.morphErodeIterations; i++) runMorphPass(passData.kernelMorphologyErode, "Erode");
                for (int i = 0; i < passData.settings.morphErodeIterations; i++) runMorphPass(passData.kernelMorphologyErode, "Erode");
                for (int i = 0; i < passData.settings.morphDilateIterations; i++) runMorphPass(passData.kernelMorphologyDilate, "Dilate");
            }
            else
            {
                // Fallback (e.g. basic Dilate then Erode)
                for (int i = 0; i < passData.settings.morphDilateIterations; i++) runMorphPass(passData.kernelMorphologyDilate, "Dilate");
                for (int i = 0; i < passData.settings.morphErodeIterations; i++) runMorphPass(passData.kernelMorphologyErode, "Erode");
            }

            if (currentInTemp)
            {
                cmd.SetComputeTextureParam(cs, passData.kernelMorphologyCopy, ShaderIDs.MorphColorIn, passData.morphColorTemp);
                cmd.SetComputeTextureParam(cs, passData.kernelMorphologyCopy, ShaderIDs.MorphTypeIn, passData.morphTypeTemp);
                cmd.SetComputeTextureParam(cs, passData.kernelMorphologyCopy, ShaderIDs.MorphColorOut_RW, passData.occlusionResultMap);
                cmd.SetComputeTextureParam(cs, passData.kernelMorphologyCopy, ShaderIDs.MorphTypeOut_RW, passData.originTypeMap);
                cmd.DispatchCompute(cs, passData.kernelMorphologyCopy, threadGroupsX, threadGroupsY, 1);
            }
        }

        // --- ステージ12: オクルージョンによってできた穴を補完し、仮想深度マップやカメラバッファとマージ ---
        if (passData.settings.holeFillingMethod != PCDRendererFeature.PCD_HoleFillingMethod.None)
        {
            cmd.SetComputeTextureParam(cs, passData.kernelInterpolate, ShaderIDs.OcclusionResultMap, passData.occlusionResultMap);
            cmd.SetComputeTextureParam(cs, passData.kernelInterpolate, ShaderIDs.VirtualDepthMap, passData.virtualDepthTexture);
            cmd.SetComputeTextureParam(cs, passData.kernelInterpolate, ShaderIDs.CameraColorTexture, passData.cameraColorTexture);
            cmd.SetComputeTextureParam(cs, passData.kernelInterpolate, ShaderIDs.OriginTypeMap, passData.originTypeMap);
            cmd.SetComputeTextureParam(cs, passData.kernelInterpolate, ShaderIDs.FinalImage_RW, passData.finalImage);
            cmd.SetComputeTextureParam(cs, passData.kernelInterpolate, ShaderIDs.OriginMap_RW, passData.debugDisplayMap);
            cmd.DispatchCompute(cs, passData.kernelInterpolate, threadGroupsX, threadGroupsY, 1);
        }

        // --- ステージ13: PixelTag または OcclusionMap を表示時は、OcclusionValueMapを常時カラー(またはグレースケール)可視化して上書き ---
        if (passData.settings.enablePixelTagMap || passData.settings.enableOcclusionMap)
        {
            int displayMode = passData.settings.enablePixelTagMap ? 1 : 2;
            cmd.SetComputeIntParam(cs, ShaderIDs.DebugDisplayMode, displayMode);
            cmd.SetComputeTextureParam(cs, passData.kernelVisualizeOcclusionDebug, ShaderIDs.OcclusionValueMap_RW, passData.occlusionValueMap);
            cmd.SetComputeTextureParam(cs, passData.kernelVisualizeOcclusionDebug, ShaderIDs.OriginMap_RW, passData.debugDisplayMap);
            cmd.DispatchCompute(cs, passData.kernelVisualizeOcclusionDebug, threadGroupsX, threadGroupsY, 1);
        }

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

    private static void ExecuteBlitPass(BlitPassData passData, RasterGraphContext context)
    {
        // デバッグ出力または通常の出力用の標準的なBlitのフォールバック
        // ※出力先(RenderTarget)の切り替えはSetupフェーズ(RecordRenderGraph)で
        // builder.SetRenderAttachment を通じて行われているため、ここではそのまま描画します
        Blitter.BlitTexture(context.cmd, passData.sourceImage, new Vector2(1, 1), 0.0f, false);
    }
}