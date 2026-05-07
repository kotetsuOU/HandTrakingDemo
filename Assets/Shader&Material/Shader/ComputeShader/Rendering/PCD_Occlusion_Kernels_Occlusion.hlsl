#ifndef PCD_OCCLUSION_KERNELS_OCCLUSION_INCLUDED
#define PCD_OCCLUSION_KERNELS_OCCLUSION_INCLUDED

#include "PCD_Occlusion_Kernels_Occlusion_SingleDirection.hlsl"
#include "PCD_Occlusion_Kernels_Occlusion_Discrete3.hlsl"
#include "PCD_Occlusion_Kernels_Occlusion_Discrete6.hlsl"

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
    Discrete3BinResult res3;
    res3.sum0 = res3.sum1 = res3.sum2 = 0.0;
    res3.wSum0 = res3.wSum1 = res3.wSum2 = 0.0;

    // 6方向ビンニング専用の変数
    Discrete6BinResult res6;
    res6.sum0 = res6.sum1 = res6.sum2 = res6.sum3 = res6.sum4 = res6.sum5 = 0.0;
    res6.wSum0 = res6.wSum1 = res6.wSum2 = res6.wSum3 = res6.wSum4 = res6.wSum5 = 0.0;

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

                    // 単一のスカラー値（不透明度）を取得
                    float occlusionValue = ComputeOcclusionValue_SingleDirection((float3)currentPos_h, (float)currentPosSq_h, (float)invCurrentPosSq_h, (float3)neighborPos_h);

                    if (occlusionValue > 0.0)
                    {
                        if (_OcclusionMode == 0 || _OcclusionMode == 1 || _OcclusionMode == 2)
                        {
                            occlusionSum += occlusionValue;
                        }
                        else if (_OcclusionMode == 3 || _OcclusionMode == 4 || _OcclusionMode == 7 || _OcclusionMode == 8)
                        {
                            int dx = searchX - (int)id.x;
                            int dy = searchY - (int)id.y;
                            AccumulateDiscrete3Bin(occlusionValue, dx, dy, res3);
                        }
                        else if (_OcclusionMode == 5 || _OcclusionMode == 6 || _OcclusionMode == 9 || _OcclusionMode == 10)
                        {
                            int dx = searchX - (int)id.x;
                            int dy = searchY - (int)id.y;
                            AccumulateDiscrete6Bin(occlusionValue, dx, dy, res6);
                        }

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
        if (_OcclusionMode == 3 || _OcclusionMode == 4 || _OcclusionMode == 7 || _OcclusionMode == 8)
        {
            // 各方向ごとの独立した平均値を算出
            float avg0 = res3.wSum0 > 0.001 ? res3.sum0 / res3.wSum0 : 1.0;
            float avg1 = res3.wSum1 > 0.001 ? res3.sum1 / res3.wSum1 : 1.0;
            float avg2 = res3.wSum2 > 0.001 ? res3.sum2 / res3.wSum2 : 1.0;

            // 多数決ロジック (AtoZ: M - Majority Voting)
            // 閾値「以上」の場合に遮蔽とみなすよう修正
            int passCount = 0;
            if (avg0 < _OcclusionThreshold)
                passCount++;
            if (avg1 < _OcclusionThreshold)
                passCount++;
            if (avg2 < _OcclusionThreshold)
                passCount++;

            // 可視化用には最も強い遮蔽値(最小値)を出力
            occlusionAverage = min(min(avg0, avg1), avg2);

            if (passCount >= 2)
            {
                alpha = 0.0; // 2方向以上が遮蔽と判定した場合のみ真の遮蔽とする
            }
            else if (_EnableSoftOcclusionFade > 0 && _OcclusionFadeWidth > 1e-4)
            {
                float halfFade = _OcclusionFadeWidth * 0.5;
                float fadeStart = max(0.0, _OcclusionThreshold - halfFade);
                float fadeEnd = min(1.0, _OcclusionThreshold + halfFade);
                alpha = smoothstep(fadeStart, fadeEnd, occlusionAverage);
            }
        }
        else if (_OcclusionMode == 5 || _OcclusionMode == 6 || _OcclusionMode == 9 || _OcclusionMode == 10)
        {
            // 各方向ごとの独立した平均値を算出
            float avg0 = res6.wSum0 > 0.001 ? res6.sum0 / res6.wSum0 : 1.0;
            float avg1 = res6.wSum1 > 0.001 ? res6.sum1 / res6.wSum1 : 1.0;
            float avg2 = res6.wSum2 > 0.001 ? res6.sum2 / res6.wSum2 : 1.0;
            float avg3 = res6.wSum3 > 0.001 ? res6.sum3 / res6.wSum3 : 1.0;
            float avg4 = res6.wSum4 > 0.001 ? res6.sum4 / res6.wSum4 : 1.0;
            float avg5 = res6.wSum5 > 0.001 ? res6.sum5 / res6.wSum5 : 1.0;

            // 多数決ロジック (AtoZ: M - Majority Voting)
            // 可視性(Visibility)が閾値を下回る（＝遮蔽されている）ビンの数をカウント
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
            else if (_EnableSoftOcclusionFade > 0 && _OcclusionFadeWidth > 1e-4)
            {
                float halfFade = _OcclusionFadeWidth * 0.5;
                float fadeStart = max(0.0, _OcclusionThreshold - halfFade);
                float fadeEnd = min(1.0, _OcclusionThreshold + halfFade);
                alpha = smoothstep(fadeStart, fadeEnd, occlusionAverage);
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