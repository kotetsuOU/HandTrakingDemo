#if USE_AUTD3_LEGACY
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AUTD3Sharp;
using AUTD3Sharp.Gain;
using AUTD3Sharp.Gain.Holo;
using AUTD3Sharp.Driver.Datagram;
using static AUTD3Sharp.Units;

#nullable enable

/// <summary>
/// 接触点の法線（向き）とデバイスの向きを比較し、
/// 最適なAUTDデバイスにGSPAT等の出力データ（Datagram）を割り当てるクラスです。
/// </summary>
public static class HAP_GSPATDeviceAllocator
{
    public static IDatagram Allocate(
        List<HAP_FociGenerator.ClusterFociData> clusterData,
        List<AUTD3Device> connectedDevices,
        HoloAlgorithm holoAlgorithm,
        bool enableDirectionalGrouping,
        float directionalAngleThreshold,
        float focusIntensityPascal,
        HAP_AUTDDebugDisabler? debugDisabler = null)
    {
        if (clusterData.Count == 0) return new Null();

        // 1台しか接続されていない場合は、AUTD3Sharp.Group内部のGCバグを完全に回避するため
        // 直接Datagramを返します。
        if (connectedDevices.Count == 1)
        {
            if (debugDisabler != null && debugDisabler.IsDisabled(connectedDevices[0].ID))
            {
                return new Null();
            }
            return GenerateDatagram(clusterData, holoAlgorithm, focusIntensityPascal);
        }

        // 割り当てなし（全デバイスで全クラスタを共有）
        if (!enableDirectionalGrouping || connectedDevices.Count == 0)
        {
            // debugDisabler でグループ制御が必要な場合は Group を使う
            if (debugDisabler != null && connectedDevices.Any(d => debugDisabler.IsDisabled(d.ID)))
            {
                return BuildGroup(connectedDevices, (devArrayIdx) =>
                {
                    var dev = connectedDevices[devArrayIdx];
                    if (debugDisabler.IsDisabled(dev.ID)) return new Null();
                    return GenerateDatagram(clusterData, holoAlgorithm, focusIntensityPascal);
                });
            }

            return GenerateDatagram(clusterData, holoAlgorithm, focusIntensityPascal);
        }

        // 1. 各デバイスが担当するクラスタのリストを初期化
        var deviceAssignments = new Dictionary<int, List<HAP_FociGenerator.ClusterFociData>>();
        foreach (var dev in connectedDevices)
        {
            deviceAssignments[dev.ID] = new List<HAP_FociGenerator.ClusterFociData>();
        }

        // 2. クラスタごとに最適なデバイスを判定して割り当て
        foreach (var cData in clusterData)
        {
            bool isAssigned = false;
            float minAngle = float.MaxValue;
            AUTD3Device? bestDevice = null;

            foreach (var dev in connectedDevices)
            {
                // 無効化されているデバイスは担当デバイスの候補から除外
                if (debugDisabler != null && debugDisabler.IsDisabled(dev.ID)) continue;

                // デバイスの正面方向と、クラスタの法線（面から外側に向かうベクトル）とのなす角（度）
                float angle = Vector3.Angle(dev.transform.forward, -cData.Cluster.Normal);
                
                if (angle < minAngle)
                {
                    minAngle = angle;
                    bestDevice = dev;
                }

                // しきい値以下（十分に正面に向かっている）なら担当デバイスに追加
                if (angle <= directionalAngleThreshold)
                {
                    deviceAssignments[dev.ID].Add(cData);
                    isAssigned = true;
                }
            }

            // どのデバイスのしきい値も満たさなかった場合、最も正面に近いデバイスに強制割り当て
            if (!isAssigned && bestDevice != null)
            {
                deviceAssignments[bestDevice.ID].Add(cData);
            }
        }

        // 3. BuildGroup を使って Group を安全に構築
        return BuildGroup(connectedDevices, (devArrayIdx) =>
        {
            var dev = connectedDevices[devArrayIdx];

            if (debugDisabler != null && debugDisabler.IsDisabled(dev.ID))
            {
                // 無効化デバイスには強制的にNullを割り当て
                return new Null();
            }
            else if (deviceAssignments.TryGetValue(dev.ID, out var assignedClusters) && assignedClusters.Count > 0)
            {
                return GenerateDatagram(assignedClusters, holoAlgorithm, focusIntensityPascal);
            }
            else
            {
                // 担当する接触点がない場合は Null
                return new Null();
            }
        });
    }

    /// <summary>
    /// AUTD3Sharp.Group を安全に構築するヘルパー。
    /// キーとして connectedDevices の配列インデックス（0, 1, 2...）を使用し、
    /// dev.Idx() との 1:1 対応を保証することで KeyNotFoundException を防ぎます。
    /// datagrams は GC に収集されないよう GroupDictionary 内で参照を保持します。
    /// </summary>
    private static IDatagram BuildGroup(
        List<AUTD3Device> connectedDevices,
        Func<int, IDatagram> datagrams)
    {
        var groupDict = new GroupDictionary();
        for (int i = 0; i < connectedDevices.Count; i++)
        {
            groupDict.Add(i, datagrams(i));
        }

        int maxIdx = connectedDevices.Count;
        return new Group(dev =>
        {
            int idx = dev.Idx();
            if (idx < 0 || idx >= maxIdx)
            {
                UnityEngine.Debug.LogError($"[BuildGroup] idx={idx} is OUT OF RANGE [0,{maxIdx}). Skipping device.");
                return null;
            }
            return (object)idx;
        }, groupDict);
    }

    /// <summary>
    /// 割り当てられたクラスタ群の焦点データから、AUTDに送信する単一のDatagramを生成します。
    /// </summary>

    private static IDatagram GenerateDatagram(List<HAP_FociGenerator.ClusterFociData> clusterData, HoloAlgorithm holoAlgorithm, float focusIntensityPascal)
    {
        if (clusterData.Count == 0) return new Null();

        bool useSTM = clusterData.Any(c => c.UseSTM && c.STMFrames != null && c.STMFrames.Count > 1);

        if (useSTM)
        {
            // STMの最大フレーム数を取得
            int maxSamples = clusterData.Max(c => c.UseSTM ? c.STMFrames.Count : 1);
            float stmFreq = clusterData.First(c => c.UseSTM).STMFrequency;
            bool isGainStm = clusterData.Any(c => c.IsGainSTM);

            if (isGainStm && (holoAlgorithm == HoloAlgorithm.GSPAT || holoAlgorithm == HoloAlgorithm.Custom))
            {
                // GainSTM (PC計算による複数焦点STM) の生成
                var gains = new List<IGain>();
                
                for (int i = 0; i < maxSamples; i++)
                {
                    var activeFoci = new List<(AUTD3Sharp.Utils.Point3, Amplitude)>();
                    foreach (var cData in clusterData)
                    {
                        if (cData.UseSTM && i < cData.STMFrames.Count)
                        {
                            foreach (var p in cData.STMFrames[i])
                            {
                                activeFoci.Add((new AUTD3Sharp.Utils.Point3(p.x, p.y, p.z), focusIntensityPascal * Pa));
                            }
                        }
                        else
                        {
                            foreach (var sf in cData.SequentialFoci)
                            {
                                activeFoci.Add(sf);
                            }
                        }
                    }
                    gains.Add(new GSPAT(activeFoci.ToArray(), new GSPATOption()));
                }
                
                return new GainSTM(gains, stmFreq * Hz, new GainSTMOption()).IntoNearest();
            }
            else
            {
                // 従来のFociSTM (ハードウェア計算による単焦点STM)
                var mergedFrames = new List<ControlPoints>();
                
                // FociSTM用のアンプレティュード（0-255）
                byte intensityVal = (byte)Mathf.Clamp((focusIntensityPascal / 10000f) * 255f, 0, 255);
                var intensity = new Intensity(intensityVal);

                for (int i = 0; i < maxSamples; i++)
                {
                    var points = new List<ControlPoint>();
                    foreach (var cData in clusterData)
                    {
                        if (cData.UseSTM && i < cData.STMFrames.Count)
                        {
                            foreach (var p in cData.STMFrames[i])
                            {
                                points.Add(new ControlPoint(new AUTD3Sharp.Utils.Point3(p.x, p.y, p.z)));
                            }
                        }
                        else
                        {
                            // 非STMのクラスタは同じSequentialFociを毎フレーム重ねる
                            foreach (var sf in cData.SequentialFoci)
                            {
                                points.Add(new ControlPoint(sf.Item1));
                            }
                        }
                    }
                    mergedFrames.Add(new ControlPoints(points.ToArray(), intensity));
                }
                
                // 指定された周波数でSTMを生成
                return new FociSTM(mergedFrames, stmFreq * Hz).IntoNearest();
            }
        }
        else
        {
            // Sequential Foci をマージして GSPAT または Naive として出力
            var mergedFoci = new List<(AUTD3Sharp.Utils.Point3, Amplitude)>();
            foreach (var cData in clusterData)
            {
                if (cData.SequentialFoci.Count > 0)
                {
                    mergedFoci.AddRange(cData.SequentialFoci);
                }
                else if (cData.STMFrames != null && cData.STMFrames.Count > 0 && cData.STMFrames[0].Count > 0)
                {
                    foreach (var p in cData.STMFrames[0])
                    {
                        mergedFoci.Add((new AUTD3Sharp.Utils.Point3(p.x, p.y, p.z), focusIntensityPascal * cData.Cluster.Force * Pa));
                    }
                }
            }

            if (mergedFoci.Count == 0)
            {
                mergedFoci.Add((new AUTD3Sharp.Utils.Point3(0, 0, 0), 0f * Pa));
            }
            if (mergedFoci.Count == 1)
            {
                mergedFoci.Add((mergedFoci[0].Item1, 0f * Pa));
            }

            // NaN/Infinity があれば LogError で通知する
            for (int fi = 0; fi < mergedFoci.Count; fi++)
            {
                var (p3, amp) = mergedFoci[fi];
                float ampPa = amp.Pascal();
                bool hasNaN = float.IsNaN(p3.X) || float.IsNaN(p3.Y) || float.IsNaN(p3.Z) || float.IsNaN(ampPa);
                bool hasInf = float.IsInfinity(p3.X) || float.IsInfinity(p3.Y) || float.IsInfinity(p3.Z);
                if (hasNaN || hasInf)
                    UnityEngine.Debug.LogError($"[HAP] INVALID foci[{fi}]: pos=({p3.X},{p3.Y},{p3.Z}) amp={ampPa}Pa NaN={hasNaN} Inf={hasInf}");
            }

            if (holoAlgorithm == HoloAlgorithm.GSPAT || holoAlgorithm == HoloAlgorithm.Custom)
                return new GSPAT(mergedFoci.ToArray(), new GSPATOption());
            else
                return new Naive(mergedFoci.ToArray(), new NaiveOption());
        }
    }
}

#else

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AUTD3;
using AUTD3.Holo;
using static AUTD3.Units;
using HoloCP = AUTD3.Holo.ControlPoint;

#nullable enable

public static class HAP_GSPATDeviceAllocator
{
    public static void Allocate(
        DatagramBuilder builder,
        Geometry geometry,
        List<HAP_FociGenerator.ClusterFociData> clusterData,
        List<AUTD3Device> connectedDevices,
        HoloAlgorithm holoAlgorithm,
        bool enableDirectionalGrouping,
        float directionalAngleThreshold,
        float focusIntensityPascal,
        HAP_AUTDDebugDisabler? debugDisabler = null)
    {
        if (clusterData.Count == 0) return;

        // 1. 各デバイスが担当するクラスタのリストを初期化
        var deviceAssignments = new Dictionary<int, List<HAP_FociGenerator.ClusterFociData>>();
        foreach (var dev in connectedDevices)
        {
            deviceAssignments[dev.ID] = new List<HAP_FociGenerator.ClusterFociData>();
        }

        // 割り当てなし（全デバイスで全クラスタを共有）
        if (!enableDirectionalGrouping || connectedDevices.Count == 0)
        {
            foreach (var dev in connectedDevices)
            {
                if (debugDisabler != null && debugDisabler.IsDisabled(dev.ID)) continue;
                deviceAssignments[dev.ID].AddRange(clusterData);
            }
        }
        else
        {
            // クラスタごとに最適なデバイスを判定して割り当て
            foreach (var cData in clusterData)
            {
                bool isAssigned = false;
                float minAngle = float.MaxValue;
                AUTD3Device? bestDevice = null;

                foreach (var dev in connectedDevices)
                {
                    if (debugDisabler != null && debugDisabler.IsDisabled(dev.ID)) continue;

                    // デバイスの正面方向と、クラスタの法線（面から外側に向かうベクトル）とのなす角（度）
                    float angle = Vector3.Angle(dev.transform.forward, -cData.Cluster.Normal);
                    
                    if (angle < minAngle)
                    {
                        minAngle = angle;
                        bestDevice = dev;
                    }

                    // しきい値以下（十分正面に向かっている）なら担当デバイスに追加
                    if (angle <= directionalAngleThreshold)
                    {
                        deviceAssignments[dev.ID].Add(cData);
                        isAssigned = true;
                    }
                }

                // どのデバイスのしきい値も満たさなかった場合、最も正面に近いデバイスに強制割り当て
                if (!isAssigned && bestDevice != null)
                {
                    deviceAssignments[bestDevice.ID].Add(cData);
                }
            }
        }

        // 2. 各デバイスに対してコマンドを生成し、PushEachでビルダーに追加
        builder.PushEach(deviceIndex =>
        {
            if (deviceIndex < 0 || deviceIndex >= connectedDevices.Count) return null;
            var dev = connectedDevices[deviceIndex];
            if (dev == null) return null;

            if (debugDisabler != null && debugDisabler.IsDisabled(dev.ID)) return null;

            if (deviceAssignments.TryGetValue(dev.ID, out var assignedClusters) && assignedClusters.Count > 0)
            {
                return GenerateDeviceCommand(geometry, deviceIndex, assignedClusters, holoAlgorithm, focusIntensityPascal);
            }

            return null;
        });
    }

    /// <summary>
    /// 割り当てられたクラスタ群の焦点データから、デバイス単位のコマンド（Pattern, FociStm, PatternStm）を生成します。
    /// </summary>
    private static ICommand? GenerateDeviceCommand(
        Geometry geometry,
        int deviceIndex,
        List<HAP_FociGenerator.ClusterFociData> clusterData,
        HoloAlgorithm holoAlgorithm,
        float focusIntensityPascal)
    {
        if (clusterData.Count == 0) return null;

        bool useSTM = clusterData.Any(c => c.UseSTM && c.STMFrames != null && c.STMFrames.Count > 1);

        if (useSTM)
        {
            // STMの最大フレーム数と周波数を取得
            int maxSamples = clusterData.Max(c => c.UseSTM ? c.STMFrames.Count : 1);
            float stmFreq = clusterData.First(c => c.UseSTM).STMFrequency;
            bool isGainStm = clusterData.Any(c => c.IsGainSTM);
            
            if (isGainStm && (holoAlgorithm == HoloAlgorithm.GSPAT || holoAlgorithm == HoloAlgorithm.Custom))
            {
                // GSPAT STM (CPU計算 -> PatternStm)
                var patterns = new PatternBuffer[maxSamples];
                var wavelength = Pattern.Wavelength(Velocity.FromMS(340f));

                // デバイス用マスクの作成
                bool[][] maskArray = new bool[geometry.NumDevices][];
                for (int d = 0; d < geometry.NumDevices; d++)
                {
                    maskArray[d] = new bool[geometry[d].NumTransducers];
                    if (d == deviceIndex)
                    {
                        for (int t = 0; t < maskArray[d].Length; t++) maskArray[d][t] = true;
                    }
                }
                var mask = TransducerMask.Masked(maskArray);
                var option = new GspatOption(repeat: 100, constraint: null, directivity: Directivity.Sphere, backend: default, mask: mask);

                for (int i = 0; i < maxSamples; i++)
                {
                    var activeFoci = new List<HoloCP>();
                    foreach (var cData in clusterData)
                    {
                        if (cData.UseSTM && i < cData.STMFrames.Count)
                        {
                            foreach (var p in cData.STMFrames[i])
                            {
                                activeFoci.Add(new HoloCP(p, Amplitude.FromPascal(focusIntensityPascal)));
                            }
                        }
                        else
                        {
                            foreach (var sf in cData.SequentialFoci)
                            {
                                activeFoci.Add(sf);
                            }
                        }
                    }

                    patterns[i] = geometry.PatternBuffer();
                    AUTD3.Holo.Holo.Gspat(geometry, activeFoci.ToArray(), wavelength, option, patterns[i]);
                }

                return new PatternStm(stmFreq * Hz, patterns).IntoNearest();
            }
            else
            {
                // Naive STM (FPGA計算 -> FociStm)
                var controlPointsList = new List<ControlPoints>();
                byte intensityVal = (byte)Mathf.Clamp((focusIntensityPascal / 10000f) * 255f, 0, 255);
                var intensity = new Intensity(intensityVal);

                for (int i = 0; i < maxSamples; i++)
                {
                    var points = new List<AUTD3.ControlPoint>();
                    foreach (var cData in clusterData)
                    {
                        if (cData.UseSTM && i < cData.STMFrames.Count)
                        {
                            foreach (var p in cData.STMFrames[i])
                            {
                                points.Add(new AUTD3.ControlPoint(p));
                            }
                        }
                        else
                        {
                            foreach (var sf in cData.SequentialFoci)
                            {
                                points.Add(new AUTD3.ControlPoint(sf.Point));
                            }
                        }
                    }
                    controlPointsList.Add(new ControlPoints(points.ToArray(), intensity));
                }

                return new FociStm(stmFreq * Hz, controlPointsList.ToArray()).IntoNearest();
            }
        }
        else
        {
            // 静的出力 (Pattern)
            var mergedFoci = new List<HoloCP>();
            foreach (var cData in clusterData)
            {
                if (cData.SequentialFoci.Count > 0)
                {
                    mergedFoci.AddRange(cData.SequentialFoci);
                }
                else if (cData.STMFrames != null && cData.STMFrames.Count > 0 && cData.STMFrames[0].Count > 0)
                {
                    foreach (var p in cData.STMFrames[0])
                    {
                        mergedFoci.Add(new HoloCP(p, Amplitude.FromPascal(focusIntensityPascal * cData.Cluster.Force)));
                    }
                }
            }

            var wavelength = Pattern.Wavelength(Velocity.FromMS(340f));

            bool[][] maskArray = new bool[geometry.NumDevices][];
            for (int d = 0; d < geometry.NumDevices; d++)
            {
                maskArray[d] = new bool[geometry[d].NumTransducers];
                if (d == deviceIndex)
                {
                    for (int t = 0; t < maskArray[d].Length; t++) maskArray[d][t] = true;
                }
            }
            var mask = TransducerMask.Masked(maskArray);

            var buffer = geometry.PatternBuffer();
            if (mergedFoci.Count == 0)
            {
                mergedFoci.Add(new HoloCP(new Vector3(0, 0, 0), Amplitude.FromPascal(0f)));
            }
            if (mergedFoci.Count == 1)
            {
                mergedFoci.Add(new HoloCP(mergedFoci[0].Point, Amplitude.FromPascal(0f)));
            }

            var option = new GspatOption(repeat: 100, constraint: null, directivity: Directivity.Sphere, backend: default, mask: mask);
            if (holoAlgorithm == HoloAlgorithm.GSPAT || holoAlgorithm == HoloAlgorithm.Custom)
            {
                AUTD3.Holo.Holo.Gspat(geometry, mergedFoci.ToArray(), wavelength, option, buffer);
            }
            else
            {
                var naiveOption = new NaiveOption(constraint: null, directivity: Directivity.Sphere, backend: default, mask: mask);
                AUTD3.Holo.Holo.Naive(geometry, mergedFoci.ToArray(), wavelength, naiveOption, buffer);
            }

            return new Pattern(PatternBank.B0, buffer);
        }
    }
}

#endif

