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
    /// ノイズの更新モード
    /// </summary>
    public enum NoiseUpdateMode
    {
        Dynamic, // 毎フレーム新しいノイズパターンを更新生成 (動的に揺れる)
        Static   // 最初に生成された固定ノイズパターンを維持 (一度決めたら固定)
    }

    /// <summary>
    /// ダミー点群に対するノイズおよび外れ値の生成パラメーター
    /// </summary>
    [Serializable]
    public class RsPointCloudNoiseSettings
    {
        [Header("Update Mode")]
        [Tooltip("Dynamic: フレームごとにノイズが動的に変化 / Static: 最初に生成された固定ノイズパターンを維持")]
        public NoiseUpdateMode updateMode = NoiseUpdateMode.Dynamic;

        [Header("Normal Direction Noise")]
        [Tooltip("True にするとメッシュ法線方向へのノイズ移動を有効化します")]
        public bool enableNoise = false;

        [Tooltip("法線方向への移動ノイズ量 (mm)")]
        [Range(0f, 50f)]
        public float noiseAmountMm = 2.0f;

        [Tooltip("全点群に対するノイズの発生割合 (0.01 = 1%, 1.0 = 100%)")]
        [Range(0f, 1f)]
        public float noiseRatio = 1.0f;

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
        private struct FixedOffset
        {
            public bool isOutlier;
            public bool isNoise;
            public float normalOffset;        // 法線方向への乗算距離 (m)
            public float outlierSign;          // 法線方向外れ値時の符号 (-1 or 1)
            public Vector3 outlierRandomDir;   // 全方向ランダム外れ値時の単位ベクトル
            public float outlierDistance;      // 外れ値の移動距離 (m)
        }

        private Vector3[] _processedPositionsCache = new Vector3[0];
        private FixedOffset[] _fixedOffsetsCache = new FixedOffset[0];
        private System.Random _random = new System.Random(42);

        private int _lastPointCount = -1;
        private bool _lastEnableNoise;
        private float _lastNoiseAmountMm;
        private float _lastNoiseRatio;
        private NoiseDistributionType _lastNoiseType;
        private bool _lastEnableOutliers;
        private float _lastOutlierRatio;
        private float _lastOutlierDistanceMm;
        private bool _lastOutlierUseRandomDirection;
        private NoiseUpdateMode _lastUpdateMode;

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

            if (_processedPositionsCache.Length < pointCount)
            {
                _processedPositionsCache = new Vector3[pointCount];
            }

            float noiseAmountMeters = settings.noiseAmountMm * 0.001f;
            float outlierDistanceMeters = settings.outlierDistanceMm * 0.001f;
            bool hasNormals = normals != null && normals.Length >= pointCount;

            // Static モード時の固定オフセットキャッシュ構築判定
            if (settings.updateMode == NoiseUpdateMode.Static)
            {
                bool isDirty = _fixedOffsetsCache.Length < pointCount ||
                               _lastPointCount != pointCount ||
                               _lastUpdateMode != settings.updateMode ||
                               _lastEnableNoise != settings.enableNoise ||
                               !Mathf.Approximately(_lastNoiseAmountMm, settings.noiseAmountMm) ||
                               !Mathf.Approximately(_lastNoiseRatio, settings.noiseRatio) ||
                               _lastNoiseType != settings.noiseType ||
                               _lastEnableOutliers != settings.enableOutliers ||
                               !Mathf.Approximately(_lastOutlierRatio, settings.outlierRatio) ||
                               !Mathf.Approximately(_lastOutlierDistanceMm, settings.outlierDistanceMm) ||
                               _lastOutlierUseRandomDirection != settings.outlierUseRandomDirection;

                if (isDirty)
                {
                    RebuildStaticOffsets(pointCount, settings);

                    _lastPointCount = pointCount;
                    _lastUpdateMode = settings.updateMode;
                    _lastEnableNoise = settings.enableNoise;
                    _lastNoiseAmountMm = settings.noiseAmountMm;
                    _lastNoiseRatio = settings.noiseRatio;
                    _lastNoiseType = settings.noiseType;
                    _lastEnableOutliers = settings.enableOutliers;
                    _lastOutlierRatio = settings.outlierRatio;
                    _lastOutlierDistanceMm = settings.outlierDistanceMm;
                    _lastOutlierUseRandomDirection = settings.outlierUseRandomDirection;
                }

                // キャッシュされた固定パラメータを使って座標を計算
                for (int i = 0; i < pointCount; i++)
                {
                    Vector3 pos = originalPositions[i];
                    Vector3 normal = hasNormals ? normals[i] : Vector3.up;
                    ref var offset = ref _fixedOffsetsCache[i];

                    if (offset.isOutlier)
                    {
                        Vector3 dir = settings.outlierUseRandomDirection ? offset.outlierRandomDir : (normal * offset.outlierSign);
                        pos += dir * offset.outlierDistance;
                    }
                    else if (settings.enableNoise && offset.isNoise)
                    {
                        pos += normal * offset.normalOffset;
                    }

                    _processedPositionsCache[i] = pos;
                }
            }
            else
            {
                // Dynamic モード: 毎フレーム乱数で動的に位置を計算
                for (int i = 0; i < pointCount; i++)
                {
                    Vector3 pos = originalPositions[i];
                    Vector3 normal = hasNormals ? normals[i] : Vector3.up;

                    bool isOutlier = settings.enableOutliers && (_random.NextDouble() < settings.outlierRatio);

                    if (isOutlier)
                    {
                        Vector3 dir;
                        if (settings.outlierUseRandomDirection)
                        {
                            dir = UnityEngine.Random.onUnitSphere;
                        }
                        else
                        {
                            float sign = (_random.NextDouble() < 0.5) ? -1f : 1f;
                            dir = normal * sign;
                        }

                        float dist = outlierDistanceMeters * (0.8f + (float)_random.NextDouble() * 0.4f);
                        pos += dir * dist;
                    }
                    else if (settings.enableNoise && noiseAmountMeters > 0f && (_random.NextDouble() < settings.noiseRatio))
                    {
                        float offset;
                        if (settings.noiseType == NoiseDistributionType.Gaussian)
                        {
                            offset = GenerateGaussianNoise(0f, noiseAmountMeters);
                        }
                        else
                        {
                            offset = ((float)_random.NextDouble() * 2f - 1f) * noiseAmountMeters;
                        }

                        pos += normal * offset;
                    }

                    _processedPositionsCache[i] = pos;
                }
            }

            Vector3[] result = new Vector3[pointCount];
            Array.Copy(_processedPositionsCache, 0, result, 0, pointCount);
            return result;
        }

        private void RebuildStaticOffsets(int pointCount, RsPointCloudNoiseSettings settings)
        {
            if (_fixedOffsetsCache.Length < pointCount)
            {
                _fixedOffsetsCache = new FixedOffset[pointCount];
            }

            float noiseAmountMeters = settings.noiseAmountMm * 0.001f;
            float outlierDistanceMeters = settings.outlierDistanceMm * 0.001f;

            // 再現性のある決定的な乱数シードを使用
            System.Random staticRand = new System.Random(12345);

            for (int i = 0; i < pointCount; i++)
            {
                FixedOffset offset = new FixedOffset();

                offset.isOutlier = settings.enableOutliers && (staticRand.NextDouble() < settings.outlierRatio);

                if (offset.isOutlier)
                {
                    offset.outlierSign = (staticRand.NextDouble() < 0.5) ? -1f : 1f;

                    // 全方向ランダム用の一様乱数単位ベクトル
                    float u = (float)staticRand.NextDouble();
                    float v = (float)staticRand.NextDouble();
                    float theta = u * 2.0f * Mathf.PI;
                    float phi = Mathf.Acos(2.0f * v - 1.0f);
                    float sinPhi = Mathf.Sin(phi);
                    offset.outlierRandomDir = new Vector3(
                        sinPhi * Mathf.Cos(theta),
                        sinPhi * Mathf.Sin(theta),
                        Mathf.Cos(phi)
                    );

                    offset.outlierDistance = outlierDistanceMeters * (0.8f + (float)staticRand.NextDouble() * 0.4f);
                }
                else
                {
                    offset.isNoise = settings.enableNoise && noiseAmountMeters > 0f && (staticRand.NextDouble() < settings.noiseRatio);
                    if (offset.isNoise)
                    {
                        if (settings.noiseType == NoiseDistributionType.Gaussian)
                        {
                            double u1 = 1.0 - staticRand.NextDouble();
                            double u2 = 1.0 - staticRand.NextDouble();
                            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
                            offset.normalOffset = (float)randStdNormal * noiseAmountMeters;
                        }
                        else
                        {
                            offset.normalOffset = ((float)staticRand.NextDouble() * 2f - 1f) * noiseAmountMeters;
                        }
                    }
                }

                _fixedOffsetsCache[i] = offset;
            }
        }

        /// <summary>
        /// Box-Muller 変換により平均 mean, 標準偏差 stdDev のガウス（正規）分布乱数を生成します。
        /// </summary>
        private float GenerateGaussianNoise(float mean, float stdDev)
        {
            double u1 = 1.0 - _random.NextDouble();
            double u2 = 1.0 - _random.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return mean + stdDev * (float)randStdNormal;
        }
    }
}
