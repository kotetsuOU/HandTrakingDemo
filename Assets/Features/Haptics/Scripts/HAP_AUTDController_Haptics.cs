using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using AUTD3Sharp;
using AUTD3Sharp.Driver.Datagram;
using AUTD3Sharp.Gain;
using AUTD3Sharp.Modulation;

#nullable enable

public partial class HAP_AUTDController
{
    /// <summary>
    /// HCD_Pipelineから接触クラスタを取得し、最適なHaptics信号（GSPATなど）を生成・送信します。
    /// Updateループから毎フレーム呼ばれます。
    /// </summary>
    private void UpdateHaptics()
    {
        // バイパスが有効な場合はここで終了し、Updateからの自動送信を行わない
        if (bypassHaptics) return;
        if (hcdPipeline == null) return;

        // トラッカーから安定化・追跡済みのクラスタリストを取得
        var trackedClusters = hcdPipeline.GetTrackedClusters();

        // 生存しており、かつ Force が有効なクラスタを抽出
        var activeClusters = trackedClusters
            .Where(c => c.IsAlive && c.Force > 0.01f).ToList();

        if (activeClusters.Count > 0)
        {
            // 1. 各クラスタから必要な焦点（Foci/STM）を生成
            var clusterFociList = HAP_FociGenerator.Generate(
                activeClusters, 
                generationMode, 
                centroidSource, 
                ellipseSource, 
                randomSource, 
                focusIntensityPascal, 
                offset);

            // 2. クラスタの法線とデバイスの向きに基づき、最適なデバイスにGSPATを割り当ててグループ化
            var groupDatagram = HAP_GSPATDeviceAllocator.Allocate(
                clusterFociList, 
                connectedDevices, 
                holoAlgorithm, 
                enableDirectionalGrouping, 
                directionalAngleThreshold, 
                focusIntensityPascal,
                debugDisabler);

            // 3. デバイスに送信
            if (_autd != null)
            {
                if (_hapticsSendTask == null || _hapticsSendTask.IsCompleted)
                {
                    _hapticsSendTask = System.Threading.Tasks.Task.Run(() => 
                    {
                        try 
                        {
                            lock (_sendLock) { _autd.Send(groupDatagram); }
                        }
                        catch (System.Exception e) { Debug.LogException(e); }
                    });
                    _isCurrentlyOff = false;
                }
            }
        }
        else
        {
            // 接触がなくなった場合、出力を停止 (Null)
            if (!_isCurrentlyOff && _autd != null)
            {
                if (_hapticsSendTask == null || _hapticsSendTask.IsCompleted)
                {
                    _hapticsSendTask = System.Threading.Tasks.Task.Run(() => 
                    {
                        try 
                        {
                            lock (_sendLock) { _autd.Send(new Null()); }
                        }
                        catch (System.Exception e) { Debug.LogException(e); }
                    });
                    _isCurrentlyOff = true;
                }
            }
        }
    }

    /// <summary>
    /// 各Haptics Sourceに設定された Modulation Override（変調の優先設定）を解決し適用します。
    /// </summary>
    private void ResolveModulationOverrides()
    {
        if (bypassHaptics || hcdPipeline == null || generationMode != HapticsGenerationMode.Precision) return;

        var overrides = new List<HapticsModulationOverride>();
        if (centroidSource.enabled && centroidSource.modulationOverride.enabled) overrides.Add(centroidSource.modulationOverride);
        if (ellipseSource.enabled && ellipseSource.modulationOverride.enabled) overrides.Add(ellipseSource.modulationOverride);
        if (randomSource.enabled && randomSource.modulationOverride.enabled) overrides.Add(randomSource.modulationOverride);
        
        if (overrides.Count > 0)
        {
            var bestOverride = overrides.OrderByDescending(o => o.priority).First();
            if (modulationMode != bestOverride.mode || (bestOverride.mode == ModulationMode.Sine && sineFrequency != bestOverride.frequency))
            {
                modulationMode = bestOverride.mode;
                sineFrequency = bestOverride.frequency;
                ApplyModulation();
            }
        }
    }
}
