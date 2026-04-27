#ifndef PCD_OCCLUSION_KERNELS_OCCLUSION_INCLUDED
#define PCD_OCCLUSION_KERNELS_OCCLUSION_INCLUDED

[numthreads(8, 8, 1)]
void ComputeOcclusion(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= (uint) _ScreenParams.x || id.y >= (uint) _ScreenParams.y)
        return;

    uint currentOriginType = _OriginTypeMap_RW[id.xy];
    uint originalCurrentOriginType = currentOriginType;
    bool useTagOptimization = (_EnableTagBasedOptimization > 0);

    // 【新規性①】Tagに基づく探索スキップ
    if (useTagOptimization && currentOriginType != 1u)
    {
        if (_RecordOcclusionDebug > 0)
        {
            if (currentOriginType == 2u)
                _OcclusionValueMap_RW[id.xy] = float2(-1.0, 0.0);
            else if (currentOriginType == 0u)
                _OcclusionValueMap_RW[id.xy] = float2(-3.0, 0.0);
            else
                _OcclusionValueMap_RW[id.xy] = float2(-2.0, 0.0);
        }

        _OcclusionResultMap_RW[id.xy] = _ColorMap[id.xy];
        if (currentOriginType == 0u)
            _OriginMap_RW[id.xy] = float4(0, 0, 0, 1);
        else if (currentOriginType == 1u)
            _OriginMap_RW[id.xy] = float4(1, 1, 1, 1);
        return;
    }

    uint pointDepth_uint = _DepthMap[id.xy];

    float3 currentPos = _ViewPositionMap[id.xy].xyz;
    float currentDepth = _ViewPositionMap[id.xy].w;

    if (!useTagOptimization && currentOriginType == 2u)
    {
        float2 uv = (float2(id.xy) + 0.5) / _ScreenParams.xy;
        float2 ndc = uv * 2.0 - 1.0;
        float farZ = _IsReversedZ > 0 ? 0.001 : 0.999;
        float4 clipPos = float4(ndc.x, ndc.y, farZ, 1.0);
        float4 viewPos = mul(_InverseProjectionMatrix, clipPos);
        currentPos = normalize(viewPos.xyz / viewPos.w) * 100.0;
        currentDepth = 100.0;
    }

    uint vDepth_uint = DEPTH_MAX_UINT;
    bool hasVirtualObj = false;

    if (_UseVirtualDepth > 0)
    {
        float vDepthRaw = _VirtualDepthMap[id.xy];
        float vDepth = _IsReversedZ > 0 ? (1.0 - vDepthRaw) : vDepthRaw;
        if (vDepth < 0.9999)
        {
            hasVirtualObj = true;
            vDepth_uint = (uint) (vDepth * (float) DEPTH_MAX_UINT);
        }
    }

    uint depthBias = DEPTH_MAX_UINT / 1000;
    if (hasVirtualObj && (vDepth_uint + depthBias) < pointDepth_uint)
    {
        if (_RecordOcclusionDebug > 0)
        {
            if (!useTagOptimization && originalCurrentOriginType == 0u)
                _OcclusionValueMap_RW[id.xy] = float2(-2.0, 0.0);
            else
                _OcclusionValueMap_RW[id.xy] = float2(1.0, 0.0);
        }
        _OcclusionResultMap_RW[id.xy] = _ColorMap[id.xy];
        _OriginMap_RW[id.xy] = float4(1, 1, 1, 1);
        _OriginTypeMap_RW[id.xy] = 1u;
        return;
    }

    // --- 【ループ外の事前計算】 (AtoZ: L - Loop-Invariant Code Motion) ---
    half3 currentPos_h = (half3) currentPos;
    half currentDepth_h = (half) currentDepth;
    half currentPosSq_h = dot(currentPos_h, currentPos_h);

    // 除算をループ外で実行し、乗算用の逆数をキャッシュ
    half invCurrentPosSq_h = 1.0h / currentPosSq_h;

    int level = _FinalNeighborhoodSizeMap[id.xy];
    uint radius = max(1u << (uint) max(0, level), 1u);

    float occlusionSum = 0.0;
    uint neighborCount = 0u;

    // Mode 3専用の変数
    float sum0 = 0.0, sum1 = 0.0, sum2 = 0.0;
    float wSum0 = 0.0, wSum1 = 0.0, wSum2 = 0.0;

    // 6方向ビンニング専用の変数
    float sum3 = 0.0, sum4 = 0.0, sum5 = 0.0;
    float wSum3 = 0.0, wSum4 = 0.0, wSum5 = 0.0;

    int2 minBound = max(int2(0, 0), (int2) id.xy - (int) radius);
    int2 maxBound = min((int2) _ScreenParams.xy - 1, (int2) id.xy + (int) radius);

    for (int searchY = minBound.y; searchY <= maxBound.y; searchY++)
    {
        for (int searchX = minBound.x; searchX <= maxBound.x; searchX++)
        {
            uint2 uv = uint2(searchX, searchY);
            uint neighborDepth_uint = _DepthMap[uv];
            uint neighborOriginType = _OriginTypeMap_RW[uv];

            bool isValidNeighbor = useTagOptimization ? (neighborOriginType == 0u) : true;

            float3 neighborPos = _ViewPositionMap[uv].xyz;
            float neighborDepth = _ViewPositionMap[uv].w;

            if (!useTagOptimization && neighborOriginType == 2u)
            {
                float2 nUv = (float2(uv) + 0.5) / _ScreenParams.xy;
                float2 nNdc = nUv * 2.0 - 1.0;
                float nFarZ = _IsReversedZ > 0 ? 0.001 : 0.999;
                float4 nClipPos = float4(nNdc.x, nNdc.y, nFarZ, 1.0);
                float4 nViewPos = mul(_InverseProjectionMatrix, nClipPos);
                neighborPos = normalize(nViewPos.xyz / nViewPos.w) * 100.0;
                neighborDepth = 100.0;
                neighborDepth_uint = (uint) (nFarZ * (float) DEPTH_MAX_UINT);
            }

            if (neighborDepth_uint < DEPTH_MAX_UINT && isValidNeighbor)
            {
                half neighborDepth_h = (half) neighborDepth;
                if (currentDepth_h - neighborDepth_h > 0.01h)
                {
                    half3 neighborPos_h = (half3) neighborPos;

                    if (_OcclusionMode == 0)
                    {
                        // 【既存】Bouchibaの内積型カーネル
                        half sqLen2_h = dot(neighborPos_h, neighborPos_h);
                        half dotP_h = dot(currentPos_h, neighborPos_h);
                        half sqLen1_h = sqLen2_h - 2.0h * dotP_h + currentPosSq_h;

                        if (sqLen1_h > 0.0001h && sqLen2_h > 0.0001h)
                        {
                            half d_h = dotP_h - sqLen2_h;
                            half occlusionValue_h = 1.0h - d_h * rsqrt(sqLen1_h * sqLen2_h) / 2.5h;
                            occlusionSum += (float) occlusionValue_h;
                            neighborCount++;
                        }
                    }
                    else if (_OcclusionMode == 1 || _OcclusionMode == 2)
                    {
                        half sq_y_h = dot(neighborPos_h, neighborPos_h);
                        half dot_xy_h = dot(currentPos_h, neighborPos_h);

                        // ループ内の除算を排除し、逆数の乗算へ置換
                        half d_ortho_sq = sq_y_h - (dot_xy_h * dot_xy_h * invCurrentPosSq_h);

                        half occlusionValue_h = 0.0h;
                        if (_OcclusionMode == 1)
                        {
                            occlusionValue_h = 1.0h - exp(-(half) _Alpha * d_ortho_sq);
                        }
                        else
                        {
                            occlusionValue_h = max(0.0h, 1.0h - ((half) _Alpha * d_ortho_sq));
                        }

                        occlusionSum += (float) occlusionValue_h;
                        neighborCount++;
                    }
                    else if (_OcclusionMode == 3 || _OcclusionMode == 4)
                    {
                        // 【新規】3方向ビンニング
                        half sq_y_h = dot(neighborPos_h, neighborPos_h);
                        half dot_xy_h = dot(currentPos_h, neighborPos_h);

                        half occlusionValue_h = 0.0h;
                        bool processNeighbor = true;

                        if (_OcclusionMode == 3)
                        {
                            // Bouchibaの内積型カーネルを流用
                            half sqLen1_h = sq_y_h - 2.0h * dot_xy_h + currentPosSq_h;
                            if (sqLen1_h > 0.0001h && sq_y_h > 0.0001h)
                            {
                                half d_h = dot_xy_h - sq_y_h;
                                occlusionValue_h = max(0.0h, 1.0h - d_h * rsqrt(sqLen1_h * sq_y_h) / 2.5h);
                            }
                            else
                            {
                                processNeighbor = false;
                            }
                        }
                        else
                        {
                            // expカーネルを利用
                            half d_ortho_sq = sq_y_h - (dot_xy_h * dot_xy_h * invCurrentPosSq_h);
                            occlusionValue_h = max(0.0h, 1.0h - exp(-(half) _Alpha * d_ortho_sq));
                        }

                        if (processNeighbor)
                        {
                            // ピクセル相対座標の取得
                            half dx = (half) (searchX - (int) id.x);
                            half dy = (half) (searchY - (int) id.y);

                            half invSqrt3 = 0.57735h;
                            half twoInvSqrt3 = 1.15470h;

                            half w0 = 0.0h, w1 = 0.0h, w2 = 0.0h;
                            half line0 = dx + invSqrt3 * dy;
                            half line1 = -dx + invSqrt3 * dy;

                            // 三角関数不要の代数セクター判定
                            if (dy >= 0.0h && line0 >= 0.0h)
                            {
                                w0 = line0;
                                w1 = twoInvSqrt3 * dy;
                            }
                            else if (line0 < 0.0h && line1 >= 0.0h)
                            {
                                w1 = line1;
                                w2 = -line0;
                            }
                            else
                            {
                                w2 = -twoInvSqrt3 * dy;
                                w0 = -line1;
                            }

                            half sumW = w0 + w1 + w2 + 1e-5h;
                            half norm_w0 = w0 / sumW;
                            half norm_w1 = w1 / sumW;
                            half norm_w2 = w2 / sumW;

                            sum0 += (float) (occlusionValue_h * norm_w0);
                            sum1 += (float) (occlusionValue_h * norm_w1);
                            sum2 += (float) (occlusionValue_h * norm_w2);

                            wSum0 += (float) norm_w0;
                            wSum1 += (float) norm_w1;
                            wSum2 += (float) norm_w2;

                            neighborCount++;
                        }
                    }
                    else if (_OcclusionMode == 5 || _OcclusionMode == 6)
                    {
                        // 【新規】6方向ビンニング
                        half sq_y_h = dot(neighborPos_h, neighborPos_h);
                        half dot_xy_h = dot(currentPos_h, neighborPos_h);

                        // 直交成分の抽出 (ループ内除算なし)
                        half d_ortho_sq = sq_y_h - (dot_xy_h * dot_xy_h * invCurrentPosSq_h);

                        half occlusionValue_h = 0.0h;
                        if (_OcclusionMode == 5)
                        {
                            // Mode 5: Bouchibaの内積方式
                            half dotP_h = dot_xy_h;
                            half sqLen1_h = sq_y_h - 2.0h * dotP_h + currentPosSq_h;
                            if (sqLen1_h > 0.0001h && sq_y_h > 0.0001h)
                            {
                                half d_h = dotP_h - sq_y_h;
                                occlusionValue_h = 1.0h - d_h * rsqrt(sqLen1_h * sq_y_h) / 2.5h;
                            }
                        }
                        else
                        {
                            // Mode 6: Expカーネル (極性統一: 距離0で0を出力)
                            occlusionValue_h = 1.0h - exp(-(half)_Alpha * d_ortho_sq);
                        }

                        // ピクセル相対座標の取得と定数
                        half dx = (half) (searchX - (int) id.x);
                        half dy = (half) (searchY - (int) id.y);

                        half invSqrt3 = 0.57735h;
                        half twoInvSqrt3 = 1.15470h;

                        half A = dx;
                        half B = dy * invSqrt3;
                        half C = dy * twoInvSqrt3;

                        half w0=0.0h, w1=0.0h, w2=0.0h, w3=0.0h, w4=0.0h, w5=0.0h;

                        // 三角関数不要の6セクター代数判定
                        if (dy >= 0.0h)
                        {
                            if (A >= B) {
                                w0 = A - B; w1 = C;
                            } else if (A >= -B) {
                                w1 = A + B; w2 = -A + B;
                            } else {
                                w2 = C; w3 = -A - B;
                            }
                        }
                        else
                        {
                            if (A <= B) {
                                w3 = -A + B; w4 = -C;
                            } else if (A <= -B) {
                                w4 = -A - B; w5 = A - B;
                            } else {
                                w5 = -C; w0 = A + B;
                            }
                        }

                        // スケール不変な正規化
                        half sumW = w0 + w1 + w2 + w3 + w4 + w5 + 1e-5h;
                        half n_w0 = w0 / sumW; half n_w1 = w1 / sumW; half n_w2 = w2 / sumW;
                        half n_w3 = w3 / sumW; half n_w4 = w4 / sumW; half n_w5 = w5 / sumW;

                        sum0 += (float) (occlusionValue_h * n_w0); wSum0 += (float) n_w0;
                        sum1 += (float) (occlusionValue_h * n_w1); wSum1 += (float) n_w1;
                        sum2 += (float) (occlusionValue_h * n_w2); wSum2 += (float) n_w2;
                        sum3 += (float) (occlusionValue_h * n_w3); wSum3 += (float) n_w3;
                        sum4 += (float) (occlusionValue_h * n_w4); wSum4 += (float) n_w4;
                        sum5 += (float) (occlusionValue_h * n_w5); wSum5 += (float) n_w5;

                        neighborCount++;
                    }
                }
            }
        }
    }

    float alpha = 1.0;
    float occlusionAverage = 1.0;

    if (neighborCount > 0)
    {
        if (_OcclusionMode == 3 || _OcclusionMode == 4)
        {
            // 各方向ごとの独立した平均値を算出
            float avg0 = wSum0 > 0.001 ? sum0 / wSum0 : 0.0;
            float avg1 = wSum1 > 0.001 ? sum1 / wSum1 : 0.0;
            float avg2 = wSum2 > 0.001 ? sum2 / wSum2 : 0.0;

            // 多数決ロジック (AtoZ: M - Majority Voting)
            int passCount = 0;
            if (avg0 < _OcclusionThreshold)
                passCount++;
            if (avg1 < _OcclusionThreshold)
                passCount++;
            if (avg2 < _OcclusionThreshold)
                passCount++;

            // 可視化用には最も強い遮蔽値を出力
            occlusionAverage = max(max(avg0, avg1), avg2);

            if (passCount >= 2)
            {
                alpha = 0.0; // 2方向以上が遮蔽と判定した場合のみ真の遮蔽とする
            }
        }
        else if (_OcclusionMode == 5 || _OcclusionMode == 6)
        {
            // 各方向ごとの独立した平均値を算出
            float avg0 = wSum0 > 0.001 ? sum0 / wSum0 : 1.0;
            float avg1 = wSum1 > 0.001 ? sum1 / wSum1 : 1.0;
            float avg2 = wSum2 > 0.001 ? sum2 / wSum2 : 1.0;
            float avg3 = wSum3 > 0.001 ? sum3 / wSum3 : 1.0;
            float avg4 = wSum4 > 0.001 ? sum4 / wSum4 : 1.0;
            float avg5 = wSum5 > 0.001 ? sum5 / wSum5 : 1.0;

            // 多数決ロジック (AtoZ: M - Majority Voting)
            int passCount = 0;
            if (avg0 < _OcclusionThreshold) passCount++;
            if (avg1 < _OcclusionThreshold) passCount++;
            if (avg2 < _OcclusionThreshold) passCount++;
            if (avg3 < _OcclusionThreshold) passCount++;
            if (avg4 < _OcclusionThreshold) passCount++;
            if (avg5 < _OcclusionThreshold) passCount++;

            // 可視化用には最も強い遮蔽値(最小値)を出力
            occlusionAverage = min(min(min(avg0, avg1), min(avg2, avg3)), min(avg4, avg5));

            int requiredPassCount = 6;
            if (level >= 4)
                requiredPassCount = 3;
            else if (level >= 2)
                requiredPassCount = 4;

            // levelに応じた閾値以上の方向が遮蔽と判定した場合のみ真の遮蔽とする
            if (passCount >= requiredPassCount)
            {
                alpha = 0.0;
            }
        }
        else
        {
            occlusionAverage = occlusionSum / (float) neighborCount;

            // 【新規性②】Soft Occlusion (FadeWidth) のトグル切り替え
            if (_EnableSoftOcclusionFade > 0 && _OcclusionFadeWidth > 1e-4)
            {
                float halfFade = _OcclusionFadeWidth * 0.5;
                float fadeStart = max(0.0, _OcclusionThreshold - halfFade);
                float fadeEnd = min(1.0, _OcclusionThreshold + halfFade);
                alpha = smoothstep(fadeStart, fadeEnd, occlusionAverage);
            }
            else
            {
                // 従来手法: ハードスレッショルド (完全な二値化)
                if (occlusionAverage < _OcclusionThreshold)
                    alpha = 0.0;
            }
        }
    }

    // ★【重要】Soft IoU評価用：狐の色や黒塗りに関係なく、純粋な「マスク値(alpha)」をエクスポートする
    if (_RecordOcclusionDebug > 0)
    {
        _NeighborCountMap_RW[id.xy] = neighborCount;

        if (originalCurrentOriginType == 2u)
        {
            _OcclusionValueMap_RW[id.xy] = float2(-1.0, occlusionAverage);
        }
        else if (!useTagOptimization && originalCurrentOriginType == 0u)
        {
            _OcclusionValueMap_RW[id.xy] = float2((alpha > 0.0) ? -3.0 : -2.0, occlusionAverage);
        }
        else
        {
            _OcclusionValueMap_RW[id.xy] = float2(alpha, occlusionAverage);
        }
    }

    if (alpha <= 0.0)
    {
        // 【新規性③】ジョイントバイラテラル穴埋めのトグル切り替え
        if (_EnableJointBilateralHoleFilling > 0)
        {
            _OcclusionResultMap_RW[id.xy] = float4(0, 0, 0, 1.0);
        }
        else
        {
            _OcclusionResultMap_RW[id.xy] = float4(0, 0, 0, 1.0);
            _OriginTypeMap_RW[id.xy] = (originalCurrentOriginType == 2u) ? 2u : 0u;
        }
    }
    else
    {
        float4 col = _ColorMap[id.xy];
        col.a *= alpha;

        if (useTagOptimization && originalCurrentOriginType == 2u)
            col.a = 1.0;

        _OcclusionResultMap_RW[id.xy] = col;

        if (currentOriginType == 0u)
            _OriginMap_RW[id.xy] = float4(0, 0, 0, 1);
        else if (currentOriginType == 1u)
            _OriginMap_RW[id.xy] = float4(1, 1, 1, 1);
    }
}

#endif // PCD_OCCLUSION_KERNELS_OCCLUSION_INCLUDED