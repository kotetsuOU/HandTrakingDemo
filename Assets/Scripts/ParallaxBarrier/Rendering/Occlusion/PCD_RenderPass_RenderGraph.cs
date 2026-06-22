using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

public partial class PCDRenderPass
{
    private string GetMethodPrefix()
    {
        if (PCDRendererFeature.Instance == null) return "";

        bool isTag = PCDRendererFeature.Instance.settings.enableTagBasedOptimization;
        bool isDensity = PCDRendererFeature.Instance.settings.enableTypeAwareDensity;
        bool isFade = PCDRendererFeature.Instance.settings.enableSoftOcclusionFade;
        bool isHoleFill = PCDRendererFeature.Instance.settings.holeFillingMethod != PCDRendererFeature.PCD_HoleFillingMethod.None;

        if (isTag && isDensity && isFade && isHoleFill) return "Proposal";
        if (!isTag && !isDensity && !isFade && !isHoleFill) return "Traditional";
        return $"Ablation_T{(isTag ? "1" : "0")}_D{(isDensity ? "1" : "0")}_F{(isFade ? "1" : "0")}_H{(isHoleFill ? "1" : "0")}";
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        // 初期化が行われていない場合は初期化を実行
        if (!_isInitialized) Initialize();
        if (!_isInitialized) return;

        // 再生中のみ処理を実行（エディタの編集中はスキップ）
        if (!UnityEngine.Application.isPlaying) return;

        // グローバルバッファモードを使用するかどうかを判断
        bool shouldUseExternal = PCDRendererFeature.Instance.IsGlobalBufferMode;

        // 外部（グローバル）のポイントクラウドデータが存在する場合、バッファをセットする
        if (shouldUseExternal && RsGlobalPointCloudManager.Instance != null)
        {
            var globalBuffer = RsGlobalPointCloudManager.Instance.GetGlobalBuffer();
            var globalCount = RsGlobalPointCloudManager.Instance.CurrentTotalCount;
            _bufferManager.SetExternalBuffer(globalBuffer, globalCount);
        }
        else
        {
            _bufferManager.SetExternalBuffer(null, 0);
        }

        // バッファの更新処理を実行
        _bufferManager.Update();

        ComputeBuffer activeBuffer = null;
        int activeCount = 0;

        // 外部バッファが使用され、データが存在する場合の処理
        if (_bufferManager.UseExternalBuffer && _bufferManager.ExternalPointBuffer != null)
        {
            // Update: If count is 0, it should be treated as 0 instead of falling back to the whole buffer size (which may still hold old cached data on the GPU).
            int extCount = _bufferManager.ExternalPointCount >= 0 ? _bufferManager.ExternalPointCount : _bufferManager.ExternalPointBuffer.count;

            // 内部データも存在する場合は、両方を結合したバッファを使用する
            if (_bufferManager.PointCount > 0)
            {
                int totalCount = extCount + _bufferManager.PointCount;
                _bufferManager.EnsureCombinedBuffer(totalCount);
                activeBuffer = _bufferManager.CombinedBuffer;
                activeCount = totalCount;
            }
            else
            {
                // 外部データのみの場合はそのまま使用
                activeBuffer = _bufferManager.ExternalPointBuffer;
                activeCount = extCount;
            }
        }
        else
        {
            // 内部データのみを使用
            activeBuffer = _bufferManager.PointBuffer;
            activeCount = _bufferManager.PointCount;
        }

        // DepthMapメッシュやPointCloudメッシュ、点群データが存在するか確認
        bool hasDepthMapMeshes = _bufferManager.HasDepthMapMeshes();
        bool hasPointCloudMeshes = _bufferManager.HasPointCloudMeshes();
        bool pointCloudHasData = activeBuffer != null && activeCount > 0 && activeBuffer.IsValid();

        // デバッグ記録時のログ出力
        if (_settings.recordOcclusionDebugMap || _settings.recordPixelTagMap || _settings.recordIntegratedDepthMap)
        {
            UnityEngine.Debug.Log($"[PCDRenderPass] Record Debug. Occlusion={_settings.recordOcclusionDebugMap} PixelTag={_settings.recordPixelTagMap} IntegratedDepth={_settings.recordIntegratedDepthMap} DepthMap={hasDepthMapMeshes} PCMeshes={hasPointCloudMeshes} PointCloudData={pointCloudHasData} (Buffer={activeBuffer!=null}, Count={activeCount})");
        }

        // 点群データもメッシュも無い、背景深度の取得のみのモード
        bool depthMapOnlyMode = hasDepthMapMeshes && !hasPointCloudMeshes && !pointCloudHasData;

        if (depthMapOnlyMode)
        {
            // DepthMap取得のみであればフルレンダリングはスキップ
            if (_settings.recordOcclusionDebugMap || _settings.recordPixelTagMap || _settings.recordIntegratedDepthMap)
            {
                UnityEngine.Debug.LogWarning("[PCDRenderPass] Skipped rendering because depthMapOnlyMode is true.");
                if (PCDRendererFeature.Instance != null && PCDRendererFeature.Instance.settings != null)
                {
                    PCDRendererFeature.Instance.settings.recordOcclusionDebugMap = false;
                    PCDRendererFeature.Instance.settings.recordPixelTagMap = false;
                    PCDRendererFeature.Instance.settings.recordIntegratedDepthMap = false;
                }
            }
            return;
        }

        // 描画すべきデータが全く無ければスキップ
        if (!pointCloudHasData && !hasDepthMapMeshes)
        {
            if (_settings.recordOcclusionDebugMap || _settings.recordPixelTagMap || _settings.recordIntegratedDepthMap)
            {
                UnityEngine.Debug.LogWarning("[PCDRenderPass] Check box pressed but ignored. No point cloud and no depth map data.");
                if (PCDRendererFeature.Instance != null && PCDRendererFeature.Instance.settings != null)
                {
                    PCDRendererFeature.Instance.settings.recordOcclusionDebugMap = false;
                    PCDRendererFeature.Instance.settings.recordPixelTagMap = false;
                    PCDRendererFeature.Instance.settings.recordIntegratedDepthMap = false;
                }
            }
            return;
        }

        // カメラやリソース情報の取得
        var cameraData = frameData.Get<UniversalCameraData>();
        var resourceData = frameData.Get<UniversalResourceData>();
        Camera camera = cameraData.camera;
        int screenWidth = cameraData.cameraTargetDescriptor.width;
        int screenHeight = cameraData.cameraTargetDescriptor.height;



        // 選択されたサイズで分割されたグリッドマップの解像度を計算
        float gs = (float)_settings.gridSize;
        if (gs == 0) gs = 16.0f; // フォールバック
        int gridGroupsX = Mathf.CeilToInt(screenWidth / gs);
        int gridHeight = Mathf.CeilToInt(screenHeight / gs);
        int l1_Width = 1, l1_Height = 1, l2_Width = 1, l2_Height = 1, l3_Width = 1, l3_Height = 1, l4_Width = 1, l4_Height = 1, l5_Width = 1, l5_Height = 1, l6_Width = 1, l6_Height = 1;

        bool hasVirtualDepth = resourceData.cameraDepthTexture.IsValid();
        bool hasVirtualObjects = hasDepthMapMeshes;
        if (hasVirtualDepth && _settings.enableVirtualDepthIntegration)
        {
            hasVirtualObjects = hasVirtualObjects || (PCDRendererFeature.Instance.LastFrameVirtualMeshPixelCount > 0);
        }

        bool needsNeighborhoodSize = hasVirtualObjects && 
            (_settings.kernelType != PCDRendererFeature.PCD_OcclusionKernel.Skip || 
             _settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.JointBilateral || 
             _settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.Morphology_OC ||
             _settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.Morphology_CO);

        bool needsDepthPyramid = hasVirtualObjects && 
            (_settings.kernelType != PCDRendererFeature.PCD_OcclusionKernel.Skip || 
             (needsNeighborhoodSize && _settings.enableGradientCorrection));

        bool hasMorphology = _settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.Morphology_OC || 
                             _settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.Morphology_CO;
        
        if (needsDepthPyramid || hasMorphology)
        {
            l1_Width = Mathf.Max(1, Mathf.CeilToInt(screenWidth / 2.0f));
            l1_Height = Mathf.Max(1, Mathf.CeilToInt(screenHeight / 2.0f));
            l2_Width = Mathf.Max(1, Mathf.CeilToInt(l1_Width / 2.0f));
            l2_Height = Mathf.Max(1, Mathf.CeilToInt(l1_Height / 2.0f));
            l3_Width = Mathf.Max(1, Mathf.CeilToInt(l2_Width / 2.0f));
            l3_Height = Mathf.Max(1, Mathf.CeilToInt(l2_Height / 2.0f));
            l4_Width = Mathf.Max(1, Mathf.CeilToInt(l3_Width / 2.0f));
            l4_Height = Mathf.Max(1, Mathf.CeilToInt(l3_Height / 2.0f));
            l5_Width = Mathf.Max(1, Mathf.CeilToInt(l4_Width / 2.0f));
            l5_Height = Mathf.Max(1, Mathf.CeilToInt(l4_Height / 2.0f));
            l6_Width = Mathf.Max(1, Mathf.CeilToInt(l5_Width / 2.0f));
            l6_Height = Mathf.Max(1, Mathf.CeilToInt(l5_Height / 2.0f));
        }

        // デバッグマップのアロケーション（外部ファイル化）
        AllocateInternalHandles(screenWidth, screenHeight);

        TextureHandle finalImageHandle;
        TextureHandle debugDisplayMapHandle_RG = default;
        TextureHandle occlusionValueMapHandle_RG = default;
        TextureHandle neighborhoodMapHandle_RG = default;
        TextureHandle neighborCountMapHandle_RG = default;
        TextureHandle integratedDepthMapHandle_RG = default;

        // コンピュートシェーダーを実行するパスをRenderGraphに追加
        using (var builder = renderGraph.AddUnsafePass<ComputePassData>(PROFILER_TAG, out var data))
        {
            builder.AllowGlobalStateModification(true);
            // パスへ渡すパラメータ（シェーダーや各種データ）を登録
            BindComputePassData(ref data, camera, screenWidth, screenHeight, activeCount, activeBuffer, depthMapOnlyMode, resourceData);

            data.hasVirtualObjects = hasVirtualObjects;

            // 仮想深度（バックグラウンドの深度）を使用する場合、カメラの深度テクスチャを登録
            if (data.hasVirtualDepth || depthMapOnlyMode)

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

            var desc = new TextureDesc(screenWidth, screenHeight) { enableRandomWrite = true };
            desc.colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.ARGBFloat, false);
            data.colorMap = _bufferManager._colorMapHandle;
            data.viewPositionMap = _bufferManager._viewPositionMapHandle;

            // 深度情報はRInt（整数型）として格納
            desc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_UInt;
            if (data.settings.recordIntegratedDepthMap)
            {
                integratedDepthMapHandle_RG = renderGraph.ImportTexture(_bufferManager._integratedDepthMapHandle);
                data.depthMap = _bufferManager._integratedDepthMapHandle;
            }
            else
            {
                data.depthMap = _bufferManager._depthMapHandle;
            }
            desc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SInt;
            if (data.settings.recordNeighborhoodMap)
            {
                neighborhoodMapHandle_RG = renderGraph.ImportTexture(_bufferManager._neighborhoodMapHandle);
                if (data.settings.enableGradientCorrection)
                {
                    data.correctedNeighborhoodSizeMap = neighborhoodMapHandle_RG;
                    data.neighborhoodSizeMap = _bufferManager._neighborhoodSizeMapHandle;
                }
                else
                {
                    data.neighborhoodSizeMap = _bufferManager._neighborhoodMapHandle;
                    data.correctedNeighborhoodSizeMap = _bufferManager._correctedNeighborhoodSizeMapHandle;
                }
            }
            else
            {
                data.neighborhoodSizeMap = _bufferManager._neighborhoodSizeMapHandle;
                data.correctedNeighborhoodSizeMap = _bufferManager._correctedNeighborhoodSizeMapHandle;
            }

            desc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_UInt;
            data.originTypeMap = _bufferManager._originTypeMapHandle;

            // 常にImportしてバインドさせる
            neighborCountMapHandle_RG = renderGraph.ImportTexture(_bufferManager._neighborCountMapHandle);
            data.neighborCountMap = _bufferManager._neighborCountMapHandle;

            if (data.settings.enableDensityBasedLOD)
            {
                // 密度とグリッドレベル用の縮小バッファを生成
                var gridDesc = new TextureDesc(gridGroupsX, gridHeight) { enableRandomWrite = true };
                gridDesc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_UInt;
                data.gridZMinMap = _bufferManager._gridZMinMapHandle;
                gridDesc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SInt;
                data.gridLevelMap = _bufferManager._gridLevelMapHandle;
                data.filteredGridLevelMap = _bufferManager._filteredGridLevelMapHandle;
                gridDesc.colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.RFloat, false);
                data.densityMap = _bufferManager._densityMapHandle;
            }

            if (needsDepthPyramid)
            {
                var descL1 = new TextureDesc(l1_Width, l1_Height) { enableRandomWrite = true, colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat };
                
                var descL2 = new TextureDesc(l2_Width, l2_Height) { enableRandomWrite = true, colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat };
                
                var descL3 = new TextureDesc(l3_Width, l3_Height) { enableRandomWrite = true, colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat };
                
                var descL4 = new TextureDesc(l4_Width, l4_Height) { enableRandomWrite = true, colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat };
                
                var descL5 = new TextureDesc(l5_Width, l5_Height) { enableRandomWrite = true, colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat };
                
                var descL6 = new TextureDesc(l6_Width, l6_Height) { enableRandomWrite = true, colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat };
                
            }

            

            if (data.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.Morphology_OC ||
                data.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.Morphology_CO)
            {
                var morphColorDesc = new TextureDesc(screenWidth, screenHeight)
                {
                    enableRandomWrite = true,
                    colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.ARGBFloat, false)
                };
                data.morphColorTemp = _bufferManager._morphColorTempHandle;

                var morphTypeDesc = new TextureDesc(screenWidth, screenHeight)
                {
                    enableRandomWrite = true,
                    colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_UInt
                };
                data.morphTypeTemp = _bufferManager._morphTypeTempHandle;

                // Morph Pyramids
                var typeDesc = new TextureDesc(1, 1) { enableRandomWrite = true, colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_UInt };
                var colDesc = new TextureDesc(1, 1) { enableRandomWrite = true, colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat };
                
                typeDesc.width = colDesc.width = l1_Width; typeDesc.height = colDesc.height = l1_Height;
                
                

                typeDesc.width = colDesc.width = l2_Width; typeDesc.height = colDesc.height = l2_Height;
                
                

                typeDesc.width = colDesc.width = l3_Width; typeDesc.height = colDesc.height = l3_Height;
                
                

                typeDesc.width = colDesc.width = l4_Width; typeDesc.height = colDesc.height = l4_Height;
                
                

                typeDesc.width = colDesc.width = l5_Width; typeDesc.height = colDesc.height = l5_Height;
                
                

                typeDesc.width = colDesc.width = l6_Width; typeDesc.height = colDesc.height = l6_Height;
                
                
            }

            bool useHoleFilling = data.settings.holeFillingMethod != PCDRendererFeature.PCD_HoleFillingMethod.None;
            bool needsOcclusionResultMap = hasVirtualObjects || useHoleFilling;

            if (needsOcclusionResultMap)
            {
                desc.colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.ARGBFloat, false);
                data.occlusionResultMap = _bufferManager._occlusionResultMapHandle;
            }
            else
            {
                data.occlusionResultMap = data.colorMap;
            }
            
            if (data.settings.recordOcclusionDebugMap || data.settings.recordPixelTagMap)
            {
                occlusionValueMapHandle_RG = renderGraph.ImportTexture(_occlusionValueMapHandle);
                data.occlusionValueMap = _occlusionValueMapHandle;
            }
            else
            {
                desc.colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.RGFloat, false);
                data.occlusionValueMap = _bufferManager._occlusionValueMapHandle;
            }
            
            if (useHoleFilling)
            {
                desc.colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.ARGBFloat, false);
                data.finalImage = _bufferManager._finalImageHandle;
            }

            if (data.settings.enablePixelTagMap || data.settings.enableOcclusionMap)
            {
                debugDisplayMapHandle_RG = renderGraph.ImportTexture(_debugDisplayMapHandle);
                data.debugDisplayMap = _debugDisplayMapHandle;
            }
            else
            {
                desc.colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.ARGBFloat, false);
                data.debugDisplayMap = _bufferManager._debugDisplayMapHandle;
            }

            finalImageHandle = renderGraph.ImportTexture(useHoleFilling ? _bufferManager._finalImageHandle : _bufferManager._occlusionResultMapHandle);

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

            EnqueueDebugReadbackPasses(renderGraph, screenWidth, screenHeight, occlusionValueMapHandle_RG, integratedDepthMapHandle_RG, neighborhoodMapHandle_RG, neighborCountMapHandle_RG);

            // --- 生成された点群（またはデバッグマップ）を最終画面に描画する(Blit)パス ---
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
                data.sourceImage = debugDisplayMapHandle_RG;
                builder.UseTexture(data.sourceImage, AccessFlags.Read);
            }
            else
            {
                data.sourceImage = finalImageHandle;
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