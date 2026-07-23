// PCD_Occlusion_Kernels_Occlusion_Discrete8.hlsl
#ifndef PCD_OCCLUSION_DISCRETE_8BINS_INCLUDED
#define PCD_OCCLUSION_DISCRETE_8BINS_INCLUDED

// 空間を8つのビンに分割

struct Discrete8BinResult
{
    float sum0, sum1, sum2, sum3, sum4, sum5, sum6, sum7;
    float wSum0, wSum1, wSum2, wSum3, wSum4, wSum5, wSum6, wSum7;
};

void AccumulateDiscrete8Bin(
    float occlusionValue, int dx, int dy,
    inout Discrete8BinResult result)
{
    half x = (half) dx;
    half y = (half) dy;

    half sqrt2 = 1.41421h;

    half w0 = 0.0h, w1 = 0.0h, w2 = 0.0h, w3 = 0.0h, w4 = 0.0h, w5 = 0.0h, w6 = 0.0h, w7 = 0.0h;

    // Algebraic sector determination without trigonometric functions (Basis Decomposition)
    if (x >= 0.0h)
    {
        if (y >= 0.0h)
        {
            if (x >= y)
            { // 0 - 45 degrees
                w0 = x - y;
                w1 = sqrt2 * y;
            }
            else
            { // 45 - 90 degrees
                w1 = sqrt2 * x;
                w2 = y - x;
            }
        }
        else // y < 0
        {
            if (x >= -y)
            { // 315 - 360 degrees
                w7 = -sqrt2 * y;
                w0 = x + y;
            }
            else
            { // 270 - 315 degrees
                w6 = -y - x;
                w7 = sqrt2 * x;
            }
        }
    }
    else // x < 0
    {
        if (y >= 0.0h)
        {
            if (-x >= y)
            { // 135 - 180 degrees
                w3 = sqrt2 * y;
                w4 = -x - y;
            }
            else
            { // 90 - 135 degrees
                w2 = y + x;
                w3 = -sqrt2 * x;
            }
        }
        else // y < 0
        {
            if (-x >= -y)
            { // 180 - 225 degrees
                w4 = -x + y;
                w5 = -sqrt2 * y;
            }
            else
            { // 225 - 270 degrees
                w5 = -sqrt2 * x;
                w6 = -y + x;
            }
        }
    }

    half sumW = w0 + w1 + w2 + w3 + w4 + w5 + w6 + w7 + 1e-5h;
    half n_w0 = w0, n_w1 = w1, n_w2 = w2, n_w3 = w3;
    half n_w4 = w4, n_w5 = w5, n_w6 = w6, n_w7 = w7;

    // Switch between Soft/Hard Binning using the newly separated variables
    // Assuming _BinningMethod is defined in a common constant buffer (0: Soft, 1: Hard)
    if (_BinningMethod == 0) // Soft Binning
    {
        n_w0 /= sumW;
        n_w1 /= sumW;
        n_w2 /= sumW;
        n_w3 /= sumW;
        n_w4 /= sumW;
        n_w5 /= sumW;
        n_w6 /= sumW;
        n_w7 /= sumW;
    }
    else // Hard Binning (_BinningMethod == 1)
    {
        half maxW1 = max(max(w0, w1), max(w2, w3));
        half maxW2 = max(max(w4, w5), max(w6, w7));
        half maxW = max(maxW1, maxW2);

        n_w0 = (w0 == maxW) ? 1.0h : 0.0h;
        n_w1 = (w1 == maxW && w0 != maxW) ? 1.0h : 0.0h;
        n_w2 = (w2 == maxW && w0 != maxW && w1 != maxW) ? 1.0h : 0.0h;
        n_w3 = (w3 == maxW && w0 != maxW && w1 != maxW && w2 != maxW) ? 1.0h : 0.0h;
        n_w4 = (w4 == maxW && w0 != maxW && w1 != maxW && w2 != maxW && w3 != maxW) ? 1.0h : 0.0h;
        n_w5 = (w5 == maxW && w0 != maxW && w1 != maxW && w2 != maxW && w3 != maxW && w4 != maxW) ? 1.0h : 0.0h;
        n_w6 = (w6 == maxW && w0 != maxW && w1 != maxW && w2 != maxW && w3 != maxW && w4 != maxW && w5 != maxW) ? 1.0h : 0.0h;
        n_w7 = (w7 == maxW && w0 != maxW && w1 != maxW && w2 != maxW && w3 != maxW && w4 != maxW && w5 != maxW && w6 != maxW) ? 1.0h : 0.0h;
    }

    result.sum0 += occlusionValue * (float) n_w0;
    result.wSum0 += (float) n_w0;
    result.sum1 += occlusionValue * (float) n_w1;
    result.wSum1 += (float) n_w1;
    result.sum2 += occlusionValue * (float) n_w2;
    result.wSum2 += (float) n_w2;
    result.sum3 += occlusionValue * (float) n_w3;
    result.wSum3 += (float) n_w3;
    result.sum4 += occlusionValue * (float) n_w4;
    result.wSum4 += (float) n_w4;
    result.sum5 += occlusionValue * (float) n_w5;
    result.wSum5 += (float) n_w5;
    result.sum6 += occlusionValue * (float) n_w6;
    result.wSum6 += (float) n_w6;
    result.sum7 += occlusionValue * (float) n_w7;
    result.wSum7 += (float) n_w7;
}

#endif