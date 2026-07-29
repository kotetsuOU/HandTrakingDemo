#ifndef PCD_OCCLUSION_DISCRETE_6BINS_INCLUDED
#define PCD_OCCLUSION_DISCRETE_6BINS_INCLUDED

// 空間を6つのビンに分割

struct Discrete6BinResult
{
    float sum0, sum1, sum2, sum3, sum4, sum5;
    float wSum0, wSum1, wSum2, wSum3, wSum4, wSum5;
};

void AccumulateDiscrete6Bin(
    float occlusionValue, int dx, int dy,
    inout Discrete6BinResult result)
{
    float dx_f = (float)dx;
    float dy_f = (float)dy;

    float invSqrt3 = 0.577350269;
    float twoInvSqrt3 = 1.154700538;

    float A = dx_f;
    float B = dy_f * invSqrt3;
    float C = dy_f * twoInvSqrt3;

    float w0=0.0, w1=0.0, w2=0.0, w3=0.0, w4=0.0, w5=0.0;

    // 三角関数不要の6セクター代数判定 (Soft Binning)
    if (dy_f >= 0.0)
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

    float sumW = w0 + w1 + w2 + w3 + w4 + w5 + 1e-5;
    float n_w0 = w0; float n_w1 = w1; float n_w2 = w2;
    float n_w3 = w3; float n_w4 = w4; float n_w5 = w5;

    // Soft/Hard Binningの切り替え
    if (_BinningMethod == 0)
    {
        n_w0 /= sumW; n_w1 /= sumW; n_w2 /= sumW;
        n_w3 /= sumW; n_w4 /= sumW; n_w5 /= sumW;
    }
    else
    {
        float maxW = max(max(max(w0, w1), max(w2, w3)), max(w4, w5));
        n_w0 = (w0 == maxW) ? 1.0 : 0.0;
        n_w1 = (w1 == maxW && w0 != maxW) ? 1.0 : 0.0;
        n_w2 = (w2 == maxW && w0 != maxW && w1 != maxW) ? 1.0 : 0.0;
        n_w3 = (w3 == maxW && w0 != maxW && w1 != maxW && w2 != maxW) ? 1.0 : 0.0;
        n_w4 = (w4 == maxW && w0 != maxW && w1 != maxW && w2 != maxW && w3 != maxW) ? 1.0 : 0.0;
        n_w5 = (w5 == maxW && w0 != maxW && w1 != maxW && w2 != maxW && w3 != maxW && w4 != maxW) ? 1.0 : 0.0;
    }

    result.sum0 += occlusionValue * n_w0; result.wSum0 += n_w0;
    result.sum1 += occlusionValue * n_w1; result.wSum1 += n_w1;
    result.sum2 += occlusionValue * n_w2; result.wSum2 += n_w2;
    result.sum3 += occlusionValue * n_w3; result.wSum3 += n_w3;
    result.sum4 += occlusionValue * n_w4; result.wSum4 += n_w4;
    result.sum5 += occlusionValue * n_w5; result.wSum5 += n_w5;
}

#endif