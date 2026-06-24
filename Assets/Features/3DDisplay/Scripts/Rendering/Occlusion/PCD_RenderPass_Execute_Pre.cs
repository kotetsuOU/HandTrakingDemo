// =============================================================================
// PCD_RenderPass_Execute_Pre.cs
// -----------------------------------------------------------------------------
// ExecuteComputePass の初期ステージ（マップクリア、仮想深度初期化、投影、
// 密度およびLOD計算）を担う partial クラス。
// =============================================================================
using UnityEngine;
using UnityEngine.Rendering;

public partial class PCDRenderPass
{
    // =========================================================================
    // ステージ1: 中間RTテクスチャのクリア
    // =========================================================================

    /// <summary>
    /// 中間RTテクスチャをクリアする。InitFromCamera が有効な場合は全ピクセルを上書き初期化するためスキップ。
    /// </summary>
    private void ExecuteStageClearMaps(CommandBuffer cmd, ComputeShader cs, ComputePassData passData,
        int threadGroupsX, int threadGroupsY, bool runInitFromCamera)
    {
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
    }

    // =========================================================================
    // ステージ2: 仮想深度マップからの初期化
    // =========================================================================

    /// <summary>
    /// Unity標準パイプラインの深度バッファとカラーバッファを読み込み、
    /// 点群パイプラインの中間テクスチャを初期化する。
    /// </summary>
    private void ExecuteStageInitFromCamera(CommandBuffer cmd, ComputeShader cs, ComputePassData passData,
        int threadGroupsX, int threadGroupsY, bool runInitFromCamera)
    {
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
            // ClearMaps の代わりにここで OriginMap も初期化する
            cmd.SetComputeTextureParam(cs, passData.kernelInitFromCamera, ShaderIDs.OriginMap_RW, passData.debugDisplayMap);
            
            cmd.SetComputeBufferParam(cs, passData.kernelInitFromCamera, ShaderIDs.StaticMeshCounter_RW, passData.staticMeshCounterBuffer);
            cmd.SetComputeMatrixParam(cs, ShaderIDs.InverseProjectionMatrix, passData.inverseProjectionMatrix);
            cmd.DispatchCompute(cs, passData.kernelInitFromCamera, threadGroupsX, threadGroupsY, 1);
        }
        else
        {
            cmd.SetComputeIntParam(cs, ShaderIDs.UseVirtualDepth, 0);
        }
    }

    // =========================================================================
    // ステージ3: 3D点群のスクリーンスペースへの投影
    // =========================================================================

    /// <summary>
    /// 3Dの点群データをビュー/プロジェクション行列でスクリーン座標に投影し、
    /// カラー・深度・ビュー座標の各マップに書き込む。
    /// </summary>
    private void ExecuteStageProjectPoints(CommandBuffer cmd, ComputeShader cs, ComputePassData passData)
    {
        if (passData.depthMapOnlyMode)
            return;

        cmd.SetComputeBufferParam(cs, passData.kernelProject, ShaderIDs.PointBuffer, passData.pointBuffer);
        cmd.SetComputeTextureParam(cs, passData.kernelProject, ShaderIDs.ColorMap_RW, passData.colorMap);
        cmd.SetComputeTextureParam(cs, passData.kernelProject, ShaderIDs.DepthMap_RW, passData.depthMap);
        cmd.SetComputeTextureParam(cs, passData.kernelProject, ShaderIDs.ViewPositionMap_RW, passData.viewPositionMap);
        cmd.SetComputeTextureParam(cs, passData.kernelProject, ShaderIDs.OriginTypeMap_RW, passData.originTypeMap);
        int projectGroups = (passData.pointCount + 255) / 256;
        cmd.DispatchCompute(cs, passData.kernelProject, projectGroups, 1, 1);
    }

    // =========================================================================
    // ステージ4〜8: 密度計算とLOD（詳細レベル）の決定
    // =========================================================================

    /// <summary>
    /// グリッドベースの密度計算を行い、各ピクセルに対する最適な近傍サイズを決定する。
    /// enableDensityBasedLOD が有効な場合: GridZMin → Density → GridLevel → MedianFilter → NeighborhoodSize
    /// 無効な場合: MinSearchLevel で全ピクセルの近傍サイズを一括初期化
    /// </summary>
    private void ExecuteStageDensityAndLOD(CommandBuffer cmd, ComputeShader cs, ComputePassData passData,
        int threadGroupsX, int threadGroupsY, int gridGroupsX, int gridGroupsY, bool needsNeighborhoodSize)
    {
        if (!needsNeighborhoodSize)
            return;

        if (passData.settings.enableDensityBasedLOD)
        {
            // ステージ4: 各グリッドセルの最小深度を計算
            cmd.SetComputeTextureParam(cs, passData.kernelCalcGridZMin, ShaderIDs.DepthMap, passData.depthMap);
            cmd.SetComputeTextureParam(cs, passData.kernelCalcGridZMin, ShaderIDs.GridZMinMap_RW, passData.gridZMinMap);
            cmd.DispatchCompute(cs, passData.kernelCalcGridZMin, gridGroupsX, gridGroupsY, 1);

            // ステージ5: 画面上のサンプル密度を計算
            cmd.SetComputeTextureParam(cs, passData.kernelCalcDensity, ShaderIDs.DepthMap, passData.depthMap);
            cmd.SetComputeTextureParam(cs, passData.kernelCalcDensity, ShaderIDs.GridZMinMap, passData.gridZMinMap);
            cmd.SetComputeTextureParam(cs, passData.kernelCalcDensity, ShaderIDs.OriginTypeMap, passData.originTypeMap);
            cmd.SetComputeTextureParam(cs, passData.kernelCalcDensity, ShaderIDs.DensityMap_RW, passData.densityMap);
            cmd.DispatchCompute(cs, passData.kernelCalcDensity, gridGroupsX, gridGroupsY, 1);

            // ステージ6: 密度に応じた詳細レベル（グリッドレベル）を決定
            cmd.SetComputeTextureParam(cs, passData.kernelCalcGridLevel, ShaderIDs.DensityMap, passData.densityMap);
            cmd.SetComputeTextureParam(cs, passData.kernelCalcGridLevel, ShaderIDs.GridLevelMap_RW, passData.gridLevelMap);
            int gridThreadX = (gridGroupsX + 15) / 16;
            int gridThreadY = (gridGroupsY + 15) / 16;
            cmd.DispatchCompute(cs, passData.kernelCalcGridLevel, Mathf.Max(1, gridThreadX), Mathf.Max(1, gridThreadY), 1);

            // ステージ7: メディアンフィルターでグリッドレベルを平滑化
            cmd.SetComputeTextureParam(cs, passData.kernelGridMedianFilter, ShaderIDs.GridLevelMap, passData.gridLevelMap);
            cmd.SetComputeTextureParam(cs, passData.kernelGridMedianFilter, ShaderIDs.FilteredGridLevelMap_RW, passData.filteredGridLevelMap);
            cmd.DispatchCompute(cs, passData.kernelGridMedianFilter, Mathf.Max(1, gridThreadX), Mathf.Max(1, gridThreadY), 1);

            // ステージ8: フィルター処理されたLODに基づいて近傍サイズを算出
            cmd.SetComputeTextureParam(cs, passData.kernelCalcNeighborhoodSize, ShaderIDs.FilteredGridLevelMap, passData.filteredGridLevelMap);
            cmd.SetComputeTextureParam(cs, passData.kernelCalcNeighborhoodSize, ShaderIDs.NeighborhoodSizeMap_RW, passData.neighborhoodSizeMap);
            cmd.DispatchCompute(cs, passData.kernelCalcNeighborhoodSize, threadGroupsX, threadGroupsY, 1);
        }
        else
        {
            // ステージ8（代替）: 密度計算をスキップし、MinSearchLevel で一括初期化
            cmd.SetComputeTextureParam(cs, passData.kernelFillNeighborhoodSizeWithMinLevel, ShaderIDs.NeighborhoodSizeMap_RW, passData.neighborhoodSizeMap);
            cmd.DispatchCompute(cs, passData.kernelFillNeighborhoodSizeWithMinLevel, threadGroupsX, threadGroupsY, 1);
        }
    }
}
