using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

public partial class PCDRenderPass : ScriptableRenderPass
{
    private const string PROFILER_TAG = "PCDRendering";

    // フレームごとの文字列ルックアップを避けるためにシェーダープロパティIDをキャッシュ
    private static class ShaderIDs
    {
        public static readonly int PointCount = Shader.PropertyToID("_PointCount");
        public static readonly int ScreenParams = Shader.PropertyToID("_ScreenParams");
        public static readonly int ViewMatrix = Shader.PropertyToID("_ViewMatrix");
        public static readonly int ProjectionMatrix = Shader.PropertyToID("_ProjectionMatrix");
        public static readonly int InverseProjectionMatrix = Shader.PropertyToID("_InverseProjectionMatrix");
        public static readonly int DensityThreshold_e = Shader.PropertyToID("_DensityThreshold_e");
        public static readonly int NeighborhoodParam_p_prime = Shader.PropertyToID("_NeighborhoodParam_p_prime");
        public static readonly int GradientThreshold_g_th = Shader.PropertyToID("_GradientThreshold_g_th");
        public static readonly int KernelType = Shader.PropertyToID("_KernelType");
        public static readonly int EvaluationMode = Shader.PropertyToID("_EvaluationMode");
        public static readonly int MinOccludedSectors = Shader.PropertyToID("_MinOccludedSectors");
        public static readonly int MinSearchLevel = Shader.PropertyToID("_MinSearchLevel");
        public static readonly int Alpha = Shader.PropertyToID("_Alpha");
        public static readonly int OcclusionThreshold = Shader.PropertyToID("_OcclusionThreshold");
        public static readonly int OcclusionFadeWidth = Shader.PropertyToID("_OcclusionFadeWidth");

        public static readonly int ColorMap = Shader.PropertyToID("_ColorMap");
        public static readonly int DepthMap = Shader.PropertyToID("_DepthMap");
        public static readonly int ColorMap_RW = Shader.PropertyToID("_ColorMap_RW");
        public static readonly int DepthMap_RW = Shader.PropertyToID("_DepthMap_RW");
        public static readonly int ViewPositionMap = Shader.PropertyToID("_ViewPositionMap");
        public static readonly int ViewPositionMap_RW = Shader.PropertyToID("_ViewPositionMap_RW");
        public static readonly int GridZMinMap = Shader.PropertyToID("_GridZMinMap");
        public static readonly int GridZMinMap_RW = Shader.PropertyToID("_GridZMinMap_RW");
        public static readonly int DensityMap = Shader.PropertyToID("_DensityMap");
        public static readonly int DensityMap_RW = Shader.PropertyToID("_DensityMap_RW");
        public static readonly int GridLevelMap = Shader.PropertyToID("_GridLevelMap");
        public static readonly int GridLevelMap_RW = Shader.PropertyToID("_GridLevelMap_RW");
        public static readonly int FilteredGridLevelMap = Shader.PropertyToID("_FilteredGridLevelMap");
        public static readonly int FilteredGridLevelMap_RW = Shader.PropertyToID("_FilteredGridLevelMap_RW");
        public static readonly int NeighborhoodSizeMap = Shader.PropertyToID("_NeighborhoodSizeMap");
        public static readonly int NeighborhoodSizeMap_RW = Shader.PropertyToID("_NeighborhoodSizeMap_RW");
        
        public static readonly int DepthPyramidL1 = Shader.PropertyToID("_DepthPyramidL1");
        public static readonly int DepthPyramidL1_RW = Shader.PropertyToID("_DepthPyramidL1_RW");
        public static readonly int DepthPyramidL2 = Shader.PropertyToID("_DepthPyramidL2");
        public static readonly int DepthPyramidL2_RW = Shader.PropertyToID("_DepthPyramidL2_RW");
        public static readonly int DepthPyramidL3 = Shader.PropertyToID("_DepthPyramidL3");
        public static readonly int DepthPyramidL3_RW = Shader.PropertyToID("_DepthPyramidL3_RW");
        public static readonly int DepthPyramidL4 = Shader.PropertyToID("_DepthPyramidL4");
        public static readonly int DepthPyramidL4_RW = Shader.PropertyToID("_DepthPyramidL4_RW");
        public static readonly int DepthPyramidL5 = Shader.PropertyToID("_DepthPyramidL5");
        public static readonly int DepthPyramidL5_RW = Shader.PropertyToID("_DepthPyramidL5_RW");
        public static readonly int DepthPyramidL6 = Shader.PropertyToID("_DepthPyramidL6");
        public static readonly int DepthPyramidL6_RW = Shader.PropertyToID("_DepthPyramidL6_RW");
        public static readonly int CorrectedNeighborhoodSizeMap_RW = Shader.PropertyToID("_CorrectedNeighborhoodSizeMap_RW");
        public static readonly int FinalNeighborhoodSizeMap = Shader.PropertyToID("_FinalNeighborhoodSizeMap");
        
        public static readonly int MorphTypePyramidL1 = Shader.PropertyToID("_MorphTypePyramidL1");
        public static readonly int MorphTypePyramidL1_RW = Shader.PropertyToID("_MorphTypePyramidL1_RW");
        public static readonly int MorphTypePyramidL2 = Shader.PropertyToID("_MorphTypePyramidL2");
        public static readonly int MorphTypePyramidL2_RW = Shader.PropertyToID("_MorphTypePyramidL2_RW");
        public static readonly int MorphTypePyramidL3 = Shader.PropertyToID("_MorphTypePyramidL3");
        public static readonly int MorphTypePyramidL3_RW = Shader.PropertyToID("_MorphTypePyramidL3_RW");
        public static readonly int MorphTypePyramidL4 = Shader.PropertyToID("_MorphTypePyramidL4");
        public static readonly int MorphTypePyramidL4_RW = Shader.PropertyToID("_MorphTypePyramidL4_RW");
        public static readonly int MorphTypePyramidL5 = Shader.PropertyToID("_MorphTypePyramidL5");
        public static readonly int MorphTypePyramidL5_RW = Shader.PropertyToID("_MorphTypePyramidL5_RW");
        public static readonly int MorphTypePyramidL6 = Shader.PropertyToID("_MorphTypePyramidL6");
        public static readonly int MorphTypePyramidL6_RW = Shader.PropertyToID("_MorphTypePyramidL6_RW");

        public static readonly int MorphColorPyramidL1 = Shader.PropertyToID("_MorphColorPyramidL1");
        public static readonly int MorphColorPyramidL1_RW = Shader.PropertyToID("_MorphColorPyramidL1_RW");
        public static readonly int MorphColorPyramidL2 = Shader.PropertyToID("_MorphColorPyramidL2");
        public static readonly int MorphColorPyramidL2_RW = Shader.PropertyToID("_MorphColorPyramidL2_RW");
        public static readonly int MorphColorPyramidL3 = Shader.PropertyToID("_MorphColorPyramidL3");
        public static readonly int MorphColorPyramidL3_RW = Shader.PropertyToID("_MorphColorPyramidL3_RW");
        public static readonly int MorphColorPyramidL4 = Shader.PropertyToID("_MorphColorPyramidL4");
        public static readonly int MorphColorPyramidL4_RW = Shader.PropertyToID("_MorphColorPyramidL4_RW");
        public static readonly int MorphColorPyramidL5 = Shader.PropertyToID("_MorphColorPyramidL5");
        public static readonly int MorphColorPyramidL5_RW = Shader.PropertyToID("_MorphColorPyramidL5_RW");
        public static readonly int MorphColorPyramidL6 = Shader.PropertyToID("_MorphColorPyramidL6");
        public static readonly int MorphColorPyramidL6_RW = Shader.PropertyToID("_MorphColorPyramidL6_RW");

        public static readonly int OcclusionResultMap = Shader.PropertyToID("_OcclusionResultMap");
        public static readonly int OcclusionResultMap_RW = Shader.PropertyToID("_OcclusionResultMap_RW");
        public static readonly int FinalImage_RW = Shader.PropertyToID("_FinalImage_RW");

        // Pull-Push
        public static readonly int PullPushLevel_In = Shader.PropertyToID("_PullPushLevel_In");
        public static readonly int PullPushLevel_Out = Shader.PropertyToID("_PullPushLevel_Out");
        public static readonly int PullPushLevel_In_RW = Shader.PropertyToID("_PullPushLevel_In_RW");
        public static readonly int PullPushLevel_Out_RW = Shader.PropertyToID("_PullPushLevel_Out_RW");
        public static readonly int PullPushIsBaseLevel = Shader.PropertyToID("_PullPushIsBaseLevel");
        public static readonly int PullPushMaxLevel = Shader.PropertyToID("_PullPushMaxLevel");
        public static readonly int PullPushCurrentLevel = Shader.PropertyToID("_PullPushCurrentLevel");
        
        public static readonly int OriginTypeMap = Shader.PropertyToID("_OriginTypeMap");
        public static readonly int OriginTypeMap_RW = Shader.PropertyToID("_OriginTypeMap_RW");
        public static readonly int OriginMap_RW = Shader.PropertyToID("_OriginMap_RW");
        public static readonly int NeighborCountMap_RW = Shader.PropertyToID("_NeighborCountMap_RW");

        public static readonly int DebugDisplayMode = Shader.PropertyToID("_DebugDisplayMode");

        public static readonly int OcclusionValueMap_RW = Shader.PropertyToID("_OcclusionValueMap_RW");
        public static readonly int RecordOcclusionDebug = Shader.PropertyToID("_RecordOcclusionDebug");

        public static readonly int MergeSrcBuffer = Shader.PropertyToID("_MergeSrcBuffer");
        public static readonly int MergeDstBuffer = Shader.PropertyToID("_MergeDstBuffer");
        public static readonly int MergeSrcOffset = Shader.PropertyToID("_MergeSrcOffset");
        public static readonly int MergeDstOffset = Shader.PropertyToID("_MergeDstOffset");
        public static readonly int MergeCopyCount = Shader.PropertyToID("_MergeCopyCount");
        public static readonly int PointBuffer = Shader.PropertyToID("_PointBuffer");
        public static readonly int StaticMeshCounter_RW = Shader.PropertyToID("_StaticMeshCounter_RW");

        public static readonly int UseVirtualDepth = Shader.PropertyToID("_UseVirtualDepth");
        public static readonly int VirtualDepthMap = Shader.PropertyToID("_VirtualDepthMap");
        public static readonly int CameraColorTexture = Shader.PropertyToID("_CameraColorTexture");

        // Morphology
        public static readonly int MorphColorIn = Shader.PropertyToID("_MorphColorIn");
        public static readonly int MorphColorOut_RW = Shader.PropertyToID("_MorphColorOut_RW");
        public static readonly int MorphTypeIn = Shader.PropertyToID("_MorphTypeIn");
        public static readonly int MorphTypeOut_RW = Shader.PropertyToID("_MorphTypeOut_RW");
        public static readonly int MorphKernelHalfSize = Shader.PropertyToID("_MorphKernelHalfSize");
    }

    private ComputeShader pointCloudCompute; // オクルージョンパイプラインを定義するコアコンピュートシェーダー
    private PCDRendererFeature.PCDRenderSettings _settings; // 機能インスペクターの値に対応する現在の設定

    // 個々のコンピュートシェーダー関数に対応するカーネルID
    private int _kernelClear, _kernelClearCounter, _kernelProject, _kernelCalcGridZMin, _kernelCalcDensity,
                _kernelCalcGridLevel, _kernelGridMedianFilter,
                _kernelCalcNeighborhoodSize, _kernelFillNeighborhoodSizeWithMinLevel,
                _kernelBuildDepthPyramidL1, _kernelBuildDepthPyramidL2,
                _kernelBuildDepthPyramidL3, _kernelBuildDepthPyramidL4,
                _kernelBuildDepthPyramidL5, _kernelBuildDepthPyramidL6,
                _kernelApplyGradient,
                _kernelComputeOcclusion, _kernelCopyColorToOcclusion, _kernelFillHoles, _kernelFillHolesPullPushInit, _kernelFillHolesPull, _kernelFillHolesPush, _kernelFillHolesPullPushFinalize, _kernelInterpolate,
                _kernelMerge, _kernelInitFromCamera, _kernelVisualizeOcclusionDebug,
                _kernelMorphologyErode, _kernelMorphologyDilate, _kernelMorphologyCopy,
                _kernelBuildMorphPyramidL1, _kernelBuildMorphPyramidL2, _kernelBuildMorphPyramidL3,
                _kernelBuildMorphPyramidL4, _kernelBuildMorphPyramidL5, _kernelBuildMorphPyramidL6;

    // 出力およびデバッグマップ
    private RTHandle _debugDisplayMapHandle;
    private RTHandle _occlusionValueMapHandle;
    private RTHandle _integratedDepthMapHandle;
    private RTHandle _neighborhoodMapHandle;
    private RTHandle _neighborCountMapHandle;
    private RTHandle _directGpuImageMapHandle;
    private RTHandle _directGpuImageLeftHandle;
    private RTHandle _directGpuImageRightHandle;
    private bool _isInitialized = false;
    private const int STRIDE = 28; // 1つのポイントデータのサイズを表す: sizeof(float)*3 + sizeof(float)*3 + sizeof(uint)

    // --- バッファ マネージャー ---
    private PCDPointBufferManager _bufferManager;

    private ComputeBuffer _staticMeshCounterBuffer;

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

    public PCDRenderPass(ComputeShader computeShader, PCDRendererFeature.PCDRenderSettings settings)
    {
        this.pointCloudCompute = computeShader;
        this._settings = settings;

        _bufferManager = new PCDPointBufferManager(); // 静的メッシュや点群のためのデータマネージャーを初期化します

        _staticMeshCounterBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Default);
        _staticMeshCounterBuffer.SetData(new uint[] { 0 });
    }

    /// <summary> 外部（スクリプトやインスペクターの変更など）からレンダラーの設定を更新します。 </summary>
    public void UpdateSettings(PCDRendererFeature.PCDRenderSettings settings)
    {
        this._settings = settings;
    }

    /// <summary> オリジンデバッグマップなどのレンダリングを切り替えます。 </summary>
    public void SetDebugFlags(bool enablePixelTagMap, bool enableOcclusionMap)
    {
        this._settings.enablePixelTagMap = enablePixelTagMap;
        this._settings.enableOcclusionMap = enableOcclusionMap;
    }

    /// <summary> 外部のコンピュートバッファを直接注入できるようにします。 </summary>
    public void SetExternalBuffer(ComputeBuffer buffer, int count)
    {
        _bufferManager.SetExternalBuffer(buffer, count);
    }

    /// <summary> 内部のPCV_Dataオブジェクトから点群データを設定します。 </summary>
    public void SetPointCloudData(PCV_Data data)
    {
        _bufferManager.SetPointCloudData(data);
    }

    /// <summary> 点群のオクルージョンと相互作用するように静的なUnityメッシュを登録します。 </summary>
    public void AddStaticMesh(Mesh mesh, Transform transform, PCDProcessingMode mode)
    {
        _bufferManager.AddStaticMesh(mesh, transform, mode);
    }

    /// <summary> バッファの更新を強制するために、点群データをダーティとしてマークします。 </summary>
    public void MarkPointCloudDataDirty()
    {
        _bufferManager.SetDataDirty();
    }

    /// <summary> トラックされている静的なUnityメッシュの登録を解除します。 </summary>
    public void RemoveStaticMesh(Mesh mesh, Transform transform)
    {
        _bufferManager.RemoveStaticMesh(mesh, transform);
    }

    /// <summary> コンピュートシェーダーの設定からカーネルのインデックスIDを取得します。 </summary>
    private void Initialize()
    {
        if (pointCloudCompute == null)
        {
            UnityEngine.Debug.LogError("Compute Shader is null. Initialization failed.");
            _isInitialized = false;
            return;
        }

        _kernelClear = pointCloudCompute.FindKernel("ClearMaps");
        _kernelClearCounter = pointCloudCompute.FindKernel("ClearCounter");
        _kernelProject = pointCloudCompute.FindKernel("ProjectPoints");
        _kernelCalcGridZMin = pointCloudCompute.FindKernel("CalculateGridZMin");
        _kernelCalcDensity = pointCloudCompute.FindKernel("CalculateDensity");
        _kernelCalcGridLevel = pointCloudCompute.FindKernel("CalculateGridLevel");
        _kernelGridMedianFilter = pointCloudCompute.FindKernel("GridMedianFilter");
        _kernelCalcNeighborhoodSize = pointCloudCompute.FindKernel("CalculateNeighborhoodSize");
        _kernelFillNeighborhoodSizeWithMinLevel = pointCloudCompute.FindKernel("FillNeighborhoodSizeWithMinLevel");

        _kernelBuildDepthPyramidL1 = pointCloudCompute.FindKernel("BuildDepthPyramidL1");
        _kernelBuildDepthPyramidL2 = pointCloudCompute.FindKernel("BuildDepthPyramidL2");
        _kernelBuildDepthPyramidL3 = pointCloudCompute.FindKernel("BuildDepthPyramidL3");
        _kernelBuildDepthPyramidL4 = pointCloudCompute.FindKernel("BuildDepthPyramidL4");
        _kernelBuildDepthPyramidL5 = pointCloudCompute.FindKernel("BuildDepthPyramidL5");
        _kernelBuildDepthPyramidL6 = pointCloudCompute.FindKernel("BuildDepthPyramidL6");
        _kernelApplyGradient = pointCloudCompute.FindKernel("ApplyAdaptiveGradientCorrection");

        _kernelComputeOcclusion = pointCloudCompute.FindKernel("ComputeOcclusion");
        _kernelCopyColorToOcclusion = pointCloudCompute.FindKernel("CopyColorToOcclusion");
        _kernelFillHoles = pointCloudCompute.FindKernel("FillHoles");
        _kernelFillHolesPullPushInit = pointCloudCompute.FindKernel("FillHolesPullPushInit");
        _kernelFillHolesPull = pointCloudCompute.FindKernel("FillHolesPull");
        _kernelFillHolesPush = pointCloudCompute.FindKernel("FillHolesPush");
        _kernelFillHolesPullPushFinalize = pointCloudCompute.FindKernel("FillHolesPullPushFinalize");
        _kernelInterpolate = pointCloudCompute.FindKernel("Interpolate");
        _kernelMerge = pointCloudCompute.FindKernel("MergeBuffer");
        _kernelInitFromCamera = pointCloudCompute.FindKernel("InitFromCamera");
        _kernelVisualizeOcclusionDebug = pointCloudCompute.FindKernel("VisualizeOcclusionDebug");
        _kernelMorphologyErode = pointCloudCompute.FindKernel("MorphologyErode");
        _kernelMorphologyDilate = pointCloudCompute.FindKernel("MorphologyDilate");
        _kernelMorphologyCopy = pointCloudCompute.FindKernel("MorphologyCopy");

        _kernelBuildMorphPyramidL1 = pointCloudCompute.FindKernel("BuildMorphPyramidL1");
        _kernelBuildMorphPyramidL2 = pointCloudCompute.FindKernel("BuildMorphPyramidL2");
        _kernelBuildMorphPyramidL3 = pointCloudCompute.FindKernel("BuildMorphPyramidL3");
        _kernelBuildMorphPyramidL4 = pointCloudCompute.FindKernel("BuildMorphPyramidL4");
        _kernelBuildMorphPyramidL5 = pointCloudCompute.FindKernel("BuildMorphPyramidL5");
        _kernelBuildMorphPyramidL6 = pointCloudCompute.FindKernel("BuildMorphPyramidL6");
 
        _isInitialized = true;
    }

    private class ComputePassData
    {
        internal ComputeShader computeShader;
        internal int pointCount;
        internal Vector4 screenParams;
        internal Matrix4x4 viewMatrix;
        internal Matrix4x4 projectionMatrix;
        
        internal PCDRendererFeature.PCDRenderSettings settings;

        internal int kernelClear, kernelClearCounter, kernelProject, kernelCalcGridZMin, kernelCalcDensity,
                     kernelCalcGridLevel, kernelGridMedianFilter,
                     kernelCalcNeighborhoodSize, kernelFillNeighborhoodSizeWithMinLevel,
                     kernelBuildDepthPyramidL1, kernelBuildDepthPyramidL2,
                     kernelBuildDepthPyramidL3, kernelBuildDepthPyramidL4,
                     kernelBuildDepthPyramidL5, kernelBuildDepthPyramidL6,
                     kernelApplyGradient,
                      kernelComputeOcclusion, kernelCopyColorToOcclusion, kernelFillHoles, kernelFillHolesPullPushInit, kernelFillHolesPull, kernelFillHolesPush, kernelFillHolesPullPushFinalize, kernelInterpolate,
                      kernelMerge, kernelInitFromCamera, kernelVisualizeOcclusionDebug,
                      kernelMorphologyErode, kernelMorphologyDilate, kernelMorphologyCopy,
                      kernelBuildMorphPyramidL1, kernelBuildMorphPyramidL2, kernelBuildMorphPyramidL3, kernelBuildMorphPyramidL4, kernelBuildMorphPyramidL5, kernelBuildMorphPyramidL6;

        // コピー用バッファ
        internal bool useExternal;
        internal ComputeBuffer externalBuffer;
        internal ComputeBuffer internalBuffer;
        internal int externalCount;
        internal int internalCount;
        internal ComputeBuffer combinedBuffer; // ターゲットバッファ
        internal ComputeBuffer pointBuffer;
        internal ComputeBuffer staticMeshCounterBuffer;

        internal RTHandle colorMap;
        internal RTHandle depthMap;
        internal TextureHandle virtualDepthTexture;
        internal TextureHandle cameraColorTexture;
        internal bool hasVirtualDepth;
        internal bool hasVirtualObjects;
        internal bool depthMapOnlyMode;
        internal Matrix4x4 inverseProjectionMatrix;
        internal RTHandle viewPositionMap;
        internal RTHandle gridZMinMap;
        internal RTHandle densityMap;
        internal RTHandle gridLevelMap;
        internal RTHandle filteredGridLevelMap;
        internal RTHandle neighborhoodSizeMap;
        internal RTHandle depthPyramidL1;
        internal RTHandle depthPyramidL2;
        internal RTHandle depthPyramidL3;
        internal RTHandle depthPyramidL4;
        internal RTHandle depthPyramidL5;
        internal RTHandle depthPyramidL6;
        internal RTHandle correctedNeighborhoodSizeMap;
        internal RTHandle occlusionResultMap;

        // Pull-Push pyramid
        internal RTHandle[] pullPushPyramid;
 
        // Morphology Temp
        internal RTHandle morphColorTemp;
        internal RTHandle morphTypeTemp;

        // Morphology Pyramid
        internal RTHandle morphTypePyramidL1;
        internal RTHandle morphTypePyramidL2;
        internal RTHandle morphTypePyramidL3;
        internal RTHandle morphTypePyramidL4;
        internal RTHandle morphTypePyramidL5;
        internal RTHandle morphTypePyramidL6;

        internal RTHandle morphColorPyramidL1;
        internal RTHandle morphColorPyramidL2;
        internal RTHandle morphColorPyramidL3;
        internal RTHandle morphColorPyramidL4;
        internal RTHandle morphColorPyramidL5;
        internal RTHandle morphColorPyramidL6;

        internal RTHandle originTypeMap;
        internal RTHandle debugDisplayMap;
        internal RTHandle occlusionValueMap;
        internal RTHandle neighborCountMap;
        internal RTHandle finalImage;
    }

    private class BlitPassData
    {
        internal TextureHandle sourceImage;
        internal TextureHandle cameraTarget;
        internal bool enablePixelTagMap;
        internal bool enableOcclusionMap;
        internal bool useDirectGpuImageBuffer; // SRD Managerでの切り替え用フラグ
        internal RenderTexture directGpuImageMap; // 実際のターゲットRenderTexture
    }

    /// <summary> デバッグマップが生成されている場合はそれを返し、そうでない場合はnullを返します。 </summary>
    public Texture GetDebugDisplayMap()
    {
        if ((_settings.enablePixelTagMap || _settings.enableOcclusionMap) && _debugDisplayMapHandle != null)
        {
            return _debugDisplayMapHandle;
        }
        return null;
    }

    /// <summary> このフレームでオクルージョンパスのパイプラインをスキップするかどうかを決定します。 </summary>
    public bool ShouldSkipRendering()
    {
        // 外部バッファの確認
        bool hasExternalData = _bufferManager.UseExternalBuffer && _bufferManager.ExternalPointBuffer != null && _bufferManager.ExternalPointBuffer.IsValid() && _bufferManager.ExternalPointCount > 0;

        // 内部バッファの確認
        bool hasInternalData = _bufferManager.PointBuffer != null && _bufferManager.PointBuffer.IsValid() && _bufferManager.PointCount > 0;

        // DepthMapモードのメッシュがあるか確認
        bool hasDepthMapMeshes = _bufferManager.HasDepthMapMeshes();

        // PointCloudモードのメッシュがあるか確認
        bool hasPointCloudMeshes = _bufferManager.HasPointCloudMeshes();

        // 点群データがなく、注入するメッシュもない場合（または背景の深度のみを生成する場合）、レンダリングをスキップします。
        bool noPointCloudData = !hasExternalData && !hasInternalData && !hasPointCloudMeshes;
        bool depthMapOnlyMode = hasDepthMapMeshes && noPointCloudData;

        return depthMapOnlyMode;
    }

    /// <summary> メモリリークを防ぐために、リソースと参照を適切に解放します。 </summary>
    public void Cleanup()
    {
        _bufferManager.Cleanup();

        _debugDisplayMapHandle?.Release();
        _debugDisplayMapHandle = null;

        _occlusionValueMapHandle?.Release();
        _occlusionValueMapHandle = null;

        _integratedDepthMapHandle?.Release();
        _integratedDepthMapHandle = null;

        _neighborhoodMapHandle?.Release();
        _neighborhoodMapHandle = null;

        _neighborCountMapHandle?.Release();
        _neighborCountMapHandle = null;

        _directGpuImageMapHandle?.Release();
        _directGpuImageMapHandle = null;

        _directGpuImageLeftHandle?.Release();
        _directGpuImageLeftHandle = null;

        _directGpuImageRightHandle?.Release();
        _directGpuImageRightHandle = null;

        _staticMeshCounterBuffer?.Release();
        _staticMeshCounterBuffer = null;

        _isInitialized = false;
    }
}