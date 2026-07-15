#if USE_AUTD3_LEGACY
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
/// 謗･隗ｦ轤ｹ縺ｮ豕慕ｷ夲ｼ亥髄縺搾ｼ峨→繝・ヰ繧､繧ｹ縺ｮ蜷代″繧呈ｯ碑ｼ・＠縲・
/// 譛驕ｩ縺ｪAUTD繝・ヰ繧､繧ｹ縺ｫGSPAT遲峨・蜃ｺ蜉帙ョ繝ｼ繧ｿ・・atagram・峨ｒ蜑ｲ繧雁ｽ薙※繧九け繝ｩ繧ｹ縺ｧ縺吶・
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

        // 蜑ｲ繧雁ｽ薙※縺ｪ縺暦ｼ亥・繝・ヰ繧､繧ｹ縺ｧ蜈ｨ繧ｯ繝ｩ繧ｹ繧ｿ繧貞・譛会ｼ・
        if (!enableDirectionalGrouping || connectedDevices.Count == 0)
        {
            if (debugDisabler != null && connectedDevices.Any(d => debugDisabler.IsDisabled(d.ID)))
            {
                // Disabler縺ｧ辟｡蜉ｹ蛹悶＆繧後※縺・ｋ繝・ヰ繧､繧ｹ縺悟ｭ伜惠縺吶ｋ蝣ｴ蜷医；roup繧剃ｽｿ縺｣縺ｦ蛟句挨縺ｫNull繧帝√ｋ蠢・ｦ√′縺ゅｋ
                var overrideGroupDict = new GroupDictionary();
                var allDatagram = GenerateDatagram(clusterData, holoAlgorithm, focusIntensityPascal);
                foreach (var dev in connectedDevices)
                {
                    if (debugDisabler.IsDisabled(dev.ID))
                        overrideGroupDict.Add(dev.ID.ToString(), new Null());
                    else
                        overrideGroupDict.Add(dev.ID.ToString(), allDatagram);
                }
                string[] deviceIds = new string[connectedDevices.Count];
                for (int i = 0; i < connectedDevices.Count; i++) {
                    deviceIds[i] = connectedDevices[i] != null ? connectedDevices[i].ID.ToString() : "null";
                }

                return new Group(dev => 
                {
                    int idx = dev.Idx();
                    if (idx < 0 || idx >= deviceIds.Length) return "null";
                    return deviceIds[idx];
                }, overrideGroupDict);
            }

            return GenerateDatagram(clusterData, holoAlgorithm, focusIntensityPascal);
        }

        // 1. 蜷・ョ繝舌う繧ｹ縺梧球蠖薙☆繧九け繝ｩ繧ｹ繧ｿ縺ｮ繝ｪ繧ｹ繝医ｒ蛻晄悄蛹・
        var deviceAssignments = new Dictionary<int, List<HAP_FociGenerator.ClusterFociData>>();
        foreach (var dev in connectedDevices)
        {
            deviceAssignments[dev.ID] = new List<HAP_FociGenerator.ClusterFociData>();
        }

        // 2. 繧ｯ繝ｩ繧ｹ繧ｿ縺斐→縺ｫ譛驕ｩ縺ｪ繝・ヰ繧､繧ｹ繧貞愛螳壹＠縺ｦ蜑ｲ繧雁ｽ薙※
        foreach (var cData in clusterData)
        {
            bool isAssigned = false;
            float minAngle = float.MaxValue;
            AUTD3Device? bestDevice = null;

            foreach (var dev in connectedDevices)
            {
                // 辟｡蜉ｹ蛹悶＆繧後※縺・ｋ繝・ヰ繧､繧ｹ縺ｯ諡・ｽ薙ョ繝舌う繧ｹ縺ｮ蛟呵｣懊°繧蛾勁螟・
                if (debugDisabler != null && debugDisabler.IsDisabled(dev.ID)) continue;

                // 繝・ヰ繧､繧ｹ縺ｮ豁｣髱｢譁ｹ蜷代→縲√け繝ｩ繧ｹ繧ｿ縺ｮ豕慕ｷ夲ｼ磯擇縺九ｉ螟門・縺ｫ蜷代°縺・・繧ｯ繝医Ν・峨・騾・婿蜷代→縺ｮ縺ｪ縺呵ｧ抵ｼ亥ｺｦ・・
                float angle = Vector3.Angle(dev.transform.forward, -cData.Cluster.Normal);
                
                if (angle < minAngle)
                {
                    minAngle = angle;
                    bestDevice = dev;
                }

                // 縺励″縺・､莉･荳具ｼ亥香蛻・↓蜷代°縺・粋縺｣縺ｦ縺・ｋ・峨↑繧画球蠖薙ョ繝舌う繧ｹ縺ｫ霑ｽ蜉�
                if (angle <= directionalAngleThreshold)
                {
                    deviceAssignments[dev.ID].Add(cData);
                    isAssigned = true;
                }
            }

            // 縺ｩ縺ｮ繝・ヰ繧､繧ｹ縺ｮ縺励″縺・､繧よｺ縺溘＆縺ｪ縺九▲縺溷�ｴ蜷医∵怙繧よｭ｣髱｢縺ｫ霑代＞繝・ヰ繧､繧ｹ縺ｫ蠑ｷ蛻ｶ蜑ｲ繧雁ｽ薙※
            if (!isAssigned && bestDevice != null)
            {
                deviceAssignments[bestDevice.ID].Add(cData);
            }
        }

        // 3. GroupDictionary 繧呈ｧ狗ｯ峨＠縲∝推繝・ヰ繧､繧ｹID縺ｫ蟇ｾ蠢懊☆繧・Datagram 繧堤函謌・
        var groupDict = new GroupDictionary();
        foreach (var dev in connectedDevices)
        {
            if (debugDisabler != null && debugDisabler.IsDisabled(dev.ID))
            {
                // 辟｡蜉ｹ蛹悶ョ繝舌う繧ｹ縺ｫ縺ｯ蠑ｷ蛻ｶ逧・↓Null繧貞牡繧雁ｽ薙※
                groupDict.Add(dev.ID.ToString(), new Null());
            }
            else if (deviceAssignments.TryGetValue(dev.ID, out var assignedClusters) && assignedClusters.Count > 0)
            {
                var datagram = GenerateDatagram(assignedClusters, holoAlgorithm, focusIntensityPascal);
                groupDict.Add(dev.ID.ToString(), datagram);
            }
            else
            {
                // 諡・ｽ薙☆繧区磁隗ｦ轤ｹ縺後↑縺・�ｴ蜷医・蜃ｺ蜉帙↑縺・
                groupDict.Add(dev.ID.ToString(), new Null());
            }
        }

        // Key縺ｯ譁・ｭ怜・縺ｨ縺励※險ｭ螳・
        string[] devIds = new string[connectedDevices.Count];
        for (int i = 0; i < connectedDevices.Count; i++) {
            devIds[i] = connectedDevices[i] != null ? connectedDevices[i].ID.ToString() : "null";
        }

        return new Group(dev => 
        {
            int idx = dev.Idx();
            if (idx < 0 || idx >= devIds.Length) return "null";
            return devIds[idx];
        }, groupDict);
    }

    /// <summary>
    /// 蜑ｲ繧雁ｽ薙※繧峨ｌ縺溘け繝ｩ繧ｹ繧ｿ鄒､縺ｮ辟ｦ轤ｹ繝・・繧ｿ縺九ｉ縲、UTD縺ｫ騾∽ｿ｡縺吶ｋ蜊倅ｸ縺ｮDatagram繧堤函謌舌＠縺ｾ縺吶・
    /// </summary>
    private static IDatagram GenerateDatagram(List<HAP_FociGenerator.ClusterFociData> clusterData, HoloAlgorithm holoAlgorithm, float focusIntensityPascal)
    {
        if (clusterData.Count == 0) return new Null();

        bool useSTM = clusterData.Any(c => c.UseSTM);

        if (useSTM)
        {
            // STM縺ｮ譛螟ｧ繝輔Ξ繝ｼ繝�謨ｰ繧貞叙蠕・
            int maxSamples = clusterData.Max(c => c.UseSTM ? c.STMFrames.Count : 1);
            var mergedFrames = new List<ControlPoints>();
            
            // FociSTM逕ｨ縺ｮ繧｢繝ｳ繝励Μ繝√Η繝ｼ繝会ｼ・縲・55・・
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
                        // 髱朶TM縺ｮ繧ｯ繝ｩ繧ｹ繧ｿ縺ｯ蜷後§SequentialFoci繧呈ｯ弱ヵ繝ｬ繝ｼ繝�驥阪・繧・
                        foreach (var sf in cData.SequentialFoci)
                        {
                            points.Add(new ControlPoint(sf.Item1));
                        }
                    }
                }
                mergedFrames.Add(new ControlPoints(points.ToArray(), intensity));
            }
            
            // 繝ｫ繝ｼ繝怜捉豕｢謨ｰ (莉ｮ鄂ｮ縺阪→縺励※ 150Hz) 
            // 窶ｻ蟆・擂逧・↓縺ｯ HAP_HapticsSource 遲峨°繧牙叙蠕励〒縺阪ｋ縺ｮ縺檎炊諠ｳ
            return new FociSTM(mergedFrames, 150f * Hz);
        }
        else
        {
            // Sequential Foci 繧偵・繝ｼ繧ｸ縺励※ GSPAT 縺ｾ縺溘・ Naive 縺ｨ縺励※蜃ｺ蜉・
            var mergedFoci = new List<(AUTD3Sharp.Utils.Point3, Amplitude)>();
            foreach (var cData in clusterData)
            {
                mergedFoci.AddRange(cData.SequentialFoci);
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

        bool useSTM = clusterData.Any(c => c.UseSTM);

        if (useSTM)
        {
            // STMの最大フレーム数を取得
            int maxSamples = clusterData.Max(c => c.UseSTM ? c.STMFrames.Count : 1);
            
            if (holoAlgorithm == HoloAlgorithm.GSPAT || holoAlgorithm == HoloAlgorithm.Custom)
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

                return new PatternStm(150f * Hz, patterns);
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

                return new FociStm(150f * Hz, controlPointsList.ToArray());
            }
        }
        else
        {
            // 静的出力 (Pattern)
            var mergedFoci = new List<HoloCP>();
            foreach (var cData in clusterData)
            {
                mergedFoci.AddRange(cData.SequentialFoci);
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
            if (holoAlgorithm == HoloAlgorithm.GSPAT || holoAlgorithm == HoloAlgorithm.Custom)
            {
                var option = new GspatOption(repeat: 100, constraint: null, directivity: Directivity.Sphere, backend: default, mask: mask);
                AUTD3.Holo.Holo.Gspat(geometry, mergedFoci.ToArray(), wavelength, option, buffer);
            }
            else
            {
                var option = new NaiveOption(constraint: null, directivity: Directivity.Sphere, backend: default, mask: mask);
                AUTD3.Holo.Holo.Naive(geometry, mergedFoci.ToArray(), wavelength, option, buffer);
            }

            return new Pattern(PatternBank.B0, buffer);
        }
    }
}

#endif

