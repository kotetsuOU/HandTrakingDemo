// =============================================================================
// PCD_RenderPass_PassData.cs
// -----------------------------------------------------------------------------
// RenderGraph のパス間でデータを受け渡すためのコンテナクラス。
//
// ComputePassData: コンピュートシェーダーによるオクルージョンパイプライン全体の
//                  実行に必要なすべてのパラメータ・バッファ・テクスチャを保持する。
//
// BlitPassData:    最終画像またはデバッグ画像をカメラターゲットに転送（Blit）する
//                  際に必要な最小限のデータを保持する。
// =============================================================================
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public partial class PCDRenderPass
{
    /// <summary>
    /// コンピュートシェーダーパスで使用する全データを格納するクラス。
    /// RenderGraph の AddUnsafePass で生成され、ExecuteComputePass に渡される。
    /// </summary>
    private class ComputePassData
    {
        // ----- コンピュートシェーダー本体 -----
        internal ComputeShader computeShader;

        // ----- 点群・画面パラメータ -----
        internal int pointCount;            // 描画対象の点群数（外部＋内部の合計）
        internal Vector4 screenParams;      // (幅, 高さ, 0, 0)
        internal Matrix4x4 viewMatrix;      // ワールド→カメラ座標変換行列
        internal Matrix4x4 projectionMatrix;// カメラ→クリップ座標変換行列
        internal Matrix4x4 inverseProjectionMatrix; // クリップ→カメラ座標の逆行列

        // ----- レンダリング設定 -----
        internal PCDRendererFeature.PCDRenderSettings settings;

        // ----- カーネルID群 -----
        // 各コンピュートシェーダー関数に対応するカーネルインデックス
        // クリア・投影
        internal int kernelClear, kernelClearCounter, kernelProject;
        // 密度・LOD 計算
        internal int kernelCalcGridZMin, kernelCalcDensity, kernelCalcGridLevel;
        internal int kernelGridMedianFilter, kernelCalcNeighborhoodSize;
        internal int kernelFillNeighborhoodSizeWithMinLevel;
        // 深度ピラミッド構築 (L1〜L6)
        internal int kernelBuildDepthPyramidL1, kernelBuildDepthPyramidL2;
        internal int kernelBuildDepthPyramidL3, kernelBuildDepthPyramidL4;
        internal int kernelBuildDepthPyramidL5, kernelBuildDepthPyramidL6;
        // 勾配補正
        internal int kernelApplyGradient;
        // オクルージョン判定
        internal int kernelComputeOcclusion, kernelCopyColorToOcclusion;
        // ホールフィリング
        internal int kernelFillHoles;
        internal int kernelFillHolesPullPushInit, kernelFillHolesPull;
        internal int kernelFillHolesPush, kernelFillHolesPullPushFinalize;
        internal int kernelInterpolate;
        // バッファマージ・カメラ初期化・デバッグ
        internal int kernelMerge, kernelInitFromCamera, kernelVisualizeOcclusionDebug;
        // モルフォロジー演算
        internal int kernelMorphologyErode, kernelMorphologyDilate, kernelMorphologyCopy;
        // モルフォロジーピラミッド構築 (L1〜L6)
        internal int kernelBuildMorphPyramidL1, kernelBuildMorphPyramidL2;
        internal int kernelBuildMorphPyramidL3, kernelBuildMorphPyramidL4;
        internal int kernelBuildMorphPyramidL5, kernelBuildMorphPyramidL6;

        // ----- バッファ管理 -----
        // 外部(GPU)バッファと内部(CPU)バッファを結合してシェーダーに渡す
        internal bool useExternal;                    // 外部バッファを使用するか
        internal ComputeBuffer externalBuffer;        // 外部（グローバル）点群バッファ
        internal ComputeBuffer internalBuffer;        // 内部（ローカル）点群バッファ
        internal int externalCount;                   // 外部バッファの有効点数
        internal int internalCount;                   // 内部バッファの有効点数
        internal ComputeBuffer combinedBuffer;        // 結合後のターゲットバッファ
        internal ComputeBuffer pointBuffer;           // 実際にシェーダーに渡すバッファ
        internal ComputeBuffer staticMeshCounterBuffer; // 静的メッシュのピクセルカウンター

        // ----- メインテクスチャマップ -----
        internal RTHandle colorMap;                   // 点群の色情報
        internal RTHandle depthMap;                   // 点群の深度情報（整数型）
        internal RTHandle viewPositionMap;            // ビュー空間座標
        internal RTHandle originTypeMap;              // ピクセルの由来タイプ (点群 / メッシュ / 背景)

        // ----- 仮想深度・カメラ -----
        internal TextureHandle virtualDepthTexture;   // Unity標準パイプラインの深度テクスチャ
        internal TextureHandle cameraColorTexture;    // Unity標準パイプラインのカラーテクスチャ
        internal bool hasVirtualDepth;                // 仮想深度が利用可能か
        internal bool hasVirtualObjects;              // シーンに仮想オブジェクトが存在するか
        internal bool depthMapOnlyMode;               // 深度マップ取得のみモード

        // ----- 密度・LOD 関連マップ -----
        internal RTHandle gridZMinMap;                // グリッドセルごとの最小深度
        internal RTHandle densityMap;                 // 画面上のサンプル密度
        internal RTHandle gridLevelMap;               // 密度から算出されたグリッドレベル
        internal RTHandle filteredGridLevelMap;        // メディアンフィルタ適用後のグリッドレベル
        internal RTHandle neighborhoodSizeMap;        // 基本的な近傍サイズ
        internal RTHandle correctedNeighborhoodSizeMap; // 勾配補正後の近傍サイズ

        // ----- 深度ピラミッド (L1〜L6) -----
        internal RTHandle depthPyramidL1;
        internal RTHandle depthPyramidL2;
        internal RTHandle depthPyramidL3;
        internal RTHandle depthPyramidL4;
        internal RTHandle depthPyramidL5;
        internal RTHandle depthPyramidL6;

        // ----- オクルージョン結果 -----
        internal RTHandle occlusionResultMap;          // オクルージョン判定後の結果画像

        // ----- Pull-Push ピラミッド -----
        internal RTHandle[] pullPushPyramid;           // Pull-Push ホールフィリング用のピラミッド配列

        // ----- モルフォロジー一時バッファ -----
        internal RTHandle morphColorTemp;              // 膨張/収縮の中間結果（カラー）
        internal RTHandle morphTypeTemp;               // 膨張/収縮の中間結果（タイプ）

        // ----- モルフォロジーピラミッド (タイプ L1〜L6) -----
        internal RTHandle morphTypePyramidL1;
        internal RTHandle morphTypePyramidL2;
        internal RTHandle morphTypePyramidL3;
        internal RTHandle morphTypePyramidL4;
        internal RTHandle morphTypePyramidL5;
        internal RTHandle morphTypePyramidL6;

        // ----- モルフォロジーピラミッド (カラー L1〜L6) -----
        internal RTHandle morphColorPyramidL1;
        internal RTHandle morphColorPyramidL2;
        internal RTHandle morphColorPyramidL3;
        internal RTHandle morphColorPyramidL4;
        internal RTHandle morphColorPyramidL5;
        internal RTHandle morphColorPyramidL6;

        // ----- デバッグ・可視化 -----
        internal RTHandle debugDisplayMap;             // デバッグ表示用マップ（PixelTag / Occlusionなど）
        internal RTHandle occlusionValueMap;            // 生のオクルージョン値（CSV出力用）
        internal RTHandle neighborCountMap;             // 近傍カウント（デバッグ記録用）
        internal RTHandle finalImage;                  // ホールフィリング後の最終合成画像
    }

    /// <summary>
    /// 最終画像のBlit（画面転送）パスで使用するデータを格納するクラス。
    /// </summary>
    private class BlitPassData
    {
        internal TextureHandle sourceImage;            // Blit元のテクスチャ
        internal TextureHandle cameraTarget;           // Blit先のカメラターゲット
        internal bool enablePixelTagMap;               // PixelTagデバッグ表示が有効か
        internal bool enableOcclusionMap;               // Occlusionデバッグ表示が有効か
        internal bool useDirectGpuImageBuffer;         // SRD Manager での直接GPU描画切り替え
        internal RenderTexture directGpuImageMap;      // 直接GPU描画の対象 RenderTexture
    }
}
