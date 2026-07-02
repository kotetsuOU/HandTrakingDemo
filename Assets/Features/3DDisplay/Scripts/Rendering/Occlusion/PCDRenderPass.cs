// =============================================================================
// PCDRenderPass.cs — 点群オクルージョンパイプラインのオーケストレーター
// =============================================================================
//
// 【アーキテクチャ概要】
//
// このクラスは Unity の ScriptableRenderPass を継承し、点群（PointCloud）と仮想
// オブジェクトの間でリアルタイムオクルージョン判定を行うレンダリングパスです。
//
// 全てのロジックは独立したコンポーネントクラスに委譲されており、
// このクラスはオーケストレーション（RenderGraph への登録と制御フロー）のみを担当します。
//
// コンポーネント構成:
//   - PCDShaderConstants       … シェーダープロパティIDの定数（static）
//   - PCDKernelRegistry        … カーネルIDの初期化と保持
//   - PCDResourcePool          … RTHandle の一元管理（Alloc/Release）
//   - PCDPointBufferManager    … GPU バッファ管理（ComputeBuffer）
//   - PCDPipelineContext       … ステージ間データ共有（Blackboard）
//   - IPCDPipelineStage        … 各処理ステージのインターフェース
//   - PCDDebugReadbackManager  … デバッグ用 AsyncReadback パス
//
// パイプライン処理フロー:
//   1. RecordRenderGraph()     — RenderGraph にコンピュートパスと Blit パスを登録
//   2. ExecuteComputePass()    — GPU 上でオクルージョンパイプラインを実行
//   3. ExecuteBlitPass()       — 結果画像をカメラターゲットに転送
//
// =============================================================================
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

public class PCDRenderPass : ScriptableRenderPass
{
    private const string PROFILER_TAG = "PCDRendering";

    // =========================================================================
    // コンピュートシェーダーと設定
    // =========================================================================
    private readonly ComputeShader _computeShader;
    private PCDRendererFeature.PCDRenderSettings _settings;

    // =========================================================================
    // コンポーネント
    // =========================================================================
    private readonly PCDKernelRegistry _kernels = new PCDKernelRegistry();
    private readonly PCDResourcePool _resources = new PCDResourcePool();
    private readonly PCDPointBufferManager _bufferManager = new PCDPointBufferManager();
    private readonly PCDDebugReadbackManager _debugManager = new PCDDebugReadbackManager();

    // =========================================================================
    // パイプラインステージ
    // =========================================================================
    private readonly IPCDPipelineStage[] _stages;

    // =========================================================================
    // 出力およびデバッグ用 RTHandle
    // =========================================================================
    private RTHandle _directGpuImageMapHandle;
    private RTHandle _directGpuImageLeftHandle;
    private RTHandle _directGpuImageRightHandle;

    // =========================================================================
    // 状態管理
    // =========================================================================
    private ComputeBuffer _staticMeshCounterBuffer;
    private PCDPointBufferManager.Point[] _virtualContactPointsArray;

    // =========================================================================
    // SRD Manager キャッシュ
    // =========================================================================
    private SRD.Core.SRDManager _cachedSrdManager;
    private float _lastSrdManagerSearchTime = -1000f;

    private SRD.Core.SRDManager GetSRDManager()
    {
        if (_cachedSrdManager != null)
            return _cachedSrdManager;

        if (Time.realtimeSinceStartup - _lastSrdManagerSearchTime > 2.0f)
        {
            _cachedSrdManager = UnityEngine.Object.FindAnyObjectByType<SRD.Core.SRDManager>();
            _lastSrdManagerSearchTime = Time.realtimeSinceStartup;
        }

        return _cachedSrdManager;
    }

    // =========================================================================
    // Blit パスデータ
    // =========================================================================
    private class BlitPassData
    {
        internal TextureHandle sourceImage;
        internal TextureHandle cameraTarget;
        internal bool enablePixelTagMap;
        internal bool enableOcclusionMap;
        internal bool useDirectGpuImageBuffer;
        internal RTHandle directGpuImageMap;
    }

    // =========================================================================
    // RenderGraph ハンドル
    // =========================================================================
    private struct RenderGraphHandles
    {
        public TextureHandle finalImage;
        public TextureHandle debugDisplayMap;
        public TextureHandle occlusionValueMap;
        public TextureHandle neighborhoodMap;
        public TextureHandle neighborCountMap;
        public TextureHandle integratedDepthMap;
    }

    // =========================================================================
    // コンストラクタ
    // =========================================================================

    public PCDRenderPass(ComputeShader computeShader, PCDRendererFeature.PCDRenderSettings settings)
    {
        _computeShader = computeShader;
        _settings = settings;

        _staticMeshCounterBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Default);
        _staticMeshCounterBuffer.SetData(new uint[] { 0 });

        _stages = new IPCDPipelineStage[]
        {
            new PCDPreProcessStage(),
            new PCDDepthPyramidStage(),
            new PCDOcclusionStage(),
            new PCDHoleFillStage(),
            new PCDPostProcessStage(),
        };
    }

    // =========================================================================
    // パブリック API
    // =========================================================================

    public void UpdateSettings(PCDRendererFeature.PCDRenderSettings settings) => _settings = settings;

    public void SetDebugFlags(bool enablePixelTagMap, bool enableOcclusionMap)
    {
        _settings.enablePixelTagMap = enablePixelTagMap;
        _settings.enableOcclusionMap = enableOcclusionMap;
    }

    public void SetExternalBuffer(ComputeBuffer buffer, int count) => _bufferManager.SetExternalBuffer(buffer, count);
    public void SetPointCloudData(PCV_Data data) => _bufferManager.SetPointCloudData(data);
    public void AddStaticMesh(Mesh mesh, Transform transform, PCDProcessingMode mode) => _bufferManager.AddStaticMesh(mesh, transform, mode);
    public void RemoveStaticMesh(Mesh mesh, Transform transform) => _bufferManager.RemoveStaticMesh(mesh, transform);
    public void MarkPointCloudDataDirty() => _bufferManager.SetDataDirty();

    public Texture GetDebugDisplayMap()
    {
        if ((_settings.enablePixelTagMap || _settings.enableOcclusionMap) && _resources.DebugDisplayMap != null)
            return _resources.DebugDisplayMap;
        return null;
    }

    public bool ShouldSkipRendering()
    {
        bool hasExternalData = _bufferManager.UseExternalBuffer && _bufferManager.ExternalPointBuffer != null && _bufferManager.ExternalPointBuffer.IsValid() && _bufferManager.ExternalPointCount > 0;
        bool hasInternalData = _bufferManager.PointBuffer != null && _bufferManager.PointBuffer.IsValid() && _bufferManager.PointCount > 0;
        bool hasDepthMapMeshes = _bufferManager.HasDepthMapMeshes();
        bool hasPointCloudMeshes = _bufferManager.HasPointCloudMeshes();
        bool noPointCloudData = !hasExternalData && !hasInternalData && !hasPointCloudMeshes;
        bool depthMapOnlyMode = hasDepthMapMeshes && noPointCloudData;
        return depthMapOnlyMode;
    }

    // =========================================================================
    // RenderGraph 登録
    // =========================================================================

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
        // カーネルの初期化
        if (!_kernels.IsInitialized) _kernels.Initialize(_computeShader);
        if (!_kernels.IsInitialized) return;

        if (!Application.isPlaying) return;

        // =========================================================================
        // 仮想接触ポイントの生成（HCD_Pipeline からの接触情報をもとに）
        // =========================================================================
        bool shouldUseExternal = PCDRendererFeature.Instance.IsGlobalBufferMode;

        if (_settings.enableVirtualContactOcclusion && HCD_Pipeline.Instance != null)
        {
            var trackedClusters = HCD_Pipeline.Instance.GetTrackedClusters();
            int estimatedMaxPoints = 0;
            float radius = _settings.virtualContactRadius;
            float spacing = Mathf.Max(0.001f, _settings.virtualContactSpacing);

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

                float offset = HCD_Pipeline.Instance.distanceProcessor.surfaceDistanceThreshold;
                centroid += normal * offset;

                Vector3 tangent = Vector3.Cross(normal, Vector3.up);
                if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.Cross(normal, Vector3.right);
                tangent.Normalize();
                Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

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
                                color = new Vector3(_settings.virtualContactColor.r, _settings.virtualContactColor.g, _settings.virtualContactColor.b),
                                originType = 0
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

        // 外部バッファの設定
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

        _bufferManager.Update();

        // =========================================================================
        // アクティブバッファの決定
        // =========================================================================
        ComputeBuffer activeBuffer = null;
        int activeCount = 0;

        if (_bufferManager.UseExternalBuffer && _bufferManager.ExternalPointBuffer != null)
        {
            int extCount = _bufferManager.ExternalPointCount >= 0 ? _bufferManager.ExternalPointCount : _bufferManager.ExternalPointBuffer.count;
            if (_bufferManager.PointCount > 0)
            {
                int totalCount = extCount + _bufferManager.PointCount;
                _bufferManager.EnsureCombinedBuffer(totalCount);
                activeBuffer = _bufferManager.CombinedBuffer;
                activeCount = totalCount;
            }
            else
            {
                activeBuffer = _bufferManager.ExternalPointBuffer;
                activeCount = extCount;
            }
        }
        else
        {
            activeBuffer = _bufferManager.PointBuffer;
            activeCount = _bufferManager.PointCount;
        }

        // =========================================================================
        // スキップ判定
        // =========================================================================
        bool hasDepthMapMeshes = _bufferManager.HasDepthMapMeshes();
        bool hasPointCloudMeshes = _bufferManager.HasPointCloudMeshes();
        bool pointCloudHasData = activeBuffer != null && activeCount > 0 && activeBuffer.IsValid();

        if (_settings.recordOcclusionDebugMap || _settings.recordPixelTagMap || _settings.recordIntegratedDepthMap)
        {
            Debug.Log($"[PCDRenderPass] Record Debug. Occlusion={_settings.recordOcclusionDebugMap} PixelTag={_settings.recordPixelTagMap} IntegratedDepth={_settings.recordIntegratedDepthMap} DepthMap={hasDepthMapMeshes} PCMeshes={hasPointCloudMeshes} PointCloudData={pointCloudHasData} (Buffer={activeBuffer!=null}, Count={activeCount})");
        }

        bool depthMapOnlyMode = hasDepthMapMeshes && !hasPointCloudMeshes && !pointCloudHasData;
        if (depthMapOnlyMode)
        {
            if (_settings.recordOcclusionDebugMap || _settings.recordPixelTagMap || _settings.recordIntegratedDepthMap)
            {
                Debug.LogWarning("[PCDRenderPass] Skipped rendering because depthMapOnlyMode is true.");
                if (PCDRendererFeature.Instance != null && PCDRendererFeature.Instance.settings != null)
                {
                    PCDRendererFeature.Instance.settings.recordOcclusionDebugMap = false;
                    PCDRendererFeature.Instance.settings.recordPixelTagMap = false;
                    PCDRendererFeature.Instance.settings.recordIntegratedDepthMap = false;
                }
            }
            return;
        }

        if (!pointCloudHasData && !hasDepthMapMeshes)
        {
            if (_settings.recordOcclusionDebugMap || _settings.recordPixelTagMap || _settings.recordIntegratedDepthMap)
            {
                Debug.LogWarning("[PCDRenderPass] Check box pressed but ignored. No point cloud and no depth map data.");
                if (PCDRendererFeature.Instance != null && PCDRendererFeature.Instance.settings != null)
                {
                    PCDRendererFeature.Instance.settings.recordOcclusionDebugMap = false;
                    PCDRendererFeature.Instance.settings.recordPixelTagMap = false;
                    PCDRendererFeature.Instance.settings.recordIntegratedDepthMap = false;
                }
            }
            return;
        }

        // =========================================================================
        // カメラ・リソース情報の取得
        // =========================================================================
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

        // =========================================================================
        // リソースのアロケーション
        // =========================================================================
        _resources.EnsureAllocated(screenWidth, screenHeight, _settings.gridSize);

        // =========================================================================
        // 1. コンピュートパスの登録
        // =========================================================================
        var outHandles = EnqueueComputePass(
            renderGraph, resourceData, camera,
            screenWidth, screenHeight, activeCount, activeBuffer,
            depthMapOnlyMode, hasVirtualObjects, hasVirtualDepth);

        // =========================================================================
        // 2. デバッグ読み戻しパスの登録
        // =========================================================================
        _debugManager.EnqueueReadbackPasses(
            renderGraph, _settings, _resources,
            screenWidth, screenHeight,
            outHandles.occlusionValueMap,
            outHandles.integratedDepthMap,
            outHandles.neighborhoodMap,
            outHandles.neighborCountMap,
            GetMethodPrefix());

        // =========================================================================
        // 3. 最終画像をカメラに描画する Blit パスの登録
        // =========================================================================
        EnqueueBlitPass(renderGraph, resourceData, outHandles);
    }

    // =========================================================================
    // Compute パスの登録
    // =========================================================================

    private RenderGraphHandles EnqueueComputePass(
        RenderGraph renderGraph,
        UniversalResourceData resourceData,
        Camera camera,
        int screenWidth, int screenHeight,
        int activeCount, ComputeBuffer activeBuffer,
        bool depthMapOnlyMode, bool hasVirtualObjects, bool hasVirtualDepth)
    {
        var outHandles = new RenderGraphHandles();

        using (var builder = renderGraph.AddUnsafePass<PCDPipelineContext>(PROFILER_TAG, out var ctx))
        {
            builder.AllowGlobalStateModification(true);

            // --- コンテキストの構築 ---
            ctx.ComputeShader = _computeShader;
            ctx.Settings = _settings;
            ctx.Kernels = _kernels;
            ctx.Resources = _resources;
            ctx.ScreenWidth = screenWidth;
            ctx.ScreenHeight = screenHeight;
            ctx.ScreenParams = new Vector4(screenWidth, screenHeight, 0, 0);
            Matrix4x4 vMatrix = camera.worldToCameraMatrix;
            var adjuster = camera.GetComponent<CameraAdjuster>();
            if (adjuster != null && adjuster.isHalfMirrorEnabled)
            {
                if (adjuster.displayTransform != null)
                {
                    // 鏡面世界（ハーフミラー）用に、Display中心で点群をローカルX軸方向に反転させる
                    Vector3 center = adjuster.displayTransform.position;
                    Quaternion rotation = adjuster.displayTransform.rotation;
                    Matrix4x4 displayTRS = Matrix4x4.TRS(center, rotation, Vector3.one);
                    Matrix4x4 flipX = Matrix4x4.Scale(new Vector3(-1, 1, 1));
                    Matrix4x4 displayInverse = displayTRS.inverse;
                    
                    vMatrix = vMatrix * displayTRS * flipX * displayInverse;
                }
                else
                {
                    // displayTransformが未設定の場合は、ワールド原点中心にX反転
                    vMatrix = vMatrix * Matrix4x4.Scale(new Vector3(-1, 1, 1));
                }
            }
            
            ctx.ViewMatrix = vMatrix;
            ctx.ProjectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
            ctx.InverseProjectionMatrix = camera.projectionMatrix.inverse;

            int gs = (int)_settings.gridSize;
            if (gs == 0) gs = 16;
            ctx.ThreadGroupsX = (screenWidth + 7) / 8;
            ctx.ThreadGroupsY = (screenHeight + 7) / 8;
            ctx.GridGroupsX = (screenWidth + gs - 1) / gs;
            ctx.GridGroupsY = (screenHeight + gs - 1) / gs;

            ctx.HasVirtualDepth = hasVirtualDepth;
            ctx.HasVirtualObjects = hasVirtualObjects;
            ctx.DepthMapOnlyMode = depthMapOnlyMode;
            ctx.StaticMeshCounterBuffer = _staticMeshCounterBuffer;

            // --- バッファの設定 ---
            bool useExternal = _bufferManager.UseExternalBuffer && _bufferManager.ExternalPointBuffer != null;
            ctx.UseExternal = useExternal;
            if (useExternal)
            {
                ctx.ExternalBuffer = _bufferManager.ExternalPointBuffer;
                ctx.ExternalCount = _bufferManager.ExternalPointCount >= 0 ? _bufferManager.ExternalPointCount : _bufferManager.ExternalPointBuffer.count;
                ctx.InternalBuffer = _bufferManager.PointBuffer;
                ctx.InternalCount = _bufferManager.PointCount;
                ctx.CombinedBuffer = _bufferManager.CombinedBuffer;
            }
            ctx.PointBuffer = activeBuffer;
            ctx.PointCount = activeCount;

            // --- 仮想深度テクスチャ ---
            if (hasVirtualDepth || depthMapOnlyMode)
            {
                ctx.VirtualDepthTexture = resourceData.cameraDepthTexture;
            }
            else
            {
                var virtualDepthFallbackDesc = new TextureDesc(1, 1)
                {
                    colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.RFloat, false)
                };
                ctx.VirtualDepthTexture = renderGraph.CreateTexture(virtualDepthFallbackDesc);
            }
            builder.UseTexture(ctx.VirtualDepthTexture, AccessFlags.Read);

            // --- カメラカラーテクスチャ ---
            if (hasVirtualDepth && resourceData.activeColorTexture.IsValid())
            {
                ctx.CameraColorTexture = resourceData.activeColorTexture;
            }
            else
            {
                var cameraColorFallbackDesc = new TextureDesc(1, 1)
                {
                    colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.ARGBFloat, false)
                };
                ctx.CameraColorTexture = renderGraph.CreateTexture(cameraColorFallbackDesc);
            }
            builder.UseTexture(ctx.CameraColorTexture, AccessFlags.Read);

            // --- RenderGraph テクスチャインポート ---
            if (_settings.recordIntegratedDepthMap)
            {
                outHandles.integratedDepthMap = renderGraph.ImportTexture(_resources.IntegratedDepthMap);
            }

            if (_settings.recordNeighborhoodMap)
            {
                outHandles.neighborhoodMap = renderGraph.ImportTexture(_resources.NeighborhoodMap);
            }

            outHandles.neighborCountMap = renderGraph.ImportTexture(_resources.NeighborCountMap);

            if (_settings.recordOcclusionDebugMap || _settings.recordPixelTagMap)
            {
                outHandles.occlusionValueMap = renderGraph.ImportTexture(_resources.OcclusionValueMap);
            }

            if (_settings.enablePixelTagMap || _settings.enableOcclusionMap)
            {
                outHandles.debugDisplayMap = renderGraph.ImportTexture(_resources.DebugDisplayMap);
            }

            bool useHoleFilling = _settings.holeFillingMethod != PCDRendererFeature.PCD_HoleFillingMethod.None;
            outHandles.finalImage = renderGraph.ImportTexture(useHoleFilling ? _resources.FinalImage : _resources.OcclusionResultMap);

            // --- ステージの参照キャプチャ ---
            var stages = _stages;

            // --- Compute 実行関数の登録 ---
            builder.SetRenderFunc((PCDPipelineContext passCtx, UnsafeGraphContext graphCtx) =>
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(graphCtx.cmd);
                foreach (var stage in stages)
                {
                    if (stage.ShouldExecute(passCtx))
                        stage.Execute(cmd, passCtx);
                }
            });

            // デバッグデータを非同期読込する場合はカリングを無効化
            if (_settings.recordOcclusionDebugMap || _settings.recordPixelTagMap || _settings.recordIntegratedDepthMap || _settings.recordNeighborhoodMap || _settings.recordNeighborCountMap)
            {
                builder.AllowPassCulling(false);
            }
        }

        return outHandles;
    }

    // =========================================================================
    // Blit パスの登録
    // =========================================================================

    private void EnqueueBlitPass(RenderGraph renderGraph, UniversalResourceData resourceData, RenderGraphHandles handles)
    {
        using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>("PCD Blit Pass", out var data))
        {
            data.cameraTarget = resourceData.activeColorTexture;
            data.directGpuImageMap = null;
            data.enablePixelTagMap = _settings.enablePixelTagMap;
            data.enableOcclusionMap = _settings.enableOcclusionMap;
            data.useDirectGpuImageBuffer = false;

            if (data.enablePixelTagMap || data.enableOcclusionMap)
            {
                data.sourceImage = handles.debugDisplayMap;
                builder.UseTexture(data.sourceImage, AccessFlags.Read);
            }
            else
            {
                data.sourceImage = handles.finalImage;
                builder.UseTexture(data.sourceImage, AccessFlags.Read);
            }

            builder.SetRenderAttachment(data.cameraTarget, 0, AccessFlags.ReadWrite);
            builder.SetRenderFunc((BlitPassData passData, RasterGraphContext context) =>
            {
                Blitter.BlitTexture(context.cmd, passData.sourceImage, new Vector2(1, 1), 0.0f, false);
            });
        }
    }

    // =========================================================================
    // リソース解放
    // =========================================================================

    public void Cleanup()
    {
        _resources.Dispose();
        _bufferManager.Cleanup();

        _directGpuImageMapHandle?.Release();
        _directGpuImageMapHandle = null;
        _directGpuImageLeftHandle?.Release();
        _directGpuImageLeftHandle = null;
        _directGpuImageRightHandle?.Release();
        _directGpuImageRightHandle = null;
        _staticMeshCounterBuffer?.Release();
        _staticMeshCounterBuffer = null;
    }
}
