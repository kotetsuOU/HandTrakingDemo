using UnityEngine;
using static PCDRendererFeature;

[ExecuteInEditMode]
public class PCDOcclusionPipelineController : MonoBehaviour
{
    public static PCDOcclusionPipelineController Instance { get; private set; }

    [Header("Occlusion Core Settings")]
    [Tooltip("オクルージョン計算に用いるカーネル関数")]
    public PCV_OcclusionKernel kernelType = PCV_OcclusionKernel.Bouchiba;

    [Tooltip("空間分割時のビニング手法（重みの計算方法）")]
    public PCV_OcclusionBinning binningMethod = PCV_OcclusionBinning.Soft;

    [Tooltip("空間の分割方向数")]
    public PCV_OcclusionDirectionCount directionCount = PCV_OcclusionDirectionCount.Single;

    [Header("Algorithm Parameters")]
    [Tooltip("指数関数の減衰係数 (Expモード専用)")]
    public float exponentAlpha;

    [Tooltip("密度計算に用いる深度のしきい値 e")]
    public float densityThreshold_e = 0.04f;

    [Tooltip("近傍領域サイズを決定するための調整パラメータ p' ")]
    public float neighborhoodParam_p_prime = 4.8f;

    [Header("Gradient Correction")]
    [Tooltip("勾配を用いた補正を有効にする")]
    public bool enableGradientCorrection = true;

    [Tooltip("勾配しきい値 g_th")]
    public float gradientThreshold_g_th = 0.05f;

    [Header("Occlusion Filtering")]
    [Tooltip("オクルージョン判定のしきい値 (論文 2.4.2節)")]
    [Range(0f, 1f)]
    public float occlusionThreshold = 0.8f;

    [Tooltip("境界を滑らかにするためのフェード幅（閾値からの減衰範囲）")]
    [Range(0f, 1f)]
    public float occlusionFadeWidth = 0.1f;

    [Header("Display Debug")]
    [Tooltip("点群(黒)と静的メッシュ(白)の由来を示すデバッグマップ(PixelTagMap)を有効にします")]
    public bool enablePixelTagMap = false;

    [Tooltip("内積計算で得た occlusionAverage(0~1) を、Record Occlusion Debug Map と同じ配色ルールで画面上に常時表示します")]
    public bool enableOcclusionMap = false;

    [Header("Record Debug")]
    [Tooltip("1フレームだけOcclusionMapを保存します（occlusionAverageをPNG/CSVへ出力）")]
    public bool recordOcclusionDebugMap = false;

    [Tooltip("1フレームだけPixelTagMap(由来情報の生値)を記録します")]
    public bool recordPixelTagMap = false;

    [Tooltip("1フレームだけ統合DepthMapを記録します")]
    public bool recordIntegratedDepthMap = false;

    [Tooltip("1フレームだけNeighborhoodMapを記録します")]
    public bool recordNeighborhoodMap = false;

    [Tooltip("1フレームだけNeighborCountMapを記録します")]
    public bool recordNeighborCountMap = false;

    [Header("SICE FES 2026 Novel Methods Toggles (Ablation Study)")]
    [Tooltip("仮想・現実の「相互オクルージョン」の統合を有効にするか")]
    public bool enableVirtualDepthIntegration = true;

    [Tooltip("①タグによる近傍探索の最適化 (ONで不要な自己遮蔽計算をスキップ)")]
    public bool enableTagBasedOptimization = true;

    [Header("Novel Methods Toggles (Ablation Study)")]
    [Tooltip("②仮想物体を区別した密度計算 (ONで従来手法のカウント漏れや過剰を補正)")]
    public bool enableTypeAwareDensity = true;

    [Tooltip("③ソフトオクルージョン (ONでグラデーションによる境界のスムージング)")]
    public bool enableSoftOcclusionFade = true;

    [Tooltip("④エッジ保持型ホールフィリング手法の選択")]
    public PCV_HoleFillingMethod holeFillingMethod = PCV_HoleFillingMethod.JointBilateral;

    [Header("Morphology Settings")]
    [Tooltip("モルフォロジーカーネルの半径（1 = 3×3, 2 = 5×5。大きいほど強く重い）")]
    [Range(1, 15)]
    public int morphKernelHalfSize = 1;

    [Tooltip("Opening の収縮回数（0 でスキップ）。孤立ノイズや細いトゲを除去する。破綻確認後に増やすこと。")]
    [Range(0, 5)]
    public int morphErodeIterations = 0;

    [Tooltip("Closing の膨張回数。多いほど疎な手の甲など深い隙間まで色が伝播する。まず 1 から試すこと。")]
    [Range(1, 5)]
    public int morphDilateIterations = 1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            DestroyImmediate(this);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        Instance = this;
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        float maxFadeWidth = Mathf.Min(occlusionThreshold, 1.0f - occlusionThreshold) * 2.0f;
        occlusionFadeWidth = Mathf.Clamp(occlusionFadeWidth, 0f, maxFadeWidth);
    }

    public PCDRenderSettings GetSettings()
    {
        return new PCDRenderSettings
        {
            kernelType = this.kernelType,
            binningMethod = this.binningMethod,
            directionCount = this.directionCount,
            exponentAlpha = this.exponentAlpha,
            densityThreshold_e = this.densityThreshold_e,
            neighborhoodParam_p_prime = this.neighborhoodParam_p_prime,
            enableGradientCorrection = this.enableGradientCorrection,
            gradientThreshold_g_th = this.gradientThreshold_g_th,
            occlusionThreshold = this.occlusionThreshold,
            occlusionFadeWidth = this.occlusionFadeWidth,
            enablePixelTagMap = this.enablePixelTagMap,
            enableOcclusionMap = this.enableOcclusionMap,
            recordOcclusionDebugMap = this.recordOcclusionDebugMap,
            recordPixelTagMap = this.recordPixelTagMap,
            recordIntegratedDepthMap = this.recordIntegratedDepthMap,
            recordNeighborhoodMap = this.recordNeighborhoodMap,
            recordNeighborCountMap = this.recordNeighborCountMap,
            enableVirtualDepthIntegration = this.enableVirtualDepthIntegration,
            enableTagBasedOptimization = this.enableTagBasedOptimization,
            enableTypeAwareDensity = this.enableTypeAwareDensity,
            enableSoftOcclusionFade = this.enableSoftOcclusionFade,
            holeFillingMethod = this.holeFillingMethod,
            morphKernelHalfSize = this.morphKernelHalfSize,
            morphErodeIterations = this.morphErodeIterations,
            morphDilateIterations = this.morphDilateIterations,
            _dynamicMultiplierRuntimeValue = PCDRendererFeature.Instance != null ? PCDRendererFeature.Instance._internalDynamicMultiplier : 1
        };
    }
}
