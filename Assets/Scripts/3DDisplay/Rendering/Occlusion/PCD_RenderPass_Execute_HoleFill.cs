// =============================================================================
// PCD_RenderPass_Execute_HoleFill.cs
// -----------------------------------------------------------------------------
// ExecuteComputePass におけるホールフィリング（Joint Bilateral, Pull-Push,
// Morphology 等の穴埋めアルゴリズム）を担う partial クラス。
// =============================================================================
using UnityEngine;
using UnityEngine.Rendering;

public partial class PCDRenderPass
{
    // =========================================================================
    // ステージ11: ホールフィリング（穴埋め）
    // =========================================================================

    /// <summary>
    /// オクルージョン判定後に残る空洞（穴）をさまざまな手法で補間する。
    /// - JointBilateral: エッジ保持型の両側フィルタ
    /// - PullPush: ピラミッド縮小→拡大による階層的補間
    /// - Morphology: 膨張(Dilate)・収縮(Erode)の形態学的演算
    /// </summary>
    private void ExecuteStageHoleFilling(CommandBuffer cmd, ComputeShader cs, ComputePassData passData,
        int sw, int sh, int threadGroupsX, int threadGroupsY, bool needsNeighborhoodSize)
    {
        if (passData.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.JointBilateral)
        {
            ExecuteHoleFillingJointBilateral(cmd, cs, passData, threadGroupsX, threadGroupsY);
        }
        else if (passData.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.PullPush)
        {
            ExecuteHoleFillingPullPush(cmd, cs, passData, sw, sh, threadGroupsX, threadGroupsY);
        }
        else if (passData.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.Morphology_OC ||
                 passData.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.Morphology_CO)
        {
            ExecuteHoleFillingMorphology(cmd, cs, passData, sw, sh, threadGroupsX, threadGroupsY);
        }
    }

    /// <summary> JointBilateral ホールフィリング: エッジ保持型の両側フィルタで穴を補間する </summary>
    private void ExecuteHoleFillingJointBilateral(CommandBuffer cmd, ComputeShader cs, ComputePassData passData,
        int threadGroupsX, int threadGroupsY)
    {
        cmd.SetComputeTextureParam(cs, passData.kernelFillHoles, ShaderIDs.ColorMap, passData.colorMap);
        cmd.SetComputeTextureParam(cs, passData.kernelFillHoles, ShaderIDs.DepthMap, passData.depthMap);
        cmd.SetComputeTextureParam(cs, passData.kernelFillHoles, ShaderIDs.VirtualDepthMap, passData.virtualDepthTexture);
        cmd.SetComputeTextureParam(cs, passData.kernelFillHoles, ShaderIDs.OriginTypeMap_RW, passData.originTypeMap);
        cmd.SetComputeTextureParam(cs, passData.kernelFillHoles, ShaderIDs.OcclusionResultMap_RW, passData.occlusionResultMap);
        cmd.SetComputeTextureParam(cs, passData.kernelFillHoles, ShaderIDs.OriginMap_RW, passData.debugDisplayMap);
        cmd.SetComputeTextureParam(cs, passData.kernelFillHoles, ShaderIDs.FinalNeighborhoodSizeMap, passData.settings.enableGradientCorrection ? passData.correctedNeighborhoodSizeMap : passData.neighborhoodSizeMap);
        cmd.DispatchCompute(cs, passData.kernelFillHoles, threadGroupsX, threadGroupsY, 1);
    }

    /// <summary> Pull-Push ホールフィリング: ピラミッド縮小(Pull)→拡大(Push)で階層的に穴を補間する </summary>
    private void ExecuteHoleFillingPullPush(CommandBuffer cmd, ComputeShader cs, ComputePassData passData,
        int sw, int sh, int threadGroupsX, int threadGroupsY)
    {
        int maxLevel = 5;

        // Phase 0: ベースレベルの初期化
        cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPullPushInit, ShaderIDs.OcclusionResultMap, passData.occlusionResultMap);
        cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPullPushInit, ShaderIDs.OriginTypeMap, passData.originTypeMap);
        cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPullPushInit, ShaderIDs.PullPushLevel_Out_RW, passData.pullPushPyramid[0]);
        cmd.DispatchCompute(cs, passData.kernelFillHolesPullPushInit, threadGroupsX, threadGroupsY, 1);

        // Phase 1: Pull（ダウンサンプル）— 有効なピクセルの情報を上位レベルに伝播
        int pw = sw;
        int ph = sh;
        for (int i = 0; i < maxLevel - 1; i++)
        {
            pw = Mathf.Max(1, (pw + 1) / 2);
            ph = Mathf.Max(1, (ph + 1) / 2);
            int pullGroupsX = (pw + 7) / 8;
            int pullGroupsY = (ph + 7) / 8;

            cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPull, ShaderIDs.PullPushLevel_In, passData.pullPushPyramid[i]);
            cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPull, ShaderIDs.PullPushLevel_Out_RW, passData.pullPushPyramid[i + 1]);
            cmd.DispatchCompute(cs, passData.kernelFillHolesPull, pullGroupsX, pullGroupsY, 1);
        }

        // Phase 2: Push（アップサンプル）— 上位レベルの補間結果を下位レベルの穴に書き込む
        for (int i = maxLevel - 2; i >= 0; i--)
        {
            // 各レベルの正確なサイズを再計算
            int curr_w = sw; int curr_h = sh;
            for (int j = 0; j < i; j++) { curr_w = Mathf.Max(1, (curr_w + 1) / 2); curr_h = Mathf.Max(1, (curr_h + 1) / 2); }
            int pushGroupsX = (curr_w + 7) / 8;
            int pushGroupsY = (curr_h + 7) / 8;

            cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPush, ShaderIDs.PullPushLevel_In, passData.pullPushPyramid[i + 1]);
            cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPush, ShaderIDs.PullPushLevel_Out_RW, passData.pullPushPyramid[i]);
            cmd.DispatchCompute(cs, passData.kernelFillHolesPush, pushGroupsX, pushGroupsY, 1);
        }

        // Phase 3: 結果を OcclusionResultMap に書き戻し、OriginTypeMap も更新
        cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPullPushFinalize, ShaderIDs.PullPushLevel_In, passData.pullPushPyramid[0]);
        cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPullPushFinalize, ShaderIDs.OcclusionResultMap_RW, passData.occlusionResultMap);
        cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPullPushFinalize, ShaderIDs.OriginTypeMap_RW, passData.originTypeMap);
        cmd.SetComputeTextureParam(cs, passData.kernelFillHolesPullPushFinalize, ShaderIDs.OriginMap_RW, passData.debugDisplayMap);
        cmd.DispatchCompute(cs, passData.kernelFillHolesPullPushFinalize, threadGroupsX, threadGroupsY, 1);
    }

    /// <summary>
    /// モルフォロジーホールフィリング: ピラミッドベースの膨張(Dilate)/収縮(Erode)演算で穴を埋める。
    /// Morphology_OC: Opening(Erode→Dilate) → Closing(Dilate→Erode) の順で適用
    /// Morphology_CO: Closing(Dilate→Erode) → Opening(Erode→Dilate) の順で適用
    /// </summary>
    private void ExecuteHoleFillingMorphology(CommandBuffer cmd, ComputeShader cs, ComputePassData passData,
        int sw, int sh, int threadGroupsX, int threadGroupsY)
    {
        int halfSize = passData.settings.morphKernelHalfSize;
        cmd.SetComputeIntParam(cs, ShaderIDs.MorphKernelHalfSize, halfSize);

        bool currentInTemp = false;

        // --- モルフォロジーパス1回分の実行ロジック ---
        // ピラミッド構築 → 膨張/収縮カーネルの実行 を1セットとして行う
        System.Action<int, string> runMorphPass = (kernelId, passName) =>
        {
            RTHandle colorIn = currentInTemp ? passData.morphColorTemp : passData.occlusionResultMap;
            RTHandle typeIn = currentInTemp ? passData.morphTypeTemp : passData.originTypeMap;
            RTHandle colorOut = currentInTemp ? passData.occlusionResultMap : passData.morphColorTemp;
            RTHandle typeOut = currentInTemp ? passData.originTypeMap : passData.morphTypeTemp;

            // モルフォロジーピラミッドの構築（L1〜L6）
            BuildMorphPyramid(cmd, cs, passData, sw, sh, colorIn, typeIn);

            // 実際の膨張/収縮カーネルを実行
            cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphColorIn, colorIn);
            cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphTypeIn, typeIn);
            cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphColorOut_RW, colorOut);
            cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphTypeOut_RW, typeOut);
            cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.FinalNeighborhoodSizeMap, passData.settings.enableGradientCorrection ? passData.correctedNeighborhoodSizeMap : passData.neighborhoodSizeMap);

            // ピラミッドテクスチャ（タイプ）をバインド
            cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphTypePyramidL1, passData.morphTypePyramidL1);
            cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphTypePyramidL2, passData.morphTypePyramidL2);
            cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphTypePyramidL3, passData.morphTypePyramidL3);
            cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphTypePyramidL4, passData.morphTypePyramidL4);
            cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphTypePyramidL5, passData.morphTypePyramidL5);
            cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphTypePyramidL6, passData.morphTypePyramidL6);

            // ピラミッドテクスチャ（カラー）をバインド
            cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphColorPyramidL1, passData.morphColorPyramidL1);
            cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphColorPyramidL2, passData.morphColorPyramidL2);
            cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphColorPyramidL3, passData.morphColorPyramidL3);
            cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphColorPyramidL4, passData.morphColorPyramidL4);
            cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphColorPyramidL5, passData.morphColorPyramidL5);
            cmd.SetComputeTextureParam(cs, kernelId, ShaderIDs.MorphColorPyramidL6, passData.morphColorPyramidL6);

            cmd.DispatchCompute(cs, kernelId, threadGroupsX, threadGroupsY, 1);
            currentInTemp = !currentInTemp;
        };

        // --- 指定された手法に応じてモルフォロジー演算を適用 ---
        if (passData.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.Morphology_OC)
        {
            // Opening (Erode → Dilate) then Closing (Dilate → Erode)
            for (int i = 0; i < passData.settings.morphErodeIterations; i++) runMorphPass(passData.kernelMorphologyErode, "Erode");
            for (int i = 0; i < passData.settings.morphDilateIterations; i++) runMorphPass(passData.kernelMorphologyDilate, "Dilate");
            for (int i = 0; i < passData.settings.morphDilateIterations; i++) runMorphPass(passData.kernelMorphologyDilate, "Dilate");
            for (int i = 0; i < passData.settings.morphErodeIterations; i++) runMorphPass(passData.kernelMorphologyErode, "Erode");
        }
        else if (passData.settings.holeFillingMethod == PCDRendererFeature.PCD_HoleFillingMethod.Morphology_CO)
        {
            // Closing (Dilate → Erode) then Opening (Erode → Dilate)
            for (int i = 0; i < passData.settings.morphDilateIterations; i++) runMorphPass(passData.kernelMorphologyDilate, "Dilate");
            for (int i = 0; i < passData.settings.morphErodeIterations; i++) runMorphPass(passData.kernelMorphologyErode, "Erode");
            for (int i = 0; i < passData.settings.morphErodeIterations; i++) runMorphPass(passData.kernelMorphologyErode, "Erode");
            for (int i = 0; i < passData.settings.morphDilateIterations; i++) runMorphPass(passData.kernelMorphologyDilate, "Dilate");
        }
        else
        {
            // フォールバック（Dilate → Erode の基本パターン）
            for (int i = 0; i < passData.settings.morphDilateIterations; i++) runMorphPass(passData.kernelMorphologyDilate, "Dilate");
            for (int i = 0; i < passData.settings.morphErodeIterations; i++) runMorphPass(passData.kernelMorphologyErode, "Erode");
        }

        // 最終結果が一時バッファに残っている場合、メインバッファにコピーバック
        if (currentInTemp)
        {
            cmd.SetComputeTextureParam(cs, passData.kernelMorphologyCopy, ShaderIDs.MorphColorIn, passData.morphColorTemp);
            cmd.SetComputeTextureParam(cs, passData.kernelMorphologyCopy, ShaderIDs.MorphTypeIn, passData.morphTypeTemp);
            cmd.SetComputeTextureParam(cs, passData.kernelMorphologyCopy, ShaderIDs.MorphColorOut_RW, passData.occlusionResultMap);
            cmd.SetComputeTextureParam(cs, passData.kernelMorphologyCopy, ShaderIDs.MorphTypeOut_RW, passData.originTypeMap);
            cmd.DispatchCompute(cs, passData.kernelMorphologyCopy, threadGroupsX, threadGroupsY, 1);
        }
    }

    /// <summary>
    /// モルフォロジーピラミッド(L1〜L6)を構築する。
    /// 入力のタイプ/カラーマップを段階的に1/2にダウンサンプルし、
    /// 膨張/収縮カーネルが広範囲の構造情報を参照できるようにする。
    /// </summary>
    private void BuildMorphPyramid(CommandBuffer cmd, ComputeShader cs, ComputePassData passData,
        int sw, int sh, RTHandle colorIn, RTHandle typeIn)
    {
        // L1: 入力マップからのダウンサンプル
        int l1_w = Mathf.Max(1, (sw + 1) / 2);
        int l1_h = Mathf.Max(1, (sh + 1) / 2);
        cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL1, ShaderIDs.MorphTypeIn, typeIn);
        cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL1, ShaderIDs.MorphColorIn, colorIn);
        cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL1, ShaderIDs.MorphTypePyramidL1_RW, passData.morphTypePyramidL1);
        cmd.SetComputeTextureParam(cs, passData.kernelBuildMorphPyramidL1, ShaderIDs.MorphColorPyramidL1_RW, passData.morphColorPyramidL1);
        cmd.DispatchCompute(cs, passData.kernelBuildMorphPyramidL1, (l1_w + 7) / 8, (l1_h + 7) / 8, 1);

        // L2〜L6: 各レベルを前レベルからダウンサンプル
        var morphKernels = new[] {
            passData.kernelBuildMorphPyramidL2, passData.kernelBuildMorphPyramidL3,
            passData.kernelBuildMorphPyramidL4, passData.kernelBuildMorphPyramidL5,
            passData.kernelBuildMorphPyramidL6
        };
        var morphTypeInputIDs = new[] { ShaderIDs.MorphTypePyramidL1, ShaderIDs.MorphTypePyramidL2, ShaderIDs.MorphTypePyramidL3, ShaderIDs.MorphTypePyramidL4, ShaderIDs.MorphTypePyramidL5 };
        var morphColorInputIDs = new[] { ShaderIDs.MorphColorPyramidL1, ShaderIDs.MorphColorPyramidL2, ShaderIDs.MorphColorPyramidL3, ShaderIDs.MorphColorPyramidL4, ShaderIDs.MorphColorPyramidL5 };
        var morphTypeOutputIDs = new[] { ShaderIDs.MorphTypePyramidL2_RW, ShaderIDs.MorphTypePyramidL3_RW, ShaderIDs.MorphTypePyramidL4_RW, ShaderIDs.MorphTypePyramidL5_RW, ShaderIDs.MorphTypePyramidL6_RW };
        var morphColorOutputIDs = new[] { ShaderIDs.MorphColorPyramidL2_RW, ShaderIDs.MorphColorPyramidL3_RW, ShaderIDs.MorphColorPyramidL4_RW, ShaderIDs.MorphColorPyramidL5_RW, ShaderIDs.MorphColorPyramidL6_RW };
        var morphTypeInputHandles = new[] { passData.morphTypePyramidL1, passData.morphTypePyramidL2, passData.morphTypePyramidL3, passData.morphTypePyramidL4, passData.morphTypePyramidL5 };
        var morphColorInputHandles = new[] { passData.morphColorPyramidL1, passData.morphColorPyramidL2, passData.morphColorPyramidL3, passData.morphColorPyramidL4, passData.morphColorPyramidL5 };
        var morphTypeOutputHandles = new[] { passData.morphTypePyramidL2, passData.morphTypePyramidL3, passData.morphTypePyramidL4, passData.morphTypePyramidL5, passData.morphTypePyramidL6 };
        var morphColorOutputHandles = new[] { passData.morphColorPyramidL2, passData.morphColorPyramidL3, passData.morphColorPyramidL4, passData.morphColorPyramidL5, passData.morphColorPyramidL6 };

        int prevW = l1_w, prevH = l1_h;
        for (int i = 0; i < morphKernels.Length; i++)
        {
            int curW = Mathf.Max(1, (prevW + 1) / 2);
            int curH = Mathf.Max(1, (prevH + 1) / 2);
            cmd.SetComputeTextureParam(cs, morphKernels[i], morphTypeInputIDs[i], morphTypeInputHandles[i]);
            cmd.SetComputeTextureParam(cs, morphKernels[i], morphColorInputIDs[i], morphColorInputHandles[i]);
            cmd.SetComputeTextureParam(cs, morphKernels[i], morphTypeOutputIDs[i], morphTypeOutputHandles[i]);
            cmd.SetComputeTextureParam(cs, morphKernels[i], morphColorOutputIDs[i], morphColorOutputHandles[i]);
            cmd.DispatchCompute(cs, morphKernels[i], (curW + 7) / 8, (curH + 7) / 8, 1);
            prevW = curW;
            prevH = curH;
        }
    }
}
