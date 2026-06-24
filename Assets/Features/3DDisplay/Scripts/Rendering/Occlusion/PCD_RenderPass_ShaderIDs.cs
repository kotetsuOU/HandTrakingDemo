// =============================================================================
// PCD_RenderPass_ShaderIDs.cs
// -----------------------------------------------------------------------------
// コンピュートシェーダーで使用するすべてのプロパティIDをキャッシュする静的クラス。
// Shader.PropertyToID() は文字列ルックアップを伴うため、毎フレーム呼び出すと
// パフォーマンスに影響します。ここで一度だけ計算し、結果を再利用します。
//
// カテゴリ:
//   - アルゴリズムパラメータ (密度閾値、近傍サイズ、オクルージョン設定など)
//   - メインテクスチャマップ (カラー、深度、座標、密度、グリッドレベルなど)
//   - 深度ピラミッド (L1〜L6 の読み取り/書き込み用)
//   - モルフォロジーピラミッド (タイプ/カラー L1〜L6)
//   - オクルージョン結果 / 最終合成
//   - Pull-Push ホールフィリング
//   - デバッグ / 可視化
//   - バッファマージ / 静的メッシュ
//   - 仮想深度 / カメラ統合
// =============================================================================
using UnityEngine;

public partial class PCDRenderPass
{
    /// <summary>
    /// フレームごとの文字列ルックアップを避けるためにシェーダープロパティIDをキャッシュする。
    /// </summary>
    private static class ShaderIDs
    {
        // =====================================================================
        // アルゴリズムパラメータ
        // =====================================================================
        public static readonly int PointCount = Shader.PropertyToID("_PointCount");
        public static readonly int ScreenParams = Shader.PropertyToID("_ScreenParams");
        public static readonly int ViewMatrix = Shader.PropertyToID("_ViewMatrix");
        public static readonly int ProjectionMatrix = Shader.PropertyToID("_ProjectionMatrix");
        public static readonly int InverseProjectionMatrix = Shader.PropertyToID("_InverseProjectionMatrix");

        /// <summary> 密度が十分かどうかを判断するための閾値 </summary>
        public static readonly int DensityThreshold_e = Shader.PropertyToID("_DensityThreshold_e");
        /// <summary> 近傍サイズの計算に用いるパラメータ p' </summary>
        public static readonly int NeighborhoodParam_p_prime = Shader.PropertyToID("_NeighborhoodParam_p_prime");
        /// <summary> 勾配補正の適用閾値 </summary>
        public static readonly int GradientThreshold_g_th = Shader.PropertyToID("_GradientThreshold_g_th");
        /// <summary> オクルージョンカーネルの種類 (Bouchiba / Exponential / Linear / Skip) </summary>
        public static readonly int KernelType = Shader.PropertyToID("_KernelType");
        /// <summary> オクルージョン評価モード (Average / SectorThreshold) </summary>
        public static readonly int EvaluationMode = Shader.PropertyToID("_EvaluationMode");
        /// <summary> SectorThreshold モード時に必要な最小遮蔽セクター数 </summary>
        public static readonly int MinOccludedSectors = Shader.PropertyToID("_MinOccludedSectors");
        /// <summary> 探索を開始するピラミッドの最小レベル </summary>
        public static readonly int MinSearchLevel = Shader.PropertyToID("_MinSearchLevel");
        /// <summary> 指数カーネルのアルファパラメータ </summary>
        public static readonly int Alpha = Shader.PropertyToID("_Alpha");
        /// <summary> オクルージョン判定の閾値 (0〜1) </summary>
        public static readonly int OcclusionThreshold = Shader.PropertyToID("_OcclusionThreshold");
        /// <summary> ソフトオクルージョンのフェード幅 </summary>
        public static readonly int OcclusionFadeWidth = Shader.PropertyToID("_OcclusionFadeWidth");

        // =====================================================================
        // メインテクスチャマップ（読み取り / 書き込み）
        // =====================================================================
        public static readonly int ColorMap = Shader.PropertyToID("_ColorMap");
        public static readonly int DepthMap = Shader.PropertyToID("_DepthMap");
        public static readonly int ColorMap_RW = Shader.PropertyToID("_ColorMap_RW");
        public static readonly int DepthMap_RW = Shader.PropertyToID("_DepthMap_RW");
        public static readonly int ViewPositionMap = Shader.PropertyToID("_ViewPositionMap");
        public static readonly int ViewPositionMap_RW = Shader.PropertyToID("_ViewPositionMap_RW");

        // --- グリッド・密度・LOD 関連 ---
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

        // =====================================================================
        // 深度ピラミッド (L1〜L6)
        // ピクセルレベルの深度情報を段階的に縮小し、
        // オクルージョンカーネルが広範囲の深度を効率的に参照できるようにする
        // =====================================================================
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

        // =====================================================================
        // モルフォロジーピラミッド (タイプ / カラー L1〜L6)
        // モルフォロジー演算（膨張・収縮）をピラミッド構造で高速化するためのテクスチャ
        // =====================================================================
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

        // =====================================================================
        // オクルージョン結果 / 最終合成
        // =====================================================================
        public static readonly int OcclusionResultMap = Shader.PropertyToID("_OcclusionResultMap");
        public static readonly int OcclusionResultMap_RW = Shader.PropertyToID("_OcclusionResultMap_RW");
        public static readonly int FinalImage_RW = Shader.PropertyToID("_FinalImage_RW");

        // =====================================================================
        // Pull-Push ホールフィリング
        // 空洞を段階的に縮小（Pull）→ 拡大（Push）して補間するアルゴリズム
        // =====================================================================
        public static readonly int PullPushLevel_In = Shader.PropertyToID("_PullPushLevel_In");
        public static readonly int PullPushLevel_Out = Shader.PropertyToID("_PullPushLevel_Out");
        public static readonly int PullPushLevel_In_RW = Shader.PropertyToID("_PullPushLevel_In_RW");
        public static readonly int PullPushLevel_Out_RW = Shader.PropertyToID("_PullPushLevel_Out_RW");
        public static readonly int PullPushIsBaseLevel = Shader.PropertyToID("_PullPushIsBaseLevel");
        public static readonly int PullPushMaxLevel = Shader.PropertyToID("_PullPushMaxLevel");
        public static readonly int PullPushCurrentLevel = Shader.PropertyToID("_PullPushCurrentLevel");

        // =====================================================================
        // デバッグ / 可視化
        // =====================================================================
        public static readonly int OriginTypeMap = Shader.PropertyToID("_OriginTypeMap");
        public static readonly int OriginTypeMap_RW = Shader.PropertyToID("_OriginTypeMap_RW");
        public static readonly int OriginMap_RW = Shader.PropertyToID("_OriginMap_RW");
        public static readonly int NeighborCountMap_RW = Shader.PropertyToID("_NeighborCountMap_RW");
        public static readonly int DebugDisplayMode = Shader.PropertyToID("_DebugDisplayMode");
        public static readonly int OcclusionValueMap_RW = Shader.PropertyToID("_OcclusionValueMap_RW");
        public static readonly int RecordOcclusionDebug = Shader.PropertyToID("_RecordOcclusionDebug");

        // =====================================================================
        // バッファマージ / 静的メッシュ
        // 外部(GPU)バッファと内部(CPU)バッファを1つの統合バッファに結合する際に使用
        // =====================================================================
        public static readonly int MergeSrcBuffer = Shader.PropertyToID("_MergeSrcBuffer");
        public static readonly int MergeDstBuffer = Shader.PropertyToID("_MergeDstBuffer");
        public static readonly int MergeSrcOffset = Shader.PropertyToID("_MergeSrcOffset");
        public static readonly int MergeDstOffset = Shader.PropertyToID("_MergeDstOffset");
        public static readonly int MergeCopyCount = Shader.PropertyToID("_MergeCopyCount");
        public static readonly int PointBuffer = Shader.PropertyToID("_PointBuffer");
        public static readonly int StaticMeshCounter_RW = Shader.PropertyToID("_StaticMeshCounter_RW");

        // =====================================================================
        // 仮想深度 / カメラ統合
        // Unityの通常レンダリングパイプラインが生成した深度/カラーを
        // 点群オクルージョンパイプラインに取り込むために使用
        // =====================================================================
        public static readonly int UseVirtualDepth = Shader.PropertyToID("_UseVirtualDepth");
        public static readonly int VirtualDepthMap = Shader.PropertyToID("_VirtualDepthMap");
        public static readonly int CameraColorTexture = Shader.PropertyToID("_CameraColorTexture");

        // =====================================================================
        // モルフォロジー演算用（単一パス入出力）
        // =====================================================================
        public static readonly int MorphColorIn = Shader.PropertyToID("_MorphColorIn");
        public static readonly int MorphColorOut_RW = Shader.PropertyToID("_MorphColorOut_RW");
        public static readonly int MorphTypeIn = Shader.PropertyToID("_MorphTypeIn");
        public static readonly int MorphTypeOut_RW = Shader.PropertyToID("_MorphTypeOut_RW");
        public static readonly int MorphKernelHalfSize = Shader.PropertyToID("_MorphKernelHalfSize");
    }
}
