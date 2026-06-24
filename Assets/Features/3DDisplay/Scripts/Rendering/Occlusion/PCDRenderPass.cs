// =============================================================================
// PCDRenderPass.cs — 点群オクルージョンパイプラインのメインクラス
// =============================================================================
//
// 【アーキテクチャ概要】
//
// このクラスは Unity の ScriptableRenderPass を継承し、点群（PointCloud）と仮想
// オブジェクトの間でリアルタイムオクルージョン判定を行うレンダリングパスです。
//
// 処理フロー:
//   1. RecordRenderGraph()  — RenderGraph にコンピュートパスと Blit パスを登録
//   2. ExecuteComputePass() — GPU 上でオクルージョンパイプラインを実行
//   3. ExecuteBlitPass()    — 結果画像をカメラターゲットに転送
//
// partial クラス構成:
//   - PCDRenderPass.cs              … フィールド定義、初期化、ライフサイクル管理
//   - PCD_RenderPass_ShaderIDs.cs   … シェーダープロパティIDのキャッシュ
//   - PCD_RenderPass_PassData.cs    … ComputePassData / BlitPassData の定義
//   - PCD_RenderPass_Execute.cs     … ExecuteComputePass（GPU実行ロジック）
//   - PCD_RenderPass_RenderGraph.cs … RecordRenderGraph（RenderGraph登録）
//   - PCD_RenderPass_Allocation.cs  … RTHandle のアロケーション / 解放
//   - PCD_RenderPass_BindParams.cs  … ComputePassData へのパラメータ転写
//   - PCD_RenderPass_Debug.cs       … デバッグ用 AsyncReadback パス
//
// =============================================================================
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

    // =========================================================================
    // コンピュートシェーダーと設定
    // =========================================================================
    private ComputeShader pointCloudCompute;                    // オクルージョンパイプラインのコアとなるコンピュートシェーダー
    private PCDRendererFeature.PCDRenderSettings _settings;     // インスペクターで設定される現在のレンダリングパラメータ


    // =========================================================================
    // 出力およびデバッグ用 RTHandle
    // =========================================================================
    private RTHandle _directGpuImageMapHandle;       // SRD 直接GPU描画用
    private RTHandle _directGpuImageLeftHandle;      // SRD 左目用
    private RTHandle _directGpuImageRightHandle;     // SRD 右目用

    // =========================================================================
    // 状態管理
    // =========================================================================
    private bool _isInitialized = false;
    private const int STRIDE = 28; // 1点のデータサイズ: sizeof(float)*3(位置) + sizeof(float)*3(色) + sizeof(uint)(タイプ)

    // =========================================================================
    // バッファ管理
    // =========================================================================
    private PCDPointBufferManager _bufferManager;
    private ComputeBuffer _staticMeshCounterBuffer;  // 仮想メッシュのピクセル数カウント用

    // =========================================================================
    // SRD Manager キャッシュ
    // =========================================================================
    private SRD.Core.SRDManager _cachedSrdManager;
    private float _lastSrdManagerSearchTime = -1000f;

    /// <summary> SRDManagerを取得する。2秒間隔でキャッシュを更新する。 </summary>
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
    // コンストラクタ
    // =========================================================================

    public PCDRenderPass(ComputeShader computeShader, PCDRendererFeature.PCDRenderSettings settings)
    {
        this.pointCloudCompute = computeShader;
        this._settings = settings;

        _bufferManager = new PCDPointBufferManager();

        _staticMeshCounterBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Default);
        _staticMeshCounterBuffer.SetData(new uint[] { 0 });
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

    // =========================================================================
    // リソース解放
    // =========================================================================

    /// <summary> メモリリークを防ぐために、リソースと参照を適切に解放します。 </summary>
    public void Cleanup()
    {
        _bufferManager.Cleanup();

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
