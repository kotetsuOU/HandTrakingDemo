using UnityEngine;
using static PCDRendererFeature;

[ExecuteInEditMode]
public class PCDOcclusionPipelineController : MonoBehaviour
{
    public static PCDOcclusionPipelineController Instance { get; private set; }

    [Header("Occlusion Core Settings")]
    [Tooltip("オクルージョン計算に用いるカーネル関数")]
    public PCD_OcclusionKernel kernelType = PCD_OcclusionKernel.Bouchiba;

    [Tooltip("オクルージョン判定の評価方法（平均値 or 各セクターごと）")]
    public PCD_OcclusionEvaluationMode evaluationMode = PCD_OcclusionEvaluationMode.Average;

    [Tooltip("各セクターごと評価時に、オクルージョン判定となるために閾値を超える必要がある最小セクター数")]
    [Range(1, 8)]
    public int minOccludedSectors = 1;

    [Tooltip("オクルージョン近傍探索の最小レベル(0〜6)。値を上げるとより広い範囲を常に探索し、隙間を埋めやすくなります。")]
    [Range(0, 6)]
    public int minSearchLevel = 0;

    [Header("Algorithm Parameters")]
    [Tooltip("指数関数の減衰係数 (Expモード専用)")]
    public float exponentAlpha;

    [Tooltip("密度計算に用いる深度のしきい値 e")]
    public float densityThreshold_e = 0.04f;

    [Tooltip("近傍領域サイズを決定するための調整パラメータ p' ")]
    public float neighborhoodParam_p_prime = 4.8f;

    [Header("Gradient Correction")]
    [Tooltip("密度に基づく動的LOD探索を有効にするか。OFFの場合は常にminSearchLevelをベースに探索します。")]
    public bool enableDensityBasedLOD = true;

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

    [Header("Virtual Contact Occlusion")]
    [Tooltip("HCDの接触検知をもとに仮想的な遮蔽点群を生成します")]
    public bool enableVirtualContactOcclusion = false;
    
    [Tooltip("仮想点群を生成する円盤の半径 (m)")]
    public float virtualContactRadius = 0.03f;
    
    [Tooltip("仮想点群の配置間隔 (m)")]
    public float virtualContactSpacing = 0.005f;

    [Tooltip("仮想点群のデバッグ描画色 (SceneビューのGizmoおよびPixelTagMap用)")]
    public Color virtualContactColor = new Color(0.8f, 0f, 0.4f, 0.8f);

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

    [Tooltip("②仮想物体を区別した密度計算 (ONで従来手法のカウント漏れや過剰を補正)")]
    public bool enableTypeAwareDensity = true;

    [Header("Novel Methods Toggles (Ablation Study)")]
    [Tooltip("③ソフトオクルージョン (ONでグラデーションによる境界のスムージング)")]
    public bool enableSoftOcclusionFade = true;

    [Tooltip("④エッジ保持型ホールフィリング手法の選択")]
    public PCD_HoleFillingMethod holeFillingMethod = PCD_HoleFillingMethod.JointBilateral;

    [Tooltip("⑤処理の最適化と検証のためのグリッドサイズ")]
    public PCD_GridSize gridSize = PCD_GridSize.Grid16x16;

    [Header("Morphology Settings")]
    [Tooltip("モルフォロジーカーネルの半径（1 = 3×3, 2 = 5×5。大きいほど強く重い）")]
    [Range(1, 25)]
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
            if (Application.isPlaying)
            {
                Destroy(this);
            }
            else
            {
                Debug.LogWarning($"[PCD] Duplicate PCDOcclusionPipelineController found on {gameObject.name}. Please remove it.");
            }
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        if (Instance == null || Instance == this)
        {
            Instance = this;
        }
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
            evaluationMode = this.evaluationMode,
            minOccludedSectors = this.minOccludedSectors,
            minSearchLevel = this.minSearchLevel,
            exponentAlpha = this.exponentAlpha,
            densityThreshold_e = this.densityThreshold_e,
            neighborhoodParam_p_prime = this.neighborhoodParam_p_prime,
            enableDensityBasedLOD = this.enableDensityBasedLOD,
            enableGradientCorrection = this.enableGradientCorrection,
            gradientThreshold_g_th = this.gradientThreshold_g_th,
            occlusionThreshold = this.occlusionThreshold,
            occlusionFadeWidth = this.occlusionFadeWidth,
            enableVirtualContactOcclusion = this.enableVirtualContactOcclusion,
            virtualContactRadius = this.virtualContactRadius,
            virtualContactSpacing = this.virtualContactSpacing,
            virtualContactColor = this.virtualContactColor,
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
            gridSize = this.gridSize,
            morphKernelHalfSize = this.morphKernelHalfSize,
            morphErodeIterations = this.morphErodeIterations,
            morphDilateIterations = this.morphDilateIterations,
            _dynamicMultiplierRuntimeValue = PCDRendererFeature.Instance != null ? PCDRendererFeature.Instance._internalDynamicMultiplier : 1
        };
    }

    private void OnDrawGizmos()
    {
        if (!enableVirtualContactOcclusion || !Application.isPlaying) return;

        // Draw the virtual contact points in the scene view for debugging
        if (HCD_Pipeline.Instance == null || HCD_Pipeline.Instance.distanceProcessor == null) return;

        var trackedClusters = HCD_Pipeline.Instance.GetTrackedClusters();
        if (trackedClusters == null) return;

        float radius = virtualContactRadius;
        float offset = HCD_Pipeline.Instance.distanceProcessor.surfaceDistanceThreshold;

        Gizmos.color = virtualContactColor;

        foreach (var c in trackedClusters)
        {
            if (!c.IsAlive) continue;

            Vector3 centroid = c.Centroid;
            Vector3 normal = c.Normal.normalized;
            if (normal.sqrMagnitude < 0.1f) normal = Vector3.up;

            centroid += normal * offset;

#if UNITY_EDITOR
            // Draw a cleaner gizmo instead of hundreds of points to reduce clutter
            UnityEditor.Handles.color = virtualContactColor;
            UnityEditor.Handles.DrawWireDisc(centroid, normal, radius);
            
            // Draw normal line
            Gizmos.DrawLine(centroid, centroid + normal * 0.05f);
#endif
        }
    }
}
