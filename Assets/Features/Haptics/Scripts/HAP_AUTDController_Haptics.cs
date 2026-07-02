using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AUTD3Sharp;
using AUTD3Sharp.Modulation;
using AUTD3Sharp.Gain.Holo;
using static AUTD3Sharp.Units;

#nullable enable

public partial class HAP_AUTDController
{
    /// <summary>
    /// 各Haptics Source（Centroid, Ellipse, Random）に設定された Modulation Override（変調の優先設定）を解決し、
    /// 最も優先度の高い変調をデバイスに適用します。
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

    /// <summary>
    /// 追跡済みのクラスタ群（手などの接触領域）から、モードに応じた超音波の焦点（Foci）やSTMフレームを生成し、
    /// デバイスへ送信します。
    /// </summary>
    /// <param name="activeClusters">Force(接触力)が有効な追跡済みクラスタのリスト</param>
    private void ProcessHapticsOutput(List<TrackedCluster> activeClusters)
    {
        if (activeClusters.Count == 0) return;

        // 【Simplified モード】
        // クラスタの重心に対して1点だけ焦点を当てる軽量モード
        if (generationMode == HapticsGenerationMode.Simplified)
        {
            var simplifiedFoci = activeClusters.Select(c => (
                new AUTD3Sharp.Utils.Point3(c.Centroid.x + offset.x, c.Centroid.y + offset.y, c.Centroid.z + offset.z), 
                (focusIntensityPascal * c.Force) * Pa
            )).ToArray();
            
            if (holoAlgorithm == HoloAlgorithm.GSPAT)
                _autd!.Send(new GSPAT(simplifiedFoci, new GSPATOption()));
            else
                _autd!.Send(new Naive(simplifiedFoci, new NaiveOption()));
            
            return;
        }

        // 【Precision モード】
        // 重心に加え、楕円STMやランダムノイズSTMを組み合わせてリッチな触覚を生成するモード
        bool useStm = ellipseSource.enabled || randomSource.enabled;
        
        if (!useStm)
        {
            // STMを使用せず Centroid だけが有効な場合は、標準の静的Holo (GSPAT/Naive) で出力
            var centroidFoci = activeClusters.Select(c => (
                new AUTD3Sharp.Utils.Point3(c.Centroid.x + offset.x, c.Centroid.y + offset.y, c.Centroid.z + offset.z), 
                centroidSource.CalculateAmplitude(c) * Pa
            )).ToArray();
            
            if (holoAlgorithm == HoloAlgorithm.GSPAT)
                _autd!.Send(new GSPAT(centroidFoci, new GSPATOption()));
            else
                _autd!.Send(new Naive(centroidFoci, new NaiveOption()));
        }
        else
        {
            // --- STM / Sequential 生成ロジック ---
            
            // 毎フレーム1点ずつ描画する Sequential 用の焦点リスト
            var sequentialFoci = new List<(AUTD3Sharp.Utils.Point3, AUTD3Sharp.Gain.Holo.Amplitude)>();
            bool useFociStm = false;
            
            // 最大STMサンプル数を計算
            int maxStmSamples = 1;
            if (ellipseSource.enabled && ellipseSource.outputMode == HapticsOutputMode.FociStm) 
                maxStmSamples = Mathf.Max(maxStmSamples, ellipseSource.stmSamplesPerCycle);
            if (randomSource.enabled && randomSource.outputMode == HapticsOutputMode.FociStm) 
                maxStmSamples = Mathf.Max(maxStmSamples, randomSource.stmSamplesPerCycle);
            
            var stmFrames = new List<List<Vector3>>();
            for (int i = 0; i < maxStmSamples; i++) stmFrames.Add(new List<Vector3>());
            
            // 1. Centroid Source の処理
            if (centroidSource.enabled)
            {
                foreach (var c in activeClusters)
                {
                    // Centroid は常に Sequential (静的) として扱い、後でSTM全フレームに加算する
                    sequentialFoci.Add((
                        new AUTD3Sharp.Utils.Point3(c.Centroid.x + offset.x, c.Centroid.y + offset.y, c.Centroid.z + offset.z), 
                        (centroidSource.CalculateAmplitude(c)) * Pa
                    ));
                }
            }

            // 2. Ellipse / Random Source の処理
            foreach (var c in activeClusters)
            {
                // 楕円形状をなぞる STM (ザラザラ感や広がり)
                if (ellipseSource.enabled)
                {
                    float ellipseAmpScale;
                    var eFrames = ellipseSource.GenerateSTMFrames(c, offset, out ellipseAmpScale);
                    
                    if (ellipseSource.outputMode == HapticsOutputMode.Sequential)
                    {
                        // Unityのフレーム単位で1点をピックアップ
                        int idx = Time.frameCount % eFrames.Count;
                        foreach (var p in eFrames[idx]) {
                            sequentialFoci.Add((new AUTD3Sharp.Utils.Point3(p.x, p.y, p.z), (focusIntensityPascal * c.Force * ellipseAmpScale) * Pa));
                        }
                    }
                    else
                    {
                        // FociStm 用にフレームをリサンプリングしてバッファへ
                        useFociStm = true;
                        for (int i = 0; i < eFrames.Count; i++) {
                            int targetIdx = Mathf.RoundToInt((float)i / eFrames.Count * (maxStmSamples - 1));
                            stmFrames[targetIdx].AddRange(eFrames[i]);
                        }
                    }
                }

                // 16点のランダムノイズ STM (強いザラザラ感)
                if (randomSource.enabled)
                {
                    var rFrames = randomSource.GenerateSTMFrames(c, offset);
                    if (randomSource.outputMode == HapticsOutputMode.Sequential)
                    {
                        // Unityのフレーム単位で1点をピックアップ
                        int idx = Time.frameCount % rFrames.Count;
                        foreach (var p in rFrames[idx]) {
                            sequentialFoci.Add((new AUTD3Sharp.Utils.Point3(p.x, p.y, p.z), (focusIntensityPascal * c.Force) * Pa));
                        }
                    }
                    else
                    {
                        // FociStm 用にフレームをリサンプリングしてバッファへ
                        useFociStm = true;
                        for (int i = 0; i < rFrames.Count; i++) {
                            int targetIdx = Mathf.RoundToInt((float)i / rFrames.Count * (maxStmSamples - 1));
                            stmFrames[targetIdx].AddRange(rFrames[i]);
                        }
                    }
                }
            }
            
            // 3. デバイスへの送信
            if (useFociStm)
            {
                // STMを使う場合、STMの全フレームに対して Sequential な焦点（Centroidなど）を合成する
                for (int i = 0; i < maxStmSamples; i++) {
                    // もしフレームが完全に空になった場合は、安全のために重心を加える
                    if (stmFrames[i].Count == 0 && activeClusters.Count > 0)
                        stmFrames[i].Add(activeClusters[0].Centroid + offset);
                    
                    // Sequential 焦点を混ぜ込む
                    foreach (var sf in sequentialFoci) {
                        stmFrames[i].Add(new Vector3(sf.Item1.X, sf.Item1.Y, sf.Item1.Z));
                    }
                }
                
                // STM API を呼び出してデバイスに転送
                SetMultiFocusStm(stmFrames, 150f, focusIntensityPascal / 10000f);
            }
            else if (sequentialFoci.Count > 0)
            {
                // 全てのソースが Sequential の場合は STM を使わず、1フレームの静的 Holo として送信
                if (holoAlgorithm == HoloAlgorithm.GSPAT)
                    _autd!.Send(new GSPAT(sequentialFoci.ToArray(), new GSPATOption()));
                else
                    _autd!.Send(new Naive(sequentialFoci.ToArray(), new NaiveOption()));
            }
        }
    }
}
