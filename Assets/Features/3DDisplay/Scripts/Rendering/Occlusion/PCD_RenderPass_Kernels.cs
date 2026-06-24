// =============================================================================
// PCD_RenderPass_Kernels.cs
// -----------------------------------------------------------------------------
// コンピュートシェーダーのカーネルID変数の定義と、初期化時のカーネルインデックス
// 取得（Initialize）を行う partial クラス。
// =============================================================================
using UnityEngine;

public partial class PCDRenderPass
{
    // =========================================================================
    // カーネルID — コンピュートシェーダー内の各関数に対応するインデックス
    // =========================================================================
    
    // クリア・投影
    private int _kernelClear, _kernelClearCounter, _kernelProject;
    
    // 密度・LOD 計算
    private int _kernelCalcGridZMin, _kernelCalcDensity, _kernelCalcGridLevel;
    private int _kernelGridMedianFilter, _kernelCalcNeighborhoodSize;
    private int _kernelFillNeighborhoodSizeWithMinLevel;
    
    // 深度ピラミッド構築 (L1〜L6)
    private int _kernelBuildDepthPyramidL1, _kernelBuildDepthPyramidL2;
    private int _kernelBuildDepthPyramidL3, _kernelBuildDepthPyramidL4;
    private int _kernelBuildDepthPyramidL5, _kernelBuildDepthPyramidL6;
    
    // 勾配補正
    private int _kernelApplyGradient;
    
    // オクルージョン判定
    private int _kernelComputeOcclusion, _kernelCopyColorToOcclusion;
    
    // ホールフィリング
    private int _kernelFillHoles;
    private int _kernelFillHolesPullPushInit, _kernelFillHolesPull;
    private int _kernelFillHolesPush, _kernelFillHolesPullPushFinalize;
    private int _kernelInterpolate;
    
    // バッファマージ・カメラ初期化・デバッグ
    private int _kernelMerge, _kernelInitFromCamera, _kernelVisualizeOcclusionDebug;
    
    // モルフォロジー演算
    private int _kernelMorphologyErode, _kernelMorphologyDilate, _kernelMorphologyCopy;
    
    // モルフォロジーピラミッド構築 (L1〜L6)
    private int _kernelBuildMorphPyramidL1, _kernelBuildMorphPyramidL2, _kernelBuildMorphPyramidL3;
    private int _kernelBuildMorphPyramidL4, _kernelBuildMorphPyramidL5, _kernelBuildMorphPyramidL6;

    // =========================================================================
    // 初期化
    // =========================================================================

    /// <summary> コンピュートシェーダーからカーネルのインデックスIDを取得して初期化します。 </summary>
    private void Initialize()
    {
        if (pointCloudCompute == null)
        {
            UnityEngine.Debug.LogError("Compute Shader is null. Initialization failed.");
            _isInitialized = false;
            return;
        }

        // --- クリア・投影 ---
        _kernelClear = pointCloudCompute.FindKernel("ClearMaps");
        _kernelClearCounter = pointCloudCompute.FindKernel("ClearCounter");
        _kernelProject = pointCloudCompute.FindKernel("ProjectPoints");

        // --- 密度・LOD 計算 ---
        _kernelCalcGridZMin = pointCloudCompute.FindKernel("CalculateGridZMin");
        _kernelCalcDensity = pointCloudCompute.FindKernel("CalculateDensity");
        _kernelCalcGridLevel = pointCloudCompute.FindKernel("CalculateGridLevel");
        _kernelGridMedianFilter = pointCloudCompute.FindKernel("GridMedianFilter");
        _kernelCalcNeighborhoodSize = pointCloudCompute.FindKernel("CalculateNeighborhoodSize");
        _kernelFillNeighborhoodSizeWithMinLevel = pointCloudCompute.FindKernel("FillNeighborhoodSizeWithMinLevel");

        // --- 深度ピラミッド構築 ---
        _kernelBuildDepthPyramidL1 = pointCloudCompute.FindKernel("BuildDepthPyramidL1");
        _kernelBuildDepthPyramidL2 = pointCloudCompute.FindKernel("BuildDepthPyramidL2");
        _kernelBuildDepthPyramidL3 = pointCloudCompute.FindKernel("BuildDepthPyramidL3");
        _kernelBuildDepthPyramidL4 = pointCloudCompute.FindKernel("BuildDepthPyramidL4");
        _kernelBuildDepthPyramidL5 = pointCloudCompute.FindKernel("BuildDepthPyramidL5");
        _kernelBuildDepthPyramidL6 = pointCloudCompute.FindKernel("BuildDepthPyramidL6");

        // --- 勾配補正 ---
        _kernelApplyGradient = pointCloudCompute.FindKernel("ApplyAdaptiveGradientCorrection");

        // --- オクルージョン判定 ---
        _kernelComputeOcclusion = pointCloudCompute.FindKernel("ComputeOcclusion");
        _kernelCopyColorToOcclusion = pointCloudCompute.FindKernel("CopyColorToOcclusion");

        // --- ホールフィリング ---
        _kernelFillHoles = pointCloudCompute.FindKernel("FillHoles");
        _kernelFillHolesPullPushInit = pointCloudCompute.FindKernel("FillHolesPullPushInit");
        _kernelFillHolesPull = pointCloudCompute.FindKernel("FillHolesPull");
        _kernelFillHolesPush = pointCloudCompute.FindKernel("FillHolesPush");
        _kernelFillHolesPullPushFinalize = pointCloudCompute.FindKernel("FillHolesPullPushFinalize");
        _kernelInterpolate = pointCloudCompute.FindKernel("Interpolate");

        // --- バッファマージ・カメラ初期化・デバッグ ---
        _kernelMerge = pointCloudCompute.FindKernel("MergeBuffer");
        _kernelInitFromCamera = pointCloudCompute.FindKernel("InitFromCamera");
        _kernelVisualizeOcclusionDebug = pointCloudCompute.FindKernel("VisualizeOcclusionDebug");

        // --- モルフォロジー演算 ---
        _kernelMorphologyErode = pointCloudCompute.FindKernel("MorphologyErode");
        _kernelMorphologyDilate = pointCloudCompute.FindKernel("MorphologyDilate");
        _kernelMorphologyCopy = pointCloudCompute.FindKernel("MorphologyCopy");

        // --- モルフォロジーピラミッド構築 ---
        _kernelBuildMorphPyramidL1 = pointCloudCompute.FindKernel("BuildMorphPyramidL1");
        _kernelBuildMorphPyramidL2 = pointCloudCompute.FindKernel("BuildMorphPyramidL2");
        _kernelBuildMorphPyramidL3 = pointCloudCompute.FindKernel("BuildMorphPyramidL3");
        _kernelBuildMorphPyramidL4 = pointCloudCompute.FindKernel("BuildMorphPyramidL4");
        _kernelBuildMorphPyramidL5 = pointCloudCompute.FindKernel("BuildMorphPyramidL5");
        _kernelBuildMorphPyramidL6 = pointCloudCompute.FindKernel("BuildMorphPyramidL6");

        _isInitialized = true;
    }
}
