// =============================================================================
// PCD_RenderPass_RenderGraph_Setup.cs
// -----------------------------------------------------------------------------
// Unity の RenderGraph システムへのパス登録のオーケストレーションを行う partial クラス。
// RecordRenderGraph を実装し、バッファのセットアップと Compute/Blit パスの登録を管理する。
// =============================================================================
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

    /// <summary>
    /// RenderGraph用の引数受け渡し構造体
    /// </summary>
    private struct RenderGraphSetupContext
    {
        public int screenWidth;
        public int screenHeight;
        public int activeCount;
        public ComputeBuffer activeBuffer;
        public bool depthMapOnlyMode;
        public bool hasVirtualObjects;
    }

    /// <summary>
    /// パス間でやり取りするTextureHandle
    /// </summary>
    private struct RenderGraphHandles
    {
        public TextureHandle finalImage;
        public TextureHandle debugDisplayMap;
        public TextureHandle occlusionValueMap;
        public TextureHandle neighborhoodMap;
        public TextureHandle neighborCountMap;
        public TextureHandle integratedDepthMap;
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

        if (_settings.enableVirtualContactOcclusion && HCD_Pipeline.Instance != null)
        {
            var trackedClusters = HCD_Pipeline.Instance.GetTrackedClusters();
            int estimatedMaxPoints = 0;
            float radius = _settings.virtualContactRadius;
            float spacing = Mathf.Max(0.001f, _settings.virtualContactSpacing); // Prevent infinite loop

            // Estimate max points (roughly pi * r^2 / spacing^2 plus some margin)
            int pointsPerCluster = (int)(Mathf.PI * radius * radius / (spacing * spacing)) + 100;
            foreach (var c in trackedClusters) if (c.IsAlive) estimatedMaxPoints += pointsPerCluster;

            if (_virtualContactPointsArray == null || _virtualContactPointsArray.Length < estimatedMaxPoints)
                _virtualContactPointsArray = new PCDPointBufferManager.Point[Mathf.Max(1024, estimatedMaxPoints * 2)];

            int idx = 0;
            foreach (var c in trackedClusters)
            {
                if (!c.IsAlive) continue;

                Vector3 centroid = c.Centroid;
                Vector3 normal = c.Normal.normalized;
                if (normal.sqrMagnitude < 0.1f) normal = Vector3.up;
                
                // オクルージョン用の仮想平面を、物体内部の重心から法線方向へ浮かせる（めり込み対策）
                float offset = HCD_Pipeline.Instance.distanceProcessor.surfaceDistanceThreshold;
                centroid += normal * offset;

                // Get tangent and bitangent
                Vector3 tangent = Vector3.Cross(normal, Vector3.up);
                if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.Cross(normal, Vector3.right);
                tangent.Normalize();
                Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

                // Generate points in a circle
                int steps = Mathf.CeilToInt(radius / spacing);
                for (int x = -steps; x <= steps; x++)
                {
                    for (int y = -steps; y <= steps; y++)
                    {
                        float dx = x * spacing;
                        float dy = y * spacing;
                        if (dx * dx + dy * dy <= radius * radius)
                        {
                            if (idx >= _virtualContactPointsArray.Length) break;

                            _virtualContactPointsArray[idx++] = new PCDPointBufferManager.Point
                            {
                                position = centroid + tangent * dx + bitangent * dy,
                                color = new Vector3(_settings.virtualContactColor.r, _settings.virtualContactColor.g, _settings.virtualContactColor.b), // 色の設定を反映
                                originType = 0 // Treat as normal point cloud
                            };
                        }
                    }
                }
            }

            _bufferManager.SetVirtualContactPoints(idx > 0 ? _virtualContactPointsArray : null, idx);
        }
        else
        {
            _bufferManager.SetVirtualContactPoints(null, 0);
        }

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

        bool hasVirtualDepth = resourceData.cameraDepthTexture.IsValid();
        bool hasVirtualObjects = hasDepthMapMeshes;
        if (hasVirtualDepth && _settings.enableVirtualDepthIntegration)
        {
            hasVirtualObjects = hasVirtualObjects || (PCDRendererFeature.Instance.LastFrameVirtualMeshPixelCount > 0);
        }

        // デバッグマップのアロケーション（外部ファイル化）
        AllocateInternalHandles(screenWidth, screenHeight);

        var setupContext = new RenderGraphSetupContext
        {
            screenWidth = screenWidth,
            screenHeight = screenHeight,
            activeCount = activeCount,
            activeBuffer = activeBuffer,
            depthMapOnlyMode = depthMapOnlyMode,
            hasVirtualObjects = hasVirtualObjects
        };

        // 1. コンピュートパスを登録し、生成されたハンドルを受け取る
        var outHandles = EnqueueComputePass(renderGraph, resourceData, camera, setupContext);

        // 2. デバッグ読み戻しパスを登録
        EnqueueDebugReadbackPasses(renderGraph, screenWidth, screenHeight, outHandles.occlusionValueMap, outHandles.integratedDepthMap, outHandles.neighborhoodMap, outHandles.neighborCountMap);

        // 3. 最終画像をカメラに描画する Blit パスを登録
        EnqueueBlitPass(renderGraph, resourceData, outHandles);
    }
}
