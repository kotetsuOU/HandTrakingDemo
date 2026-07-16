using UnityEngine;
using System.Collections.Generic;
using System.Linq;
#if !USE_AUTD3_LEGACY
using AUTD3;
using AUTD3.Holo;
#else
using AUTD3Sharp;
using AUTD3Sharp.Driver.Datagram;
using AUTD3Sharp.Gain;
using AUTD3Sharp.Modulation;
#endif

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

        bool useFoxFootHaptics = foxFootHapticsController != null && foxFootHapticsController.enabled;
        bool hasActiveTargets = false;
        List<TrackedCluster> activeClusters = new List<TrackedCluster>();

        if (useFoxFootHaptics)
        {
            hasActiveTargets = foxFootHapticsController!.HasActiveTargets();
        }
        else
        {
            if (hcdPipeline != null)
            {
                var trackedClusters = hcdPipeline.GetTrackedClusters();
                activeClusters = trackedClusters.Where(c => c.IsAlive && c.Force > 0.01f).ToList();
                hasActiveTargets = activeClusters.Count > 0;
            }
        }

        // カスタムアルゴリズム選択時のフォールバック処理
        HoloAlgorithm effectiveAlgorithm = holoAlgorithm;
        if (effectiveAlgorithm == HoloAlgorithm.Custom)
        {
            if (useFoxFootHaptics)
            {
                // FoxFootCustom用の単焦点内部ソルバー（Naive or GSPAT）をコントローラーから取得し、HoloAlgorithmに変換
                effectiveAlgorithm = foxFootHapticsController!.customInnerAlgorithm == HoloSolverAlgorithm.Naive
                    ? HoloAlgorithm.Naive
                    : HoloAlgorithm.GSPAT;
            }
            else
            {
                // Custom対応クラスが未接続の場合はGSPATにフォールバック
                effectiveAlgorithm = HoloAlgorithm.GSPAT;
            }
        }

        string profilerLabel = useFoxFootHaptics
            ? $"[FoxFoot-{foxFootHapticsController!.stmMode}({foxFootHapticsController!.trackMode})-{effectiveAlgorithm}]"
            : $"[{effectiveAlgorithm}]";

        if (hasActiveTargets)
        {
            var profiler = performanceProfiler;

            // ── Total 計測開始 ──
            profiler.BeginTotal();

            try
            {
                // 1. 各クラスタから必要な焦点（Foci/STM）を生成
                profiler.BeginFociGenerate();
            List<HAP_FociGenerator.ClusterFociData> clusterFociList;
            if (useFoxFootHaptics)
            {
                clusterFociList = foxFootHapticsController!.GetFootFociList(focusIntensityPascal, offset);
            }
            else
            {
                clusterFociList = HAP_FociGenerator.Generate(
                    activeClusters, 
                    generationMode, 
                    centroidSource, 
                    ellipseSource, 
                    randomSource, 
                    focusIntensityPascal, 
                    offset);
            }
            profiler.EndFociGenerate();

#if !USE_AUTD3_LEGACY
            if (_client != null && geometry != null)
            {
                // 2. クラスタの法線とデバイスの向きに基づき、最適なデバイスにGSPATを割り当ててグループ化
                profiler.BeginDeviceAllocate();
                var builder = _client.DatagramBuilder();
                HAP_GSPATDeviceAllocator.Allocate(
                    builder,
                    geometry,
                    clusterFociList, 
                    connectedDevices, 
                    effectiveAlgorithm, 
                    enableDirectionalGrouping, 
                    directionalAngleThreshold, 
                    focusIntensityPascal,
                    debugDisabler);
                var frames = builder.Build();
                profiler.EndDeviceAllocate();

                // 3. デバイスに送信
                if (synchronousSend)
                {
                    // ── 同期モード（論文計測用） ──
                    try
                    {
                        profiler.BeginSend();
                        // wait synchronously 
                        foreach (var frame in frames) { _client.SendCheckedAsync(frame).GetAwaiter().GetResult(); }
                        frames.Dispose();
                        builder.Dispose();
                        profiler.EndSend();
#else
            // 2. クラスタの法線とデバイスの向きに基づき、最適なデバイスにGSPATを割り当ててグループ化
            profiler.BeginDeviceAllocate();
            var groupDatagram = HAP_GSPATDeviceAllocator.Allocate(
                clusterFociList, 
                connectedDevices, 
                effectiveAlgorithm, 
                enableDirectionalGrouping, 
                directionalAngleThreshold, 
                focusIntensityPascal,
                debugDisabler);
            profiler.EndDeviceAllocate();

            // 3. デバイスに送信
            if (_autd != null)
            {
                if (synchronousSend)
                {
                    // ── 同期モード（論文計測用） ──
                    // 全処理がメインスレッドで実行されるため、
                    // CPU Usage → Hierarchy に全マーカーが表示され、
                    // Profile Analyzer で中央値を直接取得できます。
                    try
                    {
                        profiler.BeginSend();
                        if (groupDatagram != null) { lock (_sendLock) { _autd.Send(groupDatagram); } }
                        profiler.EndSend();
#endif

                        // ── Total 計測終了 ──
                        profiler.EndTotal(profilerLabel);
                    }
                    catch (System.Exception e) { Debug.LogException(e); }
                    _isCurrentlyOff = false;
                }
                else
                {
                    // ── 非同期モード（通常運用） ──
#if USE_AUTD3_LEGACY
                    // Send をバックグラウンドスレッドで実行し、メインスレッドのFPSを維持
#endif

                    if (_hapticsSendTask == null || _hapticsSendTask.IsCompleted)
                    {
#if !USE_AUTD3_LEGACY
                        _hapticsSendTask = System.Threading.Tasks.Task.Run(async () => 
                        {
                            try 
                            {
                                profiler.BeginSend();
                                foreach (var frame in frames) { await _client.SendCheckedAsync(frame); }
                                frames.Dispose();
                                builder.Dispose();
                                profiler.EndSend();
#else
                        _hapticsSendTask = System.Threading.Tasks.Task.Run(() => 
                        {
                            try 
                            {
                                profiler.BeginSend();
                                if (groupDatagram != null) { lock (_sendLock) { _autd.Send(groupDatagram); } }
                                profiler.EndSend();
#endif

                                // ── Total 計測終了（Send完了後） ──
                                profiler.EndTotal(profilerLabel);
                            }
                            catch (System.Exception e) { Debug.LogException(e); }
                        });
                        _isCurrentlyOff = false;
                    }
#if !USE_AUTD3_LEGACY
                    else
                    {
                        // Dispose properly if skipped
                        frames.Dispose();
                        builder.Dispose();
                    }
#endif
                }
            }
            }
            finally
            {
                profiler.EndMainThreadMarker();
            }
        }
        else
        {
            // 接触がなくなった場合、出力を停止 (Null)
#if !USE_AUTD3_LEGACY
            if (!_isCurrentlyOff && _client != null && geometry != null)
#else
            if (!_isCurrentlyOff && _autd != null)
#endif
            {
                if (synchronousSend)
                {
                    try
                    {
#if !USE_AUTD3_LEGACY
                        using var builder = _client.DatagramBuilder();
                        var buffer = geometry.PatternBuffer();
                        Pattern.Null(buffer);
                        builder.Push(new Pattern(PatternBank.B0, buffer));
                        using var frames = builder.Build();
                        foreach (var frame in frames) { _client.SendCheckedAsync(frame).GetAwaiter().GetResult(); }
#else
                        lock (_sendLock) { _autd.Send(new Null()); }
#endif
                    }
                    catch (System.Exception e) { Debug.LogException(e); }
                    _isCurrentlyOff = true;
                }
                else if (_hapticsSendTask == null || _hapticsSendTask.IsCompleted)
                {
#if !USE_AUTD3_LEGACY
                    _hapticsSendTask = System.Threading.Tasks.Task.Run(async () => 
                    {
                        try 
                        {
                            using var builder = _client.DatagramBuilder();
                            var buffer = geometry.PatternBuffer();
                            Pattern.Null(buffer);
                            builder.Push(new Pattern(PatternBank.B0, buffer));
                            using var frames = builder.Build();
                            foreach (var frame in frames) { await _client.SendCheckedAsync(frame); }
                        }
                        catch (System.Exception e) { Debug.LogException(e); }
                    });
#else
                    _hapticsSendTask = System.Threading.Tasks.Task.Run(() => 
                    {
                        try 
                        {
                            lock (_sendLock) { _autd.Send(new Null()); }
                        }
                        catch (System.Exception e) { Debug.LogException(e); }
                    });
#endif
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
