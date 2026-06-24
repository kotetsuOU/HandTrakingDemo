// =============================================================================
// PCD_RenderPass_RenderGraph_Compute.cs
// -----------------------------------------------------------------------------
// Unity の RenderGraph にオクルージョン計算用の Compute Pass を登録する処理を担う。
// =============================================================================
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

public partial class PCDRenderPass
{
    /// <summary>
    /// コンピュートシェーダーを実行するパスをRenderGraphに追加し、後段で使用するテクスチャハンドルを返す。
    /// </summary>
    private RenderGraphHandles EnqueueComputePass(
        RenderGraph renderGraph, 
        UniversalResourceData resourceData, 
        Camera camera, 
        RenderGraphSetupContext ctx)
    {
        RenderGraphHandles outHandles = new RenderGraphHandles();

        using (var builder = renderGraph.AddUnsafePass<ComputePassData>(PROFILER_TAG, out var data))
        {
            builder.AllowGlobalStateModification(true);

            // パスへ渡すパラメータ（シェーダーや各種データ）を登録
            BindComputePassData(ref data, camera, ctx.screenWidth, ctx.screenHeight, ctx.activeCount, ctx.activeBuffer, ctx.depthMapOnlyMode, resourceData);

            data.hasVirtualObjects = ctx.hasVirtualObjects;

            // 仮想深度（バックグラウンドの深度）を使用する場合、カメラの深度テクスチャを登録
            if (data.hasVirtualDepth || ctx.depthMapOnlyMode)
            {
                data.virtualDepthTexture = resourceData.cameraDepthTexture;
            }
            else
            {
                // 使用しない場合のフォールバックテクスチャとしてのダミーを作成
                var virtualDepthFallbackDesc = new TextureDesc(1, 1)
                {
                    colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.RFloat, false)
                };
                data.virtualDepthTexture = renderGraph.CreateTexture(virtualDepthFallbackDesc);
            }
            builder.UseTexture(data.virtualDepthTexture, AccessFlags.Read);

            // カメラのカラーテクスチャを登録
            if (data.hasVirtualDepth && resourceData.activeColorTexture.IsValid())
            {
                data.cameraColorTexture = resourceData.activeColorTexture;
            }
            else
            {
                var cameraColorFallbackDesc = new TextureDesc(1, 1)
                {
                    colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.ARGBFloat, false)
                };
                data.cameraColorTexture = renderGraph.CreateTexture(cameraColorFallbackDesc);
            }
            builder.UseTexture(data.cameraColorTexture, AccessFlags.Read);

            // 中間処理で使用する各種バッファを生成（カラー、深度、座標情報など）
            data.depthPyramidL1 = _bufferManager.depthPyramidL1;
            data.depthPyramidL2 = _bufferManager.depthPyramidL2;
            data.depthPyramidL3 = _bufferManager.depthPyramidL3;
            data.depthPyramidL4 = _bufferManager.depthPyramidL4;
            data.depthPyramidL5 = _bufferManager.depthPyramidL5;
            data.depthPyramidL6 = _bufferManager.depthPyramidL6;
            data.pullPushPyramid = _bufferManager.pullPushPyramid;
            data.morphTypePyramidL1 = _bufferManager.morphTypePyramidL1;
            data.morphTypePyramidL2 = _bufferManager.morphTypePyramidL2;
            data.morphTypePyramidL3 = _bufferManager.morphTypePyramidL3;
            data.morphTypePyramidL4 = _bufferManager.morphTypePyramidL4;
            data.morphTypePyramidL5 = _bufferManager.morphTypePyramidL5;
            data.morphTypePyramidL6 = _bufferManager.morphTypePyramidL6;
            data.morphColorPyramidL1 = _bufferManager.morphColorPyramidL1;
            data.morphColorPyramidL2 = _bufferManager.morphColorPyramidL2;
            data.morphColorPyramidL3 = _bufferManager.morphColorPyramidL3;
            data.morphColorPyramidL4 = _bufferManager.morphColorPyramidL4;
            data.morphColorPyramidL5 = _bufferManager.morphColorPyramidL5;
            data.morphColorPyramidL6 = _bufferManager.morphColorPyramidL6;

            data.colorMap = _bufferManager._colorMapHandle;
            data.viewPositionMap = _bufferManager._viewPositionMapHandle;

            if (data.settings.recordIntegratedDepthMap)
            {
                outHandles.integratedDepthMap = renderGraph.ImportTexture(_bufferManager._integratedDepthMapHandle);
                data.depthMap = _bufferManager._integratedDepthMapHandle;
            }
            else
            {
                data.depthMap = _bufferManager._depthMapHandle;
            }

            if (data.settings.recordNeighborhoodMap)
            {
                outHandles.neighborhoodMap = renderGraph.ImportTexture(_bufferManager._neighborhoodMapHandle);
                if (data.settings.enableGradientCorrection)
                {
                    data.correctedNeighborhoodSizeMap = outHandles.neighborhoodMap;
                    data.neighborhoodSizeMap = _bufferManager._neighborhoodSizeMapHandle;
                }
                else
                {
                    data.neighborhoodSizeMap = outHandles.neighborhoodMap;
                    data.correctedNeighborhoodSizeMap = _bufferManager._correctedNeighborhoodSizeMapHandle;
                }
            }
            else
            {
                data.neighborhoodSizeMap = _bufferManager._neighborhoodSizeMapHandle;
                data.correctedNeighborhoodSizeMap = _bufferManager._correctedNeighborhoodSizeMapHandle;
            }

            data.originTypeMap = _bufferManager._originTypeMapHandle;

            // 常にImportしてバインドさせる
            outHandles.neighborCountMap = renderGraph.ImportTexture(_bufferManager._neighborCountMapHandle);
            data.neighborCountMap = _bufferManager._neighborCountMapHandle;

            if (data.settings.enableDensityBasedLOD)
            {
                data.gridZMinMap = _bufferManager._gridZMinMapHandle;
                data.gridLevelMap = _bufferManager._gridLevelMapHandle;
                data.filteredGridLevelMap = _bufferManager._filteredGridLevelMapHandle;
                data.densityMap = _bufferManager._densityMapHandle;
            }

            if (data.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.Morphology_OC ||
                data.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.Morphology_CO)
            {
                data.morphColorTemp = _bufferManager._morphColorTempHandle;
                data.morphTypeTemp = _bufferManager._morphTypeTempHandle;
            }

            bool useHoleFilling = data.settings.holeFillingMethod != PCDRendererFeature.PCD_HoleFillingMethod.None;
            bool needsOcclusionResultMap = ctx.hasVirtualObjects || useHoleFilling;

            if (needsOcclusionResultMap)
            {
                data.occlusionResultMap = _bufferManager._occlusionResultMapHandle;
            }
            else
            {
                data.occlusionResultMap = data.colorMap;
            }
            
            if (data.settings.recordOcclusionDebugMap || data.settings.recordPixelTagMap)
            {
                outHandles.occlusionValueMap = renderGraph.ImportTexture(_bufferManager._occlusionValueMapHandle);
                data.occlusionValueMap = _bufferManager._occlusionValueMapHandle;
            }
            else
            {
                data.occlusionValueMap = _bufferManager._occlusionValueMapHandle;
            }
            
            if (useHoleFilling)
            {
                data.finalImage = _bufferManager._finalImageHandle;
            }

            if (data.settings.enablePixelTagMap || data.settings.enableOcclusionMap)
            {
                outHandles.debugDisplayMap = renderGraph.ImportTexture(_bufferManager._debugDisplayMapHandle);
                data.debugDisplayMap = _bufferManager._debugDisplayMapHandle;
            }
            else
            {
                data.debugDisplayMap = _bufferManager._debugDisplayMapHandle;
            }

            outHandles.finalImage = renderGraph.ImportTexture(useHoleFilling ? _bufferManager._finalImageHandle : _bufferManager._occlusionResultMapHandle);

            // アロケーションが終わったら、実際のComputeShader実行関数を登録
            builder.SetRenderFunc((ComputePassData passData, UnsafeGraphContext context) =>
            {
                ExecuteComputePass(passData, context);
            });

            // デバッグデータを非同期読込する場合はカリングを無効化
            if (data.settings.recordOcclusionDebugMap || data.settings.recordPixelTagMap || data.settings.recordIntegratedDepthMap || data.settings.recordNeighborhoodMap || data.settings.recordNeighborCountMap)
            {
                builder.AllowPassCulling(false);
            }
        }

        return outHandles;
    }
}
