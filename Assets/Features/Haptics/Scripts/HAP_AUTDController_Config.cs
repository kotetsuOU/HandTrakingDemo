using System;
using UnityEngine;
using AUTD3Sharp;
using AUTD3Sharp.Gain;
using AUTD3Sharp.Modulation;
using static AUTD3Sharp.Units;

#nullable enable

public partial class HAP_AUTDController
{
    /// <summary>
    /// インスペクターで変更された設定を検知し、ハードウェアに適用します。
    /// Updateループから毎フレーム呼ばれます。
    /// </summary>
    private void CheckForConfigChanges()
    {
        bool modulationChanged = (_prevModMode != modulationMode) ||
                                 (_prevSineFreq != sineFrequency) ||
                                 (_prevStaticAmp != staticAmplitude);
        if (modulationChanged) ApplyModulation();

        bool silencerChanged = (_prevSilencerMode != silencerMode) ||
                               (_prevSilStepPhase != silencerStepPhase) ||
                               (_prevSilStepAmp != silencerStepAmplitude);
        if (silencerChanged) ApplySilencer();

        if (_prevFanState != enableFan) ApplyFan();
        if (_prevTemperature != temperature) ApplyTemperature();
    }

    /// <summary>
    /// 変調（Modulation）設定をデバイスに送信します。
    /// 超音波をどの周波数で点滅させるかを決定し、触覚の質感を変化させます。
    /// </summary>
    private void ApplyModulation()
    {
        if (_autd == null) return;
        
        try
        {
            switch (modulationMode)
            {
                case ModulationMode.Sine:
                    lock (_sendLock) { _autd.Send(new Sine(freq: sineFrequency * Hz, option: new SineOption())); }
                    break;
                case ModulationMode.Static:
                    lock (_sendLock) { _autd.Send(new Static()); }
                    break;
            }
            
            _prevModMode = modulationMode;
            _prevSineFreq = sineFrequency;
            _prevStaticAmp = staticAmplitude;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HAP_AUTDController] Failed to apply modulation: {ex.Message}");
        }
    }

    /// <summary>
    /// サイレンサー（Silencer）設定をデバイスに送信します。
    /// 超音波出力の急激な変化をなまらせることで、デバイスから発生する可聴ノイズ（ジージー音）を低減します。
    /// </summary>
    private void ApplySilencer()
    {
        if (_autd == null) return;

        try
        {
            switch (silencerMode)
            {
                case SilencerMode.Disabled:
                    lock (_sendLock) { _autd.Send(Silencer.Disable()); }
                    break;
                case SilencerMode.FixedUpdateRate:
                    lock (_sendLock) { _autd.Send(new Silencer(new FixedUpdateRate { Intensity = silencerStepAmplitude, Phase = silencerStepPhase })); }
                    break;
                case SilencerMode.FixedCompletionTime:
                    lock (_sendLock) { _autd.Send(new Silencer(new FixedCompletionTime())); }
                    break;
            }
            
            _prevSilencerMode = silencerMode;
            _prevSilStepPhase = silencerStepPhase;
            _prevSilStepAmp = silencerStepAmplitude;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HAP_AUTDController] Failed to apply silencer: {ex.Message}");
        }
    }

    /// <summary>
    /// 冷却ファンの状態（ON/OFF）を適用します。
    /// 強出力を持続する場合は発熱するためONを推奨します。
    /// </summary>
    private void ApplyFan()
    {
        if (_autd == null) return;
        
        try
        {
            lock (_sendLock) { _autd.Send(new ForceFan(dev => enableFan)); }
            _prevFanState = enableFan;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HAP_AUTDController] Failed to apply fan state: {ex.Message}");
        }
    }

    /// <summary>
    /// 環境温度（摂氏）を適用します。音速は温度に依存するため、
    /// 正確な焦点距離を計算するために必要です。
    /// </summary>
    private void ApplyTemperature()
    {
        if (_autd == null) return;
        
        try
        {
            _autd.Environment.SetSoundSpeedFromTemp(temperature);
            _prevTemperature = temperature;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HAP_AUTDController] Failed to set temperature: {ex.Message}");
        }
    }
}
