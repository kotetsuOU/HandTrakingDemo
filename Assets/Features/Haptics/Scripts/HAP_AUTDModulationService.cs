using UnityEngine;
using System;
using Core.Logging;
using Features.Haptics.Debug;
#if !USE_AUTD3_LEGACY
using AUTD3;
using static AUTD3.Units;
#else
using AUTD3Sharp;
using AUTD3Sharp.Gain;
using AUTD3Sharp.Modulation;
using static AUTD3Sharp.Units;
#endif

#nullable enable

/// <summary>
/// AUTD3デバイスの変調 (Modulation)、サイレンサー (Silencer)、ファン、温度等のパラメータ制御を
/// 担う純粋なC#サービスクラス (非MonoBehaviour)。
/// </summary>
public class HAP_AUTDModulationService
{
    private readonly HAP_AUTDLinkService _linkService;

    // 前回の設定を記憶して差分変更を検知するためのプライベート変数
    private ModulationMode _prevModMode;
    private float _prevSineFreq;
    private float _prevStaticAmp;

    private SilencerMode _prevSilencerMode;
    private ushort _prevSilStepPhase;
    private ushort _prevSilStepAmp;

    private bool _prevFanState;
#if USE_AUTD3_LEGACY
    private float _prevTemperature;
#endif

    public HAP_AUTDModulationService(HAP_AUTDLinkService linkService)
    {
        _linkService = linkService;
    }

    /// <summary>
    /// パラメータの変更を監視し、変更があった場合のみデバイスへ送信します。
    /// </summary>
    public void CheckAndApply(
        ModulationMode modMode, float sineFreq, float staticAmp,
        SilencerMode silMode, ushort silStepPhase, ushort silStepAmp,
        bool enableFan, float temperature)
    {
        if (!_linkService.IsConnected) return;

        bool modChanged = (_prevModMode != modMode) || (_prevSineFreq != sineFreq) || (_prevStaticAmp != staticAmp);
        if (modChanged) ApplyModulation(modMode, sineFreq, staticAmp);

        bool silChanged = (_prevSilencerMode != silMode) || (_prevSilStepPhase != silStepPhase) || (_prevSilStepAmp != silStepAmp);
        if (silChanged) ApplySilencer(silMode, silStepPhase, silStepAmp);

        if (_prevFanState != enableFan) ApplyFan(enableFan);
#if USE_AUTD3_LEGACY
        if (_prevTemperature != temperature) ApplyTemperature(temperature);
#endif
    }

    /// <summary>
    /// 変調（Modulation）設定をデバイスに送信します。
    /// </summary>
#if !USE_AUTD3_LEGACY
    public async void ApplyModulation(ModulationMode modMode, float sineFreq, float staticAmp)
    {
        if (_linkService.Client == null || _linkService.Geometry == null) return;
        try
        {
            var client = _linkService.Client;
            using var builder = client.DatagramBuilder();
            using var modulationBuffer = Modulation.ModulationBuffer();

            switch (modMode)
            {
                case ModulationMode.Sine:
                    Modulation.Sine(sineFreq * Hz, new SineOption(), modulationBuffer);
                    builder.Push(new Modulation(SamplingConfig.Freq4k, modulationBuffer));
                    break;
                case ModulationMode.Static:
                    byte intensity = (byte)Mathf.Clamp(staticAmp * 255f, 0, 255);
                    Modulation.Constant(intensity, modulationBuffer);
                    builder.Push(new Modulation(SamplingConfig.Freq4k, modulationBuffer));
                    break;
            }

            using var frames = builder.Build();
            foreach (var frame in frames) { await client.SendCheckedAsync(frame); }

            _prevModMode = modMode;
            _prevSineFreq = sineFreq;
            _prevStaticAmp = staticAmp;
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning(null, HAP_LogTriggers.TagModulationService, $"Failed to apply modulation: {ex.Message}");
        }
    }
#else
    public void ApplyModulation(ModulationMode modMode, float sineFreq, float staticAmp)
    {
        if (_linkService.Autd == null) return;
        try
        {
            var autd = _linkService.Autd;
            switch (modMode)
            {
                case ModulationMode.Sine:
                    lock (_linkService.SendLock) { autd.Send(new Sine(freq: sineFreq * Hz, option: new SineOption())); }
                    break;
                case ModulationMode.Static:
                    lock (_linkService.SendLock) { autd.Send(new Static()); }
                    break;
            }

            _prevModMode = modMode;
            _prevSineFreq = sineFreq;
            _prevStaticAmp = staticAmp;
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning(null, HAP_LogTriggers.TagModulationService, $"Failed to apply modulation: {ex.Message}");
        }
    }
#endif

    /// <summary>
    /// サイレンサー（Silencer）設定をデバイスに送信します。
    /// </summary>
#if !USE_AUTD3_LEGACY
    public async void ApplySilencer(SilencerMode silMode, ushort silStepPhase, ushort silStepAmp)
    {
        if (_linkService.Client == null || _linkService.Geometry == null) return;
        try
        {
            var client = _linkService.Client;
            using var builder = client.DatagramBuilder();

            switch (silMode)
            {
                case SilencerMode.Disabled:
                    builder.Push(SetSilencer.Disable());
                    break;
                case SilencerMode.FixedUpdateRate:
                    builder.Push(new SetSilencer(new FixedUpdateRate(intensity: silStepAmp, phase: silStepPhase)));
                    break;
                case SilencerMode.FixedCompletionTime:
                    builder.Push(new SetSilencer(new FixedCompletionTime()));
                    break;
            }

            using var frames = builder.Build();
            foreach (var frame in frames) { await client.SendCheckedAsync(frame); }

            _prevSilencerMode = silMode;
            _prevSilStepPhase = silStepPhase;
            _prevSilStepAmp = silStepAmp;
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning(null, HAP_LogTriggers.TagModulationService, $"Failed to apply silencer: {ex.Message}");
        }
    }
#else
    public void ApplySilencer(SilencerMode silMode, ushort silStepPhase, ushort silStepAmp)
    {
        if (_linkService.Autd == null) return;
        try
        {
            var autd = _linkService.Autd;
            switch (silMode)
            {
                case SilencerMode.Disabled:
                    lock (_linkService.SendLock) { autd.Send(Silencer.Disable()); }
                    break;
                case SilencerMode.FixedUpdateRate:
                    lock (_linkService.SendLock) { autd.Send(new Silencer(new FixedUpdateRate { Intensity = silStepAmp, Phase = silStepPhase })); }
                    break;
                case SilencerMode.FixedCompletionTime:
                    lock (_linkService.SendLock) { autd.Send(new Silencer(new FixedCompletionTime())); }
                    break;
            }

            _prevSilencerMode = silMode;
            _prevSilStepPhase = silStepPhase;
            _prevSilStepAmp = silStepAmp;
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning(null, HAP_LogTriggers.TagModulationService, $"Failed to apply silencer: {ex.Message}");
        }
    }
#endif

    /// <summary>
    /// 冷却ファンの状態（ON/OFF）を適用します。
    /// </summary>
#if !USE_AUTD3_LEGACY
    public async void ApplyFan(bool enableFan)
    {
        if (_linkService.Client == null || _linkService.Geometry == null) return;
        try
        {
            var client = _linkService.Client;
            using var builder = client.DatagramBuilder();
            builder.Push(new ForceFan(enableFan));

            using var frames = builder.Build();
            foreach (var frame in frames) { await client.SendCheckedAsync(frame); }

            _prevFanState = enableFan;
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning(null, HAP_LogTriggers.TagModulationService, $"Failed to apply fan state: {ex.Message}");
        }
    }
#else
    public void ApplyFan(bool enableFan)
    {
        if (_linkService.Autd == null) return;
        try
        {
            var autd = _linkService.Autd;
            lock (_linkService.SendLock) { autd.Send(new ForceFan(dev => enableFan)); }
            _prevFanState = enableFan;
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning(null, HAP_LogTriggers.TagModulationService, $"Failed to apply fan state: {ex.Message}");
        }
    }
#endif

#if USE_AUTD3_LEGACY
    public void ApplyTemperature(float temperature)
    {
        if (_linkService.Autd == null) return;
        try
        {
            var autd = _linkService.Autd;
            autd.Environment.SetSoundSpeedFromTemp(temperature);
            _prevTemperature = temperature;
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning(null, HAP_LogTriggers.TagModulationService, $"Failed to set temperature: {ex.Message}");
        }
    }
#endif
}
