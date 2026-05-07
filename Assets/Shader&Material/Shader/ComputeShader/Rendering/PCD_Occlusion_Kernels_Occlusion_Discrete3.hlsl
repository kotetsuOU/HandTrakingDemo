#ifndef PCD_OCCLUSION_DISCRETE_3BINS_INCLUDED
#define PCD_OCCLUSION_DISCRETE_3BINS_INCLUDED

// [事実（離散ヒストグラム方式 / 3方向分割）] 空間を3つのビンに分割

struct Discrete3BinResult
{
    float sum0, sum1, sum2;
    float wSum0, wSum1, wSum2;
};

void AccumulateDiscrete3Bin(
    float occlusionValue, int dx, int dy,
    inout Discrete3BinResult result)
{
    half dx_h = (half)dx;
    half dy_h = (half)dy;

    half invSqrt3 = 0.57735h;
    half twoInvSqrt3 = 1.15470h;

    half line0 = dx_h + invSqrt3 * dy_h;
    half line1 = -dx_h + invSqrt3 * dy_h;

    half w0 = 0.0h, w1 = 0.0h, w2 = 0.0h;

    // 三角関数不要の代数セクター判定 (Basis Decomposition / Soft Binning)
    if (dy_h >= 0.0h && line0 >= 0.0h)
    {
        w0 = line0;
        w1 = twoInvSqrt3 * dy_h;
    }
    else if (line0 < 0.0h && line1 >= 0.0h)
    {
        w1 = line1;
        w2 = -line0;
    }
    else
    {
        w2 = -twoInvSqrt3 * dy_h;
        w0 = -line1;
    }

    // スケール不変な正規化
    half sumW = w0 + w1 + w2 + 1e-5h;
    half norm_w0 = w0;
    half norm_w1 = w1;
    half norm_w2 = w2;

    // Soft/Hard Binningの切り替え
    if (_OcclusionMode == 3 || _OcclusionMode == 4)
    {
        // Soft Binning: 線形分配 (線形補間のような平滑化)
        norm_w0 /= sumW;
        norm_w1 /= sumW;
        norm_w2 /= sumW;
    }
    else if (_OcclusionMode == 7 || _OcclusionMode == 8)
    {
        // Hard Binning: 最大の重みを持つビンに1.0を割り当て、他は0.0 (Winner-takes-all / 扇形領域判定と同義)
        half maxW = max(max(w0, w1), w2);
        norm_w0 = (w0 == maxW) ? 1.0h : 0.0h;
        norm_w1 = (w1 == maxW && w0 != maxW) ? 1.0h : 0.0h; // 一意にするための排他処理
        norm_w2 = (w2 == maxW && w0 != maxW && w1 != maxW) ? 1.0h : 0.0h;
    }

    result.sum0 += occlusionValue * (float)norm_w0;
    result.sum1 += occlusionValue * (float)norm_w1;
    result.sum2 += occlusionValue * (float)norm_w2;

    result.wSum0 += (float)norm_w0;
    result.wSum1 += (float)norm_w1;
    result.wSum2 += (float)norm_w2;
}

#endif