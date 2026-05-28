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
    half dx_h = (half)dx;
    half dy_h = (half)dy;

    half invSqrt3 = 0.57735h;
    half twoInvSqrt3 = 1.15470h;

    half A = dx_h;
    half B = dy_h * invSqrt3;
    half C = dy_h * twoInvSqrt3;

    half w0=0.0h, w1=0.0h, w2=0.0h, w3=0.0h, w4=0.0h, w5=0.0h;

    // 三角関数不要の6セクター代数判定 (Soft Binning)
    if (dy_h >= 0.0h)
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

    half sumW = w0 + w1 + w2 + w3 + w4 + w5 + 1e-5h;
    half n_w0 = w0; half n_w1 = w1; half n_w2 = w2;
    half n_w3 = w3; half n_w4 = w4; half n_w5 = w5;

    // Soft/Hard Binningの切り替え
    if (_BinningMethod == 0)
    {
        n_w0 /= sumW; n_w1 /= sumW; n_w2 /= sumW;
        n_w3 /= sumW; n_w4 /= sumW; n_w5 /= sumW;
    }
    else
    {
        half maxW = max(max(max(w0, w1), max(w2, w3)), max(w4, w5));
        n_w0 = (w0 == maxW) ? 1.0h : 0.0h;
        n_w1 = (w1 == maxW && w0 != maxW) ? 1.0h : 0.0h;
        n_w2 = (w2 == maxW && w0 != maxW && w1 != maxW) ? 1.0h : 0.0h;
        n_w3 = (w3 == maxW && w0 != maxW && w1 != maxW && w2 != maxW) ? 1.0h : 0.0h;
        n_w4 = (w4 == maxW && w0 != maxW && w1 != maxW && w2 != maxW && w3 != maxW) ? 1.0h : 0.0h;
        n_w5 = (w5 == maxW && w0 != maxW && w1 != maxW && w2 != maxW && w3 != maxW && w4 != maxW) ? 1.0h : 0.0h;
    }

    result.sum0 += occlusionValue * (float)n_w0; result.wSum0 += (float)n_w0;
    result.sum1 += occlusionValue * (float)n_w1; result.wSum1 += (float)n_w1;
    result.sum2 += occlusionValue * (float)n_w2; result.wSum2 += (float)n_w2;
    result.sum3 += occlusionValue * (float)n_w3; result.wSum3 += (float)n_w3;
    result.sum4 += occlusionValue * (float)n_w4; result.wSum4 += (float)n_w4;
    result.sum5 += occlusionValue * (float)n_w5; result.wSum5 += (float)n_w5;
}

#endif