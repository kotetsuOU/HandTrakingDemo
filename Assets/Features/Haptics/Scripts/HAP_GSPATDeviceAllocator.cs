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

        // 割り当てなし（全デバイスで全クラスタを共有）
        if (!enableDirectionalGrouping || connectedDevices.Count == 0)
        {
            if (debugDisabler != null && connectedDevices.Any(d => debugDisabler.IsDisabled(d.ID)))
            {
                // Disablerで無効化されているデバイスが存在する場合、Groupを使って個別にNullを送る必要がある
                var overrideGroupDict = new GroupDictionary();
                var allDatagram = GenerateDatagram(clusterData, holoAlgorithm, focusIntensityPascal);
                foreach (var dev in connectedDevices)
                {
                    if (debugDisabler.IsDisabled(dev.ID))
                        overrideGroupDict.Add(dev.ID.ToString(), new Null());
                    else
                        overrideGroupDict.Add(dev.ID.ToString(), allDatagram);
                }
                return new Group(dev => connectedDevices[dev.Idx()].ID.ToString(), overrideGroupDict);
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

                // デバイスの正面方向と、クラスタの法線（面から外側に向かうベクトル）の逆方向とのなす角（度）
                float angle = Vector3.Angle(dev.transform.forward, -cData.Cluster.Normal);
                
                if (angle < minAngle)
                {
                    minAngle = angle;
                    bestDevice = dev;
                }

                // しきい値以下（十分に向かい合っている）なら担当デバイスに追加
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

        // 3. GroupDictionary を構築し、各デバイスIDに対応する Datagram を生成
        var groupDict = new GroupDictionary();
        foreach (var dev in connectedDevices)
        {
            if (debugDisabler != null && debugDisabler.IsDisabled(dev.ID))
            {
                // 無効化デバイスには強制的にNullを割り当て
                groupDict.Add(dev.ID.ToString(), new Null());
            }
            else if (deviceAssignments.TryGetValue(dev.ID, out var assignedClusters) && assignedClusters.Count > 0)
            {
                var datagram = GenerateDatagram(assignedClusters, holoAlgorithm, focusIntensityPascal);
                groupDict.Add(dev.ID.ToString(), datagram);
            }
            else
            {
                // 担当する接触点がない場合は出力なし
                groupDict.Add(dev.ID.ToString(), new Null());
            }
        }

        // Keyは文字列として設定
        return new Group(dev => connectedDevices[dev.Idx()].ID.ToString(), groupDict);
    }

    /// <summary>
    /// 割り当てられたクラスタ群の焦点データから、AUTDに送信する単一のDatagramを生成します。
    /// </summary>
    private static IDatagram GenerateDatagram(List<HAP_FociGenerator.ClusterFociData> clusterData, HoloAlgorithm holoAlgorithm, float focusIntensityPascal)
    {
        if (clusterData.Count == 0) return new Null();

        bool useSTM = clusterData.Any(c => c.UseSTM);

        if (useSTM)
        {
            // STMの最大フレーム数を取得
            int maxSamples = clusterData.Max(c => c.UseSTM ? c.STMFrames.Count : 1);
            var mergedFrames = new List<ControlPoints>();
            
            // FociSTM用のアンプリチュード（0〜255）
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
            
            // ループ周波数 (仮置きとして 150Hz) 
            // ※将来的には HAP_HapticsSource 等から取得できるのが理想
            return new FociSTM(mergedFrames, 150f * Hz);
        }
        else
        {
            // Sequential Foci をマージして GSPAT または Naive として出力
            var mergedFoci = new List<(AUTD3Sharp.Utils.Point3, Amplitude)>();
            foreach (var cData in clusterData)
            {
                mergedFoci.AddRange(cData.SequentialFoci);
            }

            if (holoAlgorithm == HoloAlgorithm.GSPAT)
                return new GSPAT(mergedFoci.ToArray(), new GSPATOption());
            else
                return new Naive(mergedFoci.ToArray(), new NaiveOption());
        }
    }
}
