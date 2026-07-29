#ifndef PCD_OCCLUSION_DISCRETE_3BINS_INCLUDED
#define PCD_OCCLUSION_DISCRETE_3BINS_INCLUDED

// 空間を3つのビンに分割

struct Discrete3BinResult
{
    float sum0, sum1, sum2;
    float wSum0, wSum1, wSum2;
};

void AccumulateDiscrete3Bin(
    float occlusionValue, int dx, int dy,
    inout Discrete3BinResult result)
{
    float dx_f = (float)dx;
    float dy_f = (float)dy;

    float invSqrt3 = 0.577350269;
    float twoInvSqrt3 = 1.154700538;

    float line0 = dx_f + invSqrt3 * dy_f;
    float line1 = -dx_f + invSqrt3 * dy_f;

    float w0 = 0.0, w1 = 0.0, w2 = 0.0;

    // 三角関数不要の代数セクター判定 (Basis Decomposition / Soft Binning)
    if (dy_f >= 0.0 && line0 >= 0.0)
    {
        w0 = line0;
        w1 = twoInvSqrt3 * dy_f;
    }
    else if (line0 < 0.0 && line1 >= 0.0)
    {
        w1 = line1;
        w2 = -line0;
    }
    else
    {
        w2 = -twoInvSqrt3 * dy_f;
        w0 = -line1;
    }

    // スケール不変な正規化
    float sumW = w0 + w1 + w2 + 1e-5;
    float norm_w0 = w0;
    float norm_w1 = w1;
    float norm_w2 = w2;

    // Soft/Hard Binningの切り替え
    if (_BinningMethod == 0)
    {
        // Soft Binning: 線形分配 (線形補間のような平滑化)
        norm_w0 /= sumW;
        norm_w1 /= sumW;
        norm_w2 /= sumW;
    }
    else
    {
        // Hard Binning: 最大の重みを持つビンに1.0を割り当て、他は0.0 (Winner-takes-all / 扇形領域判定と同義)
        float maxW = max(max(w0, w1), w2);
        norm_w0 = (w0 == maxW) ? 1.0 : 0.0;
        norm_w1 = (w1 == maxW && w0 != maxW) ? 1.0 : 0.0; // 一意にするための排他処理
        norm_w2 = (w2 == maxW && w0 != maxW && w1 != maxW) ? 1.0 : 0.0;
    }

    result.sum0 += occlusionValue * norm_w0;
    result.sum1 += occlusionValue * norm_w1;
    result.sum2 += occlusionValue * norm_w2;

    result.wSum0 += norm_w0;
    result.wSum1 += norm_w1;
    result.wSum2 += norm_w2;
}

#endif