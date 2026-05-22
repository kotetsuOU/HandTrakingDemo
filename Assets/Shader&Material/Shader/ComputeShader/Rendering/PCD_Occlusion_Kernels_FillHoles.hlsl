#ifndef PCD_OCCLUSION_KERNELS_FILLHOLES_INCLUDED
#define PCD_OCCLUSION_KERNELS_FILLHOLES_INCLUDED

// 10. Fill Holes
// 点群が描画されなかったピクセル（深度がないピクセル）を対象としたジョイントバイラテラルライクの穴埋め
[numthreads(8, 8, 1)]
void FillHoles(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= (uint) _ScreenParams.x || id.y >= (uint) _ScreenParams.y)
        return;
    
    uint originType = _OriginTypeMap_RW[id.xy];
    if (originType == 0u)
    {
        // 既に点群としてオクルージョン計算済みのピクセルはスキップ
        return;
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

    int fillRadius = 6;
    float totalWeight = 0.0;
    float4 accumulatedColor = float4(0, 0, 0, 0);
    float weightedOriginSum = 0.0;

    uint thresholdDepth = (hasVirtualObj) ? vDepth_uint : DEPTH_MAX_UINT;

    // --- Pass 1: 最小深度の探索 ---
    uint minDepth = thresholdDepth;

    int2 minBound = max(int2(0, 0), (int2)id.xy - fillRadius);
    int2 maxBound = min((int2)_ScreenParams.xy - 1, (int2)id.xy + fillRadius);

    for (int searchY = minBound.y; searchY <= maxBound.y; searchY++)
    {
        for (int searchX = minBound.x; searchX <= maxBound.x; searchX++)
        {
            uint2 uv = uint2(searchX, searchY);
            if (_OriginTypeMap_RW[uv] == 0u)
            {
                minDepth = min(minDepth, _DepthMap[uv]);
            }
        }
    }

    // --- Pass 2: ジョイントバイラテラル加重平均 ---
    if (minDepth < thresholdDepth)
    {
        uint depthTolerance = (DEPTH_MAX_UINT / 1000) + (uint)((float)minDepth * 0.02);

        for (int searchY = minBound.y; searchY <= maxBound.y; searchY++)
        {
            for (int searchX = minBound.x; searchX <= maxBound.x; searchX++)
            {
                uint2 uv = uint2(searchX, searchY);
                uint nDepth_uint = _DepthMap[uv];

                if (nDepth_uint < thresholdDepth && nDepth_uint >= minDepth && (nDepth_uint - minDepth) <= depthTolerance)
                {
                    float2 offset = float2(searchX - (int)id.x, searchY - (int)id.y);
                    float distSq = dot(offset, offset);
                    float spatialWeight = 1.0 / (1.0 + distSq * 0.5);

                    float depthDiff = (float)(nDepth_uint - minDepth) / (float)depthTolerance;
                    float depthWeight = 1.0 - smoothstep(0.0, 1.0, depthDiff);
                    float weight = spatialWeight * depthWeight;

                    float4 c = _ColorMap[uv];
                    accumulatedColor += c * weight;
                    totalWeight += weight;

                    uint nType = _OriginTypeMap_RW[uv];
                    weightedOriginSum += (float) nType * weight;
                }
            }
        }
    }

    if (totalWeight > 0.01)
    {
        _OcclusionResultMap_RW[id.xy] = accumulatedColor / totalWeight;

        float avgType = weightedOriginSum / totalWeight;
        if (avgType < 0.5)
        {
            _OriginMap_RW[id.xy] = float4(0, 0, 0, 1);
            _OriginTypeMap_RW[id.xy] = 0u;
        }
        else
        {
            _OriginMap_RW[id.xy] = float4(1, 1, 1, 1);
            _OriginTypeMap_RW[id.xy] = 1u;
        }
    }
}

// ==========================================
// Pull-Push Algorithmic Hole Filling
// ==========================================

[numthreads(8, 8, 1)]
void FillHolesPullPushInit(uint3 id : SV_DispatchThreadID)
{
    float w, h;
    _PullPushLevel_Out_RW.GetDimensions(w, h);
    if (id.x >= (uint)w || id.y >= (uint)h) return;

    uint originType = _OriginTypeMap[id.xy];
    float4 color = _OcclusionResultMap[id.xy];
    
    float weight = (originType == 0u) ? 0.0 : 1.0;
    _PullPushLevel_Out_RW[id.xy] = float4(color.rgb * weight, weight);
}

[numthreads(8, 8, 1)]
void FillHolesPull(uint3 id : SV_DispatchThreadID)
{
    float w, h;
    _PullPushLevel_Out_RW.GetDimensions(w, h);
    if (id.x >= (uint)w || id.y >= (uint)h) return;

    uint2 src = id.xy * 2;
    
    float4 c00 = _PullPushLevel_In[src + uint2(0, 0)];
    float4 c10 = _PullPushLevel_In[src + uint2(1, 0)];
    float4 c01 = _PullPushLevel_In[src + uint2(0, 1)];
    float4 c11 = _PullPushLevel_In[src + uint2(1, 1)];
    
    float4 sum = c00 + c10 + c01 + c11;
    _PullPushLevel_Out_RW[id.xy] = sum / 4.0;
}

[numthreads(8, 8, 1)]
void FillHolesPush(uint3 id : SV_DispatchThreadID)
{
    float w, h;
    _PullPushLevel_Out_RW.GetDimensions(w, h);
    if (id.x >= (uint)w || id.y >= (uint)h) return;

    float4 current = _PullPushLevel_Out_RW[id.xy];
    float currentWeight = current.a;
    
    if (currentWeight < 1.0)
    {
        float srcW, srcH;
        _PullPushLevel_In.GetDimensions(srcW, srcH);
        
        float2 uv = (id.xy + 0.5) / float2(w, h);
        float2 srcPos = uv * float2(srcW, srcH) - 0.5;
        
        int2 p00 = max(0, min((int2)srcPos, int2(srcW - 1, srcH - 1)));
        int2 p11 = min(p00 + 1, int2(srcW - 1, srcH - 1));
        
        float2 f = frac(srcPos);
        
        float4 c00 = _PullPushLevel_In[p00];
        float4 c10 = _PullPushLevel_In[int2(p11.x, p00.y)];
        float4 c01 = _PullPushLevel_In[int2(p00.x, p11.y)];
        float4 c11 = _PullPushLevel_In[p11];
        
        float4 interp = lerp(lerp(c00, c10, f.x), lerp(c01, c11, f.x), f.y);
        
        float4 blended = current + (1.0 - currentWeight) * interp;
        _PullPushLevel_Out_RW[id.xy] = blended;
    }
}

[numthreads(8, 8, 1)]
void FillHolesPullPushFinalize(uint3 id : SV_DispatchThreadID)
{
    float w, h;
    _OcclusionResultMap_RW.GetDimensions(w, h);
    if (id.x >= (uint)w || id.y >= (uint)h) return;

    uint originType = _OriginTypeMap[id.xy];
    
    if (originType == 0u)
    {
        float4 pulled = _PullPushLevel_In[id.xy];
        if (pulled.a > 0.0001)
        {
            _OcclusionResultMap_RW[id.xy] = float4(pulled.rgb / pulled.a, 1.0);
            _OriginTypeMap_RW[id.xy] = 0u; 
            _OriginMap_RW[id.xy] = float4(0, 0, 0, 1);
        }
    }
}

// ==========================================
// Morphology (Erode, Dilate, Copy)
// ==========================================

[numthreads(8, 8, 1)]
void MorphologyErode(uint3 id : SV_DispatchThreadID)
{
    float w, h;
    _MorphColorOut_RW.GetDimensions(w, h);
    if (id.x >= (uint)w || id.y >= (uint)h) return;

    uint originType = _MorphTypeIn[id.xy];
    float4 color = _MorphColorIn[id.xy];

    // Erode only affects valid point cloud pixels (type == 0)
    if (originType == 0u)
    {
        bool hasInvalidNeighbor = false;
        int r = _MorphKernelHalfSize;

        for (int y = -r; y <= r; y++)
        {
            for (int x = -r; x <= r; x++)
            {
                int2 uv = (int2)id.xy + int2(x, y);
                if (uv.x >= 0 && uv.x < (int)w && uv.y >= 0 && uv.y < (int)h)
                {
                    if (_MorphTypeIn[uv] != 0u)
                    {
                        hasInvalidNeighbor = true;
                        break;
                    }
                }
                else
                {
                    hasInvalidNeighbor = true;
                    break;
                }
            }
            if (hasInvalidNeighbor) break;
        }

        if (hasInvalidNeighbor)
        {
            originType = 1u;
            color = float4(0, 0, 0, 1);
        }
    }

    _MorphTypeOut_RW[id.xy] = originType;
    _MorphColorOut_RW[id.xy] = color;
}

[numthreads(8, 8, 1)]
void MorphologyDilate(uint3 id : SV_DispatchThreadID)
{
    float w, h;
    _MorphColorOut_RW.GetDimensions(w, h);
    if (id.x >= (uint)w || id.y >= (uint)h) return;

    uint originType = _MorphTypeIn[id.xy];
    float4 color = _MorphColorIn[id.xy];

    // Dilate only affects invalid pixels (type != 0)
    if (originType != 0u)
    {
        float4 sumColor = float4(0, 0, 0, 0);
        float totalWeight = 0.0;
        int r = _MorphKernelHalfSize;

        for (int y = -r; y <= r; y++)
        {
            for (int x = -r; x <= r; x++)
            {
                int2 uv = (int2)id.xy + int2(x, y);
                if (uv.x >= 0 && uv.x < (int)w && uv.y >= 0 && uv.y < (int)h)
                {
                    if (_MorphTypeIn[uv] == 0u)
                    {
                        sumColor += _MorphColorIn[uv];
                        totalWeight += 1.0;
                    }
                }
            }
        }

        if (totalWeight > 0.0)
        {
            originType = 0u;
            color = sumColor / totalWeight;
        }
    }

    _MorphTypeOut_RW[id.xy] = originType;
    _MorphColorOut_RW[id.xy] = color;
}

[numthreads(8, 8, 1)]
void MorphologyCopy(uint3 id : SV_DispatchThreadID)
{
    float w, h;
    _MorphColorOut_RW.GetDimensions(w, h);
    if (id.x >= (uint)w || id.y >= (uint)h) return;

    _MorphColorOut_RW[id.xy] = _MorphColorIn[id.xy];
    _MorphTypeOut_RW[id.xy] = _MorphTypeIn[id.xy];
}

#endif // PCD_OCCLUSION_KERNELS_FILLHOLES_INCLUDED
