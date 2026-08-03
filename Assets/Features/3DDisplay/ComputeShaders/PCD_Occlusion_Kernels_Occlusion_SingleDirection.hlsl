// PCD_Occlusion_Kernels_Occlusion_SingleDirection.hlsl
#ifndef PCD_OCCLUSION_SINGLE_DIRECTION_INCLUDED
#define PCD_OCCLUSION_SINGLE_DIRECTION_INCLUDED

// 単一方向 vs 連続系 vs 離散ヒストグラムのレイヤー構造:
// [事実] 探索範囲内の不透明度を単一のスカラー値 S に集約します。

float ComputeOcclusionValue_SingleDirection(float3 currentPos, float currentPosSq, float invCurrentPosSq, float3 neighborPos)
{
    if (_KernelType == 4) // DepthOnly (深度比較のみ) カーネル
    {
        return 0.0;
    }

    float sq_y = dot(neighborPos, neighborPos);
    float dotP = dot(currentPos, neighborPos);

    if (_KernelType == 0) // Bouchiba 内積カーネル
    {
        float sqLen1 = sq_y - 2.0 * dotP + currentPosSq;
        if (sqLen1 > 0.0001 && sq_y > 0.0001)
        {
            float d = dotP - sq_y;
            // rsqrt(sqLen1 * sq_y) が大きくなりすぎないよう、正規化済み内積 [-1,1] に saturate でクランプ
            float cosTheta = saturate(d * rsqrt(sqLen1 * sq_y) / 2.5);
            float val = 1.0 - cosTheta;
            return val > 0.0 ? max(1e-7, val) : 0.0;
        }
        return 0.0;
    }
    else // Exponential (_KernelType == 1) または Linear (_KernelType == 2) カーネル
    {
        // 浮動小数点誤差で dotP^2 * invCurrentPosSq が sq_y を超えて負になると
        // exp(正値) >> 1 となり occlusionValue がオーバーフローするため max(0.0) でクランプ
        float d_ortho_sq = max(0.0, sq_y - (dotP * dotP * invCurrentPosSq));

        // [Fact] 従来のMode 1 (Single Exponential3D) のみ生Expを許容し、
        // それ以外 (Linearカーネルや、方向分割時のExpカーネル) は 0.0 でクリップする仕様を再現する。
        float val = 1.0 - exp(-_Alpha * d_ortho_sq);
        if (_KernelType == 1)
        {
            return val > 0.0 ? max(1e-7, val) : 0.0;
        }
        else
        {
            return val > 0.0 ? max(1e-7, val) : 0.0;
        }
    }
}

#endif