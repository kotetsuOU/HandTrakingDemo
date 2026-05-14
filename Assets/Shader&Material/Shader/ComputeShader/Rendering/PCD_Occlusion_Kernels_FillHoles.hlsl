#ifndef PCD_OCCLUSION_KERNELS_FILLHOLES_INCLUDED
#define PCD_OCCLUSION_KERNELS_FILLHOLES_INCLUDED

// -----------------------------------------------------------------------------
// モルフォロジー演算カーネル群
// Opening（収縮→膨張）でノイズを除去したあと、
// Closing（膨張→収縮）で疎な点群の隙間を埋める。
//
// 処理順（C# Execute 側から呼ばれる）:
//   各 Morph 操作の前: CopyOriginType（ping-pong スナップショット作成）
//   Opening:  CopyOriginType → MorphErode × N  →  CopyOriginType → MorphDilate + CopyBack × N
//   Closing:  CopyOriginType → MorphDilate + CopyBack × M  →  CopyOriginType → MorphErode × M
//   仕上げ:   FillHoles × 1（バイラテラル細緻化）
//
// SRV/UAV エイリアシング対策:
//   MorphDilate / MorphErode は _MorphOriginMapPing（ping コピー）から読み、
//   _OriginTypeMap_RW（main）へ書く。CopyOriginType が毎回スナップショットを作成する。
// -----------------------------------------------------------------------------

// CopyOriginType
// MorphDilate / MorphErode / FillHoles の各 Dispatch 直前に呼ぶ ping-pong スナップショット。
// OriginTypeMap と DepthMap の両方を ping テクスチャへコピーし、
// 同一 Dispatch 内の SRV/UAV エイリアシングを回避する。
[numthreads(8, 8, 1)]
void CopyOriginType(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= (uint) _ScreenParams.x || id.y >= (uint) _ScreenParams.y)
        return;
    _MorphOriginMapPing_RW[id.xy] = _OriginTypeMap_RW[id.xy];
    _MorphDepthMapPing_RW[id.xy]  = _DepthMap_RW[id.xy];
}

// CopyBack
// MorphDilate が OcclusionResultMap へ書き込んだ色を ColorMap へ転写し、
// sentinel 3u を 0u（点群由来）にリセットする。
// MorphDilate の各イテレーション後に必ず呼ぶことで、
// 次イテレーションが新たに膨張したピクセルを正しいソースとして読める。
[numthreads(8, 8, 1)]
void CopyBack(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= (uint) _ScreenParams.x || id.y >= (uint) _ScreenParams.y)
        return;

    if (_OriginTypeMap_RW[id.xy] != 3u)
        return;

    _ColorMap_RW[id.xy] = _OcclusionResultMap_RW[id.xy];
    _OriginTypeMap_RW[id.xy] = 0u;
}

// MorphDilate（膨張）
// cv2.dilate 相当。点群領域 (originType == 0) を _MorphKernelHalfSize px 外側へ拡張し、
// 最近傍点群ピクセルの色・深度を伝播させる。
// 読み取りは _MorphOriginMapPing（ping コピー）から行い SRV/UAV 競合を防ぐ。
// 書き込み先は OcclusionResultMap（ColorMap との競合を避けるため）。
// 呼び出し後に必ず CopyBack を Dispatch すること。
//
// [Fix] 仮想オブジェクト (selfType == 1u) へは膨張しない。
// [Fix] 仮想深度より奥の点群色は伝播しない。
[numthreads(8, 8, 1)]
void MorphDilate(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= (uint) _ScreenParams.x || id.y >= (uint) _ScreenParams.y)
        return;

    // ping コピーから読む（SRV/UAV エイリアシング回避）
    uint selfType = _MorphOriginMapPing[id.xy];

    // 点群・sentinel・仮想オブジェクトはスキップ
    // （1u = 仮想物体: 手色を上書きしない）
    if (selfType == 0u || selfType == 3u || selfType == 1u)
        return;

    // 仮想深度チェック: 仮想オブジェクトより奥への膨張を禁止
    uint thresholdDepth = DEPTH_MAX_UINT;
    if (_UseVirtualDepth > 0)
    {
        float vDepthRaw = _VirtualDepthMap[id.xy];
        float vDepth = _IsReversedZ > 0 ? (1.0 - vDepthRaw) : vDepthRaw;
        if (vDepth < 0.9999)
            thresholdDepth = (uint) (vDepth * (float) DEPTH_MAX_UINT);
    }

    int2 minBound = max(int2(0, 0), (int2) id.xy - _MorphKernelHalfSize);
    int2 maxBound = min((int2) _ScreenParams.xy - 1, (int2) id.xy + _MorphKernelHalfSize);

    float4 bestColor = float4(0, 0, 0, 0);
    uint   bestDepth = DEPTH_MAX_UINT;
    bool   found     = false;

    for (int y = minBound.y; y <= maxBound.y; y++)
    {
        for (int x = minBound.x; x <= maxBound.x; x++)
        {
            uint2 uv = uint2(x, y);
            if (_MorphOriginMapPing[uv] == 0u)
            {
                // [Fix] 深度は ping コピーから読む（DepthMap の SRV/UAV 競合を回避）
                uint d = _MorphDepthMapPing[uv];
                if (d < thresholdDepth && (!found || d < bestDepth))
                {
                    bestDepth = d;
                    bestColor = _ColorMap[uv];
                    found = true;
                }
            }
        }
    }

    if (found)
    {
        _OcclusionResultMap_RW[id.xy] = bestColor;
        _DepthMap_RW[id.xy]           = bestDepth;
        _OriginTypeMap_RW[id.xy]      = 3u; // sentinel: CopyBack 待ち
    }
}

// MorphErode（収縮）
// cv2.erode 相当。点群領域 (originType == 0) の境界ピクセル（穴に隣接する）を
// 穴（1u）に戻すことで、孤立ノイズや細いトゲを除去する。
// 読み取りは _MorphOriginMapPing（ping コピー）から行い SRV/UAV 競合を防ぐ。
//
// [Fix] 収縮時に ColorMap / OcclusionResultMap / DepthMap もクリアする。
//       これにより後段 Interpolate が収縮済みピクセルの残留色を表示しなくなる。
[numthreads(8, 8, 1)]
void MorphErode(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= (uint) _ScreenParams.x || id.y >= (uint) _ScreenParams.y)
        return;

    // ping コピーから読む（SRV/UAV エイリアシング回避）
    if (_MorphOriginMapPing[id.xy] != 0u)
        return;

    int2 minBound = max(int2(0, 0), (int2) id.xy - _MorphKernelHalfSize);
    int2 maxBound = min((int2) _ScreenParams.xy - 1, (int2) id.xy + _MorphKernelHalfSize);

    for (int y = minBound.y; y <= maxBound.y; y++)
    {
        for (int x = minBound.x; x <= maxBound.x; x++)
        {
            uint nType = _MorphOriginMapPing[uint2(x, y)];
            // [Fix] 1u（仮想物体）は穴扱いしない。
            // MorphDilate が 1u へ膨張しないのと対称にし、仮想物体境界の手ピクセルを保護する。
            if (nType != 0u && nType != 3u && nType != 1u)
            {
                _OriginTypeMap_RW[id.xy]      = 1u;
                _ColorMap_RW[id.xy]           = float4(0, 0, 0, 0);
                _OcclusionResultMap_RW[id.xy] = float4(0, 0, 0, 0);
                _DepthMap_RW[id.xy]           = DEPTH_MAX_UINT;
                return;
            }
        }
    }
}

// 10. Fill Holes
// 点群が描画されなかったピクセル（深度がないピクセル）を対象としたジョイントバイラテラルライクの穴埋め
[numthreads(8, 8, 1)]
void FillHoles(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= (uint) _ScreenParams.x || id.y >= (uint) _ScreenParams.y)
        return;
    
    // 既に点群としてオクルージョン計算済みのピクセルはスキップ
    uint originType = _MorphOriginMapPing[id.xy];
    if (originType == 0u)
        return;

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
            if (_MorphOriginMapPing[uv] == 0u)
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

                    uint nType = _MorphOriginMapPing[uv];
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

#endif // PCD_OCCLUSION_KERNELS_FILLHOLES_INCLUDED
