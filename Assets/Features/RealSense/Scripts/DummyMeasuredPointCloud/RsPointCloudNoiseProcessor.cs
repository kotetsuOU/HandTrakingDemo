using System;
using UnityEngine;

namespace RealSense.DummyPointCloud
{
    /// <summary>
    /// ノイズの分布タイプ
    /// </summary>
    public enum NoiseDistributionType
    {
        Uniform,  // 一様分布 [-noiseAmount, +noiseAmount]
        Gaussian  // ガウス分布 (標準偏差 sigma = noiseAmount)
    }

    /// <summary>
    /// ダミー点群に対するノイズおよび外れ値の生成パラメーター
    /// </summary>
    [Serializable]
    public class RsPointCloudNoiseSettings
    {
        [Header("Normal Direction Noise")]
        [Tooltip("True にするとメッシュ法線方向へのノイズ移動を有効化します")]
        public bool enableNoise = false;

        [Tooltip("法線方向への移動ノイズ量 (mm)")]
        [Range(0f, 50f)]
        public float noiseAmountMm = 2.0f;

        [Tooltip("ノイズの確率分布パターン")]
        public NoiseDistributionType noiseType = NoiseDistributionType.Gaussian;

        [Header("Outliers Settings")]
        [Tooltip("True にすると外れ値（飛び値）の生成を有効化します")]
        public bool enableOutliers = false;

        [Tooltip("全点群に対する外れ値の発生割合 (0.01 = 1%)")]
        [Range(0f, 0.2f)]
        public float outlierRatio = 0.02f;

        [Tooltip("外れ値の移動距離 (mm)")]
        [Range(1f, 500f)]
        public float outlierDistanceMm = 50.0f;

        [Tooltip("True: 全方向ランダムに飛ばす / False: メッシュ法線方向に飛ばす")]
        public bool outlierUseRandomDirection = false;
    }

    /// <summary>
    /// 点群データに対してメッシュ法線方向のノイズおよび外れ値を付与するプロセッサクラス。
    /// 毎フレームの GC Alloc を防ぐため内部バッファを再利用します。
    /// </summary>
    public class RsPointCloudNoiseProcessor
    {
        private Vector3[] _processedPositionsCache = new Vector3[0];
        private System.Random _random = new System.Random(42);

        /// <summary>
        /// ノイズおよび外れ値を適用した座標配列を取得します。
        /// ノイズ・外れ値ともに無効な場合は元の配列をそのまま返します。
        /// </summary>
        public Vector3[] ProcessPointCloud(Vector3[] originalPositions, Vector3[] normals, int pointCount, RsPointCloudNoiseSettings settings)
        {
            if (originalPositions == null || pointCount <= 0 || settings == null)
            {
                return originalPositions;
            }

            if (!settings.enableNoise && !settings.enableOutliers)
            {
                return originalPositions;
            }

            // 配列サイズの確保
            if (_processedPositionsCache.Length < pointCount)
            {
                _processedPositionsCache = new Vector3[pointCount];
            }

            float noiseAmountMeters = settings.noiseAmountMm * 0.001f;
            float outlierDistanceMeters = settings.outlierDistanceMm * 0.001f;
            bool hasNormals = normals != null && normals.Length >= pointCount;

            for (int i = 0; i < pointCount; i++)
            {
                Vector3 pos = originalPositions[i];
                Vector3 normal = hasNormals ? normals[i] : Vector3.up;

                // 1. 外れ値の判定と適用
                bool isOutlier = settings.enableOutliers && (_random.NextDouble() < settings.outlierRatio);

                if (isOutlier)
                {
                    Vector3 dir;
                    if (settings.outlierUseRandomDirection)
                    {
                        // 球面一様ランダム方向
                        dir = UnityEngine.Random.onUnitSphere;
                    }
                    else
                    {
                        // 法線方向（正負ランダム）
                        float sign = (_random.NextDouble() < 0.5) ? -1f : 1f;
                        dir = normal * sign;
                    }

                    // 指定の離脱距離 + 若干のバラつき
                    float dist = outlierDistanceMeters * (0.8f + (float)_random.NextDouble() * 0.4f);
                    pos += dir * dist;
                }
                // 2. 通常の法線方向ノイズの適用
                else if (settings.enableNoise && noiseAmountMeters > 0f)
                {
                    float offset;
                    if (settings.noiseType == NoiseDistributionType.Gaussian)
                    {
                        // Box-Muller 法による正規分布ノイズ生成 (平均0, 標準偏差 sigma = noiseAmountMeters)
                        offset = GenerateGaussianNoise(0f, noiseAmountMeters);
                    }
                    else
                    {
                        // 一様分布 [-noiseAmountMeters, +noiseAmountMeters]
                        offset = ((float)_random.NextDouble() * 2f - 1f) * noiseAmountMeters;
                    }

                    pos += normal * offset;
                }

                _processedPositionsCache[i] = pos;
            }

            // 元の点数に合わせた切りだし配列またはキャッシュ参照
            Vector3[] result = new Vector3[pointCount];
            Array.Copy(_processedPositionsCache, 0, result, 0, pointCount);
            return result;
        }

        /// <summary>
        /// Box-Muller 変換により平均 mean, 標準偏差 stdDev のガウス（正規）分布乱数を生成します。
        /// </summary>
        private float GenerateGaussianNoise(float mean, float stdDev)
        {
            double u1 = 1.0 - _random.NextDouble(); // (0, 1]
            double u2 = 1.0 - _random.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return mean + stdDev * (float)randStdNormal;
        }
    }
}
