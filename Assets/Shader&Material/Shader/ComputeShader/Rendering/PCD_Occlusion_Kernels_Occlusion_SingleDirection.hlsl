#ifndef PCD_OCCLUSION_SINGLE_DIRECTION_INCLUDED
#define PCD_OCCLUSION_SINGLE_DIRECTION_INCLUDED

// 単一方向 vs 連続系 vs 離散ヒストグラムのレイヤー構造:
// [事実（単一方向方式）] 探索範囲内の不透明度を単一のスカラー値 S に集約します。

float ComputeOcclusionValue_SingleDirection(float3 currentPos_h, float currentPosSq_h, float invCurrentPosSq_h, float3 neighborPos_h)
{
    half sq_y_h = dot((half3)neighborPos_h, (half3)neighborPos_h);
    half dotP_h = dot((half3)currentPos_h, (half3)neighborPos_h);

    if (_OcclusionMode == 0 || _OcclusionMode == 3 || _OcclusionMode == 5 || _OcclusionMode == 7 || _OcclusionMode == 9) // Bouchiba 内積カーネル
    {
        half sqLen1_h = sq_y_h - 2.0h * dotP_h + (half)currentPosSq_h;
        if (sqLen1_h > 0.0001h && sq_y_h > 0.0001h)
        {
            half d_h = dotP_h - sq_y_h;
            return max(0.0, 1.0 - (float)(d_h * rsqrt(sqLen1_h * sq_y_h) / 2.5h));
        }
        return 0.0;
    }
    else // Exp カーネル (Mode 1, 2, 4, 6, 8, 10 等)
    {
        half d_ortho_sq = sq_y_h - (dotP_h * dotP_h * (half)invCurrentPosSq_h);
        // Mode 1 は生Exp、それ以外（Soft/HardやLinear）は 0.0でクリップ
        if (_OcclusionMode == 1)
            return 1.0 - (float)exp(-(half)_Alpha * d_ortho_sq);
        else
            return max(0.0, 1.0 - (float)exp(-(half)_Alpha * d_ortho_sq)); // Expカーネル共通
    }
}

#endif