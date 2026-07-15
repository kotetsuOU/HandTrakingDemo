#if !USE_AUTD3_LEGACY
using System.Collections.Generic;
using UnityEngine;
using AUTD3.Holo;
using static AUTD3.Units;

#nullable enable

/// <summary>
/// 接触点（TrackedCluster）と各種設定を受け取り、
/// 空間的な焦点座標（Foci）やSTMフレームのリストを生成する純粋な計算クラスです。
/// </summary>
public static class HAP_FociGenerator
{
    public class ClusterFociData
    {
        public TrackedCluster Cluster;
        public List<AUTD3.Holo.ControlPoint> SequentialFoci = new List<AUTD3.Holo.ControlPoint>();
        public List<List<Vector3>> STMFrames = new List<List<Vector3>>();
        public bool UseSTM;

        public ClusterFociData(TrackedCluster cluster)
        {
            Cluster = cluster;
        }
    }

    /// <summary>
    /// 有効なクラスタ群から、それぞれの焦点データを計算して返します。
    /// </summary>
    public static List<ClusterFociData> Generate(
        List<TrackedCluster> activeClusters,
        HapticsGenerationMode generationMode,
        HAP_HapticsCentroidSource centroidSource,
        HAP_HapticsEllipseSource ellipseSource,
        HAP_HapticsRandomSource randomSource,
        float focusIntensityPascal,
        Vector3 offset)
    {
        var result = new List<ClusterFociData>();

        foreach (var c in activeClusters)
        {
            var data = new ClusterFociData(c);

            // 【Simplified モード】
            if (generationMode == HapticsGenerationMode.Simplified)
            {
                data.SequentialFoci.Add(new AUTD3.Holo.ControlPoint(
                    new Vector3(c.Centroid.x + offset.x, c.Centroid.y + offset.y, c.Centroid.z + offset.z),
                    Amplitude.FromPascal(focusIntensityPascal * c.Force)
                ));
                result.Add(data);
                continue;
            }

            // 【Precision モード】
            bool useStm = ellipseSource.enabled || randomSource.enabled;
            data.UseSTM = useStm;

            if (!useStm)
            {
                // STMを使用せず Centroid だけが有効な場合→静的Holoとして出力
                data.SequentialFoci.Add(new AUTD3.Holo.ControlPoint(
                    new Vector3(c.Centroid.x + offset.x, c.Centroid.y + offset.y, c.Centroid.z + offset.z),
                    Amplitude.FromPascal(centroidSource.CalculateAmplitude(c))
                ));
            }
            else
            {
                // STMサンプルの最大数を決定
                int maxStmSamples = 1;
                if (ellipseSource.enabled && ellipseSource.outputMode == HapticsOutputMode.FociStm)
                    maxStmSamples = Mathf.Max(maxStmSamples, ellipseSource.stmSamplesPerCycle);
                if (randomSource.enabled && randomSource.outputMode == HapticsOutputMode.FociStm)
                    maxStmSamples = Mathf.Max(maxStmSamples, randomSource.stmSamplesPerCycle);

                for (int i = 0; i < maxStmSamples; i++) data.STMFrames.Add(new List<Vector3>());

                // 1. Centroid Source の処理
                if (centroidSource.enabled)
                {
                    data.SequentialFoci.Add(new AUTD3.Holo.ControlPoint(
                        new Vector3(c.Centroid.x + offset.x, c.Centroid.y + offset.y, c.Centroid.z + offset.z),
                        Amplitude.FromPascal(centroidSource.CalculateAmplitude(c))
                    ));
                }

                // 2. Ellipse Source の処理
                if (ellipseSource.enabled)
                {
                    float ellipseAmpScale;
                    var eFrames = ellipseSource.GenerateSTMFrames(c, offset, out ellipseAmpScale);

                    if (ellipseSource.outputMode == HapticsOutputMode.Sequential)
                    {
                        int idx = Time.frameCount % eFrames.Count;
                        foreach (var p in eFrames[idx])
                        {
                            data.SequentialFoci.Add(new AUTD3.Holo.ControlPoint(
                                new Vector3(p.x, p.y, p.z),
                                Amplitude.FromPascal(focusIntensityPascal * c.Force * ellipseAmpScale)
                            ));
                        }
                    }
                    else
                    {
                        for (int i = 0; i < eFrames.Count; i++)
                        {
                            int targetIdx = Mathf.RoundToInt((float)i / eFrames.Count * (maxStmSamples - 1));
                            data.STMFrames[targetIdx].AddRange(eFrames[i]);
                        }
                    }
                }

                // 3. Random Source の処理
                if (randomSource.enabled)
                {
                    var rFrames = randomSource.GenerateSTMFrames(c, offset);
                    if (randomSource.outputMode == HapticsOutputMode.Sequential)
                    {
                        int idx = Time.frameCount % rFrames.Count;
                        foreach (var p in rFrames[idx])
                        {
                            data.SequentialFoci.Add(new AUTD3.Holo.ControlPoint(
                                new Vector3(p.x, p.y, p.z),
                                Amplitude.FromPascal(focusIntensityPascal * c.Force)
                            ));
                        }
                    }
                    else
                    {
                        for (int i = 0; i < rFrames.Count; i++)
                        {
                            int targetIdx = Mathf.RoundToInt((float)i / rFrames.Count * (maxStmSamples - 1));
                            data.STMFrames[targetIdx].AddRange(rFrames[i]);
                        }
                    }
                }

                // STMを使う場合、STMの全フレームに対して Sequential な焦点（Centroidなど）を合成する
                for (int i = 0; i < maxStmSamples; i++)
                {
                    if (data.STMFrames[i].Count == 0)
                        data.STMFrames[i].Add(c.Centroid + offset);

                    foreach (var sf in data.SequentialFoci)
                    {
                        data.STMFrames[i].Add(sf.Point);
                    }
                }
            }

            result.Add(data);
        }

        return result;
    }
}

#else

using System.Collections.Generic;
using UnityEngine;
using AUTD3Sharp.Gain.Holo;
using static AUTD3Sharp.Units;

#nullable enable

/// <summary>
/// 接触点（TrackedCluster）と各種設定を受け取り、
/// 空間的な焦点座標（Foci）やSTMフレームのリストを生成する純粋な計算クラスです。
/// </summary>
public static class HAP_FociGenerator
{
    public class ClusterFociData
    {
        public TrackedCluster Cluster;
        public List<(AUTD3Sharp.Utils.Point3, Amplitude)> SequentialFoci = new List<(AUTD3Sharp.Utils.Point3, Amplitude)>();
        public List<List<Vector3>> STMFrames = new List<List<Vector3>>();
        public bool UseSTM;

        public ClusterFociData(TrackedCluster cluster)
        {
            Cluster = cluster;
        }
    }

    /// <summary>
    /// 有効なクラスタ群から、それぞれの焦点データを計算して返します。
    /// </summary>
    public static List<ClusterFociData> Generate(
        List<TrackedCluster> activeClusters,
        HapticsGenerationMode generationMode,
        HAP_HapticsCentroidSource centroidSource,
        HAP_HapticsEllipseSource ellipseSource,
        HAP_HapticsRandomSource randomSource,
        float focusIntensityPascal,
        Vector3 offset)
    {
        var result = new List<ClusterFociData>();

        foreach (var c in activeClusters)
        {
            var data = new ClusterFociData(c);

            // 【Simplified モード】
            if (generationMode == HapticsGenerationMode.Simplified)
            {
                data.SequentialFoci.Add((
                    new AUTD3Sharp.Utils.Point3(c.Centroid.x + offset.x, c.Centroid.y + offset.y, c.Centroid.z + offset.z),
                    focusIntensityPascal * c.Force * Pa
                ));
                result.Add(data);
                continue;
            }

            // 【Precision モード】
            bool useStm = ellipseSource.enabled || randomSource.enabled;
            data.UseSTM = useStm;

            if (!useStm)
            {
                // STMを使用せず Centroid だけが有効な場合→静的Holoとして出力
                data.SequentialFoci.Add((
                    new AUTD3Sharp.Utils.Point3(c.Centroid.x + offset.x, c.Centroid.y + offset.y, c.Centroid.z + offset.z),
                    centroidSource.CalculateAmplitude(c) * Pa
                ));
            }
            else
            {
                // STMサンプルの最大数を決定
                int maxStmSamples = 1;
                if (ellipseSource.enabled && ellipseSource.outputMode == HapticsOutputMode.FociStm)
                    maxStmSamples = Mathf.Max(maxStmSamples, ellipseSource.stmSamplesPerCycle);
                if (randomSource.enabled && randomSource.outputMode == HapticsOutputMode.FociStm)
                    maxStmSamples = Mathf.Max(maxStmSamples, randomSource.stmSamplesPerCycle);

                for (int i = 0; i < maxStmSamples; i++) data.STMFrames.Add(new List<Vector3>());

                // 1. Centroid Source の処理
                if (centroidSource.enabled)
                {
                    data.SequentialFoci.Add((
                        new AUTD3Sharp.Utils.Point3(c.Centroid.x + offset.x, c.Centroid.y + offset.y, c.Centroid.z + offset.z),
                        centroidSource.CalculateAmplitude(c) * Pa
                    ));
                }

                // 2. Ellipse Source の処理
                if (ellipseSource.enabled)
                {
                    float ellipseAmpScale;
                    var eFrames = ellipseSource.GenerateSTMFrames(c, offset, out ellipseAmpScale);

                    if (ellipseSource.outputMode == HapticsOutputMode.Sequential)
                    {
                        int idx = Time.frameCount % eFrames.Count;
                        foreach (var p in eFrames[idx])
                        {
                            data.SequentialFoci.Add((
                                new AUTD3Sharp.Utils.Point3(p.x, p.y, p.z),
                                focusIntensityPascal * c.Force * ellipseAmpScale * Pa
                            ));
                        }
                    }
                    else
                    {
                        for (int i = 0; i < eFrames.Count; i++)
                        {
                            int targetIdx = Mathf.RoundToInt((float)i / eFrames.Count * (maxStmSamples - 1));
                            data.STMFrames[targetIdx].AddRange(eFrames[i]);
                        }
                    }
                }

                // 3. Random Source の処理
                if (randomSource.enabled)
                {
                    var rFrames = randomSource.GenerateSTMFrames(c, offset);
                    if (randomSource.outputMode == HapticsOutputMode.Sequential)
                    {
                        int idx = Time.frameCount % rFrames.Count;
                        foreach (var p in rFrames[idx])
                        {
                            data.SequentialFoci.Add((
                                new AUTD3Sharp.Utils.Point3(p.x, p.y, p.z),
                                focusIntensityPascal * c.Force * Pa
                            ));
                        }
                    }
                    else
                    {
                        for (int i = 0; i < rFrames.Count; i++)
                        {
                            int targetIdx = Mathf.RoundToInt((float)i / rFrames.Count * (maxStmSamples - 1));
                            data.STMFrames[targetIdx].AddRange(rFrames[i]);
                        }
                    }
                }

                // STMを使う場合、STMの全フレームに対して Sequential な焦点（Centroidなど）を合成する
                for (int i = 0; i < maxStmSamples; i++)
                {
                    if (data.STMFrames[i].Count == 0)
                        data.STMFrames[i].Add(c.Centroid + offset);

                    foreach (var sf in data.SequentialFoci)
                    {
                        data.STMFrames[i].Add(new Vector3(sf.Item1.X, sf.Item1.Y, sf.Item1.Z));
                    }
                }
            }

            result.Add(data);
        }

        return result;
    }
}

#endif
